using System;
using HarmonyLib;
using UnityEngine;
#if MELONLOADER
// MelonLoader generates the interop assemblies with the Il2Cpp namespace prefix.
using Il2CppRewired;
using Il2CppVampireSurvivors;
using Il2CppVampireSurvivors.App.UI;
using Il2CppVampireSurvivors.Data;
using Il2CppVampireSurvivors.Framework;
using Il2CppVampireSurvivors.Framework.Speedup;
#else
// The BepInEx interop assemblies and the Mono game assemblies have no namespace prefix.
using Rewired;
using VampireSurvivors;
using VampireSurvivors.App.UI;
using VampireSurvivors.Data;
using VampireSurvivors.Framework;
using VampireSurvivors.Framework.Speedup;
#endif

namespace VampireSurvivorsUx
{
    /// <summary>
    /// Raises the speed limit of the game from 2x to 5x.
    /// The game has a speed-up feature in <see cref="SpeedupManager"/>. It needs the Speed-Up relic, it is off
    /// on stages with <c>isSpeedupBanned</c>, and it is off in online runs. The mod keeps all three rules and
    /// only raises the limit. It also replaces the toggle order, because the game code stops at 2x.
    /// </summary>
    internal static class GameSpeed
    {
        private const float MaxSpeed = 5f;

        /// <summary>The order of the toggle: 1x, 2x, 3x, 4x, 5x, then back to 1x.</summary>
        private static readonly float[] Stops = { 1f, 2f, 3f, 4f, 5f };

        /// <summary>The game ignores the speed-up button while the player holds Rewired button 26.</summary>
        private const int ModifierButtonId = 26;

        // ---------------------------------------------------------------- toggle order

        /// <summary>
        /// The game code goes 1x, 1.5x, 2x, then back to 1x. This replaces the order. It keeps the two rules
        /// that the game checks here: the modifier button and the Speed-Up relic.
        /// </summary>
        [HarmonyPatch(typeof(SpeedupManager), nameof(SpeedupManager.ToggleSpeedup))]
        private static class SpeedupManager_ToggleSpeedup_Patch
        {
            private static bool Prefix(SpeedupManager __instance)
            {
                try
                {
                    Player player = ReInput.players != null ? ReInput.players.GetPlayer(0) : null;
                    if (player != null && player.GetButton(ModifierButtonId)) return false;
                    if (!IsSpeedupUnlocked()) return false;
                    Advance(__instance);
                }
                catch (Exception e)
                {
                    ModLog.Error("SpeedupManager.ToggleSpeedup prefix failed", e);
                }
                return false;
            }
        }

        /// <summary>The on-screen button uses the same order as the controller button.</summary>
        [HarmonyPatch(typeof(FastForwardButton), "FastForward")]
        private static class FastForwardButton_FastForward_Patch
        {
            private static bool Prefix()
            {
                try
                {
                    Advance(SpeedupManager.Instance);
                }
                catch (Exception e)
                {
                    ModLog.Error("FastForwardButton.FastForward prefix failed", e);
                }
                return false;
            }
        }

        private static bool IsSpeedupUnlocked()
        {
            GameManager gm = GM.Core;
            PlayerOptionsData config = gm != null && gm.PlayerOptions != null ? gm.PlayerOptions.Config : null;
            if (config == null) return false;
            return config.HasCollectedItem(ItemType.RELIC_SPEEDUP)
                && !config.SealedItems.Contains(ItemType.RELIC_SPEEDUP);
        }

        /// <summary>
        /// Moves to the next stop. <c>SetSpeedup</c> still checks the stage ban, the sealed relic, and the
        /// online run, so a blocked run keeps the speed it has.
        /// </summary>
        private static void Advance(SpeedupManager manager)
        {
            if (manager == null) return;
            // Raise the limit before every step. A postfix on SpeedupManager.Setup looks like the right
            // place, but Harmony does not patch that method on the IL2CPP build, so the limit stayed at 2x
            // and every step above 2x was clamped back. ClearSpeedupManager also drops the instance at the
            // end of a run, so the limit has to be set again anyway.
            Priv.SetMaxSpeed(manager, MaxSpeed);
            float current = manager.CurrentSpeedMultiplier;
            manager.SetSpeedup(NextStop(current));
            ModLog.Info("Speed " + current + "x -> " + manager.CurrentSpeedMultiplier + "x. limit="
                + Priv.MaxSpeed(manager));
        }

        /// <summary>The first stop above the current speed. Wraps to the first stop at the top.</summary>
        private static float NextStop(float current)
        {
            for (int i = 0; i < Stops.Length; i++)
            {
                if (current < Stops[i] - 0.01f) return Stops[i];
            }
            return Stops[0];
        }

        // ---------------------------------------------------------------- speed arrows

        /// <summary>
        /// The button has three stacked icons: one arrow (<c>fastForward</c>), two arrows
        /// (<c>fastForwardX2</c>), and three arrows (<c>fastForwardX3</c>). The game shows the third one for
        /// every speed from 2x up, so 4x and 5x look the same as 3x. This adds one or two more arrows to the
        /// right of the button. The first three speeds keep the icons of the game.
        /// The arrows follow the third icon, so every rule that hides the button hides them too.
        /// </summary>
        [HarmonyPatch(typeof(FastForwardButton), "Update")]
        private static class FastForwardButton_Update_Patch
        {
            private static void Postfix(FastForwardButton __instance)
            {
                try
                {
                    UpdateArrows(__instance);
                }
                catch (Exception e)
                {
                    ModLog.Error("FastForwardButton.Update postfix failed", e);
                }
            }
        }

        private const string ArrowName = "VampireSurvivorsUx_SpeedArrows";

        /// <summary>The arrows fill about 70 of the 100 units of the icon, so the extra group sits 70 to the right.</summary>
        private const float ExtraOffsetX = 70f;

        private static GameObject _extra;
        private static UnityEngine.UI.Image _extraImage;

        /// <summary>
        /// Adds the missing arrows with the art of the game and at its size. 4x is the three arrow icon plus
        /// the one arrow icon. 5x is the three arrow icon plus the two arrow icon.
        /// </summary>
        private static void UpdateArrows(FastForwardButton button)
        {
            GameObject icon3 = Priv.FastForwardIcon3(button);
            bool iconsVisible = icon3 != null && icon3.activeInHierarchy;
            float speed = SpeedupManager.Instance.CurrentSpeedMultiplier;

            // The button is rebuilt with every run, so drop the object of the previous run.
            if (_extra == null) _extraImage = null;

            int extraArrows = 0;
            if (iconsVisible)
            {
                if (speed >= 5f) extraArrows = 2;
                else if (speed >= 4f) extraArrows = 1;
            }

            if (extraArrows == 0)
            {
                if (_extra != null && _extra.activeSelf) _extra.SetActive(false);
                return;
            }

            if (_extra == null && !CreateExtra(button)) return;

            GameObject source = extraArrows == 2 ? Priv.FastForwardIcon2(button) : Priv.FastForwardIcon1(button);
            var sourceImage = source != null ? source.GetComponent<UnityEngine.UI.Image>() : null;
            if (sourceImage != null && _extraImage != null && _extraImage.sprite != sourceImage.sprite)
            {
                _extraImage.sprite = sourceImage.sprite;
            }
            if (!_extra.activeSelf) _extra.SetActive(true);
        }

        private static bool CreateExtra(FastForwardButton button)
        {
            GameObject template = Priv.FastForwardIcon1(button);
            if (template == null)
            {
                ModLog.Warn("Speed arrows: the one arrow icon is missing.");
                return false;
            }
            _extra = UnityEngine.Object.Instantiate(template, button.transform, false);
            _extra.name = ArrowName;
            _extraImage = _extra.GetComponent<UnityEngine.UI.Image>();
            var rect = _extra.GetComponent<RectTransform>();
            var source = template.GetComponent<RectTransform>();
            if (rect != null && source != null)
            {
                Vector2 pos = source.anchoredPosition;
                pos.x += ExtraOffsetX;
                rect.anchoredPosition = pos;
                rect.localScale = Vector3.one;
            }
            _extra.SetActive(false);
            ModLog.Info("Speed arrows: extra icon added at x offset " + ExtraOffsetX + ".");
            return true;
        }
    }
}

using System;
using HarmonyLib;
using UnityEngine;
#if MELONLOADER
// MelonLoader generates the interop assemblies with the Il2Cpp namespace prefix.
using Il2CppRewired;
using Il2CppTMPro;
using Il2CppVampireSurvivors;
using Il2CppVampireSurvivors.App.UI;
using Il2CppVampireSurvivors.Data;
using Il2CppVampireSurvivors.Framework;
using Il2CppVampireSurvivors.Framework.Speedup;
#else
// The BepInEx interop assemblies and the Mono game assemblies have no namespace prefix.
using Rewired;
using TMPro;
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

        private const string LabelName = "VampireSurvivorsUx_SpeedLabel";

        /// <summary>The game shows an icon for 1x, 1.5x, and 2x. The label starts at 3x.</summary>
        private const float LabelFromSpeed = 3f;

        // ---------------------------------------------------------------- speed limit

        /// <summary>
        /// <c>Stage.Start</c> calls <c>Setup</c> once per run. <c>ClearSpeedupManager</c> drops the instance at
        /// the end of the run, so a new instance starts with a limit of 2x again.
        /// </summary>
        [HarmonyPatch(typeof(SpeedupManager), nameof(SpeedupManager.Setup))]
        private static class SpeedupManager_Setup_Patch
        {
            private static void Postfix(SpeedupManager __instance)
            {
                try
                {
                    Priv.SetMaxSpeed(__instance, MaxSpeed);
                    ModLog.Info("Speed limit raised to " + MaxSpeed + "x.");
                }
                catch (Exception e)
                {
                    ModLog.Error("SpeedupManager.Setup postfix failed", e);
                }
            }
        }

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
            float current = manager.CurrentSpeedMultiplier;
            manager.SetSpeedup(NextStop(current));
            ModLog.Info("Speed " + current + "x -> " + manager.CurrentSpeedMultiplier + "x.");
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

        // ---------------------------------------------------------------- speed label

        /// <summary>
        /// The game has three icons for the speed button and the third one covers everything from 2x up.
        /// This adds the number next to the button from 3x, so 3x, 4x, and 5x are not the same picture.
        /// The label follows the third icon, so every rule that hides the button hides the label too.
        /// </summary>
        [HarmonyPatch(typeof(FastForwardButton), "Update")]
        private static class FastForwardButton_Update_Patch
        {
            private static void Postfix(FastForwardButton __instance)
            {
                try
                {
                    UpdateLabel(__instance);
                }
                catch (Exception e)
                {
                    ModLog.Error("FastForwardButton.Update postfix failed", e);
                }
            }
        }

        private static TextMeshProUGUI _label;

        private static void UpdateLabel(FastForwardButton button)
        {
            GameObject icon = Priv.FastForwardIcon3(button);
            float speed = SpeedupManager.Instance.CurrentSpeedMultiplier;
            bool show = icon != null && icon.activeInHierarchy && speed >= LabelFromSpeed;

            if (_label == null)
            {
                if (!show) return;
                _label = GetOrCreateLabel(button);
                if (_label == null) return;
            }
            if (_label.gameObject.activeSelf != show) _label.gameObject.SetActive(show);
            if (!show) return;
            string text = "x" + Mathf.RoundToInt(speed);
            if (_label.text != text) _label.text = text;
        }

        private static TextMeshProUGUI GetOrCreateLabel(FastForwardButton button)
        {
            Transform root = button.transform;
            Transform existing = root.Find(LabelName);
            if (existing != null) return existing.GetComponent<TextMeshProUGUI>();

            RectTransform buttonRect = button.GetComponent<RectTransform>();
            float height = buttonRect != null ? buttonRect.rect.height : 64f;

            var go = new GameObject(LabelName);
            go.layer = root.gameObject.layer;
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            RectTransform rect = text.rectTransform;
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(height * 2f, height * 0.6f);
            rect.anchoredPosition = Vector2.zero;

            // Copy the font and the outline material from the kill counter of the HUD.
            TextMeshProUGUI template = TemplateText();
            if (template != null)
            {
                text.font = template.font;
                text.fontSharedMaterial = template.fontSharedMaterial;
                text.color = template.color;
            }
            else ModLog.Warn("Speed label: no HUD text found. The label uses the default font.");
            text.enableAutoSizing = false;
            text.fontSize = height * 0.5f;
            text.alignment = TextAlignmentOptions.Top;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;

            ModLog.Info("Speed label added to " + root.name + ". buttonHeight=" + height);
            return text;
        }

        private static TextMeshProUGUI TemplateText()
        {
            GameManager gm = GM.Core;
            return gm != null && gm.MainUI != null ? gm.MainUI.KillsText : null;
        }
    }
}

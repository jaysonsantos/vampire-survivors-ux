using System;
using HarmonyLib;
using UnityEngine;
#if MELONLOADER
// MelonLoader generates the interop assemblies with the Il2Cpp namespace prefix.
using Il2CppVampireSurvivors.UI;
#else
// The BepInEx interop assemblies and the Mono game assemblies have no namespace prefix.
using VampireSurvivors.UI;
#endif

namespace VampireSurvivorsUx
{
    /// <summary>
    /// A double click on an arcana card selects and confirms it. The game needs a click on the card and then a
    /// click on the GET button. This covers the major page, the minor page, the Darkana page, and the Survarots
    /// page, because all of them use <see cref="ArcanaCardUI"/> and a public <c>Select</c>.
    /// </summary>
    internal static class ArcanaDoubleClick
    {
        /// <summary>Same window as the character page and the popups.</summary>
        private const float DoubleClickSeconds = 0.45f;

        private static ObjId _lastCard;
        private static float _lastClickTime;

        /// <summary>
        /// <c>OnClick</c> runs on every click on a card. <c>SetInfo</c> is not usable here, because the game
        /// returns early from it when the card is already the selected one.
        /// </summary>
        [HarmonyPatch(typeof(ArcanaCardUI), nameof(ArcanaCardUI.OnClick))]
        private static class ArcanaCardUI_OnClick_Patch
        {
            private static void Postfix(ArcanaCardUI __instance)
            {
                try
                {
                    OnCardClicked(__instance);
                }
                catch (Exception e)
                {
                    ModLog.Error("ArcanaCardUI.OnClick postfix failed", e);
                }
            }
        }

        private static void OnCardClicked(ArcanaCardUI card)
        {
            if (card == null) { _lastCard = ObjId.None; return; }

            ObjId id = ObjId.Of(card);
            float now = Time.unscaledTime;
            bool isDouble = id.Same(_lastCard) && now - _lastClickTime <= DoubleClickSeconds;
            _lastCard = isDouble ? ObjId.None : id;
            _lastClickTime = now;
            if (!isDouble) return;

            // One frame later, so the first click has set the selected card on the page.
            FrameScheduler.RunAfterFrames(1, Confirm);
        }

        /// <summary>
        /// Presses GET on the page that is open. The same card prefab is also on the collection page, where no
        /// selection page is open. That case does nothing.
        /// </summary>
        private static void Confirm()
        {
            ArcanaMainSelectionPage main = UnityEngine.Object.FindObjectOfType<ArcanaMainSelectionPage>();
            if (main != null)
            {
                if (!Priv.IsArcanaPageReady(main))
                {
                    ModLog.Info("Arcana double click: the page is not ready. Ignored.");
                    return;
                }
                ModLog.Info("Arcana double click: confirming on the arcana page.");
                main.Select();
                return;
            }

            SurvarotsSelectionPage survarots = UnityEngine.Object.FindObjectOfType<SurvarotsSelectionPage>();
            if (survarots != null)
            {
                if (!Priv.IsSurvarotsPageReady(survarots))
                {
                    ModLog.Info("Arcana double click: the page is not ready. Ignored.");
                    return;
                }
                ModLog.Info("Arcana double click: confirming on the survarots page.");
                survarots.Select();
            }
        }
    }
}

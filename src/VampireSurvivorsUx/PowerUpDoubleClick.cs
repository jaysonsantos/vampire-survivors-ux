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
    /// A double click on a power-up buys it. The game needs a click on the item and then a click on the BUY
    /// button. A maxed out power-up is not bought, because the game uses the same call to toggle it on and off.
    /// </summary>
    internal static class PowerUpDoubleClick
    {
        /// <summary>Same window as the character page and the popups.</summary>
        private const float DoubleClickSeconds = 0.45f;

        private static ObjId _lastItem;
        private static float _lastClickTime;

        /// <summary>
        /// <c>SetInfo</c> runs on every click on a power-up and on every selection with a pad or the keyboard.
        /// It writes the info panel and makes the item the selected one of the page.
        /// </summary>
        [HarmonyPatch(typeof(PowerUpItemUI), nameof(PowerUpItemUI.SetInfo))]
        private static class PowerUpItemUI_SetInfo_Patch
        {
            private static void Postfix(PowerUpItemUI __instance)
            {
                try
                {
                    OnItemClicked(__instance);
                }
                catch (Exception e)
                {
                    ModLog.Error("PowerUpItemUI.SetInfo postfix failed", e);
                }
            }
        }

        private static void OnItemClicked(PowerUpItemUI item)
        {
            if (item == null) { _lastItem = ObjId.None; return; }

            ObjId id = ObjId.Of(item);
            float now = Time.unscaledTime;
            bool isDouble = id.Same(_lastItem) && now - _lastClickTime <= DoubleClickSeconds;
            _lastItem = isDouble ? ObjId.None : id;
            _lastClickTime = now;
            if (!isDouble) return;

            if (item.IsMaxedOut())
            {
                ModLog.Info("Power-up double click: " + item._type + " is at the maximum rank. Ignored.");
                return;
            }

            PowerUpsPage page = item._page;
            if (page == null) { ModLog.Warn("Power-up double click: the page is null."); return; }

            // One frame later, so the first click has set the selected item and the price on the page.
            FrameScheduler.RunAfterFrames(1, () =>
            {
                try
                {
                    if (page == null || item == null) return;
                    PowerUpItemUI selected = page.GetCurrentSelected();
                    if (selected == null || !ObjId.Of(selected).Same(ObjId.Of(item)))
                    {
                        ModLog.Info("Power-up double click: the selection changed. Ignored.");
                        return;
                    }
                    ModLog.Info("Power-up double click: buying " + item._type + ".");
                    page.PurchaseSelected();
                }
                catch (Exception e)
                {
                    ModLog.Error("Power-up purchase failed", e);
                }
            });
        }
    }
}

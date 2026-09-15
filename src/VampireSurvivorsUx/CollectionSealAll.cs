using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
#if MELONLOADER
// MelonLoader generates the interop assemblies with the Il2Cpp namespace prefix.
using Il2CppI2.Loc;
using Il2CppTMPro;
using Il2CppVampireSurvivors;
using Il2CppVampireSurvivors.Data;
using Il2CppVampireSurvivors.Data.Items;
using Il2CppVampireSurvivors.Data.Weapons;
using Il2CppVampireSurvivors.Framework;
using Il2CppVampireSurvivors.Objects;
using Il2CppVampireSurvivors.UI;
#else
// The BepInEx interop assemblies and the Mono game assemblies have no namespace prefix.
using I2.Loc;
using TMPro;
using VampireSurvivors;
using VampireSurvivors.Data;
using VampireSurvivors.Data.Items;
using VampireSurvivors.Data.Weapons;
using VampireSurvivors.Framework;
using VampireSurvivors.Objects;
using VampireSurvivors.UI;
#endif

namespace VampireSurvivorsUx
{
    /// <summary>
    /// The collection page gets a "Seal all" button below the "Unseal all" button of the game. It seals every
    /// item and weapon that a click can seal, until the seal limit of the player is full.
    /// The button uses the rules of the game: the item must be seen and <c>sealable</c>, a content group seal
    /// stays, and the number of seals is the limit.
    /// </summary>
    internal static class CollectionSealAll
    {
        private const string SealAllButtonName = "VampireSurvivorsUx_SealAllButton";
        private const string Label = "Seal all";
        private const float ButtonGap = 8f;

        [HarmonyPatch(typeof(CollectionsPage), "OnShowStart")]
        private static class CollectionsPage_OnShowStart_Patch
        {
            private static void Postfix(CollectionsPage __instance)
            {
                try
                {
                    EnsureButton(__instance);
                }
                catch (Exception e)
                {
                    ModLog.Error("CollectionsPage.OnShowStart postfix failed", e);
                }
            }
        }

        // ---------------------------------------------------------------- button

        private static void EnsureButton(CollectionsPage page)
        {
            Button unseal = FindUnsealAllButton(page);
            if (unseal == null)
            {
                ModLog.Warn("Collection page: the Unseal all button was not found. No Seal all button added.");
                return;
            }

            Transform parent = unseal.transform.parent;
            Transform existing = parent != null ? parent.Find(SealAllButtonName) : null;
            if (existing != null)
            {
                existing.gameObject.SetActive(unseal.gameObject.activeSelf);
                return;
            }

            GameObject clone = UnityEngine.Object.Instantiate(unseal.gameObject, parent, false);
            clone.name = SealAllButtonName;

            LayoutGroup layout = parent != null ? parent.GetComponent<LayoutGroup>() : null;
            RectTransform sourceRect = unseal.GetComponent<RectTransform>();
            RectTransform cloneRect = clone.GetComponent<RectTransform>();
            bool layoutPlaces = layout != null && layout.enabled;
            if (layoutPlaces)
            {
                clone.transform.SetSiblingIndex(unseal.transform.GetSiblingIndex() + 1);
            }
            else
            {
                MoveBelow(sourceRect, cloneRect);
            }
            ModLog.Info("Collection page: Unseal all button=" + unseal.gameObject.name
                + " parent=" + (parent != null ? parent.name : "null")
                + " layout=" + (layout != null ? Interop.TypeNameOf(layout) : "none")
                + " layoutEnabled=" + (layout != null && layout.enabled)
                + " height=" + (sourceRect != null ? sourceRect.rect.height : -1f));

            // A layout group that does not move the clone leaves it on top of the button of the game, so the
            // page looks like it lost the Unseal all button. One frame later the layout has run. Check the
            // position and move the clone down when it is still the same.
            if (layoutPlaces)
            {
                FrameScheduler.RunAfterFrames(1, () =>
                {
                    if (sourceRect == null || cloneRect == null) return;
                    Vector3 source = sourceRect.position;
                    Vector3 target = cloneRect.position;
                    if (Mathf.Abs(source.x - target.x) > 1f || Mathf.Abs(source.y - target.y) > 1f) return;
                    ModLog.Info("Collection page: the layout group did not move the Seal all button. Placing it by hand.");
                    MoveBelow(sourceRect, cloneRect);
                });
            }

            // Label. Disable the localization component first, otherwise it rewrites the text.
            var localize = clone.GetComponentInChildren<Localize>(true);
            if (localize != null) localize.enabled = false;
            var text = clone.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.enableAutoSizing = false;
                text.overflowMode = TextOverflowModes.Overflow;
                text.text = Label;
            }
            else ModLog.Warn(SealAllButtonName + ": no TextMeshProUGUI child found.");

            // Focus helper: do not steal the default selection of the page.
            var selectableUi = clone.GetComponent<SelectableUI>();
            if (selectableUi != null)
            {
                selectableUi.IsDefaultSelectedOnPage = false;
                selectableUi.ReselectIfDefaultSelectedOnPage = false;
            }

            // Click. Replace the event to drop the persistent UnsealAll listener copied from the prefab.
            Button button = clone.GetComponent<Button>();
            if (button == null)
            {
                ModLog.Warn(SealAllButtonName + ": the clone has no Button component. Removing it.");
                UnityEngine.Object.Destroy(clone);
                return;
            }
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(Interop.ToUnityAction(SealAll));
            clone.SetActive(unseal.gameObject.activeSelf);
        }

        /// <summary>Puts the clone one button height below the button of the game.</summary>
        private static void MoveBelow(RectTransform source, RectTransform clone)
        {
            if (source == null || clone == null) return;
            Vector2 pos = source.anchoredPosition;
            pos.y -= source.rect.height + ButtonGap;
            clone.anchoredPosition = pos;
        }

        /// <summary>
        /// Finds the button of the game by its persistent listener, because the object name of the prefab can
        /// change with a game update. The fallback is the object name.
        /// </summary>
        private static Button FindUnsealAllButton(CollectionsPage page)
        {
            var buttons = page.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || button.gameObject.name == SealAllButtonName) continue;
                var click = button.onClick;
                if (click == null) continue;
                for (int listener = 0; listener < click.GetPersistentEventCount(); listener++)
                {
                    string method = click.GetPersistentMethodName(listener);
                    if (!string.IsNullOrEmpty(method) && method.IndexOf("Unseal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return button;
                    }
                }
            }
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || button.gameObject.name == SealAllButtonName) continue;
                if (button.gameObject.name.IndexOf("unseal", StringComparison.OrdinalIgnoreCase) >= 0) return button;
            }
            return null;
        }

        // ---------------------------------------------------------------- seal all

        private static void SealAll()
        {
            try
            {
                CollectionsPage page = UnityEngine.Object.FindObjectOfType<CollectionsPage>();
                PlayerOptions options = SystemPlatform.Instance?.PlayerOptions;
                if (page == null || options == null)
                {
                    ModLog.Warn("Seal all: the collection page or the player options are missing.");
                    return;
                }

                // GetMaxSeals also writes Config.Seals, same as the page does when it opens.
                int max = options.GetMaxSeals();
                if (max <= 0)
                {
                    ModLog.Info("Seal all: the player has no seal.");
                    return;
                }

                PlayerOptionsData config = options.Config;
                int used = options.GetUsedSeals();
                int added = 0;
                var items = page.GetComponentsInChildren<CollectionItemUI>(true);
                for (int i = 0; i < items.Length && used < max; i++)
                {
                    CollectionItemUI item = items[i];
                    if (item == null) continue;
                    if (TrySealWeapon(config, item) || TrySealItem(config, item))
                    {
                        used++;
                        added++;
                    }
                }

                SoundManager.PlaySound(added > 0 ? SfxType.Banish : SfxType.ClickOut);
                SealPanel panel = page.GetComponentInChildren<SealPanel>(true);
                if (panel != null) panel.UpdateValues();
                ModLog.Info("Seal all: " + added + " new seals. used=" + used + " max=" + max);
            }
            catch (Exception e)
            {
                ModLog.Error("Seal all failed", e);
            }
        }

        private static bool TrySealWeapon(PlayerOptionsData config, CollectionItemUI item)
        {
            WeaponType type = item.GetWeaponType();
            if (type == WeaponType.VOID) return false;
            WeaponData data = item.GetWeaponData();
            if (data == null || !data.seen || !data.sealable) return false;
            if (config.SealedWeapons.Contains(type) || config.ContentGroupSealedWeapons.Contains(type)) return false;
            config.SealedWeapons.Add(type);
            item.Seal();
            return true;
        }

        private static bool TrySealItem(PlayerOptionsData config, CollectionItemUI item)
        {
            ItemType type = item.GetItemType();
            if (type == ItemType.VOID) return false;
            ItemData data = item.GetItemData();
            if (data == null || !data.seen || !data.sealable) return false;
            if (config.SealedItems.Contains(type) || config.ContentGroupSealedItems.Contains(type)) return false;
            config.SealedItems.Add(type);
            item.Seal();
            return true;
        }
    }
}

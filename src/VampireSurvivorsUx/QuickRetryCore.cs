using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if MELONLOADER
// MelonLoader generates the interop assemblies with the Il2Cpp namespace prefix.
using Il2CppI2.Loc;
using Il2CppTMPro;
using Il2CppVampireSurvivors;
using Il2CppVampireSurvivors.App.Scripts.Framework.Adventures;
using Il2CppVampireSurvivors.Data;
using Il2CppVampireSurvivors.Data.Stage;
using Il2CppVampireSurvivors.Framework;
using Il2CppVampireSurvivors.Objects;
using Il2CppVampireSurvivors.UI;
#else
// The BepInEx interop assemblies and the Mono game assemblies have no namespace prefix.
using I2.Loc;
using TMPro;
using VampireSurvivors;
using VampireSurvivors.App.Scripts.Framework.Adventures;
using VampireSurvivors.Data;
using VampireSurvivors.Data.Stage;
using VampireSurvivors.Framework;
using VampireSurvivors.Objects;
using VampireSurvivors.UI;
#endif

namespace VampireSurvivorsUx
{
    internal enum PendingAction
    {
        None,
        Retry,
        NextStage,
        NextNewStage,
    }

    /// <summary>Loader-independent mod logic. The loader entry point calls <see cref="Initialize"/>.</summary>
    internal static class QuickRetryCore
    {
        public const string Version = "0.1.1";
        private const string RetryButtonName = "QuickRetry_RetryButton";
        private const string NextStageButtonName = "QuickRetry_NextStageButton";
        private const string NextNewStageButtonName = "QuickRetry_NextNewStageButton";
        private const float ButtonGap = 12f;
        private const float StartDelaySeconds = 1.6f;

        public static PendingAction Pending = PendingAction.None;
        public static RunSnapshot Snapshot;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            harmony.PatchAll(typeof(QuickRetryCore).Assembly);
            ModLog.Info("VampireSurvivorsUx " + Version + " patched.");
        }

        // ---------------------------------------------------------------- recap page

        [HarmonyPatch(typeof(RecapPage), "OnShowStart")]
        private static class RecapPage_OnShowStart_Patch
        {
            private static void Postfix(RecapPage __instance)
            {
                try
                {
                    OnRecapShown(__instance);
                }
                catch (Exception e)
                {
                    ModLog.Error("RecapPage.OnShowStart postfix failed", e);
                }
            }
        }

        private static void OnRecapShown(RecapPage page)
        {
            Pending = PendingAction.None;
            bool allowed = IsRetryAllowed(out string reason);
            if (!allowed)
            {
                ModLog.Info("Buttons hidden: " + reason);
                SetButtonsActive(page, false);
                Snapshot = null;
                return;
            }

            PlayerOptions options = Priv.PlayerOptionsOf(page) ?? SystemPlatform.Instance?.PlayerOptions;
            MultiplayerManager multiplayer = MultiplayerManager.Instance;
            if (options == null)
            {
                ModLog.Warn("PlayerOptions not found. Buttons hidden.");
                SetButtonsActive(page, false);
                return;
            }

            Snapshot = RunSnapshot.Capture(options, multiplayer);
            ModLog.Info("Snapshot: " + Snapshot.Describe());
            EnsureButtons(page);
        }

        private static bool IsRetryAllowed(out string reason)
        {
            GameManager gm = GM.Core;
            if (gm != null && gm.StartedAsOnlineMultiplayerRun)
            {
                reason = "online run";
                return false;
            }
            MultiplayerManager multiplayer = MultiplayerManager.Instance;
            if (multiplayer != null && multiplayer.IsOnlineMultiplayer)
            {
                reason = "online multiplayer";
                return false;
            }
            if (AdventureManager.IsInAdventureMode)
            {
                reason = "adventure mode";
                return false;
            }
            reason = null;
            return true;
        }

        private static void SetButtonsActive(RecapPage page, bool active)
        {
            Selectable done = Priv.DoneButton(page);
            if (done == null) return;
            Transform parent = done.transform.parent;
            if (parent == null) return;
            Transform retry = parent.Find(RetryButtonName);
            if (retry != null) retry.gameObject.SetActive(active);
            Transform next = parent.Find(NextStageButtonName);
            if (next != null) next.gameObject.SetActive(active);
            Transform nextNew = parent.Find(NextNewStageButtonName);
            if (nextNew != null) nextNew.gameObject.SetActive(active);
        }

        private static void EnsureButtons(RecapPage page)
        {
            Selectable done = Priv.DoneButton(page);
            if (done == null)
            {
                ModLog.Warn("_DoneButton is null. Buttons not added.");
                return;
            }
            Transform parent = done.transform.parent;
            RectTransform doneRect = done.GetComponent<RectTransform>();
            float width = doneRect != null ? doneRect.rect.width : 200f;

            LayoutGroup layout = parent != null ? parent.GetComponent<LayoutGroup>() : null;
            ModLog.Info("Done button parent=" + (parent != null ? parent.name : "null")
                + " layout=" + (layout != null ? Interop.TypeNameOf(layout) : "none")
                + " width=" + width
                + " anchoredPos=" + (doneRect != null ? doneRect.anchoredPosition.ToString() : "?"));

            GameObject retry = GetOrCreateButton(page, done, RetryButtonName, "Retry", 1, width, layout != null, OnRetryClicked);
            GameObject next = GetOrCreateButton(page, done, NextStageButtonName, "Next stage", 2, width, layout != null, OnNextStageClicked);
            GameObject nextNew = GetOrCreateButton(page, done, NextNewStageButtonName, "Next new", 3, width, layout != null, OnNextNewStageClicked);
            if (retry != null) retry.SetActive(true);
            if (next != null) next.SetActive(true);
            if (nextNew != null) nextNew.SetActive(true);
        }

        private static GameObject GetOrCreateButton(RecapPage page, Selectable done, string name, string label,
            int slotsLeftOfDone, float width, bool parentHasLayout, Action onClick)
        {
            Transform parent = done.transform.parent;
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing != null) return existing.gameObject;

            GameObject clone = UnityEngine.Object.Instantiate(done.gameObject, parent, false);
            clone.name = name;

            if (parentHasLayout)
            {
                clone.transform.SetSiblingIndex(done.transform.GetSiblingIndex());
            }
            else
            {
                RectTransform rect = clone.GetComponent<RectTransform>();
                RectTransform doneRect = done.GetComponent<RectTransform>();
                if (rect != null && doneRect != null)
                {
                    Vector2 pos = doneRect.anchoredPosition;
                    pos.x -= slotsLeftOfDone * (width + ButtonGap);
                    rect.anchoredPosition = pos;
                }
            }

            // Label. Disable the localization component first, otherwise it rewrites the text.
            var localize = clone.GetComponentInChildren<Localize>(true);
            if (localize != null) localize.enabled = false;
            var text = clone.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                ModLog.Info(name + ": label fontSize=" + text.fontSize + " autoSize=" + text.enableAutoSizing
                    + " min=" + text.fontSizeMin + " max=" + text.fontSizeMax + " overflow=" + text.overflowMode
                    + " rectW=" + text.rectTransform.rect.width + " margin=" + text.margin);
                text.enableAutoSizing = false;
                text.overflowMode = TextOverflowModes.Overflow;
                text.text = label;
                if (label.Length > 6) text.fontSize = text.fontSize * 0.7f;
            }
            else ModLog.Warn(name + ": no TextMeshProUGUI child found.");

            // Focus helper: do not steal the default selection from Done.
            var selectableUi = clone.GetComponent<SelectableUI>();
            if (selectableUi != null)
            {
                selectableUi.IsDefaultSelectedOnPage = false;
                selectableUi.ReselectIfDefaultSelectedOnPage = false;
            }

            // Click. Replace the event to drop the persistent DoneClicked listener copied from the prefab.
            Button button = clone.GetComponent<Button>();
            if (button == null)
            {
                ModLog.Warn(name + ": clone has no Button component. Removing it.");
                UnityEngine.Object.Destroy(clone);
                return null;
            }
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(Interop.ToUnityAction(onClick));
            return clone;
        }

        private static RecapPage FindRecapPage()
        {
            return UnityEngine.Object.FindObjectOfType<RecapPage>();
        }

        private static void OnRetryClicked()
        {
            try
            {
                if (Snapshot == null) { ModLog.Warn("Retry: no snapshot."); return; }
                Pending = PendingAction.Retry;
                ModLog.Info("Retry clicked. Stage=" + Snapshot.SelectedStage);
                RecapPage page = FindRecapPage();
                if (page == null) { ModLog.Error("Retry: RecapPage not found."); Pending = PendingAction.None; return; }
                page.DoneClicked();
            }
            catch (Exception e)
            {
                Pending = PendingAction.None;
                ModLog.Error("Retry failed", e);
            }
        }

        private static void OnNextStageClicked()
        {
            StartOnOtherStage(PendingAction.NextStage, onlyIncomplete: false);
        }

        private static void OnNextNewStageClicked()
        {
            StartOnOtherStage(PendingAction.NextNewStage, onlyIncomplete: true);
        }

        /// <summary>Moves the snapshot to another stage, then runs the normal Done flow.</summary>
        private static void StartOnOtherStage(PendingAction action, bool onlyIncomplete)
        {
            try
            {
                if (Snapshot == null) { ModLog.Warn(action + ": no snapshot."); return; }
                PlayerOptions options = SystemPlatform.Instance?.PlayerOptions;
                DataManager data = SystemPlatform.Instance?.DataManager;
                if (options == null || data == null)
                {
                    ModLog.Error(action + ": SystemPlatform services missing.");
                    return;
                }
                StageType current = Snapshot.SelectedStage;
                CharacterType character = Snapshot.SelectedCharacter;
                if (!TryGetNextStage(data, options, current, onlyIncomplete ? character : (CharacterType?)null, out StageType next, out StageData nextData))
                {
                    if (onlyIncomplete)
                    {
                        ModLog.Warn(action + ": every unlocked stage is complete for " + character + ". Using the next stage.");
                        if (!TryGetNextStage(data, options, current, null, out next, out nextData)) next = current;
                    }
                    else
                    {
                        ModLog.Warn(action + ": no other unlocked stage. Falling back to retry.");
                        next = current;
                    }
                }
                if (next != current)
                {
                    Snapshot.SelectedStage = next;
                    ApplyBgmForStage(data, options, nextData);
                }
                Pending = action;
                ModLog.Info(action + " clicked. " + current + " -> " + next);
                RecapPage page = FindRecapPage();
                if (page == null) { ModLog.Error(action + ": RecapPage not found."); Pending = PendingAction.None; return; }
                page.DoneClicked();
            }
            catch (Exception e)
            {
                Pending = PendingAction.None;
                ModLog.Error(action + " failed", e);
            }
        }

        // ---------------------------------------------------------------- stage order

        /// <summary>
        /// Picks the stage after <paramref name="current"/> in stage-select order.
        /// Uses only unlocked stages. Skips STAGEX, MACHINE, and MACHINE2 like quick start does.
        /// Wraps to the first stage.
        /// </summary>
        /// <param name="incompleteFor">When set, only stages that this character has not completed are candidates.</param>
        internal static bool TryGetNextStage(DataManager data, PlayerOptions options, StageType current,
            CharacterType? incompleteFor, out StageType next, out StageData nextData)
        {
            next = current;
            nextData = null;
            // Copy the completion log into a managed set. The game list type differs between IL2CPP and Mono.
            System.Collections.Generic.HashSet<StageType> completed = null;
            if (incompleteFor.HasValue)
            {
                var log = options.Config.StageCompletionLog;
                if (log != null && log.ContainsKey(incompleteFor.Value))
                {
                    var done = log[incompleteFor.Value];
                    completed = new System.Collections.Generic.HashSet<StageType>();
                    for (int i = 0; done != null && i < done.Count; i++) completed.Add(done[i]);
                }
            }
            var available = StageSelectPage.GetAvailableStages(data, options);
            if (available == null) return false;

            var ordered = new System.Collections.Generic.List<(int order, StageType type, StageData data)>();
            foreach (StageType type in (StageType[])Enum.GetValues(typeof(StageType)))
            {
                if (type == StageType.STAGEX || type == StageType.MACHINE || type == StageType.MACHINE2) continue;
                if (!available.ContainsKey(type)) continue;
                var list = available[type];
                if (list == null || list.Count == 0) continue;
                StageData sd = list[0];
                if (sd == null || !sd.unlocked) continue;
                ordered.Add((sd.order, type, sd));
            }
            if (ordered.Count == 0) return false;
            ordered.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : ((int)a.type).CompareTo((int)b.type));

            int index = ordered.FindIndex(t => t.type == current);
            for (int step = 1; step <= ordered.Count; step++)
            {
                var candidate = ordered[(Math.Max(index, -1) + step) % ordered.Count];
                if (candidate.type == current) continue;
                if (incompleteFor.HasValue && completed != null && completed.Contains(candidate.type)) continue;
                next = candidate.type;
                nextData = candidate.data;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Same rule as the song panel: a locked track stays. Else the character track wins.
        /// Else the stage track (side B when inverse is on).
        /// </summary>
        private static void ApplyBgmForStage(DataManager data, PlayerOptions options, StageData stage)
        {
            if (Snapshot.SelectedBGMSave || stage == null) return;

            BgmType bgm = stage.BGM;
            var sideB = stage.sideBBGM;
            if (Snapshot.SelectedInverse && sideB != null && sideB.HasValue) bgm = sideB.Value;

            try
            {
                var characters = data.GetConvertedCharacterData();
                if (characters != null && characters.ContainsKey(Snapshot.SelectedCharacter))
                {
                    var list = characters[Snapshot.SelectedCharacter];
                    if (list != null && list.Count > 0 && list[0] != null && !string.IsNullOrEmpty(list[0].bgm))
                    {
                        bgm = (BgmType)Enum.Parse(typeof(BgmType), list[0].bgm);
                    }
                }
            }
            catch (Exception e)
            {
                ModLog.Warn("Character BGM lookup failed, using stage BGM: " + e.Message);
            }

            Snapshot.SelectedBGM = bgm;
            Snapshot.SelectedBGMMod = BgmModType.Normal;
        }

        // ---------------------------------------------------------------- character double click

        private const float DoubleClickSeconds = 0.45f;
        private static ObjId _lastClickedItem;
        private static float _lastClickTime;

        /// <summary>Called every frame by the loader entry point. A double click on a character selects and confirms it.</summary>
        public static void OnFrame()
        {
            try
            {
                if (!Input.GetMouseButtonDown(0)) return;
                EventSystem es = EventSystem.current;
                GameObject selected = es != null ? es.currentSelectedGameObject : null;
                CharacterItemUI item = selected != null ? selected.GetComponent<CharacterItemUI>() : null;
                if (item == null) { _lastClickedItem = ObjId.None; return; }

                ObjId id = ObjId.Of(item);
                float now = Time.unscaledTime;
                bool isDouble = id.Same(_lastClickedItem) && now - _lastClickTime <= DoubleClickSeconds;
                _lastClickedItem = isDouble ? ObjId.None : id;
                _lastClickTime = now;
                if (!isDouble) return;

                if (!item.IsAvailable())
                {
                    ModLog.Info("Double click on a locked character. Ignored.");
                    return;
                }
                CharacterSelectionPage page = UnityEngine.Object.FindObjectOfType<CharacterSelectionPage>();
                if (page == null) return;
                ModLog.Info("Double click: confirming " + item.Type + ".");
                page.SelectCharacter(false);
                FrameScheduler.RunAfterFrames(1, () =>
                {
                    if (page != null) page.ConfirmCharacter();
                });
            }
            catch (Exception e)
            {
                ModLog.Error("Double click handler failed", e);
            }
        }

        // ---------------------------------------------------------------- popup double click

        private static ObjId _lastPopup;
        private static int _lastPopupIndex = -1;
        private static float _lastPopupTime;

        /// <summary>
        /// Party size and CPU type use LargeMultiOptionPopup. A click on an option calls SelectOption and moves the
        /// selection to the Confirm button. The same option selected twice within the double click time confirms.
        /// </summary>
        [HarmonyPatch(typeof(LargeMultiOptionPopup), nameof(LargeMultiOptionPopup.SelectOption), new[] { typeof(GameObject) })]
        private static class LargeMultiOptionPopup_SelectOption_Patch
        {
            private static void Postfix(LargeMultiOptionPopup __instance)
            {
                try
                {
                    ObjId id = ObjId.Of(__instance);
                    int index = Priv.SelectedIndex(__instance);
                    float now = Time.unscaledTime;
                    bool isDouble = id.Same(_lastPopup) && index == _lastPopupIndex && now - _lastPopupTime <= DoubleClickSeconds;
                    _lastPopup = isDouble ? ObjId.None : id;
                    _lastPopupIndex = index;
                    _lastPopupTime = now;
                    if (!isDouble) return;
                    ModLog.Info("Double click on popup option " + index + ": confirming.");
                    FrameScheduler.RunAfterFrames(1, () =>
                    {
                        if (__instance != null) __instance.Confirm();
                    });
                }
                catch (Exception e)
                {
                    ModLog.Error("Popup double click failed", e);
                }
            }
        }

        // ---------------------------------------------------------------- landing page

        /// <summary>After the recap the app shows the "press to start" page. Skip it when a retry is pending.</summary>
        [HarmonyPatch(typeof(AppLandingPageState), nameof(AppLandingPageState.OnEnter))]
        private static class AppLandingPageState_OnEnter_Patch
        {
            private static void Postfix()
            {
                if (Pending == PendingAction.None || Snapshot == null) return;
                ModLog.Info("Landing page: skipping to the main menu for " + Pending + ".");
                FrameScheduler.RunAfterFrames(2, () =>
                {
                    AppStateMachine sm = AppStateMachine.Instance;
                    if (sm == null || !Interop.Is<AppLandingPageState>(sm.CurrentState)) return;
                    sm.FireEvent("MAIN_MENU");
                });
            }
        }

        // ---------------------------------------------------------------- main menu

        [HarmonyPatch(typeof(AppMainMenuState), nameof(AppMainMenuState.OnEnter))]
        private static class AppMainMenuState_OnEnter_Patch
        {
            private static void Postfix()
            {
                try
                {
                    OnMainMenuEntered();
                }
                catch (Exception e)
                {
                    Pending = PendingAction.None;
                    ModLog.Error("AppMainMenuState.OnEnter postfix failed", e);
                }
            }
        }

        private static void OnMainMenuEntered()
        {
            if (Pending == PendingAction.None) return;
            PendingAction action = Pending;
            Pending = PendingAction.None;

            RunSnapshot snapshot = Snapshot;
            if (snapshot == null)
            {
                ModLog.Warn("Main menu: pending " + action + " but no snapshot.");
                return;
            }
            PlayerOptions options = SystemPlatform.Instance?.PlayerOptions;
            MultiplayerManager multiplayer = MultiplayerManager.Instance;
            if (options == null)
            {
                ModLog.Error("Main menu: PlayerOptions missing. Cannot restore.");
                return;
            }

            snapshot.Restore(options, multiplayer);
            ModLog.Info("Restored for " + action + ": " + snapshot.Describe());

            // The main menu background runs a 1 s pixelate tween. AppGameplayState.OnEnter kills all tweens.
            // A start before the tween completes leaves the pixelate render feature active in the run.
            FrameScheduler.RunAfterSeconds(StartDelaySeconds, StartGameFromMainMenu);
        }

        private static void StartGameFromMainMenu()
        {
            AppStateMachine sm = AppStateMachine.Instance;
            if (sm == null)
            {
                ModLog.Error("Start: AppStateMachine.Instance is null.");
                return;
            }
            var state = sm.CurrentState;
            if (!Interop.Is<AppMainMenuState>(state))
            {
                ModLog.Error("Start: app state is not the main menu. Aborting.");
                return;
            }
            ModLog.Info("Firing START_GAME.");
            sm.FireEvent("START_GAME");
        }
    }
}

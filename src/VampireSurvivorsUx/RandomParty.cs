using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
#if MELONLOADER
// MelonLoader generates the interop assemblies with the Il2Cpp namespace prefix.
using Il2CppI2.Loc;
using Il2CppTMPro;
using Il2CppVampireSurvivors;
using Il2CppVampireSurvivors.App.Scripts.Framework.Adventures;
using Il2CppVampireSurvivors.App.Scripts.UI;
using Il2CppVampireSurvivors.Data;
using Il2CppVampireSurvivors.Data.Stage;
using Il2CppVampireSurvivors.Framework;
using Il2CppVampireSurvivors.Graphics;
using Il2CppVampireSurvivors.Objects;
using Il2CppVampireSurvivors.Objects.Algorithm;
using Il2CppVampireSurvivors.UI;
#else
// The BepInEx interop assemblies and the Mono game assemblies have no namespace prefix.
using I2.Loc;
using TMPro;
using VampireSurvivors;
using VampireSurvivors.App.Scripts.Framework.Adventures;
using VampireSurvivors.App.Scripts.UI;
using VampireSurvivors.Data;
using VampireSurvivors.Data.Stage;
using VampireSurvivors.Framework;
using VampireSurvivors.Graphics;
using VampireSurvivors.Objects;
using VampireSurvivors.Objects.Algorithm;
using VampireSurvivors.UI;
#endif

namespace VampireSurvivorsUx
{
    /// <summary>
    /// The Random party button on the main menu. It asks one time for the CPU behaviour, then it shows the run
    /// modifiers. It fills the four local slots with random bought characters and starts a run on the next stage
    /// that the main character has not completed.
    /// </summary>
    internal static class RandomParty
    {
        private const string ButtonName = "VampireSurvivorsUx_RandomPartyButton";
        private const int SlotCount = 4;
        private const float ButtonGap = 12f;

        // A tick box of the game is 100x120 and its label is 137 wide, so a cell needs more than the box.
        // The stage select page puts the boxes about 140 apart. Four of them fit in the width of the popup.
        private const int GridColumns = 4;
        private const float CellWidth = 150f;
        private const float CellHeight = 150f;
        private const float CellSpacing = 10f;
        private const float HostPadding = 20f;

        private static readonly System.Random Rng = new System.Random();

        /// <summary>The time of the last main menu show. The start waits for the pixelate tween of the menu.</summary>
        private static float _menuShownAt;

        // The choice of the player between the popups.
        private static AIType _ai;
        private static List<CharacterType> _picks;
        private static StageType _stage;
        private static List<Row> _rows;

        // The open modifier popup. A click on a line toggles that line in place.
        private static LargeMultiOptionPopup _popup;
        private static ObjId _popupId;
        private static readonly Dictionary<Row, TickBoxUI> Boxes = new Dictionary<Row, TickBoxUI>();

        /// <summary>Every popup needs its own id, because the mod opens the next one before the old one closes.</summary>
        private static int _popupSerial;

        /// <summary>One modifier of the run. Every one is a tick box in the popup.</summary>
        private enum Row
        {
            Hyper,
            Hurry,
            Arcanas,
            LimitBreak,
            Inverse,
            Endless,
            RandomEvents,
            RandomLevels,
            SharePassives,
            GoldenEggs,
            Survarots,
        }

        // ---------------------------------------------------------------- button

        [HarmonyPatch(typeof(MainMenuPage), "OnShowStart")]
        private static class MainMenuPage_OnShowStart_Patch
        {
            private static void Postfix(MainMenuPage __instance)
            {
                try
                {
                    OnMainMenuShown(__instance);
                }
                catch (Exception e)
                {
                    ModLog.Error("MainMenuPage.OnShowStart postfix failed", e);
                }
            }
        }

        private static void OnMainMenuShown(MainMenuPage page)
        {
            _menuShownAt = Time.unscaledTime;

            Button quickStart = Priv.QuickStartButton(page);
            if (quickStart == null)
            {
                ModLog.Warn("Random party: _QuickStartButton is null. Button not added.");
                return;
            }

            bool allowed = IsAllowed(out string reason);
            Transform existing = quickStart.transform.Find(ButtonName);
            if (existing == null)
            {
                if (!allowed)
                {
                    ModLog.Info("Random party button hidden: " + reason);
                    return;
                }
                existing = CreateButton(quickStart);
                if (existing == null) return;
            }
            existing.gameObject.SetActive(allowed);
        }

        /// <summary>Party mode needs the party relic, same rule as the character selection page.</summary>
        private static bool IsAllowed(out string reason)
        {
            if (AdventureManager.IsInAdventureMode)
            {
                reason = "adventure mode";
                return false;
            }
            MultiplayerManager multiplayer = MultiplayerManager.Instance;
            if (multiplayer != null && multiplayer.IsOnlineMultiplayer)
            {
                reason = "online multiplayer";
                return false;
            }
            PlayerOptions options = SystemPlatform.Instance?.PlayerOptions;
            if (options == null)
            {
                reason = "no player options";
                return false;
            }
            PlayerOptionsData config = options.Config;
            if (!config.CollectedItems.Contains(ItemType.RELIC_PARTY))
            {
                reason = "party relic not collected";
                return false;
            }
            if (config.SealedItems.Contains(ItemType.RELIC_PARTY))
            {
                reason = "party relic sealed";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>
        /// Clones the Quick start button. The clone is a child of that button, so it follows every layout move of
        /// the menu and it hides with the button of the game.
        /// </summary>
        private static Transform CreateButton(Button quickStart)
        {
            RectTransform sourceRect = quickStart.GetComponent<RectTransform>();
            float height = sourceRect != null ? sourceRect.rect.height : 60f;

            GameObject clone = UnityEngine.Object.Instantiate(quickStart.gameObject, quickStart.transform, false);
            clone.name = ButtonName;
            clone.transform.localScale = Vector3.one;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localPosition = new Vector3(0f, height + ButtonGap, 0f);

            // Label. Disable the localization component first, otherwise it rewrites the text.
            var localize = clone.GetComponentInChildren<Localize>(true);
            if (localize != null) localize.enabled = false;
            var text = clone.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.enableAutoSizing = false;
                text.overflowMode = TextOverflowModes.Overflow;
                text.text = "Random party";
                text.fontSize = text.fontSize * 0.7f;
            }
            else ModLog.Warn("Random party: no TextMeshProUGUI child found.");

            // Focus helper: do not steal the default selection of the menu.
            var selectableUi = clone.GetComponent<SelectableUI>();
            if (selectableUi != null)
            {
                selectableUi.IsDefaultSelectedOnPage = false;
                selectableUi.ReselectIfDefaultSelectedOnPage = false;
            }

            Button button = clone.GetComponent<Button>();
            if (button == null)
            {
                ModLog.Warn("Random party: the clone has no Button component. Removing it.");
                UnityEngine.Object.Destroy(clone);
                return null;
            }
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(Interop.ToUnityAction(OnClicked));

            ModLog.Info("Random party button added. quickStart=" + quickStart.name
                + " height=" + height
                + " pos=" + (sourceRect != null ? sourceRect.anchoredPosition.ToString() : "?"));
            return clone.transform;
        }

        // ---------------------------------------------------------------- CPU behaviour popup

        private static void OnClicked()
        {
            try
            {
                DataManager data = SystemPlatform.Instance?.DataManager;
                if (data == null)
                {
                    ModLog.Error("Random party: DataManager missing.");
                    return;
                }
                var types = new[] { AIType.Aggressive, AIType.Defensive };
                var labels = new string[types.Length];
                var values = new string[types.Length];
                var icons = new Sprite[types.Length];
                for (int i = 0; i < types.Length; i++)
                {
                    labels[i] = AiLabel(data, types[i], out icons[i]);
                    values[i] = string.Empty;
                }
                ModLog.Info("Random party: asking for the CPU behaviour.");
                Popups.ShowOptions(NextPopupId(), "Random party", "CPU behaviour", labels, values, icons,
                    index => OnAiPicked(index >= 0 && index < types.Length ? types[index] : AIType.Aggressive),
                    OnCancelled);
            }
            catch (Exception e)
            {
                ModLog.Error("Random party: the popup failed", e);
            }
        }

        private static string AiLabel(DataManager data, AIType type, out Sprite icon)
        {
            icon = null;
            string fallback = type.ToString();
            var all = data.AllCPU;
            if (all == null || !all.ContainsKey(type)) return fallback;
            AIData ai = all[type];
            if (ai == null) return fallback;
#pragma warning disable 618 // The game itself uses this overload for the CPU icons.
            icon = SpriteManager.GetSprite(ai.AIIconSprite, ai.AIIconTexture, true);
#pragma warning restore 618
            string translated = LocalizationManager.GetTranslation(ai.AINameLocalTerm);
            return string.IsNullOrEmpty(translated) ? fallback : translated;
        }

        private static void OnAiPicked(AIType ai)
        {
            try
            {
                PlayerOptions options = SystemPlatform.Instance?.PlayerOptions;
                DataManager data = SystemPlatform.Instance?.DataManager;
                if (options == null || data == null)
                {
                    ModLog.Error("Random party: game services missing. No run started.");
                    return;
                }
                _ai = ai;
                _picks = PickCharacters(options, data, SlotCount);
                _stage = PickStage(data, options, _picks[0]);
                ModLog.Info("Random party: cpu=" + ai + " main=" + _picks[0] + " stage=" + _stage);
                ShowModifiers(options, data);
            }
            catch (Exception e)
            {
                ModLog.Error("Random party: the character pick failed", e);
            }
        }

        // ---------------------------------------------------------------- modifier popup

        /// <summary>
        /// Shows the run modifiers as the tick boxes of the stage select page. The mod clones every tick box of
        /// that page, so the art, the label, and the sound are the ones of the game. Confirm starts the run.
        /// </summary>
        private static void ShowModifiers(PlayerOptions options, DataManager data)
        {
            PlayerOptionsData config = options.Config;
            _rows = BuildRows(config, _stage);
            Boxes.Clear();
            string stageName = StageName(data, _stage);
            // One line of the popup hosts the tick boxes. The popup needs at least one line, because its own
            // animation reads the selected line.
            LargeMultiOptionPopup popup = Popups.ShowOptions(NextPopupId(), "Random party", stageName,
                new[] { string.Empty }, new[] { string.Empty }, null, OnConfirmed, OnCancelled);
            if (popup == null) return;
            _popup = popup;
            _popupId = ObjId.Of(popup);
            // The popup builds its lines in a coroutine, so fill the host one frame later.
            FrameScheduler.RunAfterFrames(2, () => BuildTickBoxes(config));
        }

        private static void BuildTickBoxes(PlayerOptionsData config)
        {
            try
            {
                if (_popup == null || _rows == null) return;
                GameObject[] items = Priv.SpawnedOptions(_popup);
                if (items.Length == 0)
                {
                    ModLog.Warn("Random party: the popup has no line to host the tick boxes.");
                    return;
                }
                var pages = Resources.FindObjectsOfTypeAll<StageSelectPage>();
                if (pages == null || pages.Length == 0)
                {
                    ModLog.Warn("Random party: no StageSelectPage found. No tick boxes.");
                    return;
                }
                StageSelectPage page = pages[0];
                TickBoxUI[] stageBoxes = Priv.StageTickBoxes(page);
                StageRandomPanel randomPanel = Priv.RandomPanel(page);

                // The host is one line of the popup. Clear its texts and its own click.
                GameObject host = items[0];
                var item = host.GetComponent<LargeMultiOptionPopupItem>();
                if (item != null)
                {
                    if (item.Title != null) item.Title.text = string.Empty;
                    if (item.Description != null) item.Description.text = string.Empty;
                    item.SetTick(false);
                }
                var hostButton = host.GetComponent<Button>();
                if (hostButton != null) hostButton.onClick = new Button.ButtonClickedEvent();

                int columns = Math.Min(_rows.Count, GridColumns);
                int lines = (_rows.Count + GridColumns - 1) / GridColumns;
                float gridWidth = columns * CellWidth + (columns - 1) * CellSpacing;
                float gridHeight = lines * CellHeight + (lines - 1) * CellSpacing;
                float height = gridHeight + HostPadding;

                // The line of the popup is the grey background. Its height comes from the layout element.
                var hostRect = host.GetComponent<RectTransform>();
                var hostLayout = host.GetComponent<LayoutElement>();
                if (hostLayout == null) hostLayout = host.AddComponent<LayoutElement>();
                hostLayout.minHeight = height;
                hostLayout.preferredHeight = height;
                hostLayout.flexibleHeight = 0f;
                if (hostRect != null) hostRect.sizeDelta = new Vector2(hostRect.sizeDelta.x, height);
                var fitter = host.GetComponent<ContentSizeFitter>();
                if (fitter != null) fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                ModLog.Info("Random party: tick box host=" + host.name
                    + " parent=" + (host.transform.parent != null ? host.transform.parent.name : "null")
                    + " parentLayout=" + LayoutNameOf(host.transform.parent)
                    + " fitter=" + (fitter != null) + " wanted=" + height
                    + " grid=" + gridWidth + "x" + gridHeight + " lines=" + lines + " boxes=" + _rows.Count);

                var grid = new GameObject("VampireSurvivorsUx_Modifiers");
                grid.layer = host.layer;
                var gridRect = grid.AddComponent<RectTransform>();
                gridRect.SetParent(host.transform, false);
                // An explicit size, so the grid does not depend on the size of the line.
                gridRect.anchorMin = new Vector2(0.5f, 0.5f);
                gridRect.anchorMax = new Vector2(0.5f, 0.5f);
                gridRect.pivot = new Vector2(0.5f, 0.5f);
                gridRect.anchoredPosition = Vector2.zero;
                gridRect.sizeDelta = new Vector2(gridWidth, gridHeight);
                var layout = grid.AddComponent<GridLayoutGroup>();
                layout.cellSize = new Vector2(CellWidth, CellHeight);
                layout.spacing = new Vector2(CellSpacing, CellSpacing);
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = columns;
                layout.childAlignment = TextAnchor.MiddleCenter;

                for (int i = 0; i < _rows.Count; i++)
                {
                    Row row = _rows[i];
                    TickBoxUI template = TemplateFor(row, stageBoxes, randomPanel);
                    if (template == null)
                    {
                        ModLog.Warn("Random party: no tick box template for " + row + ".");
                        continue;
                    }
                    AddTickBox(grid.transform, template, row, config);
                }

                FrameScheduler.RunAfterFrames(2, () =>
                {
                    if (hostRect == null || gridRect == null) return;
                    ModLog.Info("Random party: after layout host=" + hostRect.rect.size
                        + " grid=" + gridRect.rect.size);
                });
            }
            catch (Exception e)
            {
                ModLog.Error("Random party: the tick boxes failed", e);
            }
        }

        private static string LayoutNameOf(Transform t)
        {
            if (t == null) return "none";
            var layout = t.GetComponent<LayoutGroup>();
            return layout != null ? Interop.TypeNameOf(layout) : "none";
        }

        /// <summary>The tick box of the game that matches the modifier.</summary>
        private static TickBoxUI TemplateFor(Row row, TickBoxUI[] stageBoxes, StageRandomPanel randomPanel)
        {
            switch (row)
            {
                case Row.Hyper: return At(stageBoxes, 0);
                case Row.Hurry: return At(stageBoxes, 1);
                case Row.Arcanas: return At(stageBoxes, 2);
                case Row.LimitBreak: return At(stageBoxes, 3);
                case Row.Inverse: return At(stageBoxes, 4);
                case Row.Endless: return At(stageBoxes, 5);
                case Row.SharePassives: return At(stageBoxes, 6);
                case Row.RandomEvents: return randomPanel != null ? randomPanel.RandomEventsTickBox : null;
                case Row.RandomLevels: return randomPanel != null ? randomPanel.RandomLevelUpsTickBox : null;
                // The power creep of the character page is a three way selector, not a tick box. The mod uses
                // one tick box of the stage page for each of the two values, with its own label.
                case Row.GoldenEggs: return At(stageBoxes, 1);
                case Row.Survarots: return At(stageBoxes, 1);
                default: return null;
            }
        }

        private static TickBoxUI At(TickBoxUI[] boxes, int index)
            => boxes != null && index < boxes.Length ? boxes[index] : null;

        private static void AddTickBox(Transform parent, TickBoxUI template, Row row, PlayerOptionsData config)
        {
            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
            clone.name = "VampireSurvivorsUx_" + row;
            clone.SetActive(true);
            clone.transform.localScale = Vector3.one;

            TickBoxUI box = clone.GetComponent<TickBoxUI>();
            if (box == null)
            {
                ModLog.Warn("Random party: the clone of " + row + " has no TickBoxUI.");
                UnityEngine.Object.Destroy(clone);
                return;
            }
            box.InitialSet(GetBool(config, row));

            string label = LabelOverride(row);
            if (label != null)
            {
                var localize = clone.GetComponentInChildren<Localize>(true);
                if (localize != null) localize.enabled = false;
                var text = clone.GetComponentInChildren<TextMeshProUGUI>(true);
                if (text != null) text.text = label;
            }

            var selectableUi = clone.GetComponent<SelectableUI>();
            if (selectableUi != null)
            {
                selectableUi.IsDefaultSelectedOnPage = false;
                selectableUi.ReselectIfDefaultSelectedOnPage = false;
            }

            // The click of the prefab calls Toggle(), and Toggle fires the OnToggle event of the stage select
            // page. That page has no selected stage here, so the mod replaces the click and writes the value.
            Button button = clone.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(Interop.ToUnityAction(() => OnTickBoxClicked(row, box)));
                Navigation navigation = button.navigation;
                navigation.mode = Navigation.Mode.Automatic;
                button.navigation = navigation;
            }
            Boxes[row] = box;
        }

        /// <summary>Every other line keeps the localized label of the game.</summary>
        private static string LabelOverride(Row row)
        {
            switch (row)
            {
                case Row.GoldenEggs: return "Golden eggs";
                case Row.Survarots: return "Survarots";
                default: return null;
            }
        }

        private static void OnTickBoxClicked(Row row, TickBoxUI box)
        {
            try
            {
                PlayerOptions options = SystemPlatform.Instance?.PlayerOptions;
                if (options == null) return;
                PlayerOptionsData config = options.Config;
                bool value = !GetBool(config, row);
                SetBool(config, row, value);
                box.InitialSet(value);
                // Golden eggs and survarots cannot run together, same rule as the character page.
                if (value && (row == Row.GoldenEggs || row == Row.Survarots))
                {
                    Row other = row == Row.GoldenEggs ? Row.Survarots : Row.GoldenEggs;
                    if (GetBool(config, other))
                    {
                        SetBool(config, other, false);
                        TickBoxUI otherBox;
                        if (Boxes.TryGetValue(other, out otherBox) && otherBox != null) otherBox.InitialSet(false);
                    }
                }
                SoundManager.PlaySound(value ? SfxType.ClickIn : SfxType.ClickOut);
                ModLog.Info("Random party: " + row + " -> " + (value ? "On" : "Off"));
            }
            catch (Exception e)
            {
                ModLog.Error("Random party: the tick box click failed", e);
            }
        }

        /// <summary>
        /// A click on the line that hosts the tick boxes. The mod swallows it, so the double click rule of the
        /// popup does not confirm the run. Returns true when the popup is the modifier popup of the mod.
        /// </summary>
        public static bool HandleOptionSelected(LargeMultiOptionPopup popup)
        {
            return _popup != null && _rows != null && ObjId.Of(popup).Same(_popupId);
        }

        private static List<Row> BuildRows(PlayerOptionsData config, StageType stage)
        {
            var rows = new List<Row>();
            if (config.UnlockedHypers != null && config.UnlockedHypers.Contains(stage)) rows.Add(Row.Hyper);
            if (config.HasCollectedItem(ItemType.RELIC_TEAR)) rows.Add(Row.Hurry);
            if (config.HasCollectedItem(ItemType.RELIC_RANDOMAZZO) || config.HasCollectedItem(ItemType.RELIC_DARKASSO))
            {
                rows.Add(Row.Arcanas);
            }
            if (config.HasCollectedItem(ItemType.RELIC_GGOSPEL)) rows.Add(Row.LimitBreak);
            if (config.HasCollectedItem(ItemType.RELIC_MIRROR)) rows.Add(Row.Inverse);
            if (config.HasCollectedItem(ItemType.RELIC_TRUMPET)) rows.Add(Row.Endless);
            if (config.HasCollectedItem(ItemType.RELIC_TRISECTION)) rows.Add(Row.RandomEvents);
            if (config.HasCollectedItem(ItemType.RELIC_BRAVESTORY)) rows.Add(Row.RandomLevels);
            rows.Add(Row.SharePassives);
            if (HasEggs(config)) rows.Add(Row.GoldenEggs);
            if (HasSurvarots(config)) rows.Add(Row.Survarots);
            return rows;
        }

        private static bool HasEggs(PlayerOptionsData config)
            => config.HasCollectedItem(ItemType.RELIC_GOLDENEGG) && !AdventureManager.IsInAdventureMode;

        private static bool HasSurvarots(PlayerOptionsData config)
            => config.HasCollectedItem(ItemType.RELIC_SURVAROCCHI);

        private static bool GetBool(PlayerOptionsData config, Row row)
        {
            switch (row)
            {
                case Row.Hyper: return config.SelectedHyper;
                case Row.Hurry: return config.SelectedHurry;
                case Row.Arcanas: return config.SelectedMazzo;
                case Row.LimitBreak: return config.SelectedLimitBreak;
                case Row.Inverse: return config.SelectedInverse;
                case Row.Endless: return config.SelectedReapers;
                case Row.RandomEvents: return config.SelectedRandomEvents;
                case Row.RandomLevels: return config.SelectedRandomLevels;
                case Row.SharePassives: return config.SelectedSharePassives;
                case Row.GoldenEggs: return config.SelectedGoldenEggs;
                case Row.Survarots: return config.SelectedSurvarots;
                default: return false;
            }
        }

        private static void SetBool(PlayerOptionsData config, Row row, bool value)
        {
            switch (row)
            {
                case Row.Hyper: config.SelectedHyper = value; break;
                case Row.Hurry: config.SelectedHurry = value; break;
                case Row.Arcanas: config.SelectedMazzo = value; break;
                case Row.LimitBreak: config.SelectedLimitBreak = value; break;
                case Row.Inverse: config.SelectedInverse = value; break;
                case Row.Endless: config.SelectedReapers = value; break;
                case Row.RandomEvents: config.SelectedRandomEvents = value; break;
                case Row.RandomLevels: config.SelectedRandomLevels = value; break;
                case Row.SharePassives: config.SelectedSharePassives = value; break;
                case Row.GoldenEggs: config.SelectedGoldenEggs = value; break;
                case Row.Survarots: config.SelectedSurvarots = value; break;
            }
        }

        /// <summary>Confirm on the modifier popup starts the run.</summary>
        private static void OnConfirmed(int index)
        {
            try
            {
                _popup = null;
                _popupId = ObjId.None;
                Boxes.Clear();
                PlayerOptions options = SystemPlatform.Instance?.PlayerOptions;
                DataManager data = SystemPlatform.Instance?.DataManager;
                if (options == null || data == null)
                {
                    ModLog.Error("Random party: game services missing. No run started.");
                    return;
                }
                StartRun(options, data);
            }
            catch (Exception e)
            {
                ModLog.Error("Random party: the start failed", e);
            }
        }

        private static void OnCancelled()
        {
            ModLog.Info("Random party: cancelled.");
            Boxes.Clear();
            _popup = null;
            _popupId = ObjId.None;
            _rows = null;
            _picks = null;
        }

        private static string NextPopupId()
        {
            _popupSerial++;
            return "VampireSurvivorsUx_RandomParty_" + _popupSerial;
        }

        private static string StageName(DataManager data, StageType stage)
        {
            StageData stageData = StageDataOf(data, stage);
            if (stageData == null) return stage.ToString();
            string translated = LocalizationManager.GetTranslation(stageData.GetLocalizedName(stage));
            return string.IsNullOrEmpty(translated) ? stageData.stageName : translated;
        }

        private static StageData StageDataOf(DataManager data, StageType stage)
        {
            var stages = data.GetConvertedStages();
            if (stages == null || !stages.ContainsKey(stage)) return null;
            var list = stages[stage];
            return list != null && list.Count > 0 ? list[0] : null;
        }

        // ---------------------------------------------------------------- party and run start

        private static void StartRun(PlayerOptions options, DataManager data)
        {
            MultiplayerManager multiplayer = MultiplayerManager.Instance;
            if (multiplayer == null || _picks == null)
            {
                ModLog.Error("Random party: no party to start.");
                return;
            }

            PlayerOptionsData config = options.Config;
            config.SelectedCharacter = _picks[0];
            config.SelectedStage = _stage;
            // Same clamp as the stage select page: a stage without a hyper unlock cannot run hyper.
            if (config.SelectedHyper && (config.UnlockedHypers == null || !config.UnlockedHypers.Contains(_stage)))
            {
                config.SelectedHyper = false;
                ModLog.Info("Random party: the stage has no hyper. Hyper off.");
            }
            // The BGM follows the inverse toggle, so pick it after the modifier popup.
            ApplyBgm(data, config, _stage, _picks[0]);

            var slots = multiplayer.GetLocalPlayerSlots();
            int count = Math.Min(slots != null ? slots.Count : 0, SlotCount);
            for (int i = 0; i < count; i++)
            {
                CoopSlotData slot = slots[i];
                slot.SelectedCharacter = _picks[i];
                slot.UnlockState = UIUnlockStates.AVAILABLE;
                if (i == 0 || slot.RewiredPlayer != null)
                {
                    // Slot 0 is the player. A slot with a controller keeps its human player.
                    slot.AIType = AIType.None;
                }
                else
                {
                    slot.AIType = _ai;
                }
            }
            if (count < SlotCount) ModLog.Warn("Random party: only " + count + " local slots exist.");
            if (slots != null && slots.Count > 0 && slots[0].RewiredPlayer == null)
            {
                ModLog.Warn("Random party: slot 0 has no controller.");
            }

            PartySizeField.Write(multiplayer, count);
            multiplayer.PartyModeEnabled = true;
            multiplayer.SelectPlayerOneToControlUI(true, false);
            multiplayer.Refresh();

            var names = new string[count];
            for (int i = 0; i < count; i++) names[i] = _picks[i] + "/" + slots[i].AIType;
            ModLog.Info("Random party: stage=" + _stage + " bgm=" + config.SelectedBGM
                + " hyper=" + config.SelectedHyper + " hurry=" + config.SelectedHurry
                + " arcanas=" + config.SelectedMazzo + " limitBreak=" + config.SelectedLimitBreak
                + " inverse=" + config.SelectedInverse + " endless=" + config.SelectedReapers
                + " randomEvents=" + config.SelectedRandomEvents + " randomLevels=" + config.SelectedRandomLevels
                + " eggs=" + config.SelectedGoldenEggs + " survarots=" + config.SelectedSurvarots
                + " sharePassives=" + config.SelectedSharePassives
                + " slots=[" + string.Join(", ", names) + "]");

            _rows = null;

            // The main menu background runs a 1 s pixelate tween. A start inside that second leaves the
            // pixelate render feature active for the whole run.
            float wait = Math.Max(0f, _menuShownAt + QuickRetryCore.StartDelaySeconds - Time.unscaledTime);
            FrameScheduler.RunAfterSeconds(wait, QuickRetryCore.StartGameFromMainMenu);
        }

        /// <summary>Random bought characters, no repeat while the pool is large enough.</summary>
        private static List<CharacterType> PickCharacters(PlayerOptions options, DataManager data, int count)
        {
            var pool = new List<CharacterType>();
            var bought = options.Config.BoughtCharacters;
            var characters = data.GetConvertedCharacterData();
            for (int i = 0; bought != null && i < bought.Count; i++)
            {
                CharacterType c = bought[i];
                if (characters != null && !characters.ContainsKey(c)) continue;
                if (!pool.Contains(c)) pool.Add(c);
            }
            // Same fallback as quick start: the four starting characters fill a small pool.
            var starters = new[] { CharacterType.ANTONIO, CharacterType.IMELDA, CharacterType.PASQUALINA, CharacterType.GENNARO };
            for (int i = 0; i < starters.Length && pool.Count < count; i++)
            {
                if (!pool.Contains(starters[i])) pool.Add(starters[i]);
            }
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                CharacterType tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }
            var picks = new List<CharacterType>();
            for (int i = 0; i < count; i++) picks.Add(pool[i % pool.Count]);
            return picks;
        }

        /// <summary>The next stage that the main character has not completed. Falls back to the next stage.</summary>
        private static StageType PickStage(DataManager data, PlayerOptions options, CharacterType character)
        {
            StageType current = options.Config.SelectedStage;
            if (QuickRetryCore.TryGetNextStage(data, options, current, character, out StageType next, out _)) return next;
            ModLog.Warn("Random party: every unlocked stage is complete for " + character + ". Using the next stage.");
            if (QuickRetryCore.TryGetNextStage(data, options, current, null, out next, out _)) return next;
            ModLog.Warn("Random party: no other unlocked stage. Keeping " + current + ".");
            return current;
        }

        private static void ApplyBgm(DataManager data, PlayerOptionsData config, StageType stage, CharacterType character)
        {
            if (config.SelectedBGMSave) return;
            StageData stageData = StageDataOf(data, stage);
            if (!QuickRetryCore.TryPickBgm(data, stageData, character, config.SelectedInverse, out BgmType bgm)) return;
            config.SelectedBGM = bgm;
            config.SelectedBGMMod = BgmModType.Normal;
        }
    }
}

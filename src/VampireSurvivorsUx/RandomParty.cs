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

        private static readonly System.Random Rng = new System.Random();

        /// <summary>The time of the last main menu show. The start waits for the pixelate tween of the menu.</summary>
        private static float _menuShownAt;

        // The choice of the player between the popups.
        private static AIType _ai;
        private static List<CharacterType> _picks;
        private static StageType _stage;
        private static List<Row> _rows;

        /// <summary>Every popup needs its own id, because the mod opens the next one before the old one closes.</summary>
        private static int _popupSerial;

        /// <summary>One line of the modifier popup.</summary>
        private enum Row
        {
            Start,
            Hyper,
            Hurry,
            Arcanas,
            LimitBreak,
            Inverse,
            Endless,
            RandomEvents,
            RandomLevels,
            SharePassives,
            PowerCreep,
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
                ShowModifiers(options, data, 0);
            }
            catch (Exception e)
            {
                ModLog.Error("Random party: the character pick failed", e);
            }
        }

        // ---------------------------------------------------------------- modifier popup

        /// <summary>
        /// Shows the run modifiers. Every line toggles one modifier and opens the popup again, so the player
        /// changes as many modifiers as necessary. The first line starts the run.
        /// </summary>
        private static void ShowModifiers(PlayerOptions options, DataManager data, int selectIndex)
        {
            PlayerOptionsData config = options.Config;
            _rows = BuildRows(config, _stage);
            var labels = new string[_rows.Count];
            var values = new string[_rows.Count];
            for (int i = 0; i < _rows.Count; i++)
            {
                labels[i] = RowLabel(_rows[i]);
                values[i] = RowValue(config, _rows[i]);
            }
            string stageName = StageName(data, _stage);
            LargeMultiOptionPopup popup = Popups.ShowOptions(NextPopupId(), "Random party", stageName,
                labels, values, null, OnRowPicked, OnCancelled);
            if (popup == null) return;
            if (selectIndex > 0 && selectIndex < _rows.Count) popup.SelectOption(selectIndex);
        }

        private static List<Row> BuildRows(PlayerOptionsData config, StageType stage)
        {
            var rows = new List<Row> { Row.Start };
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
            if (HasEggs(config) || HasSurvarots(config)) rows.Add(Row.PowerCreep);
            rows.Add(Row.SharePassives);
            return rows;
        }

        private static bool HasEggs(PlayerOptionsData config)
            => config.HasCollectedItem(ItemType.RELIC_GOLDENEGG) && !AdventureManager.IsInAdventureMode;

        private static bool HasSurvarots(PlayerOptionsData config)
            => config.HasCollectedItem(ItemType.RELIC_SURVAROCCHI);

        private static string RowLabel(Row row)
        {
            switch (row)
            {
                case Row.Start: return "Start run";
                case Row.Hyper: return "Hyper";
                case Row.Hurry: return "Hurry";
                case Row.Arcanas: return "Arcanas";
                case Row.LimitBreak: return "Limit break";
                case Row.Inverse: return "Inverse";
                case Row.Endless: return "Endless";
                case Row.RandomEvents: return "Random events";
                case Row.RandomLevels: return "Random level ups";
                case Row.SharePassives: return "Share passives";
                case Row.PowerCreep: return "Power creep";
                default: return row.ToString();
            }
        }

        private static string RowValue(PlayerOptionsData config, Row row)
        {
            switch (row)
            {
                case Row.Start: return string.Empty;
                case Row.PowerCreep:
                    if (config.SelectedGoldenEggs) return "Golden eggs";
                    if (config.SelectedSurvarots) return "Survarots";
                    return "Off";
                default: return GetBool(config, row) ? "On" : "Off";
            }
        }

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
            }
        }

        /// <summary>Power creep has three states, same as the selector of the character page.</summary>
        private static void CyclePowerCreep(PlayerOptionsData config)
        {
            bool eggs = HasEggs(config);
            bool survarots = HasSurvarots(config);
            if (config.SelectedGoldenEggs)
            {
                config.SelectedGoldenEggs = false;
                config.SelectedSurvarots = survarots;
            }
            else if (config.SelectedSurvarots)
            {
                config.SelectedGoldenEggs = false;
                config.SelectedSurvarots = false;
            }
            else
            {
                config.SelectedGoldenEggs = eggs;
                config.SelectedSurvarots = !eggs && survarots;
            }
        }

        private static void OnRowPicked(int index)
        {
            try
            {
                PlayerOptions options = SystemPlatform.Instance?.PlayerOptions;
                DataManager data = SystemPlatform.Instance?.DataManager;
                if (options == null || data == null || _rows == null)
                {
                    ModLog.Error("Random party: game services missing. No run started.");
                    return;
                }
                if (index < 0 || index >= _rows.Count) index = 0;
                Row row = _rows[index];
                if (row == Row.Start)
                {
                    StartRun(options, data);
                    return;
                }
                PlayerOptionsData config = options.Config;
                if (row == Row.PowerCreep) CyclePowerCreep(config);
                else SetBool(config, row, !GetBool(config, row));
                ModLog.Info("Random party: " + RowLabel(row) + " -> " + RowValue(config, row));
                ShowModifiers(options, data, index);
            }
            catch (Exception e)
            {
                ModLog.Error("Random party: the modifier popup failed", e);
            }
        }

        private static void OnCancelled()
        {
            ModLog.Info("Random party: cancelled.");
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

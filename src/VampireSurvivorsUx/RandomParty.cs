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
    /// The Random party button on the main menu. It asks one time for the CPU behaviour, fills the four local
    /// slots with random bought characters, and starts a run on the next stage that the main character has not
    /// completed.
    /// </summary>
    internal static class RandomParty
    {
        private const string ButtonName = "VampireSurvivorsUx_RandomPartyButton";
        private const string PopupId = "VampireSurvivorsUx_RandomPartyAi";
        private const int SlotCount = 4;
        private const float ButtonGap = 12f;

        private static readonly System.Random Rng = new System.Random();

        /// <summary>The time of the last main menu show. The start waits for the pixelate tween of the menu.</summary>
        private static float _menuShownAt;

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
                var icons = new Sprite[types.Length];
                for (int i = 0; i < types.Length; i++)
                {
                    labels[i] = AiLabel(data, types[i], out icons[i]);
                }
                ModLog.Info("Random party: asking for the CPU behaviour.");
                Popups.ShowOptions(PopupId, "CPU", labels, icons, index =>
                {
                    AIType picked = index >= 0 && index < types.Length ? types[index] : AIType.Aggressive;
                    Start(picked);
                });
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

        // ---------------------------------------------------------------- party and run start

        private static void Start(AIType ai)
        {
            try
            {
                PlayerOptions options = SystemPlatform.Instance?.PlayerOptions;
                DataManager data = SystemPlatform.Instance?.DataManager;
                MultiplayerManager multiplayer = MultiplayerManager.Instance;
                if (options == null || data == null || multiplayer == null)
                {
                    ModLog.Error("Random party: game services missing. No run started.");
                    return;
                }

                List<CharacterType> picks = PickCharacters(options, data, SlotCount);
                PlayerOptionsData config = options.Config;
                config.SelectedCharacter = picks[0];
                StageType stage = PickStage(data, options, picks[0]);
                config.SelectedStage = stage;
                ApplyBgm(data, config, stage, picks[0]);

                var slots = multiplayer.GetLocalPlayerSlots();
                int count = Math.Min(slots != null ? slots.Count : 0, SlotCount);
                for (int i = 0; i < count; i++)
                {
                    CoopSlotData slot = slots[i];
                    slot.SelectedCharacter = picks[i];
                    slot.UnlockState = UIUnlockStates.AVAILABLE;
                    if (i == 0 || slot.RewiredPlayer != null)
                    {
                        // Slot 0 is the player. A slot with a controller keeps its human player.
                        slot.AIType = AIType.None;
                    }
                    else
                    {
                        slot.AIType = ai;
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
                for (int i = 0; i < count; i++) names[i] = picks[i] + "/" + slots[i].AIType;
                ModLog.Info("Random party: stage=" + stage + " bgm=" + config.SelectedBGM
                    + " slots=[" + string.Join(", ", names) + "]");

                // The main menu background runs a 1 s pixelate tween. A start inside that second leaves the
                // pixelate render feature active for the whole run.
                float wait = Math.Max(0f, _menuShownAt + QuickRetryCore.StartDelaySeconds - Time.unscaledTime);
                FrameScheduler.RunAfterSeconds(wait, QuickRetryCore.StartGameFromMainMenu);
            }
            catch (Exception e)
            {
                ModLog.Error("Random party: the start failed", e);
            }
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
            StageData stageData = null;
            var stages = data.GetConvertedStages();
            if (stages != null && stages.ContainsKey(stage))
            {
                var list = stages[stage];
                if (list != null && list.Count > 0) stageData = list[0];
            }
            if (!QuickRetryCore.TryPickBgm(data, stageData, character, config.SelectedInverse, out BgmType bgm)) return;
            config.SelectedBGM = bgm;
            config.SelectedBGMMod = BgmModType.Normal;
        }
    }
}

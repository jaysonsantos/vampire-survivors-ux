using System;
using HarmonyLib;
using UnityEngine;
#if BEPINEX
// BepInEx generates the interop assemblies without a namespace prefix.
using I2.Loc;
using TMPro;
using VampireSurvivors;
using VampireSurvivors.Data;
using VampireSurvivors.Data.Stage;
using VampireSurvivors.Framework;
using VampireSurvivors.Objects;
#else
// MelonLoader generates the interop assemblies with the Il2Cpp namespace prefix.
using Il2CppI2.Loc;
using Il2CppTMPro;
using Il2CppVampireSurvivors;
using Il2CppVampireSurvivors.Data;
using Il2CppVampireSurvivors.Data.Stage;
using Il2CppVampireSurvivors.Framework;
using Il2CppVampireSurvivors.Objects;
#endif

namespace VampireSurvivorsUx
{
    /// <summary>Shows the name of the current stage at the top of the pause page.</summary>
    internal static class PauseStageName
    {
        private const string LabelName = "VampireSurvivorsUx_StageName";
        private const float TopMargin = 24f;
        private const float FontSize = 40f;

        [HarmonyPatch(typeof(PausePage), "OnShowStart")]
        private static class PausePage_OnShowStart_Patch
        {
            private static void Postfix(PausePage __instance)
            {
                try
                {
                    OnPauseShown(__instance);
                }
                catch (Exception e)
                {
                    ModLog.Error("PausePage.OnShowStart postfix failed", e);
                }
            }
        }

        private static void OnPauseShown(PausePage page)
        {
            TextMeshProUGUI label = GetOrCreateLabel(page);
            if (label == null) return;
            string stageName = GetStageName();
            label.text = stageName ?? string.Empty;
            label.gameObject.SetActive(!string.IsNullOrEmpty(stageName));
        }

        private static string GetStageName()
        {
            GameManager gm = GM.Core;
            var stage = gm != null ? gm.Stage : null;
            if (stage == null) return null;
            StageType type = stage.StageType;
            StageData data = stage.ActiveStageData;
            if (data == null) return type.ToString();
            string translated = LocalizationManager.GetTranslation(data.GetLocalizedName(type));
            return string.IsNullOrEmpty(translated) ? data.stageName : translated;
        }

        private static TextMeshProUGUI GetOrCreateLabel(PausePage page)
        {
            Transform root = page.transform;
            Transform existing = root.Find(LabelName);
            if (existing != null) return existing.GetComponent<TextMeshProUGUI>();

            // Copy the font and the material (outline) from the Resume button label.
            RectTransform resume = page._ResumeButton;
            TextMeshProUGUI template = resume != null ? resume.GetComponentInChildren<TextMeshProUGUI>(true) : null;

            var go = new GameObject(LabelName);
            go.layer = root.gameObject.layer;
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            RectTransform rect = text.rectTransform;
            rect.SetParent(root, false);
            rect.SetAsLastSibling();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(1200f, FontSize * 1.5f);
            rect.anchoredPosition = new Vector2(0f, -TopMargin);

            if (template != null)
            {
                text.font = template.font;
                text.fontSharedMaterial = template.fontSharedMaterial;
                text.color = template.color;
            }
            else ModLog.Warn("Pause page: no Resume label found. The stage name uses the default font.");
            text.enableAutoSizing = false;
            text.fontSize = FontSize;
            text.alignment = TextAlignmentOptions.Top;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            ModLog.Info("Pause page: stage label added. root=" + root.name
                + " size=" + (rootRect != null ? rootRect.rect.size.ToString() : "?")
                + " children=" + root.childCount);
            return text;
        }
    }
}

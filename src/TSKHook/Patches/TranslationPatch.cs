using HarmonyLib;
using Il2Cpp;
using Il2CppUtage;
using TSKHook.Services;

namespace TSKHook.Patches;

[HarmonyPatch]
internal static class TranslationPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdvDataManager), "DownloadChaperKeyFileUsed")]
    private static void SelectChapter(string scenarioLabel)
    {
        if (Config.TranslationEnabled.Value)
            PatchManager.SetCurrentChapter(scenarioLabel);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdventureTitleBandView), "Initialize")]
    private static void TranslateTitle(AdventureTitleBandView __instance)
    {
        if (!Config.TranslationEnabled.Value || __instance == null)
            return;

        string chapterId = PatchManager.CurrentChapterId;
        if (!string.IsNullOrEmpty(chapterId))
            Translation.GetChapterTranslationAsync(chapterId).GetAwaiter().GetResult();

        if (PatchManager.TryTranslateCurrentChapter(__instance.TitleText, out string translated))
            __instance.TitleText = translated;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AdventureTitleBandView), "Initialize")]
    private static void ApplyTitleFonts(AdventureTitleBandView __instance)
    {
        if (!Config.TranslationEnabled.Value || __instance == null)
            return;

        var font = Translation.TmpFontAsset;
        if (font == null)
            return;

        if (__instance.upTextComp?.text != null)
            __instance.upTextComp.text.font = font;
        if (__instance.donwTextComp?.text != null)
            __instance.donwTextComp.text.font = font;

        var titleTexts = __instance.titleText;
        if (titleTexts != null)
        {
            for (int i = 0; i < titleTexts.Length; i++)
            {
                if (titleTexts[i] != null)
                    titleTexts[i].font = font;
            }
        }

        var downTextObjects = __instance.donwTextObject;
        if (downTextObjects == null)
            return;

        for (int i = 0; i < downTextObjects.Length; i++)
        {
            var component = downTextObjects[i]?.GetComponent<TKSTextTMPGUI>();
            if (component?.text != null)
                component.text.font = font;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UguiNovelText), "OnEnable")]
    private static void ApplyNovelFont(UguiNovelText __instance)
    {
        if (Config.TranslationEnabled.Value && __instance != null && Translation.LegacyFont != null)
            __instance.font = Translation.LegacyFont;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AdvPage), "get_NameText")]
    private static void TranslatePageName(ref string __result) => TranslateName(ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(AdvBacklog), "get_MainCharacterNameText")]
    private static void TranslateBacklogName(ref string __result) => TranslateName(ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LanguageManagerBase), "ParseCellLocalizedTextBySwapDefaultLanguage")]
    private static void TranslateScenarioText(ref string __result)
    {
        if (
            Config.TranslationEnabled.Value
            && PatchManager.TryTranslateCurrentChapter(__result, out string translated)
        )
            __result = translated;
    }

    private static void TranslateName(ref string value)
    {
        if (
            Config.TranslationEnabled.Value
            && Translation.TryGetNameTranslation(value, out string translated)
        )
            value = translated;
    }
}

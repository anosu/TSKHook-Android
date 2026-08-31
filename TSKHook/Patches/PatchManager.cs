using System;
using System.Threading;
using HarmonyLib;
using TSKHook.Services;

namespace TSKHook.Patches;

public static class PatchManager
{
    private static HarmonyLib.Harmony _harmony;
    private static string _currentChapterId;

    public static string CurrentChapterId => Volatile.Read(ref _currentChapterId);

    public static void Initialize()
    {
        if (_harmony != null)
            return;

        ResetState();
        var harmony = new HarmonyLib.Harmony(ModInfo.Name);
        try
        {
            harmony.PatchAll(typeof(PatchManager).Assembly);
            _harmony = harmony;
            Logger.Info("Harmony patches applied");
        }
        catch
        {
            try
            {
                harmony.UnpatchSelf();
            }
            catch (Exception exception)
            {
                Logger.Error($"Harmony rollback failed: {exception}");
            }

            ResetState();
            throw;
        }
    }

    public static void Shutdown()
    {
        var harmony = _harmony;
        _harmony = null;
        try
        {
            harmony?.UnpatchSelf();
            if (harmony != null)
                Logger.Info("Harmony patches removed");
        }
        finally
        {
            ResetState();
        }
    }

    public static void SetCurrentChapter(string scenarioLabel)
    {
        if (!Translation.TryNormalizeChapterLabel(scenarioLabel, out string chapterId))
        {
            Volatile.Write(ref _currentChapterId, null);
            if (!string.IsNullOrWhiteSpace(scenarioLabel))
                Logger.Warn($"Ignored invalid scenario label: {scenarioLabel}");
            return;
        }

        Volatile.Write(ref _currentChapterId, chapterId);
        _ = Translation.GetChapterTranslationAsync(chapterId);
        Logger.Info($"Scenario: {chapterId}");
    }

    public static bool TryTranslateCurrentChapter(string source, out string translated)
    {
        translated = null;
        string chapterId = CurrentChapterId;
        return !string.IsNullOrEmpty(source)
            && !string.IsNullOrEmpty(chapterId)
            && Translation.TryGetChapterTranslation(chapterId, source, out translated);
    }

    private static void ResetState() => Volatile.Write(ref _currentChapterId, null);
}

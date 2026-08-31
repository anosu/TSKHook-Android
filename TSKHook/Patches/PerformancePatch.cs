using HarmonyLib;
using Il2Cpp;

namespace TSKHook.Patches;

[HarmonyPatch]
internal static class PerformancePatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(GameConfig), "set_FixedFrameRate")]
    private static void OverrideFixedFrameRate(ref int value)
    {
        value = RuntimeController.TargetFrameRate;
    }
}

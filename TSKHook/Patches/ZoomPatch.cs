using System;
using HarmonyLib;
using Il2Cpp;

namespace TSKHook.Patches;

[HarmonyPatch]
internal static class ZoomPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MaximizeCharaView), "SetCharaRoot_Scale")]
    private static void ScalePinchDelta(MaximizeZoomEventData _zoomData)
    {
        if (_zoomData == null || _zoomData.PinchData == 0)
            return;

        _zoomData.PinchData = MathF.CopySign(RuntimeController.ZoomRatio, _zoomData.PinchData);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MaximizeCharaView), "initialize")]
    private static void ExtendMinimumZoom(MaximizeCharaView __instance)
    {
        if (__instance != null)
            __instance.maximizeMinSize = 0.1f;
    }
}

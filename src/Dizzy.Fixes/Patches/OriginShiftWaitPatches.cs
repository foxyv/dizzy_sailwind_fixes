using System;
using HarmonyLib;

namespace Dizzy.Fixes
{
    // Vanilla play-mode origin moves call ShiftSmoothly -> WaitForShift,
    // which fades wakes for smoothShiftFrames*2 (~100) fixed updates with
    // GameState.waitingForShift set, then teleports 512 m. That is a
    // 1-2 s freeze then a jerk. Skip the wait; Shift still runs NewShift
    // PrepareForShifting / RestoreMomentum. Zero smoothShiftFrames so the
    // post-teleport wake fade-in loop is empty too.
    internal static class OriginShiftWait
    {
        private static int _vanillaFrames = int.MinValue;
        private static bool _loggedMissing;

        internal static bool Enabled()
        {
            return FixesConfig.SkipSmoothOriginShift != null
                && FixesConfig.SkipSmoothOriginShift.Value;
        }

        internal static bool ShiftNow(FloatingOriginManager manager, int x, int z)
        {
            if (manager == null)
                return false;

            Traverse t = Traverse.Create(manager);
            if (!t.Method("Shift", new[] { typeof(int), typeof(int) }).MethodExists())
            {
                if (!_loggedMissing)
                {
                    Plugin.Log.LogWarning("SkipSmoothOriginShift: Shift is missing; leaving vanilla origin wait.");
                    _loggedMissing = true;
                }

                return false;
            }

            if (_vanillaFrames == int.MinValue)
                _vanillaFrames = manager.smoothShiftFrames;
            manager.smoothShiftFrames = 0;

            if (t.Field("shiftingSmoothly").FieldExists())
                t.Field("shiftingSmoothly").SetValue(true);

            t.Method("Shift", new[] { typeof(int), typeof(int) }).GetValue(x, z);
            return true;
        }
    }

    [HarmonyPatch(typeof(FloatingOriginManager), "ShiftSmoothly")]
    internal static class SkipSmoothOriginShiftPatch
    {
        private static bool Prefix(FloatingOriginManager __instance, int x, int z)
        {
            if (!OriginShiftWait.Enabled())
                return true;
            return !OriginShiftWait.ShiftNow(__instance, x, z);
        }
    }
}

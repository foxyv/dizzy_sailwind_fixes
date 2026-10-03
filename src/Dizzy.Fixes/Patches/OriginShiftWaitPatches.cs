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
        private static readonly Action<FloatingOriginManager, int, int> Shift = GameMembers.Method<Action<FloatingOriginManager, int, int>>(typeof(FloatingOriginManager), "Shift", new[] { typeof(int), typeof(int) });
        private static readonly AccessTools.FieldRef<FloatingOriginManager, bool> ShiftingSmoothly = GameMembers.Field<FloatingOriginManager, bool>("shiftingSmoothly");

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

            if (Shift == null)
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

            if (ShiftingSmoothly != null)
                ShiftingSmoothly(manager) = true;

            Shift(manager, x, z);
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

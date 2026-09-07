using HarmonyLib;

namespace Dizzy.Fixes
{
    // BoatCamera.SwitchOn/Off (C / CameraMode) plays UISounds.buttonClick.
    internal static class CameraModeClick
    {
        private static bool _skip;

        internal static void Arm()
        {
            _skip = FixesConfig.MuteCameraModeSound.Value;
        }

        internal static void Disarm()
        {
            _skip = false;
        }

        internal static bool Consume()
        {
            if (!_skip)
                return false;
            _skip = false;
            return true;
        }
    }

    [HarmonyPatch(typeof(BoatCamera), nameof(BoatCamera.SwitchOn))]
    internal static class BoatCameraSwitchOnPatch
    {
        private static void Prefix()
        {
            CameraModeClick.Arm();
        }

        private static void Postfix()
        {
            CameraModeClick.Disarm();
        }
    }

    [HarmonyPatch(typeof(BoatCamera), nameof(BoatCamera.SwitchOff))]
    internal static class BoatCameraSwitchOffPatch
    {
        private static void Prefix()
        {
            CameraModeClick.Arm();
        }

        private static void Postfix()
        {
            CameraModeClick.Disarm();
        }
    }

    [HarmonyPatch(typeof(UISoundPlayer), nameof(UISoundPlayer.PlayUISound))]
    internal static class MuteCameraModeSoundPatch
    {
        private static bool Prefix()
        {
            return !CameraModeClick.Consume();
        }
    }
}

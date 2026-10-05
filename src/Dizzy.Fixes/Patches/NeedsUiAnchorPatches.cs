using System;
using HarmonyLib;

namespace Dizzy.Fixes
{
    // The main scene parents the needs bars ("needs ui") to the VR left-hand
    // anchor. PlayerNeedsUI only moves them to the PC anchor in front of the
    // camera from UpdateAnchor, which vanilla calls when you open the
    // inventory (ToggleUI) or a needs warning plays (PlayWarning).
    // ShowFeedback, the pop-up when you eat or drink, doesn't, so eating or
    // drinking before either of those shows the bars twisted off to one side.
    // Anchor them the way PlayWarning does first.
    [HarmonyPatch(typeof(PlayerNeedsUI), nameof(PlayerNeedsUI.ShowFeedback))]
    internal static class NeedsUiFeedbackAnchorPatch
    {
        private static readonly Action<PlayerNeedsUI> UpdateAnchor = GameMembers.Method<Action<PlayerNeedsUI>>(typeof(PlayerNeedsUI), "UpdateAnchor");

        private static void Prefix(PlayerNeedsUI __instance)
        {
            if (!FixesConfig.AnchorNeedsBarsOnFeedback.Value || UpdateAnchor == null)
                return;
            UpdateAnchor(__instance);
        }
    }
}

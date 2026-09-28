using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Dropping a carried item only runs when the look ray is on nothing.
    // Two-handed items (PickupableItem.big) cannot be placed onto another
    // object, but the ray still highlights the next one and that highlight
    // blocks the drop.
    internal static class BigCrateCarry
    {
        internal static bool Enabled()
        {
            return FixesConfig.DropBigCratePastOtherCrates != null
                && FixesConfig.DropBigCratePastOtherCrates.Value;
        }

        internal static bool Blocking;

        internal static bool IsDropOnlyCarry(PickupableItem held)
        {
            return held != null && held.big;
        }

        internal static void ClearLook(PickupableItem held, ref GoPointerButton pointed)
        {
            Blocking = Enabled() && IsDropOnlyCarry(held);
            if (!Blocking || pointed == null || pointed == held)
                return;

            pointed.ForceUnlook();
            pointed = null;
        }
    }

    // Look() is what arms the outline. Unlooking it again the same frame
    // turns that outline off in UpdateColor, and OnEnable restarts the
    // global thickness pulse, so the other crate flickers.
    [HarmonyPatch(typeof(GoPointerButton), "Look")]
    internal static class DropBigCrateBlockLookPatch
    {
        private static bool Prefix(GoPointerButton __instance, GoPointer lookingPointer)
        {
            if (lookingPointer == null || !BigCrateCarry.Enabled() || !BigCrateCarry.IsDropOnlyCarry(lookingPointer.GetHeldItem()))
                return true;

            return __instance == lookingPointer.GetHeldItem();
        }
    }

    [HarmonyPatch(typeof(GoPointerButton), "UpdateColor")]
    internal static class DropBigCrateOutlinePatch
    {
        private static bool Prefix(GoPointerButton __instance)
        {
            if (!BigCrateCarry.Blocking || __instance == null)
                return true;

            PickupableItem self = __instance as PickupableItem;
            if (self != null && self.held != null && BigCrateCarry.IsDropOnlyCarry(self))
                return true;

            if (__instance.IsLookedAt())
                __instance.ForceUnlook();
            __instance.forceDisableRedOutline = true;
            OpenCrateHighlightPatch.SuppressOutline(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter("Dizzy.Fixes.PreferCrateInventorySlotPatch")]
    internal static class DropBigCrateRayPatch
    {
        private static void Postfix(PickupableItem ___heldItem, ref GoPointerButton ___pointedAtButton)
        {
            BigCrateCarry.ClearLook(___heldItem, ref ___pointedAtButton);
        }
    }

    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter(
        "Dizzy.Fixes.CrateSlotPreviewPatch",
        "Dizzy.Fixes.ItemPlaceAlignPreviewPatch")]
    internal static class DropBigCrateClickPatch
    {
        private static void Prefix(PickupableItem ___heldItem, ref GoPointerButton ___pointedAtButton)
        {
            BigCrateCarry.ClearLook(___heldItem, ref ___pointedAtButton);
        }
    }
}

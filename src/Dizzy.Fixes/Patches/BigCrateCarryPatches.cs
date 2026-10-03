using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Dropping a carried item only runs when the look ray is on nothing.
    // Two-handed items (PickupableItem.big) cannot be placed onto another
    // object, but the ray still highlights the next one and that highlight
    // blocks the drop. A target the carried item can use stays highlighted,
    // so a held barrel can still be clicked on another barrel to refill,
    // and the merchant sell button and boat ladders stay clickable.
    internal static class BigCrateCarry
    {
        internal static bool Enabled()
        {
            return FixesConfig.DropBigCratePastOtherCrates != null
                && FixesConfig.DropBigCratePastOtherCrates.Value;
        }

        internal static bool Blocking;

        internal static PickupableItem Held;

        internal static bool IsDropOnlyCarry(PickupableItem held)
        {
            return held != null && held.big;
        }

        // ShipItemBottle.AllowOnItemClick is true for another bottle, so
        // vanilla OnItemClick can pour. Equal barrels fill the held one.
        // The merchant parchment is GPButtonBuyItem; clearing it drops the
        // crate instead of selling. BoatLadder is the hull rope ladder.
        internal static bool CanUse(PickupableItem held, GoPointerButton button)
        {
            if (button == null || button == held)
                return false;
            if (button is GPButtonBuyItem || button is BoatLadder)
                return true;
            if (held == null)
                return false;
            return held.AllowOnItemClick(button);
        }

        internal static void ClearLook(PickupableItem held, ref GoPointerButton pointed)
        {
            Held = held;
            Blocking = Enabled() && IsDropOnlyCarry(held);
            if (!Blocking || pointed == null || pointed == held || CanUse(held, pointed))
                return;

            pointed.ForceUnlook();
            pointed = null;
        }

        // Climbing disables the ladder collider, so the pickup-button
        // release no longer has a target and vanilla drops the carried item.
        internal static void HoldLadderClick(PickupableItem held, GoPointerButton clicked, ref GoPointerButton pointed)
        {
            if (!Enabled() || held == null || !(clicked is BoatLadder))
                return;

            pointed = clicked;
        }

        // Runs from UpdateColor every frame, so fill one reused list instead
        // of allocating an array of every child component.
        private static readonly List<cakeslice.Outline> Outlines = new List<cakeslice.Outline>();

        internal static void SuppressOutline(GoPointerButton button)
        {
            button.GetComponentsInChildren(true, Outlines);
            for (int i = 0; i < Outlines.Count; i++)
            {
                cakeslice.Outline outline = Outlines[i];
                if (outline != null && outline.enabled)
                    outline.enabled = false;
            }

            Outlines.Clear();
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
            PickupableItem held = lookingPointer != null ? lookingPointer.GetHeldItem() : null;
            if (lookingPointer == null || !BigCrateCarry.Enabled() || !BigCrateCarry.IsDropOnlyCarry(held))
                return true;
            if (__instance == held || BigCrateCarry.CanUse(held, __instance))
                return true;

            return false;
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
            if (BigCrateCarry.CanUse(BigCrateCarry.Held, __instance))
                return true;

            if (__instance.IsLookedAt())
                __instance.ForceUnlook();
            __instance.forceDisableRedOutline = true;
            BigCrateCarry.SuppressOutline(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    [HarmonyPriority(Priority.Last)]
    internal static class DropBigCrateRayPatch
    {
        private static void Postfix(PickupableItem ___heldItem, ref GoPointerButton ___pointedAtButton)
        {
            BigCrateCarry.ClearLook(___heldItem, ref ___pointedAtButton);
        }
    }

    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    [HarmonyPriority(Priority.Last)]
    internal static class DropBigCrateClickPatch
    {
        private static void Prefix(PickupableItem ___heldItem, GoPointerButton ___clickedButton, ref GoPointerButton ___pointedAtButton)
        {
            BigCrateCarry.ClearLook(___heldItem, ref ___pointedAtButton);
            BigCrateCarry.HoldLadderClick(___heldItem, ___clickedButton, ref ___pointedAtButton);
        }
    }
}

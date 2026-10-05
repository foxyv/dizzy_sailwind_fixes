using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Vanilla has no way to pour from a held barrel into a container of the
    // same size. Left-clicking a container with a held bottle
    // (ShipItemBottle.OnItemClick) moves liquid from the bigger one into the
    // smaller one, so a held barrel fills bottles, buckets and mugs but pulls
    // from another barrel instead. Right-click only lifts a held barrel to
    // drink (OnAltHeld). This makes right-click pour the held barrel into the
    // container you're looking at: another barrel, a bottle, bucket or mug,
    // or (water only, as vanilla's left-click) a kettle or soup pot.
    // A press that starts on a container pours and doesn't also lift the
    // barrel to drink; a press that starts anywhere else drinks as vanilla.
    internal static class BarrelPour
    {
        // UpdateLookText names anything holding 30 or more a barrel. Every
        // vanilla barrel holds 60; the biggest other container holds 10.
        private const float BarrelCapacity = 30f;

        private static readonly HashSet<ShipItemBottle> PourPresses = new HashSet<ShipItemBottle>();

        internal static bool Enabled() => FixesConfig.PourFromHeldBarrel != null && FixesConfig.PourFromHeldBarrel.Value;

        internal static bool IsPourPress(ShipItemBottle barrel) => PourPresses.Contains(barrel);

        internal static void Forget(ShipItemBottle barrel) => PourPresses.Remove(barrel);

        // The container a held barrel would pour into, or null.
        internal static ShipItem Target(ShipItemBottle barrel, ShipItem target)
        {
            if (barrel == null || target == null || target == barrel)
                return null;
            if (barrel.GetCapacity() < BarrelCapacity || !barrel.sold || OnMission(barrel))
                return null;
            if (barrel.health <= 0f || barrel.amount == 0f)
                return null;
            if (!target.sold || OnMission(target))
                return null;

            ShipItemBottle bottle = target as ShipItemBottle;
            if (bottle != null)
                return SoupMugs.HasSoup(bottle) ? null : target;
            if (target is ShipItemKettle || target is ShipItemSoup)
                return barrel.amount == 1f ? target : null;
            return null;
        }

        internal static void Press(ShipItemBottle barrel)
        {
            PourPresses.Remove(barrel);
            if (barrel.held == null)
                return;
            ShipItem target = Target(barrel, barrel.held.GetPointedAtItem());
            if (target == null)
                return;

            PourPresses.Add(barrel);
            Pour(barrel, target);
        }

        private static void Pour(ShipItemBottle barrel, ShipItem target)
        {
            float before = barrel.health;
            ShipItemBottle bottle = target as ShipItemBottle;
            if (bottle != null)
            {
                // FillBottle refuses a different liquid, caps at the bottle's
                // room, plays the pour sound and updates the bottle.
                barrel.health = bottle.FillBottle(barrel.amount, barrel.health);
            }
            else
            {
                // Kettle and soup pot: vanilla's left-click fill from a held
                // water bottle, which other mods (co-op) already hook.
                target.OnItemClick(barrel);
            }

            if (barrel.health == before)
                return;
            if (barrel.health <= 0f)
                barrel.EmptyBottle();
            barrel.UpdateLookText();
            if (barrel.itemRigidbodyC != null)
                barrel.itemRigidbodyC.UpdateMass();
            if (Plugin.Log != null)
                Plugin.Log.LogInfo("PourFromHeldBarrel: poured " + (before - barrel.health) + " units into " + target.gameObject.name + ".");
        }

        private static bool OnMission(ShipItem item)
        {
            Good good = item.GetComponent<Good>();
            return good != null && good.GetMissionIndex() > -1;
        }
    }

    [HarmonyPatch(typeof(ShipItemBottle), nameof(ShipItemBottle.OnAltActivate))]
    internal static class BarrelPourPressPatch
    {
        private static void Prefix(ShipItemBottle __instance)
        {
            if (!BarrelPour.Enabled())
                return;
            BarrelPour.Press(__instance);
        }
    }

    [HarmonyPatch(typeof(ShipItemBottle), nameof(ShipItemBottle.OnAltHeld))]
    internal static class BarrelPourSkipDrinkPatch
    {
        private static bool Prefix(ShipItemBottle __instance)
        {
            return !(BarrelPour.Enabled() && BarrelPour.IsPourPress(__instance));
        }
    }

    [HarmonyPatch(typeof(ShipItemBottle), nameof(ShipItemBottle.OnDrop))]
    internal static class BarrelPourDropPatch
    {
        private static void Postfix(ShipItemBottle __instance)
        {
            BarrelPour.Forget(__instance);
        }
    }

    // Adds "fill" on the right mouse button to the look prompt, and renames
    // vanilla's left-click "fill" to "drain" while holding a barrel.
    [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowLookText))]
    internal static class BarrelPourPromptPatch
    {
        private static readonly AccessTools.FieldRef<LookUI, GoPointer> Pointer = GameMembers.Field<LookUI, GoPointer>("pointer");
        private static readonly AccessTools.FieldRef<LookUI, TextMesh> ControlsText = GameMembers.Field<LookUI, TextMesh>("controlsText");
        private static readonly Action<LookUI> ShowRicon = GameMembers.Method<Action<LookUI>>(typeof(LookUI), "ShowRicon");

        private static void Postfix(LookUI __instance, GoPointerButton button)
        {
            if (!BarrelPour.Enabled() || button == null || !Settings.controlsTextEnabled)
                return;
            if (Pointer == null || ControlsText == null || ShowRicon == null)
                return;
            GoPointer pointer = Pointer(__instance);
            if (pointer == null)
                return;
            ShipItemBottle barrel = pointer.GetHeldItem() as ShipItemBottle;
            if (BarrelPour.Target(barrel, button.GetComponent<ShipItem>()) == null)
                return;

            TextMesh controls = ControlsText(__instance);
            if (controls == null)
                return;
            string text = controls.text ?? "";
            int newline = text.IndexOf('\n');
            string left = newline >= 0 ? text.Substring(0, newline) : text;
            if (left == "fill")
                left = "drain";
            controls.text = left + "\nfill";
            ShowRicon(__instance);
        }
    }
}

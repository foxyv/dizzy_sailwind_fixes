using System.Collections.Generic;
using HarmonyLib;

namespace Dizzy.Fixes
{
    // ItemRigidbody.FixedUpdate destroys any sold item more than 600 m from
    // the camera that is not on a boat walk collider. Items in a player house
    // belong to the house's SaveableObject (save parent > 0), whose
    // BoatLocalItems would cache them at 1000 m like a boat's, but the 600 m
    // destroy runs first and removes them from the save. Keep the destroy
    // countdown at zero for those items so vanilla only freezes them
    // (kinematic) while they are out of range.
    internal static class HouseItems
    {
        private static readonly HashSet<int> Logged = new HashSet<int>();

        internal static bool Enabled()
        {
            return FixesConfig.KeepHouseItemsWhenAway != null
                && FixesConfig.KeepHouseItemsWhenAway.Value;
        }

        internal static bool BelongsToSaveableObject(ShipItem item)
        {
            if (item == null || !item.sold || item.currentWalkCol != null)
                return false;

            SaveablePrefab saveable = item.GetComponent<SaveablePrefab>();
            return saveable != null && saveable.GetParentObject() > 0;
        }

        internal static void NoteKept(ShipItem item)
        {
            if (!Logged.Add(item.GetInstanceID()))
                return;

            Plugin.Log.LogInfo("KeepHouseItemsWhenAway: kept " + item.gameObject.name + " (save parent "
                + item.GetComponent<SaveablePrefab>().GetParentObject() + ") out of range instead of destroying it.");
        }
    }

    // ShipItem.OnEnterInventory only calls ExitBoat(), so an item put in the
    // hotbar inside a player house keeps the house as its save parent. The
    // house's BoatLocalItems then caches it at 1000 m like any house item, and
    // it vanishes from the hotbar until the house respawns it. Every slot
    // insert (pickup and PutInInventory after a load) goes through
    // EnterInventorySlot, so clear the parent there, as ExitHouse would.
    internal static class HotbarItems
    {
        internal static bool Enabled()
        {
            return FixesConfig.KeepHotbarItemsWhenAway != null
                && FixesConfig.KeepHotbarItemsWhenAway.Value;
        }

        internal static void ReleaseFromOwner(ShipItem item)
        {
            if (item == null)
                return;

            SaveablePrefab saveable = item.GetComponent<SaveablePrefab>();
            if (saveable == null || saveable.GetParentObject() <= 0)
                return;

            saveable.SetParentObject(-1);
            if (FloatingOriginManager.instance != null)
                item.transform.parent = FloatingOriginManager.instance.transform;
        }
    }

    [HarmonyPatch(typeof(ItemRigidbody), nameof(ItemRigidbody.EnterInventorySlot))]
    internal static class KeepHotbarItemsWhenAwayPatch
    {
        private static void Postfix(ShipItem ___item)
        {
            if (!HotbarItems.Enabled())
                return;

            HotbarItems.ReleaseFromOwner(___item);
        }
    }

    [HarmonyPatch(typeof(ItemRigidbody), "FixedUpdate")]
    internal static class KeepHouseItemsWhenAwayPatch
    {
        private static void Prefix(ShipItem ___item, ref int ___framesUntilDestroy)
        {
            // The countdown only runs while the item is out of range.
            if (___framesUntilDestroy == 0 || !HouseItems.Enabled())
                return;
            if (!HouseItems.BelongsToSaveableObject(___item))
                return;

            ___framesUntilDestroy = 0;
            HouseItems.NoteKept(___item);
        }
    }
}

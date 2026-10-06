using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // While a crate's grid is open, CrateInventory.LateUpdate stops pinning
    // its items to the crate and the UI lays them out in the world on the
    // grid squares. A stove or smoker next to the crate then pulls in any
    // that touch it: StoveCookTrigger.OnTriggerEnter takes any food that
    // enters an empty cook slot, and StoveFuel.Update calls InsertFuel for
    // any firewood touching the fuel trigger that isn't held. Neither checks
    // whether the item is still in a crate. SaveablePrefab.currentCrateId is
    // the marker: InsertItem sets it and WithdrawItem clears it before the
    // item is free, so food or firewood taken out of a crate still cooks.
    internal static class CrateStove
    {
        internal static bool Enabled() => FixesConfig.KeepCrateItemsOutOfStove != null && FixesConfig.KeepCrateItemsOutOfStove.Value;

        internal static bool InCrate(Component item)
        {
            if (item == null)
                return false;
            SaveablePrefab saveable = item.GetComponent<SaveablePrefab>();
            return saveable != null && saveable.currentCrateId > 0;
        }
    }

    [HarmonyPatch(typeof(StoveCookTrigger), nameof(StoveCookTrigger.OnTriggerEnter))]
    internal static class CrateFoodStayOutOfStovePatch
    {
        private static bool Prefix(Collider other)
        {
            return !(CrateStove.Enabled() && CrateStove.InCrate(other));
        }
    }

    // StoveFuel.Update retries every frame while the firewood touches the
    // fuel trigger, so this stays quiet.
    [HarmonyPatch(typeof(StoveFuelTrigger), nameof(StoveFuelTrigger.InsertFuel))]
    internal static class CrateFuelStayOutOfStovePatch
    {
        private static bool Prefix(ShipItem item)
        {
            return !(CrateStove.Enabled() && CrateStove.InCrate(item));
        }
    }
}

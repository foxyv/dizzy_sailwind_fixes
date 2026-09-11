using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Vanilla pours water into a soup pot (bottle.amount == 1) or drinks from
    // the pot. Clicking an empty mug on the pot places the mug. There is no
    // kettle-style pour of soup into a drinking cup.
    // Mug.Update only drinks/spills when health is exactly 1, 2, or 3.
    // Soup leftover is often fractional (pot water ticks by 2*dt), so 0.4
    // in a cup cannot be drunk, dumped, or (if the pot is empty) refilled.
    internal static class SoupMugs
    {
        private const string Key = "Dizzy.Fixes.SoupMug.v1";
        private const float MugCapacityMax = 5f;
        private const float FullEpsilon = 0.01f;

        private static readonly Dictionary<int, Contents> ByPrefabId = new Dictionary<int, Contents>();
        private static readonly Dictionary<int, Contents> ByMug = new Dictionary<int, Contents>();

        internal sealed class Contents
        {
            internal int PrefabId;
            internal float Water;
            internal float Energy;
            internal float Uncooked;
            internal float Spoiled;
            internal float Salted;
            internal float Vitamins;
            internal float Protein;
            internal float Heat;
        }

        internal static bool IsMug(ShipItemBottle bottle)
        {
            if (bottle == null)
                return false;
            float cap = bottle.GetCapacity();
            return cap > 0f && cap < MugCapacityMax;
        }

        internal static bool HasSoup(ShipItemBottle bottle)
        {
            Contents contents = Get(bottle);
            return contents != null && contents.Water > FullEpsilon;
        }

        internal static bool CanReceive(ShipItemBottle mug)
        {
            if (!IsMug(mug) || !mug.sold)
                return false;
            if (mug.health <= FullEpsilon || mug.amount == 0f)
                return true;
            if (!HasSoup(mug))
                return false;
            return mug.GetRemainingCapacity() > FullEpsilon;
        }

        internal static bool CanFillFromPot(ShipItemSoup pot, ShipItemBottle mug)
        {
            if (pot == null || !pot.sold || pot.currentWater <= FullEpsilon)
                return false;
            return CanReceive(mug);
        }

        internal static bool TryFillPrompt(PickupableItem held, GoPointerButton lookedAt)
        {
            if (held == null || lookedAt == null)
                return false;

            ShipItemSoup heldSoup = held.GetComponent<ShipItemSoup>();
            ShipItemBottle lookedMug = lookedAt.GetComponent<ShipItemBottle>();
            if (CanFillFromPot(heldSoup, lookedMug))
                return true;

            ShipItemBottle heldMug = held.GetComponent<ShipItemBottle>();
            ShipItemSoup lookedPot = lookedAt.GetComponent<ShipItemSoup>();
            return CanFillFromPot(lookedPot, heldMug);
        }

        internal static bool ShouldHandleMugClick(ShipItemSoup pot, ShipItemBottle mug)
        {
            if (CanFillFromPot(pot, mug))
                return true;
            return IsMug(mug) && HasSoup(mug);
        }

        internal static bool HandleMugClick(ShipItemSoup pot, ShipItemBottle mug)
        {
            if (CanFillFromPot(pot, mug))
                return Pour(pot, mug);
            if (IsMug(mug) && HasSoup(mug) && (pot == null || pot.currentWater <= FullEpsilon))
                return Dump(mug);
            return Pour(pot, mug);
        }

        internal static bool Dump(ShipItemBottle mug)
        {
            if (!IsMug(mug) || !HasSoup(mug))
                return false;
            Forget(mug);
            mug.EmptyBottle();
            if (mug.itemRigidbodyC != null)
                mug.itemRigidbodyC.UpdateMass();
            return true;
        }

        internal static bool Pour(ShipItemSoup pot, ShipItemBottle mug)
        {
            if (!CanFillFromPot(pot, mug))
            {
                if (IsMug(mug) && mug.GetRemainingCapacity() <= FullEpsilon)
                    NotificationUi.instance.ShowNotification("Mug is full!", 1f);
                else if (pot != null && pot.currentWater <= FullEpsilon)
                    NotificationUi.instance.ShowNotification("Pot is empty!", 1f);
                return false;
            }

            float poured = Mathf.Min(pot.currentWater, mug.GetRemainingCapacity());
            if (poured <= FullEpsilon)
                return false;

            Contents dest = GetOrCreate(mug);
            Transfer(pot, dest, poured);

            mug.amount = 1f;
            mug.health += poured;
            dest.Water = mug.health;

            CookableFoodSoup cookable = pot.GetComponent<CookableFoodSoup>();
            if (cookable != null)
                dest.Heat = cookable.GetCurrentHeat();

            Mug mugFx = mug.GetComponent<Mug>();
            if (mugFx != null)
                mugFx.SetHeat(dest.Heat);

            UISoundPlayer.instance.PlayLiquidPourSound();
            pot.UpdateLookText();
            mug.UpdateLookText();
            if (pot.itemRigidbodyC != null)
                pot.itemRigidbodyC.UpdateMass();
            if (mug.itemRigidbodyC != null)
                mug.itemRigidbodyC.UpdateMass();
            WriteModData();
            return true;
        }

        internal static void TryFractionalSpillOrDrink(Mug mug, ShipItemBottle bottle)
        {
            if (mug == null || bottle == null)
                return;
            if (bottle.GetCapacity() == 9f)
                return;

            float level = bottle.health;
            if (level <= 0f)
                return;
            if (level == 1f || level == 2f || level == 3f)
                return;

            float upright = mug.transform.up.y;
            if (upright >= SpillUprightThreshold(level))
                return;

            if (bottle.amount != 9f && bottle.IsDrinking())
            {
                bottle.TryDrinkBottle();
                return;
            }

            Traverse spill = Traverse.Create(mug).Method("Spill");
            if (spill.MethodExists())
                spill.GetValue();
        }

        private static float SpillUprightThreshold(float level)
        {
            if (level >= 3f)
                return 0.85f;
            if (level >= 2f)
                return Mathf.Lerp(0.66f, 0.85f, level - 2f);
            if (level >= 1f)
                return Mathf.Lerp(0.52f, 0.66f, level - 1f);
            return 0.52f;
        }

        internal static bool TryDrink(ShipItemBottle mug)
        {
            Contents contents = Get(mug);
            if (contents == null || contents.Water <= FullEpsilon)
                return false;
            if (!mug.sold || mug.held == null || mug.health <= 0f)
                return false;

            SyncToHealth(mug);
            contents = Get(mug);
            if (contents == null || contents.Water <= FullEpsilon)
                return false;

            float sip = Mathf.Min(1f, mug.health, contents.Water);
            if (sip <= 0f)
                return false;

            PlayerNeeds.instance.eatCooldown = 0.5f;
            PlayerNeedsUI.instance.ShowFeedback();
            Refs.playerMouthCol.PlayDrinkSound();

            float water = contents.Water;
            float portion = sip / water;
            float energy = contents.Energy * portion;
            float uncooked = contents.Uncooked * portion;
            float vitamins = contents.Vitamins * portion;
            float protein = contents.Protein * portion;
            float spoiled = contents.Spoiled * portion;

            float cookedBonus = 1f;
            if (energy + uncooked > 0f)
                cookedBonus = Mathf.Lerp(1f, 1.25f, energy / (energy + uncooked));

            // Vanilla pot sips ~2*dt and does spoiled -= dE * spoiled, which
            // explodes on a 1-unit mug gulp and tanks food on the last sip.
            float spoiledPct = 0f;
            float sipFood = energy + uncooked;
            if (sipFood > 0f)
                spoiledPct = spoiled / sipFood;

            contents.Water -= sip;
            contents.Energy -= energy;
            contents.Uncooked -= uncooked;
            contents.Vitamins -= vitamins;
            contents.Protein -= protein;
            contents.Spoiled -= spoiled;

            float hydration = sip;
            if (spoiledPct > 0.9f)
            {
                float t = Mathf.InverseLerp(0.9f, 1f, spoiledPct);
                energy = Mathf.Lerp(energy, (0f - energy) * 3f, t);
                protein = Mathf.Lerp(protein, 0f, t);
                vitamins = Mathf.Lerp(vitamins, 0f, t);
                hydration = Mathf.Lerp(0f, 0f - energy, t);
            }

            PlayerNeeds.water += (float)Liquids.GetLiquidHydration(1f) * hydration;
            PlayerNeeds.food += energy * 1.5f * cookedBonus;
            PlayerNeeds.vitamins += vitamins * cookedBonus;
            PlayerNeeds.protein += protein * cookedBonus;

            mug.health -= sip;
            if (mug.health <= FullEpsilon || contents.Water <= FullEpsilon)
            {
                Forget(mug);
                mug.EmptyBottle();
            }
            else
            {
                mug.UpdateLookText();
                if (mug.itemRigidbodyC != null)
                    mug.itemRigidbodyC.UpdateMass();
                WriteModData();
            }

            return true;
        }

        internal static void SyncToHealth(ShipItemBottle mug)
        {
            Contents contents = Get(mug);
            if (contents == null)
                return;
            if (mug.health <= FullEpsilon || mug.amount == 0f)
            {
                Forget(mug);
                return;
            }

            if (Mathf.Abs(contents.Water - mug.health) <= FullEpsilon)
                return;
            if (contents.Water <= FullEpsilon)
            {
                Forget(mug);
                return;
            }

            float scale = mug.health / contents.Water;
            contents.Water = mug.health;
            contents.Energy *= scale;
            contents.Uncooked *= scale;
            contents.Spoiled *= scale;
            contents.Salted *= scale;
            contents.Vitamins *= scale;
            contents.Protein *= scale;
            WriteModData();
        }

        internal static void Forget(ShipItemBottle mug)
        {
            if (mug == null)
                return;
            bool removed = ByMug.Remove(mug.GetInstanceID());
            int prefabId = PrefabId(mug);
            if (prefabId != 0)
                removed = ByPrefabId.Remove(prefabId) || removed;
            if (removed)
                WriteModData();
        }

        internal static void RestoreAfterLoad()
        {
            ReadModData();
            ShipItemBottle[] mugs = UnityEngine.Object.FindObjectsOfType<ShipItemBottle>();
            for (int i = 0; i < mugs.Length; i++)
            {
                ShipItemBottle mug = mugs[i];
                int id = PrefabId(mug);
                if (id == 0 || !ByPrefabId.TryGetValue(id, out Contents saved))
                    continue;
                ByMug[mug.GetInstanceID()] = saved;
                SyncToHealth(mug);
            }
        }

        internal static void ReadModData()
        {
            ByPrefabId.Clear();
            ByMug.Clear();
            if (GameState.modData == null)
                return;
            if (!GameState.modData.TryGetValue(Key, out string raw) || string.IsNullOrEmpty(raw))
                return;

            string[] lines = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                Contents parsed = Parse(lines[i], out int id);
                if (parsed != null && id != 0)
                    ByPrefabId[id] = parsed;
            }
        }

        internal static void WriteModData()
        {
            if (GameState.modData == null)
                GameState.modData = new Dictionary<string, string>();

            ByPrefabId.Clear();
            foreach (Contents contents in ByMug.Values)
            {
                if (contents == null || contents.Water <= FullEpsilon || contents.PrefabId == 0)
                    continue;
                ByPrefabId[contents.PrefabId] = contents;
            }

            if (ByPrefabId.Count == 0)
            {
                GameState.modData.Remove(Key);
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<int, Contents> pair in ByPrefabId)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(Format(pair.Key, pair.Value));
            }

            GameState.modData[Key] = sb.ToString();
        }

        internal static Contents Get(ShipItemBottle mug)
        {
            if (mug == null)
                return null;
            if (ByMug.TryGetValue(mug.GetInstanceID(), out Contents live))
                return live;
            int id = PrefabId(mug);
            if (id != 0 && ByPrefabId.TryGetValue(id, out Contents saved))
            {
                ByMug[mug.GetInstanceID()] = saved;
                return saved;
            }

            return null;
        }

        private static Contents GetOrCreate(ShipItemBottle mug)
        {
            Contents contents = Get(mug);
            if (contents != null)
                return contents;
            contents = new Contents();
            contents.PrefabId = PrefabId(mug);
            ByMug[mug.GetInstanceID()] = contents;
            if (contents.PrefabId != 0)
                ByPrefabId[contents.PrefabId] = contents;
            return contents;
        }

        private static void Transfer(ShipItemSoup pot, Contents dest, float poured)
        {
            float portion = poured / pot.currentWater;
            float energy = pot.currentEnergy * portion;
            float uncooked = pot.currentUncookedEnergy * portion;
            float spoiled = pot.currentSpoiled * portion;
            float salted = pot.currentSalted * portion;
            float vitamins = pot.currentVitamins * portion;
            float protein = pot.currentProtein * portion;

            pot.currentWater -= poured;
            pot.currentEnergy -= energy;
            pot.currentUncookedEnergy -= uncooked;
            pot.currentSpoiled -= spoiled;
            pot.currentSalted -= salted;
            pot.currentVitamins -= vitamins;
            pot.currentProtein -= protein;

            dest.Water += poured;
            dest.Energy += energy;
            dest.Uncooked += uncooked;
            dest.Spoiled += spoiled;
            dest.Salted += salted;
            dest.Vitamins += vitamins;
            dest.Protein += protein;
        }

        private static int PrefabId(ShipItemBottle mug)
        {
            SaveablePrefab prefab = mug.GetComponent<SaveablePrefab>();
            return prefab != null ? prefab.instanceId : 0;
        }

        private static string Format(int id, Contents contents)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}={1},{2},{3},{4},{5},{6},{7},{8}",
                id,
                contents.Water,
                contents.Energy,
                contents.Uncooked,
                contents.Spoiled,
                contents.Salted,
                contents.Vitamins,
                contents.Protein,
                contents.Heat);
        }

        private static Contents Parse(string line, out int id)
        {
            id = 0;
            int eq = line.IndexOf('=');
            if (eq <= 0)
                return null;
            if (!int.TryParse(line.Substring(0, eq), NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                return null;

            string[] parts = line.Substring(eq + 1).Split(',');
            if (parts.Length < 8)
                return null;

            Contents contents = new Contents();
            contents.PrefabId = id;
            if (!TryFloat(parts[0], out contents.Water)
                || !TryFloat(parts[1], out contents.Energy)
                || !TryFloat(parts[2], out contents.Uncooked)
                || !TryFloat(parts[3], out contents.Spoiled)
                || !TryFloat(parts[4], out contents.Salted)
                || !TryFloat(parts[5], out contents.Vitamins)
                || !TryFloat(parts[6], out contents.Protein)
                || !TryFloat(parts[7], out contents.Heat))
            {
                return null;
            }

            return contents;
        }

        private static bool TryFloat(string raw, out float value)
        {
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }

    [HarmonyPatch(typeof(ShipItemSoup), nameof(ShipItemSoup.OnItemClick))]
    internal static class SoupPotReceiveMugPatch
    {
        private static bool Prefix(ShipItemSoup __instance, PickupableItem heldItem)
        {
            if (!FixesConfig.PourSoupIntoMug.Value)
                return true;
            ShipItemBottle mug = heldItem != null ? heldItem.GetComponent<ShipItemBottle>() : null;
            if (!SoupMugs.ShouldHandleMugClick(__instance, mug))
                return true;
            SoupMugs.HandleMugClick(__instance, mug);
            return false;
        }
    }

    [HarmonyPatch(typeof(ShipItemSoup), nameof(ShipItemSoup.AllowOnItemClick))]
    internal static class SoupPotAllowMugPatch
    {
        private static void Postfix(ShipItemSoup __instance, GoPointerButton lookedAtButton, ref bool __result)
        {
            if (!FixesConfig.PourSoupIntoMug.Value || __result)
                return;
            if (!__instance.sold)
                return;
            ShipItemBottle mug = lookedAtButton != null ? lookedAtButton.GetComponent<ShipItemBottle>() : null;
            if (SoupMugs.CanFillFromPot(__instance, mug))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(ShipItemBottle), nameof(ShipItemBottle.OnItemClick))]
    internal static class MugReceiveSoupPatch
    {
        private static bool Prefix(ShipItemBottle __instance, PickupableItem heldItem)
        {
            if (!FixesConfig.PourSoupIntoMug.Value || heldItem == null)
                return true;

            ShipItemBottle heldBottle = heldItem.GetComponent<ShipItemBottle>();
            if (heldBottle != null && (SoupMugs.HasSoup(__instance) || SoupMugs.HasSoup(heldBottle)))
                return false;

            ShipItemSoup soup = heldItem.GetComponent<ShipItemSoup>();
            if (soup == null)
                return true;
            if (!SoupMugs.ShouldHandleMugClick(soup, __instance))
                return true;
            SoupMugs.HandleMugClick(soup, __instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(ShipItemBottle), nameof(ShipItemBottle.Drink))]
    internal static class DrinkSoupFromMugPatch
    {
        private static bool Prefix(ShipItemBottle __instance)
        {
            if (!FixesConfig.PourSoupIntoMug.Value)
                return true;
            return !SoupMugs.TryDrink(__instance);
        }
    }

    [HarmonyPatch(typeof(ShipItemBottle), nameof(ShipItemBottle.EmptyBottle))]
    internal static class EmptySoupMugPatch
    {
        private static void Postfix(ShipItemBottle __instance)
        {
            if (!FixesConfig.PourSoupIntoMug.Value)
                return;
            SoupMugs.Forget(__instance);
        }
    }

    [HarmonyPatch(typeof(ShipItemBottle), nameof(ShipItemBottle.UpdateLookText))]
    internal static class SoupMugLookTextPatch
    {
        private static void Postfix(ShipItemBottle __instance)
        {
            if (!FixesConfig.PourSoupIntoMug.Value)
                return;
            if (!SoupMugs.HasSoup(__instance))
                return;
            __instance.description = "mug of soup\n" + __instance.health + " / " + __instance.GetCapacity();
        }
    }

    [HarmonyPatch(typeof(Mug), "Update")]
    internal static class SoupMugSpillSyncPatch
    {
        private static void Postfix(Mug __instance)
        {
            if (!FixesConfig.PourSoupIntoMug.Value)
                return;
            ShipItemBottle bottle = __instance.GetComponent<ShipItemBottle>();
            if (bottle != null)
            {
                SoupMugs.SyncToHealth(bottle);
                SoupMugs.TryFractionalSpillOrDrink(__instance, bottle);
            }
        }
    }

    [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowLookText))]
    internal static class SoupMugFillPromptPatch
    {
        private static void Postfix(LookUI __instance, GoPointerButton button)
        {
            if (!FixesConfig.PourSoupIntoMug.Value || button == null)
                return;
            GoPointer pointer = Traverse.Create(__instance).Field("pointer").GetValue<GoPointer>();
            if (pointer == null)
                return;
            if (!SoupMugs.TryFillPrompt(pointer.GetHeldItem(), button))
                return;

            TextMesh controls = Traverse.Create(__instance).Field("controlsText").GetValue<TextMesh>();
            if (controls != null)
                controls.text = "fill\n";
        }
    }

    [HarmonyPatch(typeof(ShipItem), nameof(ShipItem.DestroyItem))]
    internal static class SoupMugDestroyPatch
    {
        private static void Prefix(ShipItem __instance)
        {
            if (!FixesConfig.PourSoupIntoMug.Value)
                return;
            ShipItemBottle mug = __instance.GetComponent<ShipItemBottle>();
            if (mug != null)
                SoupMugs.Forget(mug);
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager), nameof(SaveLoadManager.LoadGame))]
    internal static class SoupMugLoadPatch
    {
        private static void Postfix()
        {
            if (!FixesConfig.PourSoupIntoMug.Value)
                return;
            try
            {
                SoupMugs.RestoreAfterLoad();
            }
            catch (Exception e)
            {
                if (Plugin.Log != null)
                    Plugin.Log.LogWarning("PourSoupIntoMug restore after load failed: " + e.Message);
            }
        }
    }
}

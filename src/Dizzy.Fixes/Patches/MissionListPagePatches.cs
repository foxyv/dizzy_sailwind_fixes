using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // Accepting a port mission calls GetMissions(0) and does not change
    // currentPage, so the list shows page 1 while the footer still says
    // page 4. Rebuild the page you are already on. The accepted mission
    // drops out and later missions shift into that page.
    internal static class KeepMissionListPage
    {
        internal static bool Pending;

        internal static bool Enabled()
        {
            return FixesConfig.KeepMissionListPage != null
                && FixesConfig.KeepMissionListPage.Value;
        }
    }

    [HarmonyPatch(typeof(MissionDetailsUI), "ClickButton")]
    internal static class KeepMissionListPageClickPatch
    {
        private static bool _loggedMissing;

        private static void Prefix(MissionDetailsUI __instance)
        {
            KeepMissionListPage.Pending = false;
            if (!KeepMissionListPage.Enabled() || __instance == null)
                return;

            Traverse details = Traverse.Create(__instance);
            if (!details.Field("currentMission").FieldExists()
                || !details.Field("clickable").FieldExists()
                || !details.Field("mapZoomedIn").FieldExists())
            {
                WarnOnce();
                return;
            }

            Mission mission = details.Field("currentMission").GetValue<Mission>();
            bool clickable = details.Field("clickable").GetValue<bool>();
            bool zoomed = details.Field("mapZoomedIn").GetValue<bool>();
            KeepMissionListPage.Pending = clickable && !zoomed && mission != null && mission.missionIndex == -1;
        }

        private static void Postfix()
        {
            KeepMissionListPage.Pending = false;
        }

        private static void WarnOnce()
        {
            if (_loggedMissing || Plugin.Log == null)
                return;
            _loggedMissing = true;
            Plugin.Log.LogWarning("KeepMissionListPage: mission details fields are missing; leaving vanilla page reset.");
        }
    }

    [HarmonyPatch(typeof(MissionListUI), "DisplayMissions")]
    internal static class KeepMissionListPageDisplayPatch
    {
        private static bool _loggedMissing;

        private static void Prefix(MissionListUI __instance, ref Mission[] missions)
        {
            if (!KeepMissionListPage.Pending || __instance == null)
                return;

            KeepMissionListPage.Pending = false;
            Traverse ui = Traverse.Create(__instance);
            if (!ui.Field("currentPage").FieldExists()
                || !ui.Field("currentPageCount").FieldExists()
                || !ui.Field("currentPortDude").FieldExists()
                || !ui.Field("pageCountText").FieldExists())
            {
                WarnOnce();
                return;
            }

            PortDude dude = ui.Field("currentPortDude").GetValue<PortDude>();
            if (dude == null)
                return;

            Port port = dude.GetPort();
            if (port == null)
                return;

            int page = ui.Field("currentPage").GetValue<int>();
            if (page < 0)
                page = 0;

            bool world = __instance.worldMissions;
            Mission[] shown = port.GetMissions(page, world);
            int pageCount = Mathf.CeilToInt(port.GetMissionCount() / 5f);
            if (pageCount < 1)
                pageCount = 1;
            if (page > pageCount - 1)
            {
                page = pageCount - 1;
                shown = port.GetMissions(page, world);
            }

            ui.Field("currentPage").SetValue(page);
            ui.Field("currentPageCount").SetValue(pageCount);
            TextMesh pageText = ui.Field("pageCountText").GetValue<TextMesh>();
            if (pageText != null)
                pageText.text = (page + 1) + " / " + pageCount;

            if (shown != null && shown.Length == missions.Length)
                missions = shown;
        }

        private static void WarnOnce()
        {
            if (_loggedMissing || Plugin.Log == null)
                return;
            _loggedMissing = true;
            Plugin.Log.LogWarning("KeepMissionListPage: mission list fields are missing; leaving vanilla page reset.");
        }
    }
}

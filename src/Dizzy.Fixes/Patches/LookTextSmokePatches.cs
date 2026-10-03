using System;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // LookUI is world-space TextMesh at the item. Pipe bowl smoke and
    // PipeExhaleEffect share the Transparent queue with ZWrite off, and
    // sit closer to the camera, so they draw over the label. Draw LookUI
    // in Overlay instead. Do not disable smoking.
    internal static class LookTextOverlay
    {
        private const int OverlayQueue = 4000;
        private const int OverlayOrder = 32767;

        private static readonly AccessTools.FieldRef<LookUI, TextMesh> ControlsText = GameMembers.Field<LookUI, TextMesh>("controlsText");
        private static readonly AccessTools.FieldRef<LookUI, TextMesh> HintText = GameMembers.Field<LookUI, TextMesh>("hintText");
        private static readonly AccessTools.FieldRef<LookUI, TextMesh> ExtraText = GameMembers.Field<LookUI, TextMesh>("extraText");
        private static readonly AccessTools.FieldRef<LookUI, TextMesh> TextLIcon = GameMembers.Field<LookUI, TextMesh>("textLicon");
        private static readonly AccessTools.FieldRef<LookUI, TextMesh> TextRIcon = GameMembers.Field<LookUI, TextMesh>("textRIcon");
        private static readonly AccessTools.FieldRef<LookUI, Renderer> MouseLIcon = GameMembers.Field<LookUI, Renderer>("mouseLIcon");
        private static readonly AccessTools.FieldRef<LookUI, Renderer> MouseRIcon = GameMembers.Field<LookUI, Renderer>("mouseRIcon");
        private static readonly AccessTools.FieldRef<LookUI, Material> LmbIcon = GameMembers.Field<LookUI, Material>("LMBicon");
        private static readonly AccessTools.FieldRef<LookUI, Material> RmbIcon = GameMembers.Field<LookUI, Material>("RMBicon");

        private static bool _loggedMissing;

        internal static bool Enabled()
        {
            return FixesConfig.KeepLookTextAboveSmoke != null
                && FixesConfig.KeepLookTextAboveSmoke.Value;
        }

        internal static void Apply(LookUI ui)
        {
            if (!Enabled() || ui == null)
                return;

            if (ControlsText == null || HintText == null || ExtraText == null
                || TextLIcon == null || TextRIcon == null
                || MouseLIcon == null || MouseRIcon == null
                || LmbIcon == null || RmbIcon == null)
            {
                WarnMissing("LookUI text/icon fields are missing; leaving vanilla look text.");
                return;
            }

            try
            {
                ApplyText(ControlsText(ui));
                ApplyText(HintText(ui));
                ApplyText(ExtraText(ui));
                ApplyText(TextLIcon(ui));
                ApplyText(TextRIcon(ui));
                ApplyIcon(MouseLIcon(ui));
                ApplyIcon(MouseRIcon(ui));
                ApplyMaterial(LmbIcon(ui));
                ApplyMaterial(RmbIcon(ui));
            }
            catch (Exception e)
            {
                WarnMissing("LookUI overlay failed; leaving vanilla look text. " + e.Message);
            }
        }

        private static void ApplyText(TextMesh mesh)
        {
            if (mesh == null)
                return;

            Renderer renderer = mesh.GetComponent<Renderer>();
            if (renderer == null)
                return;

            renderer.sortingOrder = OverlayOrder;
            ApplyMaterial(renderer.material);
        }

        private static void ApplyIcon(Renderer renderer)
        {
            if (renderer == null)
                return;

            renderer.sortingOrder = OverlayOrder;
        }

        private static void ApplyMaterial(Material material)
        {
            if (material == null)
                return;
            if (material.renderQueue < OverlayQueue)
                material.renderQueue = OverlayQueue;
        }

        private static void WarnMissing(string message)
        {
            if (_loggedMissing || Plugin.Log == null)
                return;

            _loggedMissing = true;
            Plugin.Log.LogWarning("KeepLookTextAboveSmoke: " + message);
        }
    }

    [HarmonyPatch(typeof(LookUI), "Start")]
    internal static class LookTextSmokeStartPatch
    {
        private static void Postfix(LookUI __instance)
        {
            LookTextOverlay.Apply(__instance);
        }
    }

    [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowLookText))]
    internal static class LookTextSmokeShowLookPatch
    {
        private static void Postfix(LookUI __instance)
        {
            LookTextOverlay.Apply(__instance);
        }
    }

    [HarmonyPatch(typeof(LookUI), nameof(LookUI.ShowHoldText))]
    internal static class LookTextSmokeShowHoldPatch
    {
        private static void Postfix(LookUI __instance)
        {
            LookTextOverlay.Apply(__instance);
        }
    }

    [HarmonyPatch(typeof(LookUI), "SetAltIcons")]
    internal static class LookTextSmokeAltIconsPatch
    {
        private static void Postfix(LookUI __instance)
        {
            LookTextOverlay.Apply(__instance);
        }
    }
}

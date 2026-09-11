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

            try
            {
                Traverse t = Traverse.Create(ui);
                if (!HasField(t, "controlsText")
                    || !HasField(t, "hintText")
                    || !HasField(t, "extraText")
                    || !HasField(t, "textLicon")
                    || !HasField(t, "textRIcon")
                    || !HasField(t, "mouseLIcon")
                    || !HasField(t, "mouseRIcon")
                    || !HasField(t, "LMBicon")
                    || !HasField(t, "RMBicon"))
                {
                    WarnMissing("LookUI text/icon fields are missing; leaving vanilla look text.");
                    return;
                }

                ApplyText(t.Field("controlsText").GetValue<TextMesh>());
                ApplyText(t.Field("hintText").GetValue<TextMesh>());
                ApplyText(t.Field("extraText").GetValue<TextMesh>());
                ApplyText(t.Field("textLicon").GetValue<TextMesh>());
                ApplyText(t.Field("textRIcon").GetValue<TextMesh>());
                ApplyIcon(t.Field("mouseLIcon").GetValue<Renderer>());
                ApplyIcon(t.Field("mouseRIcon").GetValue<Renderer>());
                ApplyMaterial(t.Field("LMBicon").GetValue<Material>());
                ApplyMaterial(t.Field("RMBicon").GetValue<Material>());
            }
            catch (Exception e)
            {
                WarnMissing("LookUI overlay failed; leaving vanilla look text. " + e.Message);
            }
        }

        private static bool HasField(Traverse t, string name)
        {
            return t.Field(name).FieldExists();
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

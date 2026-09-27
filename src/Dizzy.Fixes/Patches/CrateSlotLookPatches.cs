using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // The look ray starts on the pointer, about a foot in front of the
    // camera. Up close that origin is already past the open grid, so the
    // physics hit is the crate or whatever sits behind the squares.
    // Slot colliders are only the discs at each center, so a corner of a
    // square still falls through. While the crosshair passes within a cell
    // of a slot, that slot wins. The open crate does not count as something
    // in front of the panel.
    internal static class CrateSlotLook
    {
        private static CrateInventoryButton _sticky;

        internal static GoPointer Pointer;
        internal static CrateInventoryButton Sticky
        {
            get { return _sticky; }
        }

        internal static bool Enabled()
        {
            return FixesConfig.PreferCrateInventorySlot != null
                && FixesConfig.PreferCrateInventorySlot.Value;
        }

        internal static CrateInventoryButton Find(
            GoPointer pointer,
            GoPointerButton current,
            RaycastHit hit,
            out float along)
        {
            along = 0f;
            Pointer = pointer;
            CrateInventoryUI ui = CrateInventoryUI.instance;
            if (pointer == null || ui == null || !ui.showingUI || ui.buttons == null)
            {
                _sticky = null;
                return null;
            }

            Transform aim = pointer.transform;
            Vector3 forward = aim.forward;
            if (forward.sqrMagnitude < 0.0001f)
                return null;
            forward.Normalize();

            Camera camera = aim.GetComponentInParent<Camera>();
            if (camera == null)
                camera = Camera.main;
            Vector3 view = camera != null ? camera.transform.position : aim.position;
            float spacing = CellSpacing(ui);
            float radius = spacing * 0.72f + 0.04f;

            CrateInventoryButton nearest = null;
            float bestLateral = radius;
            float bestAlong = 0f;
            for (int i = 0; i < ui.buttons.Length; i++)
            {
                CrateInventoryButton button = ui.buttons[i];
                if (button == null || !button.gameObject.activeInHierarchy)
                    continue;

                Vector3 toPointer = button.transform.position - aim.position;
                float pointerAlong = Vector3.Dot(toPointer, forward);
                float lateral = (toPointer - forward * pointerAlong).magnitude;
                float viewAlong = Vector3.Dot(button.transform.position - view, forward);

                if (lateral >= bestLateral)
                    continue;
                if (viewAlong < 0.05f)
                    continue;

                bestLateral = lateral;
                bestAlong = pointerAlong;
                nearest = button;
            }

            if (nearest == null)
            {
                _sticky = null;
                return null;
            }

            bool occluded = CloserThanGrid(ui, current, hit, bestAlong);
            if (occluded)
            {
                _sticky = null;
                return null;
            }

            bool held = false;
            CrateInventoryButton chosen = nearest;
            if (_sticky != null && _sticky != nearest && _sticky.gameObject.activeInHierarchy)
            {
                float stickyLateral = Lateral(pointer, _sticky, forward);
                // The shared edge of two squares is only a hair either way.
                // Keep the square already highlighted until the crosshair is
                // clearly closer to the next one.
                if (stickyLateral <= bestLateral + spacing * 0.22f)
                {
                    chosen = _sticky;
                    held = true;
                }
            }

            along = held ? Along(pointer, chosen, forward) : bestAlong;
            if (along < 0.05f)
                along = 0.05f;
            _sticky = chosen;
            return chosen;
        }

        private static float Lateral(GoPointer pointer, CrateInventoryButton button, Vector3 forward)
        {
            Vector3 toPointer = button.transform.position - pointer.transform.position;
            float along = Vector3.Dot(toPointer, forward);
            return (toPointer - forward * along).magnitude;
        }

        private static float Along(GoPointer pointer, CrateInventoryButton button, Vector3 forward)
        {
            return Vector3.Dot(button.transform.position - pointer.transform.position, forward);
        }

        private static float CellSpacing(CrateInventoryUI ui)
        {
            float spacing = float.MaxValue;
            for (int i = 0; i < ui.buttons.Length; i++)
            {
                CrateInventoryButton button = ui.buttons[i];
                if (button == null || !button.gameObject.activeInHierarchy)
                    continue;

                for (int j = i + 1; j < ui.buttons.Length; j++)
                {
                    CrateInventoryButton other = ui.buttons[j];
                    if (other == null || !other.gameObject.activeInHierarchy)
                        continue;
                    float distance = Vector3.Distance(button.transform.position, other.transform.position);
                    if (distance > 0.05f && distance < spacing)
                        spacing = distance;
                }
            }

            if (spacing == float.MaxValue)
                spacing = 0.5f;
            return spacing;
        }

        private static bool CloserThanGrid(
            CrateInventoryUI ui,
            GoPointerButton current,
            RaycastHit hit,
            float slotAlong)
        {
            if (current == null || current is CrateInventoryButton)
                return false;
            if (hit.collider == null)
                return false;
            if (hit.distance >= slotAlong - 0.08f)
                return false;

            return !BelongsToOpenPanel(hit.collider, ui);
        }

        private static bool BelongsToOpenPanel(Collider collider, CrateInventoryUI ui)
        {
            if (collider == null)
                return true;

            Transform transform = collider.transform;
            if (collider.CompareTag("ItemSubcollider") && transform.parent != null)
                transform = transform.parent;

            if (transform.GetComponent<CrateInventoryButton>() != null
                || transform.GetComponentInParent<CrateInventoryButton>() != null)
                return true;

            if (ui != null && transform.IsChildOf(ui.transform))
                return true;

            CrateInventory crate = ui != null ? ui.currentCrate : null;
            return crate != null && transform.IsChildOf(crate.transform);
        }

        internal static void Plant(GoPointer pointer, CrateInventoryButton slot, float along)
        {
            if (pointer == null || slot == null)
                return;

            Vector3 cursor = pointer.transform.position + pointer.transform.forward * along;
            if (pointer.pointer != null)
                pointer.pointer.position = cursor;

            PickupableItem held = pointer.GetHeldItem();
            // Moving food onto the slot sweeps it through the mouth trigger,
            // so looking at an open chest eats the bread in your hand.
            if (held != null && !held.big && !(held is ShipItemFood))
            {
                held.transform.position = cursor + Vector3.up * held.furniturePlaceHeight;
                held.forceDisableRedOutline = true;
            }

            CrateInventory crate = CrateInventoryUI.instance != null
                ? CrateInventoryUI.instance.currentCrate
                : null;
            if (crate == null)
                return;

            ShipItemCrate box = crate.GetComponent<ShipItemCrate>();
            if (box == null)
                return;

            box.forceDisableRedOutline = true;
            Behaviour outline = box.GetComponent("Outline") as Behaviour;
            if (outline != null)
                outline.enabled = false;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter(
        "Dizzy.Fixes.SittingItemLookPatch",
        "Dizzy.Fixes.SkipOtherFoodWhileHoldingPatch")]
    internal static class PreferCrateInventorySlotPatch
    {
        private static void Postfix(
            GoPointer __instance,
            RaycastHit ___hit,
            ref GoPointerButton ___pointedAtButton,
            ref float ___currentLookDistance)
        {
            if (!CrateSlotLook.Enabled())
                return;
            if (GameState.sleeping || GameState.inBed || BoatCamera.on)
                return;

            float along;
            CrateInventoryButton slot = CrateSlotLook.Find(
                __instance,
                ___pointedAtButton,
                ___hit,
                out along);
            // Hitting the crate while holding an item does not call Look(),
            // and the pointer keeps the slot from last time. After a few
            // frames the slot drops its highlight and the next real hit
            // turns it back on, which pulses the outline.
            if (___pointedAtButton is CrateInventoryButton currentSlot)
            {
                if (slot != null)
                    currentSlot.Look(__instance);
                return;
            }

            if (slot == null || slot == ___pointedAtButton)
                return;

            if (___pointedAtButton != null)
                ___pointedAtButton.ForceUnlook();

            ___pointedAtButton = slot;
            slot.Look(__instance);
            ___currentLookDistance = along;
            CrateSlotLook.Plant(__instance, slot, along);
        }
    }

    // DoRaycast runs in FixedUpdate. The click and the held-item preview
    // run later in LateUpdate, and the crate hit is still the one they use
    // whenever the crosshair is on the edge of a square.
    [HarmonyPatch(typeof(GoPointer), "LateUpdate")]
    internal static class CrateSlotPreviewPatch
    {
        private static void Prefix(
            GoPointer __instance,
            RaycastHit ___hit,
            ref GoPointerButton ___pointedAtButton,
            ref float ___currentLookDistance)
        {
            if (!CrateSlotLook.Enabled())
                return;
            if (GameState.sleeping || GameState.inBed || BoatCamera.on)
                return;

            CrateInventoryUI ui = CrateInventoryUI.instance;
            if (ui == null || !ui.showingUI)
                return;

            float along;
            CrateInventoryButton slot = CrateSlotLook.Find(__instance, ___pointedAtButton, ___hit, out along);
            if (slot != null && ___pointedAtButton is CrateInventoryButton currentSlot)
            {
                currentSlot.Look(__instance);
            }
            else if (slot != null)
            {
                if (___pointedAtButton != null)
                    ___pointedAtButton.ForceUnlook();
                ___pointedAtButton = slot;
                slot.Look(__instance);
                ___currentLookDistance = along;
                CrateSlotLook.Plant(__instance, slot, along);
            }
        }

        private static void Postfix(GoPointer __instance, GoPointerButton ___pointedAtButton, float ___currentLookDistance)
        {
            if (!CrateSlotLook.Enabled())
                return;

            CrateInventoryButton slot = ___pointedAtButton as CrateInventoryButton;
            if (slot == null)
                return;

            CrateInventoryUI ui = CrateInventoryUI.instance;
            if (ui == null || !ui.showingUI)
                return;

            CrateSlotLook.Plant(__instance, slot, ___currentLookDistance);
        }
    }

    // UpdateColor turns the crate outline on whenever the ray hits the box.
    // Enabling an outline also restarts the global thickness pulse, so doing
    // that every frame makes the slot highlight flicker. Skip the recolor
    // and leave the crate outline off.
    [HarmonyPatch(typeof(GoPointerButton), "UpdateColor")]
    internal static class OpenCrateOutlinePatch
    {
        private static bool Prefix(GoPointerButton __instance)
        {
            CrateInventoryButton aimed = __instance as CrateInventoryButton;
            if (aimed != null && aimed == CrateSlotLook.Sticky && CrateSlotLook.Pointer != null)
            {
                // UpdateColor was painting this square off, then Look() ran
                // afterwards. Paint it as looked-at instead of letting the
                // outline disable and pulse back on.
                aimed.Look(CrateSlotLook.Pointer);
                return true;
            }

            if (!OpenCrateHighlightPatch.IsOpenCrate(__instance))
                return true;

            __instance.ForceUnlook();
            __instance.forceDisableRedOutline = true;
            OpenCrateHighlightPatch.SuppressOutline(__instance);
            return false;
        }
    }

    // The crate's own LateUpdate turns its outline back on after the look
    // ray hits the box, even when the click is going into a slot.
    [HarmonyPatch(typeof(GoPointerButton), "LateUpdate")]
    internal static class OpenCrateHighlightPatch
    {
        private static void Prefix(GoPointerButton __instance)
        {
            if (!IsOpenCrate(__instance))
                return;

            __instance.ForceUnlook();
            __instance.forceDisableRedOutline = true;
        }

        private static void Postfix(GoPointerButton __instance)
        {
            if (!IsOpenCrate(__instance))
                return;

            __instance.ForceUnlook();
            __instance.forceDisableRedOutline = true;
            SuppressOutline(__instance);
        }

        internal static void SuppressOutline(GoPointerButton box)
        {
            Component[] parts = box.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null || parts[i].GetType().Name != "Outline")
                    continue;

                Behaviour outline = parts[i] as Behaviour;
                if (outline != null && outline.enabled)
                    outline.enabled = false;
            }
        }

        internal static bool IsOpenCrate(GoPointerButton button)
        {
            if (!CrateSlotLook.Enabled() || button == null || button is CrateInventoryButton)
                return false;

            CrateInventoryUI ui = CrateInventoryUI.instance;
            if (ui == null || !ui.showingUI || ui.currentCrate == null)
                return false;

            return button.gameObject == ui.currentCrate.gameObject;
        }
    }

}

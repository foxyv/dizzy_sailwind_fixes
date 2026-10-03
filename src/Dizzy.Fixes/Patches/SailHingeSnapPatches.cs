using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // SailHingeAudio plays sailSnapSoundPrefab when |Δω| >= minAccel.
    // Origin shift only Prepare/RestoreMomentum's boat ShiftingRigidbody;
    // sail RBs stay dynamic on a HingeJoint to the hull, so a lateen
    // (Big Dhow especially) slams and snaps. Do not freeze those RBs —
    // kinematic + copied velocity overwrites the boom transform, which
    // Shipyard Expansion saves as install angle, so a restart loads
    // centered/furled. Mute snap audio on wait/sleep/load/teleport only.
    // Vanilla already skips currentlyLoading / justStarted / !playing.
    internal static class BogusSailSnap
    {
        private const float TeleportAccelFactor = 2f;

        private static readonly Dictionary<int, ShiftingRigidbody> ShifterByShip = new Dictionary<int, ShiftingRigidbody>();

        private static bool _loggedLastVelocity;
        private static bool _loggedShifting;

        internal static bool Enabled()
        {
            return FixesConfig.SuppressBogusSailSnap != null
                && FixesConfig.SuppressBogusSailSnap.Value;
        }

        internal static bool ShiftingThisFrame()
        {
            try
            {
                return FloatingOriginManager.ShiftingThisFrame;
            }
            catch
            {
                return false;
            }
        }

        internal static ShiftingRigidbody FindShifter(Rigidbody ship)
        {
            if (ship == null)
                return null;

            int id = ship.GetInstanceID();
            ShiftingRigidbody shifter;
            if (ShifterByShip.TryGetValue(id, out shifter) && shifter != null)
                return shifter;

            shifter = ship.GetComponent<ShiftingRigidbody>();
            if (shifter == null)
                shifter = ship.GetComponentInParent<ShiftingRigidbody>();
            if (shifter == null)
                shifter = ship.GetComponentInChildren<ShiftingRigidbody>();
            if (shifter != null)
                ShifterByShip[id] = shifter;
            return shifter;
        }

        internal static bool ShipIsShifting(Rigidbody ship)
        {
            ShiftingRigidbody shifter = FindShifter(ship);
            if (shifter == null)
                return false;

            try
            {
                return shifter.IsShifting();
            }
            catch
            {
                if (!_loggedShifting)
                {
                    Plugin.Log.LogWarning("SuppressBogusSailSnap: ShiftingRigidbody.IsShifting is missing; leaving vanilla sail physics.");
                    _loggedShifting = true;
                }

                return false;
            }
        }

        internal static bool ShouldMuteSnap(Rigidbody sailBody)
        {
            if (GameState.waitingForShift || GameState.recovering || GameState.sleeping || GameState.justWokeUp)
                return true;
            if (ShiftingThisFrame())
                return true;

            Sail sail = SailFrom(sailBody);
            Rigidbody ship = sail != null ? sail.shipRigidbody : null;
            if (ship != null && ship.isKinematic)
                return true;
            return ShipIsShifting(ship);
        }

        internal static bool IsTeleportDelta(float delta, float maxAccel)
        {
            if (maxAccel <= 0f)
                return false;
            return delta > maxAccel * TeleportAccelFactor;
        }

        internal static Sail SailFrom(Rigidbody sailBody)
        {
            if (sailBody == null)
                return null;
            Sail sail = sailBody.GetComponent<Sail>();
            if (sail == null)
                sail = sailBody.GetComponentInParent<Sail>();
            return sail;
        }

        internal static void SyncLastVelocity(SailHingeAudio audio, Rigidbody sailBody)
        {
            Traverse last = Traverse.Create(audio).Field("lastVelocity");
            if (!last.FieldExists())
            {
                if (!_loggedLastVelocity)
                {
                    Plugin.Log.LogWarning("SuppressBogusSailSnap: SailHingeAudio.lastVelocity is missing; leaving vanilla snap audio.");
                    _loggedLastVelocity = true;
                }

                return;
            }

            last.SetValue(sailBody.angularVelocity.magnitude);
        }
    }

    [HarmonyPatch(typeof(SailHingeAudio), "FixedUpdate")]
    internal static class SailHingeAudioMutePatch
    {
        private static bool Prefix(SailHingeAudio __instance)
        {
            if (!BogusSailSnap.Enabled())
                return true;
            if (__instance == null || __instance.sail == null)
                return true;

            Rigidbody sailBody = __instance.sail;
            if (BogusSailSnap.ShouldMuteSnap(sailBody))
            {
                BogusSailSnap.SyncLastVelocity(__instance, sailBody);
                return false;
            }

            Traverse last = Traverse.Create(__instance).Field("lastVelocity");
            if (!last.FieldExists())
                return true;

            float delta = Mathf.Abs(sailBody.angularVelocity.magnitude - last.GetValue<float>());
            if (BogusSailSnap.IsTeleportDelta(delta, __instance.maxAccel))
            {
                BogusSailSnap.SyncLastVelocity(__instance, sailBody);
                return false;
            }

            return true;
        }
    }
}

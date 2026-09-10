using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // SailHingeAudio plays sailSnapSoundPrefab when |Δω| >= minAccel.
    // Origin shift only Prepare/RestoreMomentum's boat ShiftingRigidbody;
    // sail RBs stay dynamic on a HingeJoint to the hull, so a lateen
    // (Big Dhow especially) slams and snaps. waitingForShift is true for
    // ~2s of wake fade before the translate — mute audio there, but keep
    // sailing. Freeze sail RBs in PrepareForShifting and release them in
    // RestoreMomentum (with the preserved hull velocity). A delayed freeze
    // after Shift() runs once the hull is dynamic again, so kinematic sails
    // pin the boat in place through the hinge.
    // Vanilla already skips currentlyLoading / justStarted / !playing.
    internal static class BogusSailSnap
    {
        private const float TeleportAccelFactor = 2f;

        private static float _lastCueTime = -99f;

        private static readonly Dictionary<int, bool> FreezeIntent = new Dictionary<int, bool>();
        private static readonly Dictionary<int, bool> FreezeByShip = new Dictionary<int, bool>();
        private static readonly Dictionary<int, bool> SkipForceByShip = new Dictionary<int, bool>();
        private static readonly Dictionary<int, string> MuteByAudio = new Dictionary<int, string>();
        private static readonly Dictionary<int, ShiftingRigidbody> ShifterByShip = new Dictionary<int, ShiftingRigidbody>();

        private static bool _loggedLastVelocity;
        private static bool _loggedSailBody;
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

        internal static void NotifyOriginShift(int x, int z)
        {
            Note("origin Shift(" + x + "," + z + ")");
            PlayShiftCue();
        }

        internal static void PlayShiftCue()
        {
            if (FixesConfig.SuppressBogusSailSnapLog == null || !FixesConfig.SuppressBogusSailSnapLog.Value)
                return;
            if (Time.unscaledTime - _lastCueTime < 0.25f)
                return;
            if (UISoundPlayer.instance == null)
                return;

            _lastCueTime = Time.unscaledTime;
            try
            {
                UISoundPlayer.instance.PlayUISound(UISounds.buttonBack, 0.9f, 0.55f);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("SuppressBogusSailSnap: origin-shift cue failed: " + e.Message);
            }
        }

        internal static bool FreezeIntentFor(Rigidbody ship)
        {
            if (ship == null)
                return false;
            bool freeze;
            return FreezeIntent.TryGetValue(ship.GetInstanceID(), out freeze) && freeze;
        }

        internal static bool ShouldFreezeSail(Rigidbody ship)
        {
            return FreezeIntentFor(ship);
        }

        internal static void SetBoatSailsFrozen(ShiftingRigidbody shifter, bool freeze)
        {
            if (shifter == null)
                return;

            Rigidbody ship = shifter.GetComponent<Rigidbody>();
            if (ship != null)
            {
                if (freeze)
                {
                    FreezeIntent[ship.GetInstanceID()] = true;
                }
                else
                {
                    if (!FreezeIntent.Remove(ship.GetInstanceID()))
                        return;
                }
            }
            else if (!freeze)
            {
                return;
            }

            Vector3 vel = Vector3.zero;
            Vector3 ang = Vector3.zero;
            if (!freeze)
            {
                Traverse t = Traverse.Create(shifter);
                Traverse pv = t.Field("preservedVelocity");
                Traverse pa = t.Field("preservedAngularVel");
                if (pv.FieldExists())
                    vel = pv.GetValue<Vector3>();
                else if (ship != null)
                    vel = ship.velocity;
                if (pa.FieldExists())
                    ang = pa.GetValue<Vector3>();
                else if (ship != null)
                    ang = ship.angularVelocity;
            }

            Sail[] sails = shifter.GetComponentsInChildren<Sail>(true);
            for (int i = 0; i < sails.Length; i++)
            {
                if (sails[i] == null)
                    continue;
                Rigidbody sailBody = sails[i].GetComponent<Rigidbody>();
                if (sailBody == null)
                    continue;

                if (freeze)
                {
                    sailBody.isKinematic = true;
                }
                else
                {
                    sailBody.isKinematic = GameState.currentShipyard != null;
                    sailBody.velocity = vel;
                    sailBody.angularVelocity = ang;
                    sailBody.WakeUp();
                }
            }

            Note((freeze ? "prepare freeze sails on " : "restore unfreeze sails on ") + ObjectName(shifter)
                + " sails=" + sails.Length
                + (freeze ? "" : " vel=" + vel.magnitude.ToString("0.##")));
        }

        internal static bool ShouldSkipSailForce(Rigidbody ship)
        {
            if (GameState.recovering || GameState.sleeping || GameState.justWokeUp)
                return true;
            if (ship != null && ship.isKinematic)
                return true;
            return false;
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

        internal static bool FreezeSailBody(Rigidbody sailBody)
        {
            if (sailBody == null)
            {
                if (!_loggedSailBody)
                {
                    Plugin.Log.LogWarning("SuppressBogusSailSnap: Sail.sailRigidbody is missing; leaving vanilla sail physics.");
                    _loggedSailBody = true;
                }

                return false;
            }

            sailBody.isKinematic = true;
            return true;
        }

        internal static string MuteReason(Rigidbody sailBody)
        {
            if (GameState.waitingForShift)
                return "waitingForShift";
            if (GameState.recovering)
                return "recovering";
            if (GameState.sleeping)
                return "sleeping";
            if (GameState.justWokeUp)
                return "justWokeUp";
            if (ShiftingThisFrame())
                return "ShiftingThisFrame";

            Sail sail = SailFrom(sailBody);
            Rigidbody ship = sail != null ? sail.shipRigidbody : null;
            if (ship != null && ship.isKinematic)
                return "shipKinematic";
            if (ShipIsShifting(ship))
                return "IsShifting";
            return "unknown";
        }

        internal static string SkipForceReason(Rigidbody ship)
        {
            if (GameState.recovering)
                return "recovering";
            if (GameState.sleeping)
                return "sleeping";
            if (GameState.justWokeUp)
                return "justWokeUp";
            if (ship != null && ship.isKinematic)
                return "shipKinematic";
            return "unknown";
        }

        internal static string ObjectName(Component c)
        {
            if (c == null || c.gameObject == null)
                return "?";
            return c.gameObject.name;
        }

        internal static void Note(string message)
        {
            if (FixesConfig.SuppressBogusSailSnapLog == null || !FixesConfig.SuppressBogusSailSnapLog.Value)
                return;
            if (Plugin.Log == null)
                return;
            Plugin.Log.LogInfo("SuppressBogusSailSnap: " + message);
        }

        internal static void NoteFreeze(Rigidbody ship, bool freeze)
        {
            if (ship == null)
                return;

            int id = ship.GetInstanceID();
            bool was;
            FreezeByShip.TryGetValue(id, out was);
            if (was == freeze)
                return;

            FreezeByShip[id] = freeze;
            if (freeze)
            {
                Note("freeze sails on " + ObjectName(ship)
                    + " IsShifting=" + ShipIsShifting(ship)
                    + " intent=" + FreezeIntentFor(ship));
            }
            else
            {
                Note("unfreeze sails on " + ObjectName(ship));
            }
        }

        internal static void NoteSkipForce(Rigidbody ship, bool skip)
        {
            if (ship == null)
                return;

            int id = ship.GetInstanceID();
            bool was;
            SkipForceByShip.TryGetValue(id, out was);
            if (was == skip)
                return;

            SkipForceByShip[id] = skip;
            if (skip)
                Note("skip wind force on " + ObjectName(ship) + " reason=" + SkipForceReason(ship));
            else
                Note("resume wind force on " + ObjectName(ship));
        }

        internal static void NoteMute(SailHingeAudio audio, Rigidbody sailBody, string reason)
        {
            if (audio == null)
                return;

            int id = audio.GetInstanceID();
            string was;
            MuteByAudio.TryGetValue(id, out was);
            if (was == reason)
                return;

            MuteByAudio[id] = reason;
            Note("mute snap on " + ObjectName(sailBody) + " reason=" + reason);
        }

        internal static void NoteAllowSnap(SailHingeAudio audio, Rigidbody sailBody, float delta, float minAccel, float maxAccel)
        {
            if (audio == null)
                return;

            int id = audio.GetInstanceID();
            string was;
            MuteByAudio.TryGetValue(id, out was);
            if (was == "allow")
                return;

            MuteByAudio[id] = "allow";
            Note("allow snap on " + ObjectName(sailBody)
                + " dOmega=" + delta.ToString("0.###")
                + " minAccel=" + minAccel.ToString("0.###")
                + " maxAccel=" + maxAccel.ToString("0.###"));
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
                BogusSailSnap.NoteMute(__instance, sailBody, BogusSailSnap.MuteReason(sailBody));
                BogusSailSnap.SyncLastVelocity(__instance, sailBody);
                return false;
            }

            Traverse last = Traverse.Create(__instance).Field("lastVelocity");
            if (!last.FieldExists())
                return true;

            float delta = Mathf.Abs(sailBody.angularVelocity.magnitude - last.GetValue<float>());
            if (BogusSailSnap.IsTeleportDelta(delta, __instance.maxAccel))
            {
                BogusSailSnap.NoteMute(__instance, sailBody,
                    "teleport dOmega=" + delta.ToString("0.###")
                    + " maxAccel=" + __instance.maxAccel.ToString("0.###"));
                BogusSailSnap.SyncLastVelocity(__instance, sailBody);
                return false;
            }

            if (delta >= __instance.minAccel)
                BogusSailSnap.NoteAllowSnap(__instance, sailBody, delta, __instance.minAccel, __instance.maxAccel);

            return true;
        }
    }

    [HarmonyPatch(typeof(Sail), "FixedUpdate")]
    internal static class SailFreezeDuringShiftPatch
    {
        private static bool Prefix(Sail __instance, Rigidbody ___sailRigidbody, Rigidbody ___shipRigidbody)
        {
            if (!BogusSailSnap.Enabled())
                return true;
            if (__instance == null || ___shipRigidbody == null)
                return true;

            bool freeze = BogusSailSnap.ShouldFreezeSail(___shipRigidbody);
            BogusSailSnap.NoteFreeze(___shipRigidbody, freeze);
            if (!freeze)
                return true;

            BogusSailSnap.FreezeSailBody(___sailRigidbody);
            return false;
        }
    }

    [HarmonyPatch(typeof(Sail), "ApplyForce")]
    internal static class SailSkipForcePatch
    {
        private static bool Prefix(Sail __instance, Rigidbody ___shipRigidbody)
        {
            if (!BogusSailSnap.Enabled())
                return true;
            if (__instance == null)
                return true;

            bool skip = BogusSailSnap.ShouldSkipSailForce(___shipRigidbody);
            BogusSailSnap.NoteSkipForce(___shipRigidbody, skip);
            return !skip;
        }
    }

    [HarmonyPatch(typeof(Sail), "ApplyRotationFromWind")]
    internal static class SailSkipRotationForcePatch
    {
        private static bool Prefix(Sail __instance, Rigidbody ___shipRigidbody)
        {
            if (!BogusSailSnap.Enabled())
                return true;
            if (__instance == null)
                return true;
            return !BogusSailSnap.ShouldSkipSailForce(___shipRigidbody);
        }
    }

    [HarmonyPatch(typeof(FloatingOriginManager), "Shift")]
    internal static class OriginShiftCuePatch
    {
        private static void Postfix(int x, int z)
        {
            if (!BogusSailSnap.Enabled())
                return;
            if (!GameState.playing || GameState.currentlyLoading || GameState.justStarted)
                return;

            BogusSailSnap.NotifyOriginShift(x, z);
        }
    }

    [HarmonyPatch(typeof(ShiftingRigidbody), "PrepareForShifting")]
    internal static class OriginShiftPrepareSailsPatch
    {
        private static void Postfix(ShiftingRigidbody __instance)
        {
            if (!BogusSailSnap.Enabled())
                return;
            if (__instance == null)
                return;
            if (!GameState.playing || GameState.currentlyLoading || GameState.justStarted)
                return;

            BogusSailSnap.SetBoatSailsFrozen(__instance, true);
        }
    }

    [HarmonyPatch(typeof(ShiftingRigidbody), "RestoreMomentum")]
    internal static class OriginShiftRestoreSailsPatch
    {
        private static void Prefix(ShiftingRigidbody __instance)
        {
            if (!BogusSailSnap.Enabled())
                return;
            if (__instance == null)
                return;

            BogusSailSnap.SetBoatSailsFrozen(__instance, false);
        }
    }
}

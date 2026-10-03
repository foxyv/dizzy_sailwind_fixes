using HarmonyLib;
using UnityEngine;

namespace Dizzy.Fixes
{
    // The stove, hook, anchor, mooring and held-item look fixes all postfix
    // GoPointer.DoRaycast and each cast the same look ray (1.8 m, vanilla's
    // look mask). Cast it once per DoRaycast call and share the hits. The
    // cache only lives between this patch's Prefix and Finalizer, so a later
    // physics step or a call from elsewhere always casts fresh.
    internal static class LookRay
    {
        internal const float MaxDistance = 1.8f;
        internal const int LayerMask = -604165;

        private static readonly RaycastHit[] AllHits = new RaycastHit[64];
        private static readonly RaycastHit[] SolidHits = new RaycastHit[64];
        private static bool _inDoRaycast;
        private static bool _cached;
        private static Ray _cachedRay;
        private static int _allCount;
        private static int _solidCount;

        internal static void Begin()
        {
            _inDoRaycast = true;
            _cached = false;
        }

        internal static void End()
        {
            _inDoRaycast = false;
            _cached = false;
        }

        // includeTriggers true matches QueryTriggerInteraction.Collide; false
        // matches UseGlobal, where Physics.queriesHitTriggers decides.
        internal static int Cast(Ray ray, bool includeTriggers, out RaycastHit[] hits)
        {
            if (!_cached || ray.origin != _cachedRay.origin || ray.direction != _cachedRay.direction)
            {
                _allCount = Physics.RaycastNonAlloc(
                    ray,
                    AllHits,
                    MaxDistance,
                    LayerMask,
                    QueryTriggerInteraction.Collide);
                _solidCount = -1;
                _cachedRay = ray;
                _cached = _inDoRaycast;
            }

            if (includeTriggers || Physics.queriesHitTriggers)
            {
                hits = AllHits;
                return _allCount;
            }

            if (_solidCount < 0)
            {
                _solidCount = 0;
                for (int i = 0; i < _allCount; i++)
                {
                    if (AllHits[i].collider != null && !AllHits[i].collider.isTrigger)
                        SolidHits[_solidCount++] = AllHits[i];
                }
            }

            hits = SolidHits;
            return _solidCount;
        }
    }

    [HarmonyPatch(typeof(GoPointer), "DoRaycast")]
    internal static class LookRayScopePatch
    {
        private static void Prefix()
        {
            LookRay.Begin();
        }

        // A void finalizer leaves any exception from DoRaycast as it is.
        private static void Finalizer()
        {
            LookRay.End();
        }
    }
}

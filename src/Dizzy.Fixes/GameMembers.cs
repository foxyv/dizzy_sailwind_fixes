using System;
using System.Reflection;
using HarmonyLib;

namespace Dizzy.Fixes
{
    // Cached accessors for private game fields and methods. Traverse looks a
    // member up by name on every call, which adds up in patches that run
    // every frame; these resolve it once. A member a game update renamed or
    // retyped comes back null with one warning, so callers can fall back to
    // vanilla instead of throwing from a static initializer.
    internal static class GameMembers
    {
        internal static AccessTools.FieldRef<T, F> Field<T, F>(string name)
        {
            FieldInfo field = AccessTools.Field(typeof(T), name);
            if (field == null || field.FieldType != typeof(F))
            {
                WarnMissing("field " + typeof(T).Name + "." + name + " (" + typeof(F).Name + ")");
                return null;
            }

            return AccessTools.FieldRefAccess<T, F>(field);
        }

        // An instance method comes back as an open delegate whose first
        // parameter is the instance, e.g. Func<GoPointer, bool>.
        internal static TDelegate Method<TDelegate>(Type type, string name, Type[] parameters = null)
            where TDelegate : Delegate
        {
            MethodInfo method = AccessTools.Method(type, name, parameters);
            if (method != null)
            {
                try
                {
                    return AccessTools.MethodDelegate<TDelegate>(method);
                }
                catch (Exception)
                {
                    // Signature no longer matches; fall through to the warning.
                }
            }

            WarnMissing("method " + type.Name + "." + name + " (" + typeof(TDelegate).Name + ")");
            return null;
        }

        private static void WarnMissing(string member)
        {
            if (Plugin.Log != null)
                Plugin.Log.LogWarning("Game " + member + " not found; the fix that uses it falls back to vanilla.");
        }
    }
}

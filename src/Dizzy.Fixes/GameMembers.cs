using System.Reflection;
using HarmonyLib;

namespace Dizzy.Fixes
{
    // Cached accessors for private game fields. Traverse looks a field up by
    // name on every call, which adds up in patches that run every frame;
    // these resolve it once. A field a game update renamed or retyped comes
    // back null with one warning, so callers can fall back to vanilla
    // instead of throwing from a static initializer.
    internal static class GameMembers
    {
        internal static AccessTools.FieldRef<T, F> Field<T, F>(string name)
        {
            FieldInfo field = AccessTools.Field(typeof(T), name);
            if (field == null || field.FieldType != typeof(F))
            {
                if (Plugin.Log != null)
                {
                    Plugin.Log.LogWarning("Game field " + typeof(T).Name + "." + name + " (" + typeof(F).Name
                        + ") not found; the fix that reads it falls back to vanilla.");
                }

                return null;
            }

            return AccessTools.FieldRefAccess<T, F>(field);
        }
    }
}

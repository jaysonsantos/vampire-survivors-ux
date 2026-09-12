using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if IL2CPP
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
#else
using System.Reflection;
#endif
#if MELONLOADER
// MelonLoader generates the interop assemblies with the Il2Cpp namespace prefix.
using Il2CppVampireSurvivors;
using Il2CppVampireSurvivors.Objects;
using Il2CppVampireSurvivors.UI;
#else
// The BepInEx interop assemblies and the Mono game assemblies have no namespace prefix.
using VampireSurvivors;
using VampireSurvivors.Objects;
using VampireSurvivors.UI;
#endif

namespace VampireSurvivorsUx
{
    /// <summary>
    /// The identity of one game object across frames.
    /// IL2CPP interop makes a new managed wrapper on every call, so it compares the native pointer.
    /// Mono compares the object reference.
    /// </summary>
    internal struct ObjId
    {
#if IL2CPP
        private IntPtr _value;

        public static ObjId Of(Il2CppObjectBase o) => new ObjId { _value = o.Pointer };

        public bool Same(ObjId other) => _value != IntPtr.Zero && _value == other._value;
#else
        private object _value;

        public static ObjId Of(object o) => new ObjId { _value = o };

        public bool Same(ObjId other) => _value != null && ReferenceEquals(_value, other._value);
#endif

        public static ObjId None => default(ObjId);
    }

    /// <summary>Small differences between the IL2CPP interop API and plain Mono.</summary>
    internal static class Interop
    {
#if IL2CPP
        public static UnityAction ToUnityAction(Action action) => DelegateSupport.ConvertDelegate<UnityAction>(action);

        public static string TypeNameOf(UnityEngine.Object o) => o.GetIl2CppType().Name;

        public static bool Is<T>(Il2CppObjectBase o) where T : Il2CppObjectBase => o != null && o.TryCast<T>() != null;
#else
        public static UnityAction ToUnityAction(Action action) => new UnityAction(action);

        public static string TypeNameOf(UnityEngine.Object o) => o.GetType().Name;

        public static bool Is<T>(object o) where T : class => o is T;
#endif
    }

    /// <summary>
    /// The private game fields that the mod reads.
    /// The IL2CPP interop assemblies make every field public. The Mono assemblies keep the access level,
    /// so the Mono build uses reflection.
    /// </summary>
    internal static class Priv
    {
#if IL2CPP
        public static Selectable DoneButton(RecapPage page) => page._DoneButton;

        public static PlayerOptions PlayerOptionsOf(RecapPage page) => page._playerOptions;

        public static int SelectedIndex(LargeMultiOptionPopup popup) => popup._selectedIndex;

        public static RectTransform ResumeButton(PausePage page) => page._ResumeButton;
#else
        public static Selectable DoneButton(RecapPage page) => Read<Selectable>(page, "_DoneButton");

        public static PlayerOptions PlayerOptionsOf(RecapPage page) => Read<PlayerOptions>(page, "_playerOptions");

        public static int SelectedIndex(LargeMultiOptionPopup popup)
        {
            FieldInfo field = Find(popup.GetType(), "_selectedIndex");
            return field != null ? (int)field.GetValue(popup) : -1;
        }

        public static RectTransform ResumeButton(PausePage page) => Read<RectTransform>(page, "_ResumeButton");

        private static T Read<T>(object target, string name) where T : class
        {
            if (target == null) return null;
            FieldInfo field = Find(target.GetType(), name);
            if (field == null)
            {
                ModLog.Warn("Field " + name + " not found on " + target.GetType().Name + ".");
                return null;
            }
            return field.GetValue(target) as T;
        }

        /// <summary>Walks the base types, because GetField does not see a private field of a base class.</summary>
        private static FieldInfo Find(Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo field = t.GetField(name, flags);
                if (field != null) return field;
            }
            return null;
        }
#endif
    }
}

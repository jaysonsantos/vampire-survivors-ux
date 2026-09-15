using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if IL2CPP
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
#else
using System.Reflection;
#endif
#if MELONLOADER
// MelonLoader generates the interop assemblies with the Il2Cpp namespace prefix.
using Il2CppTMPro;
using Il2CppVampireSurvivors;
using Il2CppVampireSurvivors.App.UI;
using Il2CppVampireSurvivors.Framework;
using Il2CppVampireSurvivors.Framework.Speedup;
using Il2CppVampireSurvivors.Objects;
using Il2CppVampireSurvivors.UI;
#else
// The BepInEx interop assemblies and the Mono game assemblies have no namespace prefix.
using TMPro;
using VampireSurvivors;
using VampireSurvivors.App.UI;
using VampireSurvivors.Framework;
using VampireSurvivors.Framework.Speedup;
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

        public static Button QuickStartButton(MainMenuPage page) => page._QuickStartButton;

        public static int SelectedIndex(LargeMultiOptionPopup popup) => popup._selectedIndex;

        public static GameObject[] SpawnedOptions(LargeMultiOptionPopup popup)
        {
            var list = popup._spawned;
            if (list == null) return new GameObject[0];
            var items = new GameObject[list.Count];
            for (int i = 0; i < items.Length; i++) items[i] = list[i];
            return items;
        }

        public static RectTransform ResumeButton(PausePage page) => page._ResumeButton;

        public static GameObject FastForwardIcon1(FastForwardButton button) => button._icon1;

        public static GameObject FastForwardIcon2(FastForwardButton button) => button._icon2;

        public static GameObject FastForwardIcon3(FastForwardButton button) => button._icon3;

        public static void SetMaxSpeed(SpeedupManager manager, float value) => manager.m_MaxSpeed = value;

        public static float MaxSpeed(SpeedupManager manager) => manager.m_MaxSpeed;

        public static bool IsArcanaPageReady(ArcanaMainSelectionPage page)
            => page._hasFinishedPopulationAnimation && !page._hasPickedRandom;

        public static bool IsSurvarotsPageReady(SurvarotsSelectionPage page)
            => page._hasFinishedPopulationAnimation && !page._hasPickedRandom;
#else
        public static Selectable DoneButton(RecapPage page) => Read<Selectable>(page, "_DoneButton");

        public static PlayerOptions PlayerOptionsOf(RecapPage page) => Read<PlayerOptions>(page, "_playerOptions");

        public static Button QuickStartButton(MainMenuPage page) => Read<Button>(page, "_QuickStartButton");

        public static int SelectedIndex(LargeMultiOptionPopup popup)
        {
            FieldInfo field = Find(popup.GetType(), "_selectedIndex");
            return field != null ? (int)field.GetValue(popup) : -1;
        }

        public static GameObject[] SpawnedOptions(LargeMultiOptionPopup popup)
        {
            var list = Read<System.Collections.Generic.List<GameObject>>(popup, "_spawned");
            return list != null ? list.ToArray() : new GameObject[0];
        }

        public static RectTransform ResumeButton(PausePage page) => Read<RectTransform>(page, "_ResumeButton");

        public static GameObject FastForwardIcon1(FastForwardButton button) => Read<GameObject>(button, "_icon1");

        public static GameObject FastForwardIcon2(FastForwardButton button) => Read<GameObject>(button, "_icon2");

        public static GameObject FastForwardIcon3(FastForwardButton button) => Read<GameObject>(button, "_icon3");

        public static void SetMaxSpeed(SpeedupManager manager, float value)
        {
            FieldInfo field = Find(manager.GetType(), "m_MaxSpeed");
            if (field == null)
            {
                ModLog.Warn("Field m_MaxSpeed not found on SpeedupManager.");
                return;
            }
            field.SetValue(manager, value);
        }

        public static float MaxSpeed(SpeedupManager manager)
        {
            FieldInfo field = Find(manager.GetType(), "m_MaxSpeed");
            return field != null ? (float)field.GetValue(manager) : -1f;
        }

        public static bool IsArcanaPageReady(ArcanaMainSelectionPage page) => IsPageReady(page);

        public static bool IsSurvarotsPageReady(SurvarotsSelectionPage page) => IsPageReady(page);

        /// <summary>Both arcana pages use the same two flags to gate the confirm button.</summary>
        private static bool IsPageReady(object page)
            => ReadBool(page, "_hasFinishedPopulationAnimation") && !ReadBool(page, "_hasPickedRandom");

        private static bool ReadBool(object target, string name)
        {
            if (target == null) return false;
            FieldInfo field = Find(target.GetType(), name);
            if (field == null)
            {
                ModLog.Warn("Field " + name + " not found on " + target.GetType().Name + ".");
                return false;
            }
            return (bool)field.GetValue(target);
        }

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

    /// <summary>
    /// The multi option popup of the game. The list type, the callback types, and the alignment argument differ
    /// between the runtimes.
    /// </summary>
    internal static class Popups
    {
        // The popup holds the native callbacks only. These fields keep the managed ones alive.
        private static Action<int> _onPick;
        private static Action _onClosed;

        /// <summary>
        /// Opens the popup. <paramref name="labels"/> is the title of every line, <paramref name="values"/> the
        /// text below it. The strings are not localization terms.
        /// </summary>
        public static LargeMultiOptionPopup ShowOptions(string id, string title, string description,
            string[] labels, string[] values, Sprite[] icons, Action<int> onPick, Action onClosed)
        {
            _onPick = onPick;
            _onClosed = onClosed;
#if IL2CPP
            var options = new Il2CppSystem.Collections.Generic.List<OptionDataSet>();
            for (int i = 0; i < labels.Length; i++)
            {
                options.Add(new OptionDataSet(labels[i], values != null ? values[i] : string.Empty,
                    icons != null ? icons[i] : null));
            }
            var pick = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<int>>(_onPick);
            var closed = _onClosed != null ? DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(_onClosed) : null;
            // The interop call unboxes the alignment argument, so it needs a real empty Nullable, not null.
            var alignment = new Il2CppSystem.Nullable<TextAlignmentOptions>();
#else
            var options = new System.Collections.Generic.List<OptionDataSet>();
            for (int i = 0; i < labels.Length; i++)
            {
                options.Add(new OptionDataSet(labels[i], values != null ? values[i] : string.Empty,
                    icons != null ? icons[i] : null));
            }
            Action<int> pick = _onPick;
            Action closed = _onClosed;
            TextAlignmentOptions? alignment = null;
#endif
            // The strings are already in the language of the player, so the popup must not translate them.
            return PopupManager.CreateLargeMultiOption(id, title, description, options, pick, closed, false,
                alignment, true);
        }
    }

    /// <summary>
    /// Access to <c>MultiplayerManager.PartySize</c> (<c>int?</c>).
    /// On Mono the field is a plain <c>Nullable</c>.
    /// IL2CPP boxes an empty <c>Nullable</c> as null, so the generated getter throws. The IL2CPP build reads the
    /// struct bytes in place. The interop field offsets of a value type include the 16 byte object header,
    /// so subtract it.
    /// </summary>
    internal static class PartySizeField
    {
#if IL2CPP
        private static bool _ready;
        private static int _baseOffset;
        private static int _hasValueOffset;
        private static int _valueOffset;

        private static bool Init()
        {
            if (_ready) return true;
            try
            {
                IntPtr managerClass = Il2CppClassPointerStore<MultiplayerManager>.NativeClassPtr;
                IntPtr nullableClass = Il2CppClassPointerStore<Il2CppSystem.Nullable<int>>.NativeClassPtr;
                int header = 2 * IntPtr.Size;
                _baseOffset = (int)IL2CPP.il2cpp_field_get_offset(IL2CPP.GetIl2CppField(managerClass, "PartySize"));
                _hasValueOffset = (int)IL2CPP.il2cpp_field_get_offset(IL2CPP.GetIl2CppField(nullableClass, "hasValue")) - header;
                _valueOffset = (int)IL2CPP.il2cpp_field_get_offset(IL2CPP.GetIl2CppField(nullableClass, "value")) - header;
                if (_baseOffset <= 0 || _hasValueOffset < 0 || _valueOffset < 0 || _hasValueOffset == _valueOffset)
                {
                    ModLog.Warn("PartySize offsets look wrong: base=" + _baseOffset + " hasValue=" + _hasValueOffset + " value=" + _valueOffset);
                    return false;
                }
                ModLog.Info("PartySize offsets: base=" + _baseOffset + " hasValue=" + _hasValueOffset + " value=" + _valueOffset);
                _ready = true;
                return true;
            }
            catch (Exception e)
            {
                ModLog.Error("PartySize offset lookup failed", e);
                return false;
            }
        }

        public static bool TryRead(MultiplayerManager manager, out int size)
        {
            size = 0;
            if (!Init()) return false;
            IntPtr obj = IL2CPP.Il2CppObjectBaseToPtrNotNull(manager);
            bool hasValue = Marshal.ReadByte(obj, _baseOffset + _hasValueOffset) != 0;
            size = Marshal.ReadInt32(obj, _baseOffset + _valueOffset);
            return hasValue;
        }

        public static void Write(MultiplayerManager manager, int size)
        {
            if (!Init()) return;
            IntPtr obj = IL2CPP.Il2CppObjectBaseToPtrNotNull(manager);
            Marshal.WriteInt32(obj, _baseOffset + _valueOffset, size);
            Marshal.WriteByte(obj, _baseOffset + _hasValueOffset, 1);
        }
#else
        public static bool TryRead(MultiplayerManager manager, out int size)
        {
            int? value = manager.PartySize;
            size = value ?? 0;
            return value.HasValue;
        }

        public static void Write(MultiplayerManager manager, int size)
        {
            manager.PartySize = size;
        }
#endif
    }
}

using System;
using System.Runtime.InteropServices;
using System.Text;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem.Collections.Generic;
#if BEPINEX
using VampireSurvivors.Data;
using VampireSurvivors.Framework;
using VampireSurvivors.Objects;
using VampireSurvivors.Objects.Algorithm;
#else
using Il2CppVampireSurvivors.Data;
using Il2CppVampireSurvivors.Framework;
using Il2CppVampireSurvivors.Objects;
using Il2CppVampireSurvivors.Objects.Algorithm;
#endif

namespace VampireSurvivorsUx
{
    /// <summary>A copy of one local co-op slot.</summary>
    internal sealed class SlotSnapshot
    {
        public CharacterType Character;
        public AIType AIType;
        public bool HadRewiredPlayer;
    }

    /// <summary>
    /// The run setup: main character, stage, BGM, run modifiers, and the local co-op slots.
    /// Captured on the recap page. Restored at the main menu.
    /// </summary>
    internal sealed class RunSnapshot
    {
        public CharacterType SelectedCharacter;
        public StageType SelectedStage;
        public bool SelectedHyper;
        public bool SelectedHurry;
        public bool SelectedMazzo;
        public bool SelectedLimitBreak;
        public bool SelectedInverse;
        public bool SelectedReapers;
        public bool SelectedGoldenEggs;
        public bool SelectedSurvarots;
        public bool SelectedSharePassives;
        public bool SelectedRandomEvents;
        public bool SelectedRandomLevels;
        public int SelectedMaxWeapons;
        public BgmType SelectedBGM;
        public BgmModType SelectedBGMMod;
        public BgmPlaybackType SelectedBGMPlayback;
        public bool SelectedBGMSave;

        public SlotSnapshot[] Slots = new SlotSnapshot[0];
        public bool PartySizeHasValue;
        public int PartySize;
        public bool PartyModeEnabled;

        public static RunSnapshot Capture(PlayerOptions options, MultiplayerManager multiplayer)
        {
            PlayerOptionsData c = options.Config;
            var s = new RunSnapshot
            {
                SelectedCharacter = c.SelectedCharacter,
                SelectedStage = c.SelectedStage,
                SelectedHyper = c.SelectedHyper,
                SelectedHurry = c.SelectedHurry,
                SelectedMazzo = c.SelectedMazzo,
                SelectedLimitBreak = c.SelectedLimitBreak,
                SelectedInverse = c.SelectedInverse,
                SelectedReapers = c.SelectedReapers,
                SelectedGoldenEggs = c.SelectedGoldenEggs,
                SelectedSurvarots = c.SelectedSurvarots,
                SelectedSharePassives = c.SelectedSharePassives,
                SelectedRandomEvents = c.SelectedRandomEvents,
                SelectedRandomLevels = c.SelectedRandomLevels,
                SelectedMaxWeapons = c.SelectedMaxWeapons,
                SelectedBGM = c.SelectedBGM,
                SelectedBGMMod = c.SelectedBGMMod,
                SelectedBGMPlayback = c.SelectedBGMPlayback,
                SelectedBGMSave = c.SelectedBGMSave,
            };

            if (multiplayer != null)
            {
                List<CoopSlotData> slots = multiplayer.GetLocalPlayerSlots();
                int count = slots != null ? slots.Count : 0;
                s.Slots = new SlotSnapshot[count];
                for (int i = 0; i < count; i++)
                {
                    CoopSlotData slot = slots[i];
                    s.Slots[i] = new SlotSnapshot
                    {
                        Character = slot.SelectedCharacter,
                        AIType = slot.AIType,
                        HadRewiredPlayer = slot.RewiredPlayer != null,
                    };
                }
                s.PartySizeHasValue = PartySizeField.TryRead(multiplayer, out s.PartySize);
                s.PartyModeEnabled = multiplayer.PartyModeEnabled;
            }
            return s;
        }

        /// <summary>Writes the snapshot back. Call this after the game has reset the slots.</summary>
        public void Restore(PlayerOptions options, MultiplayerManager multiplayer)
        {
            PlayerOptionsData c = options.Config;
            c.SelectedCharacter = SelectedCharacter;
            c.SelectedStage = SelectedStage;
            c.SelectedHyper = SelectedHyper;
            c.SelectedHurry = SelectedHurry;
            c.SelectedMazzo = SelectedMazzo;
            c.SelectedLimitBreak = SelectedLimitBreak;
            c.SelectedInverse = SelectedInverse;
            c.SelectedReapers = SelectedReapers;
            c.SelectedGoldenEggs = SelectedGoldenEggs;
            c.SelectedSurvarots = SelectedSurvarots;
            c.SelectedSharePassives = SelectedSharePassives;
            c.SelectedRandomEvents = SelectedRandomEvents;
            c.SelectedRandomLevels = SelectedRandomLevels;
            c.SelectedMaxWeapons = SelectedMaxWeapons;
            c.SelectedBGM = SelectedBGM;
            c.SelectedBGMMod = SelectedBGMMod;
            c.SelectedBGMPlayback = SelectedBGMPlayback;
            c.SelectedBGMSave = SelectedBGMSave;

            if (multiplayer == null) return;
            List<CoopSlotData> slots = multiplayer.GetLocalPlayerSlots();
            int count = Math.Min(slots != null ? slots.Count : 0, Slots.Length);
            for (int i = 0; i < count; i++)
            {
                CoopSlotData slot = slots[i];
                SlotSnapshot snap = Slots[i];
                bool humanLostPad = i > 0 && snap.AIType == AIType.None && snap.HadRewiredPlayer && slot.RewiredPlayer == null;
                if (humanLostPad)
                {
                    // The game removed this player because the controller is gone. Leave the slot empty.
                    ModLog.Warn("Slot " + i + ": controller not connected, slot left empty.");
                    continue;
                }
                slot.SelectedCharacter = snap.Character;
                slot.AIType = snap.AIType;
            }
            if (PartySizeHasValue)
            {
                PartySizeField.Write(multiplayer, PartySize);
            }
            multiplayer.PartyModeEnabled = PartyModeEnabled;
            multiplayer.Refresh();
        }

        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("char=").Append(SelectedCharacter)
              .Append(" stage=").Append(SelectedStage)
              .Append(" bgm=").Append(SelectedBGM).Append('/').Append(SelectedBGMMod)
              .Append(" hyper=").Append(SelectedHyper)
              .Append(" hurry=").Append(SelectedHurry)
              .Append(" arcanas=").Append(SelectedMazzo)
              .Append(" limitBreak=").Append(SelectedLimitBreak)
              .Append(" inverse=").Append(SelectedInverse)
              .Append(" endless=").Append(SelectedReapers)
              .Append(" party=").Append(PartySizeHasValue ? PartySize.ToString() : "none")
              .Append(" slots=[");
            for (int i = 0; i < Slots.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(i).Append(':').Append(Slots[i].Character).Append('/').Append(Slots[i].AIType);
                if (Slots[i].HadRewiredPlayer) sb.Append("/pad");
            }
            sb.Append(']');
            return sb.ToString();
        }
    }

    /// <summary>
    /// Raw access to <c>MultiplayerManager.PartySize</c> (<c>int?</c>).
    /// IL2CPP boxes an empty <c>Nullable</c> as null, so the generated getter throws. This reads the struct bytes in place.
    /// The interop field offsets of a value type include the 16 byte object header, so subtract it.
    /// </summary>
    internal static class PartySizeField
    {
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
    }
}

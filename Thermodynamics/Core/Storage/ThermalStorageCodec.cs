using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    public struct StoredTemperature
    {
        public Vector3I Position;
        public float Temperature;


        public StoredTemperature(Vector3I position, float temperature)
        {
            Position = position;
            Temperature = temperature;
        }
    }

    public struct StoredLoop
    {
        public long Signature;
        public float Temperature;


        public StoredLoop(long signature, float temperature)
        {
            Signature = signature;
            Temperature = temperature;
        }
    }

    public struct StoredLoopFill
    {
        public long Signature;
        public float Fill;


        public StoredLoopFill(long signature, float fill)
        {
            Signature = signature;
            Fill = fill;
        }
    }

    public struct StoredHeldCoolant
    {
        public Vector3I Position;
        public float Capacity;


        public StoredHeldCoolant(Vector3I position, float capacity)
        {
            Position = position;
            Capacity = capacity;
        }
    }

    public struct StoredRoom
    {
        public Vector3I Anchor;
        public float Temperature;


        public StoredRoom(Vector3I anchor, float temperature)
        {
            Anchor = anchor;
            Temperature = temperature;
        }
    }

    public static class ThermalStorageCodec
    {
        private const byte Version2Marker = 0xFD;
        private const byte SectionBlocks = 1;
        private const byte SectionLoops = 2;
        private const byte SectionRooms = 3;
        private const byte SectionHeldCoolant = 4;
        private const byte SectionLoopFill = 5;

        private const int LegacyRecordSize = 6;
        private const int LegacyLoopRecordSize = 3;

        private const int Int32Size = 4;
        private const int RecordSize = 12;



        public static string Encode(IList<StoredTemperature> blocks, IList<StoredLoop> loops)
        {

            return Encode(blocks, loops, null);
        }


        public static string Encode(IList<StoredTemperature> blocks, IList<StoredLoop> loops, IList<StoredRoom> rooms)
        {

            return Encode(blocks, loops, rooms, null);
        }


        public static string Encode(IList<StoredTemperature> blocks, IList<StoredLoop> loops,
            IList<StoredRoom> rooms, IList<StoredHeldCoolant> held)
        {

            return Encode(blocks, loops, rooms, held, null);
        }


        public static string Encode(IList<StoredTemperature> blocks, IList<StoredLoop> loops,
            IList<StoredRoom> rooms, IList<StoredHeldCoolant> held, IList<StoredLoopFill> fills)
        {
            int blockCount = blocks == null ? 0 : blocks.Count;
            int loopCount = loops == null ? 0 : loops.Count;
            int roomCount = rooms == null ? 0 : rooms.Count;
            int heldCount = held == null ? 0 : held.Count;
            int fillCount = fills == null ? 0 : fills.Count;

            int size = 1
                + (1 + Int32Size + (blockCount * RecordSize))
                + (1 + Int32Size + (loopCount * RecordSize));

            if (roomCount > 0) size += 1 + Int32Size + (roomCount * RecordSize);

            if (heldCount > 0) size += 1 + Int32Size + (heldCount * RecordSize);

            if (fillCount > 0) size += 1 + Int32Size + (fillCount * RecordSize);

            byte[] bytes = new byte[size];
            int at = 0;

            bytes[at++] = Version2Marker;
            bytes[at++] = SectionBlocks;
            WriteInt32(bytes, ref at, blockCount);

            for (int i = 0; i < blockCount; i++)
            {
                StoredTemperature entry = blocks[i];
                WriteInt64(bytes, ref at, GridMath.Key(entry.Position));
                WriteSingle(bytes, ref at, entry.Temperature);
            }

            bytes[at++] = SectionLoops;
            WriteInt32(bytes, ref at, loopCount);

            for (int i = 0; i < loopCount; i++)
            {
                StoredLoop entry = loops[i];
                WriteInt64(bytes, ref at, entry.Signature);
                WriteSingle(bytes, ref at, entry.Temperature);
            }

            if (roomCount > 0)
            {
                bytes[at++] = SectionRooms;
                WriteInt32(bytes, ref at, roomCount);

                for (int i = 0; i < roomCount; i++)
                {
                    StoredRoom entry = rooms[i];
                    WriteInt64(bytes, ref at, GridMath.Key(entry.Anchor));
                    WriteSingle(bytes, ref at, entry.Temperature);
                }
            }

            if (heldCount > 0)
            {
                bytes[at++] = SectionHeldCoolant;
                WriteInt32(bytes, ref at, heldCount);

                for (int i = 0; i < heldCount; i++)
                {
                    StoredHeldCoolant entry = held[i];
                    WriteInt64(bytes, ref at, GridMath.Key(entry.Position));
                    WriteSingle(bytes, ref at, entry.Capacity);
                }
            }

            if (fillCount > 0)
            {
                bytes[at++] = SectionLoopFill;
                WriteInt32(bytes, ref at, fillCount);

                for (int i = 0; i < fillCount; i++)
                {
                    StoredLoopFill entry = fills[i];
                    WriteInt64(bytes, ref at, entry.Signature);
                    WriteSingle(bytes, ref at, entry.Fill);
                }
            }

            return Convert.ToBase64String(bytes);
        }


        public static bool TryDecode(string data, List<StoredTemperature> blocks, List<StoredLoop> loops)
        {

            return TryDecode(data, blocks, loops, null);
        }


        public static bool TryDecode(string data, List<StoredTemperature> blocks, List<StoredLoop> loops, List<StoredRoom> rooms)
        {

            return TryDecode(data, blocks, loops, rooms, null);
        }


        public static bool TryDecode(string data, List<StoredTemperature> blocks, List<StoredLoop> loops,
            List<StoredRoom> rooms, List<StoredHeldCoolant> held)
        {

            return TryDecode(data, blocks, loops, rooms, held, null);
        }


        public static bool TryDecode(string data, List<StoredTemperature> blocks, List<StoredLoop> loops,
            List<StoredRoom> rooms, List<StoredHeldCoolant> held, List<StoredLoopFill> fills)
        {
            bool legacy;
            return TryDecode(data, blocks, loops, rooms, held, fills, out legacy);
        }


        internal static bool TryDecode(string data, List<StoredTemperature> blocks, List<StoredLoop> loops,
            List<StoredRoom> rooms, List<StoredHeldCoolant> held, List<StoredLoopFill> fills, out bool legacy)
        {
            legacy = false;
            if (blocks != null) blocks.Clear();
            if (loops != null) loops.Clear();
            if (rooms != null) rooms.Clear();
            if (held != null) held.Clear();
            if (fills != null) fills.Clear();

            if (string.IsNullOrEmpty(data)) return false;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(data);
            }
            catch (FormatException)
            {
                return false;
            }

            if (bytes.Length == 0) return false;

            if (bytes[0] == Version2Marker)
            {

                return TryDecodeVersion2(bytes, blocks, loops, rooms, held, fills);
            }

            legacy = true;
            return TryDecodeLegacyBlocks(bytes, blocks);
        }


        private static bool TryDecodeVersion2(byte[] bytes, List<StoredTemperature> blocks,
            List<StoredLoop> loops, List<StoredRoom> rooms, List<StoredHeldCoolant> held,
            List<StoredLoopFill> fills)
        {
            int at = 1;
            while (at < bytes.Length)
            {
                byte section = bytes[at++];

                if (at + Int32Size > bytes.Length) return false;

                int count = ReadInt32(bytes, ref at);
                if (count < 0) return false;

                if (count > (bytes.Length - at) / RecordSize) return false;

                for (int i = 0; i < count; i++)
                {

                    long key = ReadInt64(bytes, ref at);

                    float temperature = ReadSingle(bytes, ref at);
                    // Recover healthy records without importing a nonfinite temperature, fill or capacity.
                    if (float.IsNaN(temperature) || float.IsInfinity(temperature)) continue;

                    if (section == SectionBlocks)
                    {
                        if (blocks != null) blocks.Add(new StoredTemperature(GridMath.FromKey(key), temperature));
                    }

                    else if (section == SectionLoops)
                    {
                        if (loops != null) loops.Add(new StoredLoop(key, temperature));
                    }

                    else if (section == SectionRooms)
                    {
                        if (rooms != null) rooms.Add(new StoredRoom(GridMath.FromKey(key), temperature));
                    }

                    else if (section == SectionHeldCoolant)
                    {
                        if (held != null) held.Add(new StoredHeldCoolant(GridMath.FromKey(key), temperature));
                    }

                    else if (section == SectionLoopFill)
                    {
                        if (fills != null) fills.Add(new StoredLoopFill(key, temperature));
                    }
                }
            }
            return true;
        }



        public static string EncodeLegacyBlocks(IList<StoredTemperature> blocks)
        {
            int count = blocks == null ? 0 : blocks.Count;
            byte[] bytes = new byte[count * LegacyRecordSize];

            int at = 0;
            for (int i = 0; i < count; i++)
            {
                int id = GridMath.LegacyFlatten(blocks[i].Position);
                bytes[at] = (byte)id;
                bytes[at + 1] = (byte)(id >> 8);
                bytes[at + 2] = (byte)(id >> 16);
                bytes[at + 3] = (byte)(id >> 24);

                short temperature = (short)blocks[i].Temperature;
                bytes[at + 4] = (byte)temperature;
                bytes[at + 5] = (byte)(temperature >> 8);

                at += LegacyRecordSize;
            }

            return Convert.ToBase64String(bytes);
        }


        public static string EncodeLegacyLoops(IList<float> loopTemperatures)
        {
            int count = loopTemperatures == null ? 0 : loopTemperatures.Count;
            byte[] bytes = new byte[count * LegacyLoopRecordSize];

            int at = 0;
            for (int i = 0; i < count; i++)
            {
                bytes[at] = (byte)i;
                short temperature = (short)loopTemperatures[i];
                bytes[at + 1] = (byte)temperature;
                bytes[at + 2] = (byte)(temperature >> 8);
                at += LegacyLoopRecordSize;
            }

            return Convert.ToBase64String(bytes);
        }


        private static bool TryDecodeLegacyBlocks(byte[] bytes, List<StoredTemperature> blocks)
        {
            if (bytes.Length % LegacyRecordSize != 0) return false;
            if (blocks == null) return true;

            for (int i = 0; i + LegacyRecordSize <= bytes.Length; i += LegacyRecordSize)
            {
                int id = bytes[i];
                id |= bytes[i + 1] << 8;
                id |= bytes[i + 2] << 16;
                id |= bytes[i + 3] << 24;

                short temperature = (short)(bytes[i + 4] | (bytes[i + 5] << 8));

                blocks.Add(new StoredTemperature(GridMath.LegacyUnflatten(id), temperature));
            }
            return true;
        }


        public static bool TryDecodeLegacyLoops(string data, List<float> temperaturesByIndex)
        {
            if (temperaturesByIndex != null) temperaturesByIndex.Clear();
            if (string.IsNullOrEmpty(data)) return false;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(data);
            }
            catch (FormatException)
            {
                return false;
            }

            if (bytes.Length % LegacyLoopRecordSize != 0) return false;
            if (temperaturesByIndex == null) return true;

            for (int i = 0; i + LegacyLoopRecordSize <= bytes.Length; i += LegacyLoopRecordSize)
            {
                int index = bytes[i];
                short temperature = (short)(bytes[i + 1] | (bytes[i + 2] << 8));

                while (temperaturesByIndex.Count <= index)
                {
                    temperaturesByIndex.Add(0f);
                }
                temperaturesByIndex[index] = temperature;
            }
            return true;
        }



        private static void WriteInt32(byte[] bytes, ref int at, int value)
        {
            bytes[at++] = (byte)value;
            bytes[at++] = (byte)(value >> 8);
            bytes[at++] = (byte)(value >> 16);
            bytes[at++] = (byte)(value >> 24);
        }


        private static int ReadInt32(byte[] bytes, ref int at)
        {
            int value = bytes[at]
                | (bytes[at + 1] << 8)
                | (bytes[at + 2] << 16)
                | (bytes[at + 3] << 24);
            at += 4;
            return value;
        }


        private static void WriteInt64(byte[] bytes, ref int at, long value)
        {
            for (int i = 0; i < 8; i++)
            {
                bytes[at++] = (byte)(value >> (i * 8));
            }
        }


        private static long ReadInt64(byte[] bytes, ref int at)
        {
            long value = 0;
            for (int i = 0; i < 8; i++)
            {
                value |= (long)bytes[at + i] << (i * 8);
            }
            at += 8;
            return value;
        }


        private static void WriteSingle(byte[] bytes, ref int at, float value)
        {
            byte[] raw = BitConverter.GetBytes(value);
            bytes[at++] = raw[0];
            bytes[at++] = raw[1];
            bytes[at++] = raw[2];
            bytes[at++] = raw[3];
        }


        private static float ReadSingle(byte[] bytes, ref int at)
        {
            float value = BitConverter.ToSingle(bytes, at);
            at += 4;
            return value;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace GothosDC.LowLevel
{
    internal class TeraDataReader : BinaryReader
    {
        public TeraDataReader(Stream input)
            : base(input, Encoding.Unicode)
        {
        }

        private static Stream CreateStreamSlice(Stream input, DataCenterRegion region)
        {
            return new StreamSlice(input, region.Start, region.Length);
        }

        private static Stream CreateMemoryStream(Stream input)
        {
            var inputLength = (int)input.Length;
            var stream = new MemoryStream(inputLength);
            input.CopyTo(stream);
            stream.Position = 0;
            return stream;
        }

        public TeraDataReader(Stream input, DataCenterRegion region)
            : base(CreateMemoryStream(CreateStreamSlice(input, region)), Encoding.Unicode, true)
        {
        }

        public byte[] ReadBytesChecked(int count)
        {
            var data = ReadBytes(count);
            if (data.Length != count)
                throw new Exception("Unexpected end of stream");
            return data;
        }

        // Tera uses null terminated litte endian UTF-16 strings
        public string ReadTeraString()
        {
            var builder = new StringBuilder();
            while (true)
            {
                var c = (char)ReadUInt16();
                if (c == 0)
                    return builder.ToString();
                builder.Append(c);
            }
        }

        public static Func<TeraDataReader, KeyValuePair<ushort, T>> WithOffset<T>(Func<TeraDataReader, T> readData, int elementSize)
        {
            Func<TeraDataReader, KeyValuePair<ushort, T>> addOffset = reader =>
            {
                var offset = reader.BaseStream.Position;
                Debug.Assert(offset % elementSize == 0);
                var data = readData(reader);
                return new KeyValuePair<ushort,T>((ushort)(offset / elementSize), data);
            };
            return addOffset;
        }

        public SegmentAddress ReadSegmentAddress()
        {
            var segmentIndex = ReadUInt16();
            var elementIndex = ReadUInt16();
            return new SegmentAddress(segmentIndex, elementIndex);
        }

        public DataCenterValueRaw ReadDcValue()
        {
            var key = ReadUInt16();
            var typeCode = ReadUInt16();
            var value = ReadUInt32();
            return new DataCenterValueRaw((TypeCode)typeCode, key, value);
        }

        public DataCenterElementRaw ReadDcObject()
        {
            var nameKey = ReadUInt16();
            var unknown = ReadUInt16();
            var argsCount = ReadUInt16();
            var subCount = ReadUInt16();
            var argsAddress = ReadSegmentAddress();
            var subAddress = ReadSegmentAddress();
            return new DataCenterElementRaw(nameKey, unknown, argsCount, subCount, argsAddress, subAddress);
        }

        public static IEnumerable<T> ReadAll<T>(Stream stream, DataCenterRegion region, Func<TeraDataReader, T> readOne)
        {
            using (var reader = new TeraDataReader(stream, region))
            {
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    yield return readOne(reader);
                }
                if (reader.BaseStream.Position > reader.BaseStream.Length)
                    throw new Exception("Read beyond the end");
            }
        }
    }
}

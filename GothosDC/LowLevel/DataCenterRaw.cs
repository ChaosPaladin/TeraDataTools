﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GothosDC.LowLevel
{
    public class DataCenterRaw
    {
        public Stream Stream { get; private set; }
        public DataCenterRegions Regions { get; private set; }

        public List<KeyValuePair<SegmentAddress, DataCenterValueRaw>> Values { get; set; }
        public List<KeyValuePair<SegmentAddress, DataCenterElementRaw>> Elements { get; set; }
        public List<KeyValuePair<SegmentAddress, string>> Strings { get; set; }
        public List<SegmentAddress> StringIds { get; set; }
        public List<KeyValuePair<SegmentAddress, string>> Names { get; set; }
        public List<SegmentAddress> NameIds { get; set; }

        public static DataCenterRaw Load(string filename)
        {
            using (var stream = File.OpenRead(filename))
            {
                return new DataCenterRaw(stream);
            }
        }

        internal DataCenterRaw(Stream stream)
        {
            Stream = stream;
            Regions = RegionListReader.ReadAllRegions(stream);

            Strings = ReadSegmented(Regions.Strings, r => r.ReadTeraString());
            Values = ReadSegmented(Regions.Values, r => r.ReadDcValue());
            StringIds = ReadAll(Regions.StringIds, r => r.ReadSegmentAddress());
            Names = ReadSegmented(Regions.Names, r => r.ReadTeraString());
            NameIds = ReadAll(Regions.NameIds, r => r.ReadSegmentAddress());
            Elements = ReadSegmented(Regions.Elements, r => r.ReadDcObject());

            AssertSequenceEquals(Strings.Select(x => x.Key), StringIds);
            AssertSequenceEquals(Names.Select(x => x.Key), NameIds);
        }

        private List<KeyValuePair<SegmentAddress, T>> ReadSegmented<T>(IEnumerable<DataCenterRegion> regions, Func<TeraDataReader, T> readOne)
        {
            return Flatten(regions.Select(region => ReadAll(region, TeraDataReader.WithOffset(readOne, region.ElementSize))));
        }

        private List<KeyValuePair<SegmentAddress, T>> Flatten<T>(IEnumerable<IEnumerable<KeyValuePair<ushort, T>>> segmentedData)
        {
            return segmentedData.SelectMany((x, segmentIndex) => x.Select(y =>new KeyValuePair<SegmentAddress, T>(new SegmentAddress((ushort)segmentIndex, y.Key), y.Value))).ToList();
        }

        private List<T> ReadAll<T>(DataCenterRegion region, Func<TeraDataReader, T> readOne)
        {
            return TeraDataReader.ReadAll(Stream, region, readOne).ToList();
        }

        private static void AssertSequenceEquals<T>(IEnumerable<T> x, IEnumerable<T> y)
        {
            var onlyX = x.Except(y);
            var onlyY = y.Except(x);
            if (onlyX.Any() || onlyY.Any())
                throw new Exception("Inconsitency detected");
        }
    }
}

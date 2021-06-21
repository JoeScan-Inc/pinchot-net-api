// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    internal class ProfileFragments
    {
        private readonly List<DataPacket> rawPackets;

        internal long TimeCreated { get; private set; }

        internal int Source { get; private set; }

        internal long Timestamp { get; private set; }

        internal bool Complete { get; private set; }

        internal int Count => rawPackets?.Count ?? 0;

        internal DataPacket this[int key] => rawPackets[key];

        internal ProfileFragments() => rawPackets = new List<DataPacket>(10);

        internal ProfileFragments(DataPacket data, long timeCreated)
            : this()
        {
            rawPackets.Add(data);
            TimeCreated = timeCreated;
            Timestamp = data.Timestamp;               // immutable
            Complete = data.NumParts == data.PartNum; // single part profile
            Source = data.Source;
        }

        internal void Add(DataPacket dataPacket)
        {
            // TODO: add sanity check that we only add packets that have the same timestamp
            rawPackets.Add(dataPacket);
            // TODO: or should we explicitly check for all numbers being there?
            Complete = rawPackets[0].NumParts == rawPackets.Count;
        }

        internal void Clear()
        {
            rawPackets.Clear();
            TimeCreated = default;
            Source = default;
            Timestamp = default;
            Complete = default;
        }

        public IEnumerator<DataPacket> GetEnumerator()
        {
            return rawPackets.GetEnumerator();
        }
    }
}
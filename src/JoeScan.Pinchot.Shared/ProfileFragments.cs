// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections;
using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    internal class ProfileFragments
    {
        private readonly List<DataPacket> rawPackets;

        internal long TimeCreated { get; }

        internal int Source { get; }

        internal long Timestamp { get; }

        internal bool Complete { get; private set; }

        internal int Count => rawPackets.Count;

        internal DataPacket this[int key]
        {
            get { return rawPackets[key]; }
        }

        internal ProfileFragments(DataPacket data, long timeCreated)
        {
            rawPackets = new List<DataPacket>(10) { data };
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
    }
}
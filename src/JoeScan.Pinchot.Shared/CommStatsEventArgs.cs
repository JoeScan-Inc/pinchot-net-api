// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    internal class CommStatsEventArgs : EventArgs
    {
        public uint ID { get; set; }
        public long CompleteProfilesReceived { get; set; }
        public double ProfileRate { get; set; }
        public long Evicted { get; set; }
        public long BytesReceived { get; set; }
        public double DataRate { get; set; }
        public long BadPackets { get; set; }
        public long GoodPackets { get; set; }
    }
}
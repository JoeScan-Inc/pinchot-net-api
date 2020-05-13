// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    internal class ScanSyncData
    {
        public int SerialNumber { get; internal set; }
        public int Sequence { get; internal set; }
        public int EncoderTimeStampSeconds { get; internal set; }
        public int EncoderTimeStampNanoseconds { get; internal set; }
        public int LastTimeStampSeconds { get; internal set; }
        public int LastTimeStampNanoseconds { get; internal set; }
        public long EncoderValue { get; internal set; }

        public TimeSpan ToSpan()
        {
            return TimeSpan.FromSeconds(EncoderTimeStampSeconds) + TimeSpan.FromTicks(
                (EncoderTimeStampNanoseconds * TimeSpan.TicksPerSecond) / 1000000);
        }

        public ScanSyncData()
        {
        }
    }
}
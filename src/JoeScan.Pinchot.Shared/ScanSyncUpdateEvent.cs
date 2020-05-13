// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    internal class ScanSyncUpdateEvent : EventArgs
    {
        internal int SerialNumber { get; }
        internal int Sequence { get; }
        internal int EncoderTimeStampSeconds { get; }
        internal int EncoderTimeStampNanoseconds { get; }
        internal int LastTimeStampSeconds { get; }
        internal int LastTimeStampNanoseconds { get; }

        public ScanSyncUpdateEvent(ScanSyncData data)
        {
            SerialNumber = data.SerialNumber;
            Sequence = data.Sequence;
            EncoderTimeStampSeconds = data.EncoderTimeStampSeconds;
            EncoderTimeStampNanoseconds = data.EncoderTimeStampNanoseconds;
            LastTimeStampSeconds = data.LastTimeStampSeconds;
            LastTimeStampNanoseconds = data.LastTimeStampNanoseconds;
        }
    }
}
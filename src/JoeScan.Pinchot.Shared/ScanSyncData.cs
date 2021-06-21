// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

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
    }
}
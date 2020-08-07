// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    internal enum ScanPacketType : byte
    {
        Invalid = 0,

        // Connect = 1, OBSOLETE
        StartScanning = 2,
        Status = 3,
        Window = 4,
        RequestMappleTable = 5,
        Disconnect = 6,
        BroadcastConnect = 7
    }
}
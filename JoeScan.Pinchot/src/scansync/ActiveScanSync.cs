// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    internal class ActiveScanSync
    {
        public ScanSyncData ScanSync { get; set; }
        public int LastUpdateTick { get; set; }
    }
}

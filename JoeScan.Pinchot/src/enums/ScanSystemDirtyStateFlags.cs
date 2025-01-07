// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enumeration for tracking configuration state changes for a <see cref="ScanSystem"/>.
    /// </summary>
    /// <seealso cref="ScanSystem.PreSendConfiguration"/>
    [Flags]
    internal enum ScanSystemDirtyStateFlags
    {
        Clean = 0,
        ScanSyncMapping = 1 << 1,
        PhaseTable = 1 << 2,
        AllDirty = ScanSyncMapping | PhaseTable
    }
}

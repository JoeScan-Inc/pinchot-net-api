// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Event args passed to the <see cref="ScanSystem.ScanSyncUpdateEvent"/> event.
    /// </summary>
    public class ScanSyncUpdateEvent : EventArgs
    {
        /// <summary>
        /// Data from the ScanSync update.
        /// </summary>
        [Obsolete("Use 'ScanSyncs' property instead to get a list of all ScanSyncs on the network.")]
        public ScanSyncData Data { get; }

        /// <summary>
        /// Data from the ScanSync update.
        /// </summary>
        public List<ScanSyncData> ScanSyncs { get; }

        internal ScanSyncUpdateEvent(List<ScanSyncData> data)
        {
            Data = data.FirstOrDefault();
            ScanSyncs = data;
        }
    }
}
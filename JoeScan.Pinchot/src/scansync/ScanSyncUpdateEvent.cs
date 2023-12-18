// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

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
        public ScanSyncData Data { get; }

        internal ScanSyncUpdateEvent(ScanSyncData data)
        {
            Data = data;
        }
    }
}
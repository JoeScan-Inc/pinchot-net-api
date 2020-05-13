// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Data type for identifying how the connection and
    /// resulting operational state should be
    /// </summary>
    internal enum ConnectionType : byte
    {
        /// <summary>
        /// Standard operation
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Internal use only, should never be used
        /// outside of the mappling process
        /// </summary>
        Mappler = 1,
    }
}
// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    /// <summary>
    /// There are two exposure modes: Interleaved and Simultaneous.
    /// In Interleaved mode, one camera exposes at the start of a scan interval,
    /// and the other 1/2 scan interval later.
    /// In Simultaneous mode, both cameras expose at the same time.
    /// </summary>
    internal enum ExposureMode
    {
        Interleaved,
        Simultaneous
    }
}
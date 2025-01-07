// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enumeration for tracking configuration state changes for a <see cref="ScanHead"/>.
    /// </summary>
    /// <seealso cref="ScanSystem.PreSendConfiguration"/>
    [Flags]
    internal enum ScanHeadDirtyStateFlags
    {
        Clean = 0,
        Window = 1 << 1,
        Configuration = 1 << 2,
        ExclusionMask = 1 << 3,
        BrightnessCorrection = 1 << 4,
        AllDirty = Window | Configuration | ExclusionMask | BrightnessCorrection
    }
}

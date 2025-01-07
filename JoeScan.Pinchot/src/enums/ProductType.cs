// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using joescan.schema;
using System.ComponentModel;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enumeration for identifying a scan head product type.
    /// </summary>
    public enum ProductType : uint
    {
        /// <summary>
        /// Invalid
        /// </summary>
        [Description("Invalid")]
        Invalid = ScanHeadType.INVALID,

        /// <summary>
        /// JS-50 WX
        /// </summary>
        [Description("JS-50 WX")]
        JS50WX = ScanHeadType.JS50WX,

        /// <summary>
        /// JS-50 WSC
        /// </summary>
        [Description("JS-50 WSC")]
        JS50WSC = ScanHeadType.JS50WSC,

        /// <summary>
        /// JS-50 X6B20
        /// </summary>
        [Description("JS-50 X6B (20°)")]
        JS50X6B20 = ScanHeadType.JS50X6B20,

        /// <summary>
        /// JS-50 X6B30
        /// </summary>
        [Description("JS-50 X6B (30°)")]
        JS50X6B30 = ScanHeadType.JS50X6B30,

        /// <summary>
        /// JS-50 MX
        /// </summary>
        [Description("JS-50 MX")]
        JS50MX = ScanHeadType.JS50MX,

        /// <summary>
        /// JS-50 Z820
        /// </summary>
        [Description("JS-50 Z8 (20°)")]
        JS50Z820 = ScanHeadType.JS50Z820,

        /// <summary>
        /// JS-50 Z830
        /// </summary>
        [Description("JS-50 Z8 (30°)")]
        JS50Z830 = ScanHeadType.JS50Z830,

        /// <summary>
        /// JS-50 Phaser
        /// </summary>
        [Description("JS-50 Phaser")]
        JS50Phaser = ScanHeadType.JS50PHASER,
    }
}

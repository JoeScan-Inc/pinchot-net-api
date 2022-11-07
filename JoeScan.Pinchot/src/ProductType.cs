// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using joescan.schema;

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
        Invalid = ScanHeadType.INVALID,

        /// <summary>
        /// JS-50 WX
        /// </summary>
        JS50WX = ScanHeadType.JS50WX,

        /// <summary>
        /// JS-50 WSC
        /// </summary>
        JS50WSC = ScanHeadType.JS50WSC,

        /// <summary>
        /// JS-50 X6B20
        /// </summary>
        JS50X6B20 = ScanHeadType.JS50X6B20,

        /// <summary>
        /// JS-50 X6B30
        /// </summary>
        JS50X6B30 = ScanHeadType.JS50X6B30,

        /// <summary>
        /// JS-50 MX
        /// </summary>
        JS50MX = ScanHeadType.JS50MX,
    }
}

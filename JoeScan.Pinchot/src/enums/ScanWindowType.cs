// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.ComponentModel;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enumeration for identifying a scan window type.
    /// </summary>
    public enum ScanWindowType
    {
        /// <summary>
        /// Invalid window.
        /// </summary>
        [Description("Invalid Window")]
        Invalid = 0,

        /// <summary>
        /// Unconstrained window.
        /// </summary>
        [Description("Unconstrained Window")]
        Unconstrained,

        /// <summary>
        /// Retangular window.
        /// </summary>
        [Description("Rectangular Window")]
        Rectangular,

        /// <summary>
        /// Polygonal window.
        /// </summary>
        [Description("Polygonal Window")]
        Polygonal
    }
}

// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.ComponentModel;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// The units that a given <see cref="ScanSystem"/> and all associated
    /// <see cref="ScanHead"/>s will use for configuration and returned data.
    /// </summary>
    public enum ScanSystemUnits
    {
        /// <summary>
        /// Invalid units.
        /// </summary>
        [Description("Invalid Units")]
        Invalid = 0,

        /// <summary>
        /// Imperial inches.
        /// </summary>
        [Description("\"")]
        Inches,

        /// <summary>
        /// Metric millimeters.
        /// </summary>
        [Description("mm")]
        Millimeters
    }
}

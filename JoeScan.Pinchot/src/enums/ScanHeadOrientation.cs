﻿// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.ComponentModel;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enumeration for identifying the orientation of a physical scan head.
    /// </summary>
    public enum ScanHeadOrientation
    {
        /// <summary>
        /// Indicates that the cable end of the scan head is oriented up-stream.
        /// </summary>
        [Description("Cable is Upstream")]
        CableIsUpstream,

        /// <summary>
        /// Indicates that the cable end of the scan head is oriented down-stream.
        /// </summary>
        [Description("Cable is Downstream")]
        CableIsDownstream
    }
}
// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.ComponentModel;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Enumeration for setting the scanning mode of the <see cref="ScanSystem"/>.
    /// </summary>
    public enum ScanningMode
    {
        /// <summary>
        /// Scanning is done on a per profile basis. Profiles should be read
        /// using <see cref="ScanHead.TakeNextProfile(System.Threading.CancellationToken)"/>,
        /// <see cref="ScanHead.TryTakeNextProfile(out IProfile, System.TimeSpan, System.Threading.CancellationToken)"/>,
        /// or <see cref="ScanHead.TryTakeProfiles(int, System.TimeSpan, System.Threading.CancellationToken)"/>.
        /// </summary>
        [Description("Profile scanning mode")]
        Profile,

        /// <summary>
        /// Scanning is done on a per frame basis. A frame contains a profile from all
        /// camera/laser pairs of all scan heads in the system.
        /// </summary>
        [Description("Frame scanning mode")]
        Frame
    }
}

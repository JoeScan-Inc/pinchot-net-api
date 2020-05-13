// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    internal enum ScanHeadVersionHardwareId : ushort
    {
        Invalid = 0,
        XU3 = 1,
        TE0820 = 2
    }

    internal enum ScanHeadVersionFlagMask : ushort
    {
        Dirty = 1 << 0,
        Develop = 1 << 1
    }

    /// <summary>
    /// Scan head firmware version information.
    /// </summary>
    public struct ScanHeadVersionInformation
    {
        /// <summary>
        /// Gets the major version component of the of the scan head firmware version.
        /// </summary>
        /// <value>The major version component of the of the scan head firmware version.</value>
        public uint Major { get; internal set; }

        /// <summary>
        /// Gets the minor version component of the of the scan head firmware version.
        /// </summary>
        /// <value>The minor version component of the of the scan head firmware version.</value>
        public uint Minor { get; internal set; }

        /// <summary>
        /// Gets the patch version component of the of the scan head firmware version.
        /// </summary>
        /// <value>The patch version component of the of the scan head firmware version.</value>
        public uint Patch { get; internal set; }

        internal uint Commit;
        internal ScanHeadVersionHardwareId HardwareId;
        internal ushort Flags;

        /// <summary>
        /// Gets the scan head firmware version.
        /// </summary>
        /// <value>The scan head firmware version.</value>
        public string Version =>
            Flags.Equals(0) ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{Flags}+{Commit:X}";
    }
}
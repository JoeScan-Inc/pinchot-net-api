// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    /// <summary>
    /// ScanSync firmware version information.
    /// </summary>
    public struct ScanSyncVersionInformation
    {
        /// <summary>
        /// Gets the major version component of the of the ScanSync firmware version.
        /// </summary>
        /// <value>The major version component of the of the ScanSync firmware version.</value>
        public uint Major { get; internal set; }

        /// <summary>
        /// Gets the minor version component of the of the ScanSync firmware version.
        /// </summary>
        /// <value>The minor version component of the of the ScanSync firmware version.</value>
        public uint Minor { get; internal set; }

        /// <summary>
        /// Gets the patch version component of the of the ScanSync firmware version.
        /// </summary>
        /// <value>The patch version component of the of the ScanSync firmware version.</value>
        public uint Patch { get; internal set; }

        /// <summary>
        /// Gets the ScanSync firmware version in 'Major.Minor.Patch' format.
        /// </summary>
        /// <returns>The ScanSync firmware version in 'Major.Minor.Patch' format.</returns>
        public override string ToString()
        {
            return $"{Major}.{Minor}.{Patch}";
        }
    }
}

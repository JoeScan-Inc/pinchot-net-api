// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// This exception is thrown when the scan head is not compatible with the operation being performed.
    /// </summary>
    /// <remarks>
    /// One example of this exception being thrown is when the API attempts to connect to a
    /// <see cref="ScanHead"/> with an incompatible version. Only scan heads loaded with
    /// firmware with the same major version as the API are compatible. <br/>
    /// Versions are defined as: <code>Major.Minor.Patch</code>
    /// </remarks>
    /// <example>
    /// A scan head with a version of <c>13.1.1</c> is compatible with an API with a version of
    /// <c>13.4.2</c> because the major version (<c>13</c>) is the same.
    /// </example>
    public class VersionCompatibilityException : InvalidOperationException
    {
        internal static string GetErrorReason(ScanHeadVersionInformation version)
        {
            string scanHeadVersion = $"{version.Major}.{version.Minor}.{version.Patch}";
            string apiVersion = $"{VersionInformation.Major}.{VersionInformation.Minor}.{VersionInformation.Patch}";
            return $"Scan head version {scanHeadVersion} is not compatible with API version {apiVersion}";
        }

        /// <summary>
        /// Set the exception message using the scan head version information supplied
        /// on <paramref name="version"/>.
        /// </summary>
        /// <param name="version">The incompatible scan head version.</param>
        internal VersionCompatibilityException(ScanHeadVersionInformation version)
            : base(GetErrorReason(version))
        {
        }

        /// <summary>
        /// Set a custom message for this exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        internal VersionCompatibilityException(string message)
            :base(message)
        {
        }
    }
}
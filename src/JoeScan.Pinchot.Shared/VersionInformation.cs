// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Linq;
using System.Reflection;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// API version information.
    /// </summary>
    public static class VersionInformation
    {
        /// <summary>
        /// Gets the version of Pinchot.
        /// </summary>
        /// <value>The version of Pinchot.</value>
        public static string Version
        {
            get
            {
                var thisAssembly = typeof(VersionInformation).Assembly;
                var version = thisAssembly.GetName().Version;

                var hashAttribute = (AssemblyInformationalVersionAttribute)thisAssembly.GetCustomAttributes(
                    typeof(AssemblyInformationalVersionAttribute),
                    false).FirstOrDefault();

                if (hashAttribute is null)
                {
                    // Should never be here...
                    return version.ToString();
                }

                string[] splits = hashAttribute.InformationalVersion.Split('-');
                if (splits.Length > 1)
                {
                    // If there is pre-release data (denoted by a hyphen after the patch number)
                    // return complete informational version.
                    return hashAttribute.InformationalVersion;
                }
                else
                {
                    // If there is no pre-release data, this is an official release, so only
                    // return the major, minor and patch information.
                    splits = hashAttribute.InformationalVersion.Split('+');
                    return splits[0];
                }
            }
        }

        /// <summary>
        /// Gets the major version component of the of the Pinchot version.
        /// </summary>
        /// <value>The major version component of the of the Pinchot version.</value>
        public static int Major => Assembly.GetExecutingAssembly().GetName().Version.Major;

        /// <summary>
        /// Gets the minor version component of the of the Pinchot version.
        /// </summary>
        /// <value>The minor version component of the of the Pinchot version.</value>
        public static int Minor => Assembly.GetExecutingAssembly().GetName().Version.Minor;

        /// <summary>
        /// Gets the patch version component of the of the Pinchot version.
        /// </summary>
        /// <value>The patch version component of the of the Pinchot version.</value>
        public static int Patch => Assembly.GetExecutingAssembly().GetName().Version.Build;
    }
}
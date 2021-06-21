// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// A scan window within which valid <see cref="Profile"/> <see cref="Point2D"/>s are to be measured.
    /// </summary>
    /// <remarks>
    /// <see cref="ScanWindow"/> class provides methods for creating <see cref="ScanWindow"/> objects. <see cref="ScanWindow"/>s
    /// are used to define the area, in mill coordinates, within which valid <see cref="Profile"/> <see cref="Point2D"/>s are to
    /// be measured. <see cref="ScanWindow"/>s can be used to restrict the scan area only to the region of interest, eliminate
    /// interference from ambient or stray light, eliminate erroneous or unnecessary data from machinery, etc. Decreasing the
    /// scan window in the scan head's y axis (depth) will allow for faster scan rates. Use <see cref="ScanSystem.GetMaxScanRate"/>
    /// to retrieve the maximum scan rate of the entire scan system based on the currently applied scan windows of all scan heads.
    /// </remarks>
    public class ScanWindow : ICloneable
    {
        [JsonProperty(nameof(WindowConstraints))]
        internal readonly IList<WindowConstraint> WindowConstraints = new List<WindowConstraint>();

        internal ScanWindow()
        {
        }

        /// <summary>
        /// Creates a rectangular <see cref="ScanWindow"/>, in mill coordinates, within which a camera will look for the laser.
        /// </summary>
        /// <param name="windowTop">The top boundary of the <see cref="ScanWindow"/> in inches.
        /// Must be greater than <paramref name="windowBottom"/>.</param>
        /// <param name="windowBottom">The bottom boundary of the <see cref="ScanWindow"/> in inches.
        /// Must be less than <paramref name="windowTop"/>.</param>
        /// <param name="windowLeft">The left boundary of the <see cref="ScanWindow"/> in inches.
        /// Must be less than <paramref name="windowRight"/>.</param>
        /// <param name="windowRight">The right boundary of the <see cref="ScanWindow"/> in inches.
        /// Must be greater than <paramref name="windowLeft"/>.</param>
        /// <returns>The created <see cref="ScanWindow"/>.</returns>
        /// <seealso cref="ScanHead.SetWindow(ScanWindow)"/>
        /// <exception cref="ArgumentException">
        /// One or more arguments are <see cref="double.NaN"/><br/>
        /// -or-<br/>
        /// One or more arguments are <see cref="double.NegativeInfinity"/> or <see cref="double.PositiveInfinity"/><br/>
        /// -or-<br/>
        /// <paramref name="windowTop"/> is less than or equal to <paramref name="windowBottom"/><br/>
        /// -or-<br/>
        /// <paramref name="windowRight"/> is less than or equal to <paramref name="windowLeft"/>
        /// </exception>
        public static ScanWindow CreateScanWindowRectangular(double windowTop, double windowBottom, double windowLeft,
            double windowRight)
        {
            if (double.IsNaN(windowTop) ||
                double.IsNaN(windowBottom) ||
                double.IsNaN(windowLeft) ||
                double.IsNaN(windowRight))
            {
                throw new ArgumentException("One or more arguments are Double.NaN.");
            }

            if (double.IsInfinity(windowTop) ||
                double.IsInfinity(windowBottom) ||
                double.IsInfinity(windowLeft) ||
                double.IsInfinity(windowRight))
            {
                throw new ArgumentException(
                    "One or more arguments are Double.NegativeInfinity or Double.PositiveInfinity.");
            }

            if (windowTop <= windowBottom)
            {
                throw new ArgumentException($"{nameof(windowTop)} must be greater than {nameof(windowBottom)}");
            }

            if (windowRight <= windowLeft)
            {
                throw new ArgumentException($"{nameof(windowRight)} must be greater than {nameof(windowLeft)}");
            }

            var scanWindow = new ScanWindow();
            scanWindow.WindowConstraints.Add(new WindowConstraint(windowLeft, windowTop, windowRight, windowTop));
            scanWindow.WindowConstraints.Add(new WindowConstraint(windowRight, windowBottom, windowLeft, windowBottom));
            scanWindow.WindowConstraints.Add(new WindowConstraint(windowRight, windowTop, windowRight, windowBottom));
            scanWindow.WindowConstraints.Add(new WindowConstraint(windowLeft, windowBottom, windowLeft, windowTop));
            return scanWindow;
        }

        /// <summary>
        /// Creates an unconstrained <see cref="ScanWindow"/> within which a camera will look for the laser.
        /// </summary>
        /// <returns>The created <see cref="ScanWindow"/>.</returns>
        /// <seealso cref="ScanHead.SetWindow(ScanWindow)"/>
        public static ScanWindow CreateScanWindowUnconstrained()
        {
            return new ScanWindow();
        }

        /// <summary>
        /// <see cref="ScanWindow"/> implements <see cref="ICloneable"/>, which is used
        /// to make sure that once a <see cref="ScanWindow"/> is set on a <see cref="ScanHead"/>
        /// via <see cref="ScanHead.SetWindow(ScanWindow)"/>, it will not change if the original is changed.
        /// </summary>
        /// <returns>A shallow copy of this <see cref="ScanWindow"/> object.</returns>
        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
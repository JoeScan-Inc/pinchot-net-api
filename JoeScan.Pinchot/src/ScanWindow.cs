// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// A scan window within which valid <see cref="IProfile"/> <see cref="Point2D"/>s are to be measured.
    /// </summary>
    /// <remarks>
    /// This class provides methods for creating scan window objects. Scan windows are used to define the area, in mill
    /// coordinates, within which valid <see cref="IProfile"/> <see cref="Point2D"/>s are to be measured. Scan windows
    /// can be used to restrict the scan area only to the region of interest, eliminate interference from ambient or
    /// stray light, and eliminate erroneous or unnecessary data from machinery. Decreasing the scan window in the scan
    /// head's y axis (depth) will allow for faster scanning. Use <see cref="ScanSystem.GetMinScanPeriod"/> to retrieve
    /// the minimum scan period of the entire scan system based on the currently applied scan windows of all scan heads.
    /// </remarks>
    public sealed class ScanWindow : ICloneable
    {
        [JsonProperty(nameof(WindowConstraints))]
        internal IList<ScanWindowConstraint> WindowConstraints = new List<ScanWindowConstraint>();

        private ScanWindow()
        {
        }

        /// <summary>
        /// Creates a rectangular scan window, in mill coordinates, within which a camera will look for the laser.
        /// </summary>
        /// <param name="windowTop">The top boundary of the <see cref="ScanWindow"/> in <see cref="ScanSystemUnits"/>.
        /// Must be greater than <paramref name="windowBottom"/>.</param>
        /// <param name="windowBottom">The bottom boundary of the <see cref="ScanWindow"/> in <see cref="ScanSystemUnits"/>.
        /// Must be less than <paramref name="windowTop"/>.</param>
        /// <param name="windowLeft">The left boundary of the <see cref="ScanWindow"/> in <see cref="ScanSystemUnits"/>.
        /// Must be less than <paramref name="windowRight"/>.</param>
        /// <param name="windowRight">The right boundary of the <see cref="ScanWindow"/> in <see cref="ScanSystemUnits"/>.
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
            scanWindow.WindowConstraints.Add(new ScanWindowConstraint(windowLeft, windowTop, windowRight, windowTop));
            scanWindow.WindowConstraints.Add(new ScanWindowConstraint(windowRight, windowBottom, windowLeft, windowBottom));
            scanWindow.WindowConstraints.Add(new ScanWindowConstraint(windowRight, windowTop, windowRight, windowBottom));
            scanWindow.WindowConstraints.Add(new ScanWindowConstraint(windowLeft, windowBottom, windowLeft, windowTop));
            return scanWindow;
        }

        /// <summary>
        /// Creates an unconstrained scan window within which a camera will look for the laser.
        /// </summary>
        /// <returns>The created <see cref="ScanWindow"/>.</returns>
        /// <seealso cref="ScanHead.SetWindow(ScanWindow)"/>
        public static ScanWindow CreateScanWindowUnconstrained()
        {
            return new ScanWindow();
        }

        /// <summary>
        /// Creates a polygonal scan window using the points passed in. The points must
        /// be ordered in a clockwise fashion and the resulting shape must be convex.
        /// The first and last points will be automatically connected so there is no
        /// need to duplicate the first point at the end of <paramref name="points"/>.
        /// </summary>
        /// <param name="points">The points that make up the polgonal window.</param>
        /// <returns>The scan window.</returns>
        /// <exception cref="ArgumentException">
        /// One or more arguments are <see cref="double.NaN"/><br/>
        /// -or-<br/>
        /// One or more arguments are <see cref="double.NegativeInfinity"/> or <see cref="double.PositiveInfinity"/><br/>
        /// -or-<br/>
        /// There are fewer than 3 points in <paramref name="points"/><br/>
        /// -or-<br/>
        /// The polygon is not convex.<br/>
        /// -or-<br/>
        /// The points were not supplied in clockwise order.
        /// </exception>
        public static ScanWindow CreateScanWindowPolygonal(ICollection<Point2D> points)
        {
            if (points.Any(p => double.IsNaN(p.X) || double.IsNaN(p.Y)))
            {
                throw new ArgumentException("One or more points contain NaN.");
            }

            if (points.Any(p => double.IsInfinity(p.X) || double.IsInfinity(p.Y)))
            {
                throw new ArgumentException("One or more points contain infinity.");
            }

            if (points.Count < 3)
            {
                throw new ArgumentException("Cannot create polygonal window with fewer than 3 points.");
            }

            var checkPoints = new List<Point2D>(points);

            // check for clockwise point ordering - https://stackoverflow.com/a/18472899
            double sum = 0.0;
            var p1 = checkPoints.Last();
            for (int i = 0; i < checkPoints.Count; i++)
            {
                var p2 = checkPoints[i];
                sum += (p2.X - p1.X) * (p2.Y + p1.Y);
                p1 = p2;
            }

            // polygon is clockwise if sum is greater than zero
            if (sum <= 0.0)
            {
                throw new ArgumentException("Polygon points not supplied in clockwise order.");
            }

            // check for convexity - https://stackoverflow.com/a/1881201
            // add the first two points to the end of the list
            // to make loop cleaner since we have to calculate
            // (p[N-2],p[N-1],p[0]) and (p[N-1],p[0],p[1]).
            checkPoints.Add(checkPoints[0]);
            checkPoints.Add(checkPoints[1]);

            var crossProducts = new List<double>();
            for (int i = 0; i < points.Count; ++i)
            {
                var pts = checkPoints.GetRange(i, 3);
                double dx1 = pts[1].X - pts[0].X;
                double dy1 = pts[1].Y - pts[0].Y;
                double dx2 = pts[2].X - pts[1].X;
                double dy2 = pts[2].Y - pts[1].Y;
                crossProducts.Add((dx1 * dy2) - (dy1 * dx2));
            }

            // polygon is convex if the sign of all cross products is the same
            if (!crossProducts.All(c => c < 0) && !crossProducts.All(c => c > 0))
            {
                throw new ArgumentException("Polygon isn't convex.");
            }

            var scanWindow = new ScanWindow();
            var previousPoint = points.First();
            foreach (var point in points.Skip(1))
            {
                scanWindow.WindowConstraints.Add(new ScanWindowConstraint(previousPoint.X, previousPoint.Y, point.X, point.Y));
                previousPoint = point;
            }

            // connect the first and last points
            var firstPoint = points.First();
            scanWindow.WindowConstraints.Add(new ScanWindowConstraint(previousPoint.X, previousPoint.Y, firstPoint.X, firstPoint.Y));

            return scanWindow;
        }

        /// <summary>
        /// Used to make sure that once a scan window is set on a <see cref="ScanHead"/>
        /// via <see cref="ScanHead.SetWindow(ScanWindow)"/>, it will not change if the original is changed.
        /// </summary>
        /// <returns>A shallow copy of this <see cref="ScanWindow"/> object.</returns>
        public object Clone()
        {
            var window = MemberwiseClone() as ScanWindow;
            window.WindowConstraints = new List<ScanWindowConstraint>(WindowConstraints);
            return window;
        }
    }
}
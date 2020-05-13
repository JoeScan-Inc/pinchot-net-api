// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Diagnostics;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// A data point consisting of X and Y spatial coordinates and a brightness value.
    /// </summary>
    [DebuggerDisplay("X: {X} Y: {Y} Brightness: {Brightness}")]
#pragma warning disable CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
    public struct Point2D : IEquatable<Point2D>
#pragma warning restore CS0659 // Type overrides Object.Equals(object o) but does not override Object.GetHashCode()
#pragma warning restore CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()
    {
        #region Public Properties

        /// <summary>
        /// Gets or sets the X coordinate value of the <see cref="Point2D"/> in inches.
        /// </summary>
        /// <value>The X coordinate value of the <see cref="Point2D"/> in inches.</value>
        public double X { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate value of the <see cref="Point2D"/> in inches.
        /// </summary>
        /// <value>The Y coordinate value of the <see cref="Point2D"/> in inches.</value>
        public double Y { get; set; }

        /// <summary>
        /// Gets or sets the brightness value of the <see cref="Point2D"/>.
        /// </summary>
        /// <value>The brightness value of the <see cref="Point2D"/>.</value>
        public int Brightness { get; set; }

        #endregion

        #region Lifecycle

        internal Point2D(float x, float y, int brightness)
        {
            X = x;
            Y = y;
            Brightness = brightness;
        }

        internal Point2D(double x, double y, int brightness)
        {
            X = x;
            Y = y;
            Brightness = brightness;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Compares two <see cref="Point2D"/> objects. The result specifies whether the two <see cref="Point2D"/> objects have
        /// coordinates within 1/10,000th of an inch and have the same brightness.
        /// </summary>
        /// <param name="pointA">A <see cref="Point2D"/> to compare.</param>
        /// <param name="pointB">A <see cref="Point2D"/> to compare.</param>
        /// <returns>`true` if the two <see cref="Point2D"/> objects have coordinates within 1/10,000th of an inch
        /// and have the same brightness.</returns>
        public static bool operator ==(Point2D pointA, Point2D pointB)
        {
            return pointA.Equals(pointB);
        }

        /// <summary>
        /// Compares two <see cref="Point2D"/> objects. The result specifies whether the two <see cref="Point2D"/> objects have
        /// coordinates that are not within 1/10,000th of an inch or do not have the same brightness.
        /// </summary>
        /// <param name="pointA">A <see cref="Point2D"/> to compare.</param>
        /// <param name="pointB">A <see cref="Point2D"/> to compare.</param>
        /// <returns>`true` if the two <see cref="Point2D"/> objects have coordinates that are not within 1/10,000th
        /// of an inch or do not have the same brightness.</returns>
        public static bool operator !=(Point2D pointA, Point2D pointB)
        {
            return !pointA.Equals(pointB);
        }

        /// <summary>
        /// Specifies whether this <see cref="Point2D"/> instance contains the same coordinates within 1/10,000th of an inch
        /// and the same brightness as the specified <see cref="object"/>.
        /// </summary>
        /// <param name="point">The <see cref="object"/> to test for equality.</param>
        /// <returns>`true` if <paramref name="point"/> is a <see cref="Point2D"/> and has the same coordinates
        /// within 1/10,000th of an inch and the same brightness as this <see cref="Point2D"/> instance.</returns>
        public override bool Equals(object point)
        {
            if (!(point is Point2D)) return false;

            return Equals((Point2D)point);
        }

        /// <summary>
        /// Specifies whether this <see cref="Point2D"/> instance contains the same coordinates within 1/10,000th of an inch
        /// and the same brightness as the specified <see cref="Point2D"/>.
        /// </summary>
        /// <param name="point">The <see cref="Point2D"/> to test for equality.</param>
        /// <returns>`true` if <paramref name="point"/> has the same coordinates within 1/10,000th of an inch
        /// and the same brightness as this <see cref="Point2D"/> instance..</returns>
        public bool Equals(Point2D point)
        {
            if (Brightness != point.Brightness)
            {
                return false;
            }

            if (Math.Abs(X - point.X) > 1E-4)
            {
                return false;
            }

            if (Math.Abs(Y - point.Y) > 1E-4)
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Internal Methods

        internal double Distance(Point2D p2)
        {
            return Math.Sqrt((X - p2.X) * (X - p2.X) + (Y - p2.Y) * (Y - p2.Y));
        }

        internal double Distance(double x, double y)
        {
            return Math.Sqrt((X - x) * (X - x) + (Y - y) * (Y - y));
        }

        internal double DistanceSq(double x, double y)
        {
            return (X - x) * (X - x) + (Y - y) * (Y - y);
        }

        internal double DistanceSq(Point2D p2)
        {
            return (X - p2.X) * (X - p2.X) + (Y - p2.Y) * (Y - p2.Y);
        }

        #endregion
    }
}
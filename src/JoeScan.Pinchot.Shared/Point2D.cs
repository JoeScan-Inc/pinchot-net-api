// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// A data point consisting of X and Y spatial coordinates and a brightness value.
    /// </summary>
    [DebuggerDisplay("X: {X} Y: {Y} Brightness: {Brightness}")]
    public struct Point2D : IEquatable<Point2D>
    {
        #region Public Properties

        /// <summary>
        /// Gets or sets the X coordinate value of the <see cref="Point2D"/> in inches.
        /// </summary>
        /// <value>The X coordinate value of the <see cref="Point2D"/> in inches.</value>
        public double X { get; set; } = double.NaN;

        /// <summary>
        /// Gets or sets the Y coordinate value of the <see cref="Point2D"/> in inches.
        /// </summary>
        /// <value>The Y coordinate value of the <see cref="Point2D"/> in inches.</value>
        public double Y { get; set; } = double.NaN;

        /// <summary>
        /// Gets or sets the brightness value of the <see cref="Point2D"/>.
        /// </summary>
        /// <value>The brightness value of the <see cref="Point2D"/>.</value>
        public int Brightness { get; set; } = Globals.ProfileDataInvalidBrightness;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Initializes a new instance of the <see cref="Point2D"/> struct.
        /// </summary>
        public Point2D(float x, float y, int brightness)
        {
            X = x;
            Y = y;
            Brightness = brightness;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Point2D"/> struct.
        /// </summary>
        public Point2D(double x, double y, int brightness)
        {
            X = x;
            Y = y;
            Brightness = brightness;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Check if this point is valid
        /// </summary>
        public bool IsValid() => !double.IsNaN(Y);

        /// <summary>
        /// Compares two <see cref="Point2D"/> objects. The result specifies whether the two <see cref="Point2D"/> objects have
        /// coordinates within 1/10,000th of an inch and have the same brightness.
        /// </summary>
        /// <param name="pointA">A <see cref="Point2D"/> to compare.</param>
        /// <param name="pointB">A <see cref="Point2D"/> to compare.</param>
        /// <returns>`true` if the two <see cref="Point2D"/> objects have coordinates within 1/10,000th of an inch
        /// and have the same brightness.</returns>
        public static bool operator ==(Point2D pointA, Point2D pointB) => pointA.Equals(pointB);

        /// <summary>
        /// Compares two <see cref="Point2D"/> objects. The result specifies whether the two <see cref="Point2D"/> objects have
        /// coordinates that are not within 1/10,000th of an inch or do not have the same brightness.
        /// </summary>
        /// <param name="pointA">A <see cref="Point2D"/> to compare.</param>
        /// <param name="pointB">A <see cref="Point2D"/> to compare.</param>
        /// <returns>`true` if the two <see cref="Point2D"/> objects have coordinates that are not within 1/10,000th
        /// of an inch or do not have the same brightness.</returns>
        public static bool operator !=(Point2D pointA, Point2D pointB) => !pointA.Equals(pointB);

        /// <summary>
        /// Specifies whether this <see cref="Point2D"/> instance contains the same coordinates within 1/10,000th of an inch
        /// and the same brightness as the specified <see cref="object"/>.
        /// </summary>
        /// <param name="point">The <see cref="object"/> to test for equality.</param>
        /// <returns>`true` if <paramref name="point"/> is a <see cref="Point2D"/> and has the same coordinates
        /// within 1/10,000th of an inch and the same brightness as this <see cref="Point2D"/> instance.</returns>
        public override bool Equals(object point)
        {
            if (!(point is Point2D))
            {
                return false;
            }

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
            return Math.Sqrt(((X - p2.X) * (X - p2.X)) + ((Y - p2.Y) * (Y - p2.Y)));
        }

        internal double Distance(double x, double y)
        {
            return Math.Sqrt(((X - x) * (X - x)) + ((Y - y) * (Y - y)));
        }

        internal double DistanceSq(double x, double y)
        {
            return ((X - x) * (X - x)) + ((Y - y) * (Y - y));
        }

        internal double DistanceSq(Point2D p2)
        {
            return ((X - p2.X) * (X - p2.X)) + ((Y - p2.Y) * (Y - p2.Y));
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 23) + X.GetHashCode();
                hash = (hash * 23) + Y.GetHashCode();
                hash = (hash * 23) + Brightness.GetHashCode();
                return hash;
            }
        }

        #endregion
    }
}
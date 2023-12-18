// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System;
using System.Diagnostics;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// A data point consisting of X and Y spatial coordinates and a brightness value.
    /// Invalid points have an X and Y value of <see cref="Globals.ProfileDataInvalidXY"/>
    /// and a brightness of <see cref="Globals.ProfileDataInvalidBrightness"/>.
    /// </summary>
    [DebuggerDisplay("X: {X} Y: {Y} Brightness: {Brightness}")]
    public struct Point2D : IEquatable<Point2D>
    {
        #region Public Properties

        /// <summary>
        /// Gets or sets the X coordinate value in <see cref="ScanSystemUnits"/>.
        /// </summary>
        /// <value>The X coordinate value in <see cref="ScanSystemUnits"/>.</value>
        public float X { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate value in <see cref="ScanSystemUnits"/>.
        /// </summary>
        /// <value>The Y coordinate value in <see cref="ScanSystemUnits"/>.</value>
        public float Y { get; set; }

        // NOTE: If we ever serialize profile data as JSON then the [JsonIgnore] will have to go.
        // This was added since window constraints use this struct and serializing a scan head in
        // SensorTester added a bunch of junk brightness fields. This will be solved when a new
        // data type is created for either the constraints or the profile data.
        /// <summary>
        /// Gets or sets the brightness value.
        /// </summary>
        /// <value>The brightness value.</value>
        [JsonIgnore]
        public int Brightness { get; set; }

        /// <summary>
        /// Checks if the point holds valid data.
        /// </summary>
        /// <value><see langword="true"/> if point is valid else <see langword="false"/>.</value>
        [JsonIgnore]
        public bool IsValid => !float.IsNaN(Y);

        #endregion

        #region Lifecycle

        /// <summary>
        /// Initializes a new instance of the <see cref="Point2D"/> struct with
        /// <see cref="Brightness"/> being set to <see cref="Globals.ProfileDataInvalidBrightness"/>.
        /// </summary>
        /// <param name="x">The X value in <see cref="ScanSystemUnits"/>.</param>
        /// <param name="y">The Y value in <see cref="ScanSystemUnits"/>.</param>
        public Point2D(float x, float y)
        {
            X = x;
            Y = y;
            Brightness = Globals.ProfileDataInvalidBrightness;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Point2D"/> struct with
        /// <see cref="Brightness"/> being set to <see cref="Globals.ProfileDataInvalidBrightness"/>.
        /// </summary>
        /// <param name="x">The X value in <see cref="ScanSystemUnits"/>.</param>
        /// <param name="y">The Y value in <see cref="ScanSystemUnits"/>.</param>
        public Point2D(double x, double y)
        {
            X = (float)x;
            Y = (float)y;
            Brightness = Globals.ProfileDataInvalidBrightness;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Point2D"/> struct.
        /// </summary>
        /// <param name="x">The X value in <see cref="ScanSystemUnits"/>.</param>
        /// <param name="y">The Y value in <see cref="ScanSystemUnits"/>.</param>
        /// <param name="brightness">The brightness value.</param>
        public Point2D(float x, float y, int brightness)
        {
            X = x;
            Y = y;
            Brightness = brightness;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Point2D"/> struct.
        /// </summary>
        /// <param name="x">The X value in <see cref="ScanSystemUnits"/>.</param>
        /// <param name="y">The Y value in <see cref="ScanSystemUnits"/>.</param>
        /// <param name="brightness">The brightness value.</param>
        public Point2D(double x, double y, int brightness)
        {
            X = (float)x;
            Y = (float)y;
            Brightness = brightness;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Compares two <see cref="Point2D"/> objects. The result specifies whether the two <see cref="Point2D"/> objects have
        /// coordinates within 1/10,000th of an <see cref="ScanSystemUnits"/> and have the same brightness.
        /// </summary>
        /// <param name="pointA">A <see cref="Point2D"/> to compare.</param>
        /// <param name="pointB">A <see cref="Point2D"/> to compare.</param>
        /// <returns>`true` if the two <see cref="Point2D"/> objects have coordinates within 1/10,000th of an <see cref="ScanSystemUnits"/>
        /// and have the same brightness.</returns>
        public static bool operator ==(Point2D pointA, Point2D pointB) => pointA.Equals(pointB);

        /// <summary>
        /// Compares two <see cref="Point2D"/> objects. The result specifies whether the two <see cref="Point2D"/> objects have
        /// coordinates that are not within 1/10,000th of an <see cref="ScanSystemUnits"/> or do not have the same brightness.
        /// </summary>
        /// <param name="pointA">A <see cref="Point2D"/> to compare.</param>
        /// <param name="pointB">A <see cref="Point2D"/> to compare.</param>
        /// <returns>`true` if the two <see cref="Point2D"/> objects have coordinates that are not within 1/10,000th
        /// of an <see cref="ScanSystemUnits"/> or do not have the same brightness.</returns>
        public static bool operator !=(Point2D pointA, Point2D pointB) => !pointA.Equals(pointB);

        /// <summary>
        /// Specifies whether this <see cref="Point2D"/> instance contains the same coordinates within 1/10,000th of an <see cref="ScanSystemUnits"/>
        /// and the same brightness as the specified <see cref="object"/>.
        /// </summary>
        /// <param name="point">The <see cref="object"/> to test for equality.</param>
        /// <returns>`true` if <paramref name="point"/> is a <see cref="Point2D"/> and has the same coordinates
        /// within 1/10,000th of an <see cref="ScanSystemUnits"/> and the same brightness as this <see cref="Point2D"/> instance.</returns>
        public override bool Equals(object point)
        {
            if (!(point is Point2D))
            {
                return false;
            }

            return Equals((Point2D)point);
        }

        /// <summary>
        /// Specifies whether this <see cref="Point2D"/> instance contains the same coordinates within 1/10,000th of an <see cref="ScanSystemUnits"/>
        /// and the same brightness as the specified <see cref="Point2D"/>.
        /// </summary>
        /// <param name="point">The <see cref="Point2D"/> to test for equality.</param>
        /// <returns>`true` if <paramref name="point"/> has the same coordinates within 1/10,000th of an <see cref="ScanSystemUnits"/>
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
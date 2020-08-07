// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Spatial transformation parameters.
    /// </summary>
    internal class AlignmentParameters
    {
        private double roll;
        private double yaw;
        private ScanHeadOrientation orientation;
        private const double Rho = Math.PI / 180.0;

        /// <summary>
        /// Gets a value indicating the orientation of the scan head. Read-only.
        /// Set during initialization of a new instance of a <see cref="AlignmentParameters"/> object.
        /// </summary>
        [JsonProperty(nameof(Orientation))]
        internal ScanHeadOrientation Orientation
        {
            get => orientation;
            private set
            {
                orientation = value;
                switch (orientation)
                {
                    case ScanHeadOrientation.CableIsUpstream:
                        yaw = 0;
                        break;
                    case ScanHeadOrientation.CableIsDownstream:
                        yaw = 180;
                        break;
                    default:
                        yaw = 0;
                        break;
                }

                CosYaw = Math.Cos(yaw * Rho);
            }
        }

        /// <summary>
        /// Gets the rotation around the Z axis in the mill coordinate system.
        /// Set during initialization of a new instance of a <see cref="AlignmentParameters"/> object.
        /// </summary>
        [JsonProperty(nameof(Roll))]
        internal double Roll
        {
            get => roll;
            private set
            {
                roll = value;
                SinRoll = Math.Sin(roll * Rho);
                CosRoll = Math.Cos(roll * Rho);
            }
        }

        /// <summary>
        /// Get the translation along the X axis in the mill coordinate system in inches.
        /// Set during initialization of a new instance of a <see cref="AlignmentParameters"/> object.
        /// </summary>
        [JsonProperty(nameof(ShiftX))]
        internal double ShiftX { get; private set; }

        /// <summary>
        /// Gets the translation along the Y axis in the mill coordinate system in inches.
        /// Set during initialization of a new instance of a <see cref="AlignmentParameters"/> object.
        /// </summary>
        [JsonProperty(nameof(ShiftY))]
        internal double ShiftY { get; private set; }

        internal double SinRoll { get; private set; }

        internal double CosRoll { get; private set; }

        internal double CosYaw { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AlignmentParameters"/> class.
        /// </summary>
        /// <param name="alignmentParameters">The p.</param>
        internal AlignmentParameters(AlignmentParameters alignmentParameters)
        {
            Roll = alignmentParameters.Roll;
            Orientation = alignmentParameters.Orientation;
            ShiftX = alignmentParameters.ShiftX;
            ShiftY = alignmentParameters.ShiftY;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AlignmentParameters"/> class.
        /// </summary>
        /// <param name="roll">The rotation around the Z axis in the mill coordinate system in degrees.</param>
        /// <param name="shiftX">The shift along the X axis in the mill coordinate system in inches.</param>
        /// <param name="shiftY">The shift along the Y axis in the mill coordinate system in inches.</param>
        /// <param name="orientation">The <see cref="ScanHeadOrientation"/>.</param>
        /// <exception cref="ArgumentException">
        /// One or more arguments are <see cref="Double.NaN"/><br/>
        /// -or-<br/>
        /// One or more arguments are <see cref="Double.NegativeInfinity"/> or <see cref="Double.PositiveInfinity"/>
        /// </exception>
        internal AlignmentParameters(double roll, double shiftX, double shiftY, ScanHeadOrientation orientation)
        {
            if (Double.IsNaN(roll) || Double.IsNaN(shiftX) || Double.IsNaN(shiftY))
            {
                throw new ArgumentException("One or more arguments are Double.NaN.");
            }

            if (Double.IsInfinity(roll) || Double.IsInfinity(shiftX) || Double.IsInfinity(shiftY))
            {
                throw new ArgumentException(
                    "One or more arguments are Double.NegativeInfinity or Double.PositiveInfinity.");
            }

            Roll = roll;
            ShiftX = shiftX;
            ShiftY = shiftY;
            Orientation = orientation;
        }

        [JsonConstructor]
        internal AlignmentParameters()
        {
        }

        /// <summary>
        /// Transform a point from its camera coordinate system to the mill coordinate system.
        /// </summary>
        /// <param name="point">A Point2D point.</param>
        /// <returns>The transformed point.</returns>
        internal Point2D CameraToMill(Point2D point)
        {
            return CameraToMill(point.X, point.Y, point.Brightness);
        }

        /// <summary>
        /// Transform a point from its camera coordinate system to the mill coordinate system.
        /// </summary>
        /// <param name="x">The point's X value.</param>
        /// <param name="y">The point's Y value.</param>
        /// <param name="brightness">The point's brightness value.</param>
        /// <returns>The transformed point.</returns>
        internal Point2D CameraToMill(double x, double y, int brightness)
        {
            return new Point2D(
                x * CosYaw * CosRoll - y * SinRoll + ShiftX,
                x * CosYaw * SinRoll + y * CosRoll + ShiftY,
                brightness);
        }

        /// <summary>
        /// Transform a point from the mill coordinate system to the point's camera coordinate system.
        /// </summary>
        /// <param name="x">The point's X value.</param>
        /// <param name="y">The point's Y value.</param>
        /// <param name="brightness">The point's brightness value.</param>
        /// <returns>The transformed point.</returns>
        internal Point2D MillToCamera(double x, double y, int brightness)
        {
            var cosNegRoll = Math.Cos(-Roll * Rho);
            var sinNegRoll = Math.Sin(-Roll * Rho);
            var cosNegYaw = Math.Cos(-yaw * Rho);
            return new Point2D(
                (x - ShiftX) * cosNegYaw * cosNegRoll - (y - ShiftY) * cosNegYaw * sinNegRoll,
                (x - ShiftX) * sinNegRoll + (y - ShiftY) * cosNegRoll,
                brightness);
        }

        internal Point2D MillToCamera(Point2D point)
        {
            return MillToCamera(point.X, point.Y, point.Brightness);
        }
    }
}
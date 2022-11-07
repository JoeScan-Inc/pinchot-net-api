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
        private ScanHeadOrientation orientation;
        private double roll;
        private double cameraToMillScale;

        /// <summary>
        /// Gets a value indicating the orientation of the scan head.
        /// Set during initialization of a new instance of a <see cref="AlignmentParameters"/> object.
        /// </summary>
        [JsonProperty(nameof(Orientation))]
        internal ScanHeadOrientation Orientation
        {
            get => orientation;
            set
            {
                orientation = value;
                CalculateTransform();
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
            set
            {
                roll = value;
                CalculateTransform();
            }
        }

        /// <summary>
        /// Get the translation along the X axis in the mill coordinate system in <see cref="ScanSystemUnits"/>.
        /// Set during initialization of a new instance of a <see cref="AlignmentParameters"/> object.
        /// </summary>
        [JsonProperty(nameof(ShiftX))]
        internal double ShiftX { get; }

        /// <summary>
        /// Gets the translation along the Y axis in the mill coordinate system in <see cref="ScanSystemUnits"/>.
        /// Set during initialization of a new instance of a <see cref="AlignmentParameters"/> object.
        /// </summary>
        [JsonProperty(nameof(ShiftY))]
        internal double ShiftY { get; }

        [JsonProperty(nameof(CameraToMillScale))]
        internal double CameraToMillScale
        {
            get => cameraToMillScale;
            set
            {
                cameraToMillScale = value;
                CalculateTransform();
            }
        }

        internal double CameraToMillXX { get; set; }
        internal double CameraToMillXY { get; set; }
        internal double CameraToMillYX { get; set; }
        internal double CameraToMillYY { get; set; }
        internal double MillToCameraXX { get; set; }
        internal double MillToCameraXY { get; set; }
        internal double MillToCameraYX { get; set; }
        internal double MillToCameraYY { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AlignmentParameters"/> class with
        /// defaults for <see cref="Roll"/>, <see cref="ShiftX"/>, <see cref="ShiftY"/>, and
        /// <see cref="Orientation"/>.
        /// </summary>
        /// <param name="cameraToMillScale">The camera-to-mill factor.</param>
        internal AlignmentParameters(double cameraToMillScale)
            : this(cameraToMillScale, 0, 0, 0, ScanHeadOrientation.CableIsUpstream)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AlignmentParameters"/> class.
        /// </summary>
        /// <param name="cameraToMillScale">The camera-to-mill factor.</param>
        /// <param name="roll">The rotation around the Z axis in the mill coordinate system in degrees.</param>
        /// <param name="shiftX">The shift along the X axis in the mill coordinate system in <see cref="ScanSystemUnits"/>.</param>
        /// <param name="shiftY">The shift along the Y axis in the mill coordinate system in <see cref="ScanSystemUnits"/>.</param>
        /// <param name="orientation">The <see cref="ScanHeadOrientation"/>.</param>
        /// <exception cref="ArgumentException">
        /// One or more arguments are <see cref="double.NaN"/><br/>
        /// -or-<br/>
        /// One or more arguments are <see cref="double.NegativeInfinity"/> or <see cref="double.PositiveInfinity"/>.
        /// </exception>
        [JsonConstructor]
        internal AlignmentParameters(double cameraToMillScale, double roll, double shiftX, double shiftY, ScanHeadOrientation orientation)
        {
            if (double.IsNaN(roll) || double.IsNaN(shiftX) || double.IsNaN(shiftY))
            {
                throw new ArgumentException("One or more arguments are double.NaN.");
            }

            if (double.IsInfinity(roll) || double.IsInfinity(shiftX) || double.IsInfinity(shiftY))
            {
                throw new ArgumentException(
                    "One or more arguments are double.NegativeInfinity or double.PositiveInfinity.");
            }

            // don't set property because it triggers a transform calculation
            this.roll = roll;
            this.orientation = orientation;
            this.cameraToMillScale = cameraToMillScale;

            ShiftX = shiftX;
            ShiftY = shiftY;
            CalculateTransform();
        }

        internal void CalculateTransform()
        {
            const double rho = Math.PI / 180.0;
            double yaw = Orientation == ScanHeadOrientation.CableIsUpstream ? 180.0 : 0.0;
            double sinRoll = Math.Sin(Roll * rho);
            double cosRoll = Math.Cos(Roll * rho);
            double cosYaw = Math.Cos(yaw * rho);
            double sinNegRoll = Math.Sin(-Roll * rho);
            double cosNegRoll = Math.Cos(-Roll * rho);
            double cosNegYaw = Math.Cos(-yaw * rho);

            // Camera units are in 1/1000 of an inch
            const int cameraToApiScale = 1000;
            CameraToMillXX = cosYaw * cosRoll * CameraToMillScale / cameraToApiScale;
            CameraToMillXY = sinRoll * CameraToMillScale / cameraToApiScale;
            CameraToMillYX = cosYaw * sinRoll * CameraToMillScale / cameraToApiScale;
            CameraToMillYY = cosRoll * CameraToMillScale / cameraToApiScale;
            MillToCameraXX = cosNegYaw * cosNegRoll / CameraToMillScale * cameraToApiScale;
            MillToCameraXY = cosNegYaw * sinNegRoll / CameraToMillScale * cameraToApiScale;
            MillToCameraYX = sinNegRoll / CameraToMillScale * cameraToApiScale;
            MillToCameraYY = cosNegRoll / CameraToMillScale * cameraToApiScale;
        }

        /// <summary>
        /// Transform a point from its camera coordinate system to the mill coordinate system.
        /// </summary>
        /// <param name="point">A <see cref="Point2D"/> point.</param>
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
                (x * CameraToMillXX) - (y * CameraToMillXY) + ShiftX,
                (x * CameraToMillYX) + (y * CameraToMillYY) + ShiftY,
                brightness);
        }

        /// <summary>
        /// Transform a point from the mill coordinate system to the point's camera coordinate system.
        /// </summary>
        /// <param name="point">A <see cref="Point2D"/> point.</param>
        /// <returns>The transformed point.</returns>
        internal Point2D MillToCamera(Point2D point)
        {
            return MillToCamera(point.X, point.Y, point.Brightness);
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
            return new Point2D(
                ((x - ShiftX) * MillToCameraXX) - ((y - ShiftY) * MillToCameraXY),
                ((x - ShiftX) * MillToCameraYX) + ((y - ShiftY) * MillToCameraYY),
                brightness);
        }
    }
}
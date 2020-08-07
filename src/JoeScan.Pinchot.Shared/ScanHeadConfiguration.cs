// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Configuration parameters for a physical scan head.
    /// </summary>
    /// <remarks>
    /// The <see cref="ScanHeadConfiguration"/> class provides properties and methods for setting
    /// and getting of configuration parameters for a physical scan head. Once created and configured,
    /// a <see cref="ScanHeadConfiguration"/> object is passed to a <see cref="ScanHead"/> using
    /// <see cref="ScanHead.Configure"/>.
    /// </remarks>
    public class ScanHeadConfiguration : ICloneable
    {
        #region Backing Fields

        private int laserDetectionThreshold = 120;
        private int saturationThreshold = 800;
        private int saturatedPercentage = 30;
        private int averageIntensity = 10;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the lower bound of the auto-exposure algorithm in microseconds.
        /// Use <see cref="SetLaserOnTime"/> to set all laser timing control values.
        /// This allows the API to validate that the parameters are valid and consistent.
        /// </summary>
        /// <value>The lower bound of the auto-exposure algorithm in microseconds.</value>
        /// <seealso cref="SetLaserOnTime"/>
        public double MinLaserOnTime { get; private set; } = 100;

        /// <summary>
        /// Gets the starting value of the auto-exposure algorithm in microseconds.
        /// Use <see cref="SetLaserOnTime"/> to set all laser timing control values.
        /// This allows the API to validate that the parameters are valid and consistent.
        /// </summary>
        /// <value>The starting value of the auto-exposure algorithm in microseconds.</value>
        /// <seealso cref="SetLaserOnTime"/>
        public double DefaultLaserOnTime { get; private set; } = 500;

        /// <summary>
        /// Gets the upper bound of the auto-exposure algorithm in microseconds.
        /// Use <see cref="SetLaserOnTime"/> to set all laser timing control values.
        /// This allows the API to validate that the parameters are valid and consistent.
        /// </summary>
        /// <value>The upper bound of the auto-exposure algorithm in microseconds.</value>
        /// <seealso cref="SetLaserOnTime"/>
        public double MaxLaserOnTime { get; private set; } = 1000;

        /// <summary>
        /// Gets the lower bound of the image mode auto-exposure algorithm in microseconds.
        /// Use <see cref="SetCameraExposureTime"/> to set all camera exposure values.
        /// This allows the API to validate that the parameters are valid and consistent.
        /// </summary>
        /// <value>The lower bound of the image mode auto-exposure algorithm in microseconds.</value>
        /// <seealso cref="SetCameraExposureTime"/>
        public double MinCameraExposureTime { get; private set; } = 10000;

        /// <summary>
        /// Gets the starting value of the image mode auto-exposure algorithm in microseconds.
        /// Use <see cref="SetCameraExposureTime"/> to set all camera exposure values.
        /// This allows the API to validate that the parameters are valid and consistent.
        /// </summary>
        /// <value>The starting value of the image mode auto-exposure algorithm in microseconds.</value>
        /// <seealso cref="SetCameraExposureTime"/>
        public double DefaultCameraExposureTime { get; private set; } = 500000;

        /// <summary>
        /// Gets the upper bound of the image mode auto-exposure algorithm in microseconds.
        /// Use <see cref="SetCameraExposureTime"/> to set all camera exposure values.
        /// This allows the API to validate that the parameters are valid and consistent.
        /// </summary>
        /// <value>The upper bound of the image mode auto-exposure algorithm in microseconds.</value>
        /// <seealso cref="SetCameraExposureTime"/>
        public double MaxCameraExposureTime { get; private set; } = 1000000;

        /// <summary>
        /// Gets or sets the minimum brightness a pixel must have to be considered a valid data point.<br/>
        /// Value Range: 0-1023<br/>
        /// Default : 120
        /// </summary>
        /// <value>The minimum brightness a pixel must have to be considered a valid data point.</value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Value out of range (0 - 1023)
        /// </exception>
        public int LaserDetectionThreshold
        {
            get { return laserDetectionThreshold; }
            set
            {
                if (value < 0 || value > 1023)
                {
                    throw new ArgumentOutOfRangeException(nameof(value),
                        "LaserDetectionThreshold out of range (0-1023)");
                }

                laserDetectionThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the minimum brightness a pixel must have to be considered saturated.<br/>
        /// Value Range: 0-1023<br/>
        /// Default : 800
        /// </summary>
        /// <value>The minimum brightness a pixel must have to be considered saturated.</value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Value out of range (0 - 1023)
        /// </exception>
        public int SaturationThreshold
        {
            get { return saturationThreshold; }
            set
            {
                if (value < 0 || value > 1023)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "SaturationThreshold out of range (0-1023)");
                }

                saturationThreshold = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum percentage of valid pixels in a scan that are allowed to be
        /// brighter than <see cref="SaturationThreshold"/>.<br/>
        /// Default: 30 (percent)
        /// </summary>
        /// <value>The maximum percentage of valid pixels in a scan that are allowed to be
        /// brighter than <see cref="SaturationThreshold"/>.</value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Value out of range (0 - 100)
        /// </exception>
        public int SaturatedPercentage
        {
            get { return saturatedPercentage; }
            set
            {
                if (value < 1 || value > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "SaturatedPercentage out of range (1-100)");
                }

                saturatedPercentage = value;
            }
        }

        /// <summary>
        /// Gets or sets the time delay in microseconds to when a scan begins on a given
        /// scan head. This can be used to ensure that multiple scan heads are
        /// performing scans at distinct points in time rather than at the same time to
        /// avoid cross-talk and interference.<br/>
        /// Default: 0μs
        /// </summary>
        /// <value>The time delay in microseconds to when a scan begins on a given
        /// scan head.</value>
        public double ScanPhaseOffset { get; set; }

        #endregion

        #region Internal Properties

        /// <summary> 
        /// In modes where image data is requested, the auto-exposure control will try to keep the image's average brightness at this level.
        /// If you find the image mode is too dark or too bright, then raise or lower this value accordingly. This setting
        /// has no effect on the measurement of points; it only changes how the image data is scaled. 0-255. Default: 150 
        /// </summary>
        /// /// <exception cref="ArgumentOutOfRangeException"></exception>
        [JsonProperty(nameof(AverageIntensity))]
        internal int AverageIntensity
        {
            get { return averageIntensity; }
            set
            {
                if (value < 0 || value > 255)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "AverageIntensity out of range (0-255)");
                }

                averageIntensity = value;
            }
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Initializes a new instance of the <see cref="ScanHeadConfiguration"/> class.
        /// </summary>
        public ScanHeadConfiguration()
        {
        }

        [JsonConstructor]
        internal ScanHeadConfiguration(double minLaserOnTime, double defaultLaserOnTime, double maxLaserOnTime,
            double minCameraExposureTime, double defaultCameraExposureTime, double maxCameraExposureTime)
        {
            MinLaserOnTime = minLaserOnTime;
            DefaultLaserOnTime = defaultLaserOnTime;
            MaxLaserOnTime = maxLaserOnTime;
            MinCameraExposureTime = minCameraExposureTime;
            DefaultCameraExposureTime = defaultCameraExposureTime;
            MaxCameraExposureTime = maxCameraExposureTime;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the clamping values for the auto-exposure algorithm in microseconds. To disable auto-exposure,
        /// set <paramref name="minTimeUs"/>, <paramref name="defaultTimeUs"/> and <paramref name="maxTimeUs"/> to the same value.
        /// </summary>
        /// <param name="minTimeUs">Lower bound for the auto-exposure algorithm. Value in microseconds.
        /// Must be smaller than or equal to <paramref name="defaultTimeUs"/>. Allowed Range 15μs - 650,000μs. Default: 100μs</param>
        /// <param name="defaultTimeUs">Starting value for the auto-exposure algorithm. Value in microseconds.
        /// Must be smaller than or equal to <paramref name="maxTimeUs"/>. Allowed Range 15μs - 650,000μs. Default: 500μs</param>
        /// <param name="maxTimeUs">Upper bound for the auto-exposure algorithm. Value in microseconds.
        /// Must be greater than <paramref name="defaultTimeUs"/>. Allowed Range 15μs - 650,000μs. Default: 1000μs</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// <paramref name="minTimeUs"/> out of range (15μs - 650,000μs)<br/>
        /// -or-<br/>
        /// <paramref name="defaultTimeUs"/> out of range (15μs - 650,000μs)<br/>
        /// -or-<br/>
        /// <paramref name="maxTimeUs"/> out of range (15μs - 650,000μs)
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="minTimeUs"/> must be less or equal to <paramref name="defaultTimeUs"/><br/>
        /// -or-<br/>
        /// <paramref name="maxTimeUs"/> must be greater or equal to <paramref name="defaultTimeUs"/>
        /// </exception>
        public void SetLaserOnTime(double minTimeUs, double defaultTimeUs, double maxTimeUs)
        {
            // TODO: what are the limits on the hardware? Using JS-25 values here for now
            if (minTimeUs < 15 || minTimeUs > 650000)
            {
                throw new ArgumentOutOfRangeException(nameof(minTimeUs),
                    "MinLaserOn out of range (15μs - 650,000μs)");
            }

            if (defaultTimeUs < 15 || defaultTimeUs > 650000)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultTimeUs),
                    "DefaultLaserOn out of range (15μs - 650,000μs)");
            }

            if (maxTimeUs < 15 || maxTimeUs > 650000)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTimeUs),
                    "MaxLaserOn out of range (15μs - 650,000μs)");
            }

            if (minTimeUs > defaultTimeUs)
            {
                throw new ArgumentException("MinLaserOn must be less or equal to DefaultLaserOn");
            }

            if (maxTimeUs < defaultTimeUs)
            {
                throw new ArgumentException("MaxLaserOn must be greater or equal to DefaultLaserOn");
            }

            this.MinLaserOnTime = minTimeUs;
            this.DefaultLaserOnTime = defaultTimeUs;
            this.MaxLaserOnTime = maxTimeUs;
        }

        /// <summary>
        /// Sets the clamping values for the image mode auto-exposure algorithm in microseconds. To disable auto-exposure,
        /// set <paramref name="minTimeUs"/>, <paramref name="defaultTimeUs"/> and <paramref name="maxTimeUs"/> to the same value.
        /// </summary>
        /// <param name="minTimeUs">Lower bound for the image mode auto-exposure algorithm. Value in microseconds.
        /// Must be smaller than or equal to <paramref name="defaultTimeUs"/>. Allowed Range 15μs - 2,000,000μs. Default: 10,000μs</param>
        /// <param name="defaultTimeUs">Starting value for the image mode auto-exposure algorithm. Value in microseconds.
        /// Must be smaller than or equal to <paramref name="maxTimeUs"/>. Allowed Range 15μs - 2,000,000μs. Default: 500,000μs</param>
        /// <param name="maxTimeUs">Upper bound for the image mode auto-exposure algorithm. Value in microseconds.
        /// Must be greater than <paramref name="defaultTimeUs"/>. Allowed Range 15μs - 2,000,000μs. Default: 1,000,000μs</param>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// <paramref name="minTimeUs"/> out of range (15μs - 2,000,000μs)<br/>
        /// -or-<br/>
        /// <paramref name="defaultTimeUs"/> out of range (15μs - 2,000,000μs)<br/>
        /// -or-<br/>
        /// <paramref name="maxTimeUs"/> out of range (15μs - 2,000,000μs)
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="minTimeUs"/> must be less or equal to <paramref name="defaultTimeUs"/><br/>
        /// -or-<br/>
        /// <paramref name="maxTimeUs"/> must be greater or equal to <paramref name="defaultTimeUs"/>
        /// </exception>
        public void SetCameraExposureTime(double minTimeUs, double defaultTimeUs, double maxTimeUs)
        {
            if (minTimeUs < 15 || minTimeUs > 2000000)
            {
                throw new ArgumentOutOfRangeException(nameof(minTimeUs),
                    "MinExposure out of range (15μs - 2,000,000μs)");
            }

            if (defaultTimeUs < 15 || defaultTimeUs > 2000000)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultTimeUs),
                    "DefaultExposure out of range (15μs - 2,000,000μs)");
            }

            if (maxTimeUs < 15 || maxTimeUs > 2000000)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTimeUs),
                    "MaxExposure out of range (15μs - 2,000,000μs)");
            }

            if (minTimeUs > defaultTimeUs)
            {
                throw new ArgumentException("MinExposure must be less or equal to DefaultExposure");
            }

            if (maxTimeUs < defaultTimeUs)
            {
                throw new ArgumentException("MaxExposure must be greater or equal to DefaultExposure");
            }

            this.MinCameraExposureTime = minTimeUs;
            this.DefaultCameraExposureTime = defaultTimeUs;
            this.MaxCameraExposureTime = maxTimeUs;
        }

        /// <summary>
        /// <see cref="ScanHeadConfiguration"/> implements <see cref="ICloneable"/>, which is used
        /// to make sure that once a <see cref="ScanHeadConfiguration"/> is set on a <see cref="ScanHead"/>
        /// via <see cref="ScanHead.Configure"/>, it will not change if the original is changed.
        /// </summary>
        /// <returns>A shallow copy of the <see cref="ScanHeadConfiguration"/> object.</returns>
        public object Clone()
        {
            return MemberwiseClone();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Performs a validation of the configuration to ensure there
        /// are no conflicts. Note, this method is not currently implemented.
        /// </summary>
        /// <returns><c>true</c> if the configuration is valid, <c>false</c> otherwise.</returns>
        internal bool Validate()
        {
            // TODO: implement 
            return true;
        }

        #endregion
    }
}
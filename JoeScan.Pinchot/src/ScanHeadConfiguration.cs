// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Configuration parameters for a physical scan head.
    /// </summary>
    /// <remarks>
    /// This class provides properties and methods for setting and getting of configuration parameters
    /// for a physical scan head. Once created and configured, a <see cref="ScanHeadConfiguration"/>
    /// object is passed to a <see cref="ScanHead"/> using
    /// <see cref="ScanHead.Configure"/>.
    /// </remarks>
    public class ScanHeadConfiguration : ICloneable
    {
        #region Backing Fields

        private const uint MinLaserOnTimeDefaultUs = 100;
        private const uint DefLaserOnTimeDefaultUs = 500;
        private const uint MaxLaserOnTimeDefaultUs = 1000;
        private const uint MinExposureTimeDefaultUs = 10000;
        private const uint DefExposureTimeDefaultUs = 500000;
        private const uint MaxExposureTimeDefaultUs = 1000000;

        private uint laserDetectionThreshold = 120;
        private uint saturationThreshold = 800;
        private uint saturationPercentage = 30;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the lower bound of the auto-exposure algorithm in microseconds.
        /// Use <see cref="SetLaserOnTime"/> to set all laser timing control values.
        /// This allows the API to validate that the parameters are valid and consistent.
        /// </summary>
        /// <value>The lower bound of the auto-exposure algorithm in microseconds.</value>
        /// <seealso cref="SetLaserOnTime"/>
        public uint MinLaserOnTimeUs { get; private set; } = MinLaserOnTimeDefaultUs;

        /// <summary>
        /// Gets the starting value of the auto-exposure algorithm in microseconds.
        /// Use <see cref="SetLaserOnTime"/> to set all laser timing control values.
        /// This allows the API to validate that the parameters are valid and consistent.
        /// </summary>
        /// <value>The starting value of the auto-exposure algorithm in microseconds.</value>
        /// <seealso cref="SetLaserOnTime"/>
        public uint DefaultLaserOnTimeUs { get; private set; } = DefLaserOnTimeDefaultUs;

        /// <summary>
        /// Gets the upper bound of the auto-exposure algorithm in microseconds.
        /// Use <see cref="SetLaserOnTime"/> to set all laser timing control values.
        /// This allows the API to validate that the parameters are valid and consistent.
        /// </summary>
        /// <value>The upper bound of the auto-exposure algorithm in microseconds.</value>
        /// <seealso cref="SetLaserOnTime"/>
        public uint MaxLaserOnTimeUs { get; private set; } = MaxLaserOnTimeDefaultUs;

        /// <summary>
        /// Gets the lower bound of the image mode auto-exposure algorithm in microseconds.
        /// </summary>
        /// <value>The lower bound of the image mode auto-exposure algorithm in microseconds.</value>
        [Obsolete("Camera exposure is only used when getting a diagnostic image.")]
        public uint MinCameraExposureTimeUs { get; private set; } = MinExposureTimeDefaultUs;

        /// <summary>
        /// Gets the starting value of the image mode auto-exposure algorithm in microseconds.
        /// </summary>
        /// <value>The starting value of the image mode auto-exposure algorithm in microseconds.</value>
        [Obsolete("Camera exposure is only used when getting a diagnostic image.")]
        public uint DefaultCameraExposureTimeUs { get; private set; } = DefExposureTimeDefaultUs;

        /// <summary>
        /// Gets the upper bound of the image mode auto-exposure algorithm in microseconds.
        /// </summary>
        /// <value>The upper bound of the image mode auto-exposure algorithm in microseconds.</value>
        [Obsolete("Camera exposure is only used when getting a diagnostic image.")]
        public uint MaxCameraExposureTimeUs { get; private set; } = MaxExposureTimeDefaultUs;

        /// <summary>
        /// Gets or sets the minimum brightness a pixel must have to be considered a valid data point.<br/>
        /// Value Range: 0-1023<br/>
        /// Default : 120
        /// </summary>
        /// <value>The minimum brightness a pixel must have to be considered a valid data point.</value>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Value out of range (0 - 1023)
        /// </exception>
        public uint LaserDetectionThreshold
        {
            get => laserDetectionThreshold;
            set
            {
                if (value > 1023)
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
        public uint SaturationThreshold
        {
            get => saturationThreshold;
            set
            {
                if (value > 1023)
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
        /// Value out of range (1 - 100)
        /// </exception>
        public uint SaturationPercentage
        {
            get => saturationPercentage;
            set
            {
                if (value < 1 || value > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "SaturationPercentage out of range (1-100)");
                }

                saturationPercentage = value;
            }
        }

        /// <summary>
        /// Gets or sets the minimum number of encoder counts that must occur between
        /// the previous and the current profile to drop the scan head into an idle state.
        /// In an idle state, the rate at which profiles are queued is determined by
        /// <see cref="IdleScanPeriodUs"/>.
        /// </summary>
        /// <remarks>
        /// Only <see cref="Encoder.Main"/> is observed.
        /// </remarks>
        /// <value>
        /// The minimum number of encoder counts between profiles needed to keep the
        /// scan head out of an idle state.
        /// </value>
        [Obsolete("Use ScanSystem.StartScanning(StartScanningOptions) to specify idle scan period instead.")]
        public uint MinimumEncoderTravel { get; set; }

        /// <summary>
        /// Gets or sets the rate at which profiles should be queued when in an idle state.
        /// See <see cref="MinimumEncoderTravel"/> on how a scan head can drop to an idle state.
        /// </summary>
        /// <value>
        /// The scan period in microseconds of a scan head in an idle state.
        /// </value>
        [Obsolete("Use ScanSystem.StartScanning(StartScanningOptions) to specify idle scan period instead.")]
        public uint IdleScanPeriodUs { get; set; }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Initializes a new instance of the <see cref="ScanHeadConfiguration"/> class.
        /// </summary>
        public ScanHeadConfiguration()
        {
        }

        internal ScanHeadConfiguration(uint minLaserOnTimeUs, uint defaultLaserOnTimeUs, uint maxLaserOnTimeUs,
            uint minCameraExposureTimeUs, uint defaultCameraExposureTimeUs, uint maxCameraExposureTimeUs)
        {
            MinLaserOnTimeUs = minLaserOnTimeUs;
            DefaultLaserOnTimeUs = defaultLaserOnTimeUs;
            MaxLaserOnTimeUs = maxLaserOnTimeUs;
            MinCameraExposureTimeUs = minCameraExposureTimeUs;
            DefaultCameraExposureTimeUs = defaultCameraExposureTimeUs;
            MaxCameraExposureTimeUs = maxCameraExposureTimeUs;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the clamping values for the auto-exposure algorithm in microseconds. To disable auto-exposure,
        /// set <paramref name="minTimeUs"/>, <paramref name="defaultTimeUs"/> and <paramref name="maxTimeUs"/> to the same value.
        /// </summary>
        /// <param name="minTimeUs">Lower bound for the auto-exposure algorithm. Value in microseconds.
        /// Must be smaller than or equal to <paramref name="defaultTimeUs"/>. Default: 100μs</param>
        /// <param name="defaultTimeUs">Starting value for the auto-exposure algorithm. Value in microseconds.
        /// Must be smaller than or equal to <paramref name="maxTimeUs"/>. Default: 500μs</param>
        /// <param name="maxTimeUs">Upper bound for the auto-exposure algorithm. Value in microseconds.
        /// Must be greater than <paramref name="defaultTimeUs"/>. Default: 1000μs</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="minTimeUs"/> must be less or equal to <paramref name="defaultTimeUs"/><br/>
        /// -or-<br/>
        /// <paramref name="maxTimeUs"/> must be greater or equal to <paramref name="defaultTimeUs"/>
        /// </exception>
        public void SetLaserOnTime(uint minTimeUs, uint defaultTimeUs, uint maxTimeUs)
        {
            if (minTimeUs > defaultTimeUs)
            {
                throw new ArgumentException($"{nameof(minTimeUs)} must be less or equal to {nameof(defaultTimeUs)}");
            }

            if (maxTimeUs < defaultTimeUs)
            {
                throw new ArgumentException($"{nameof(maxTimeUs)} must be greater or equal to {nameof(defaultTimeUs)}");
            }

            MinLaserOnTimeUs = minTimeUs;
            DefaultLaserOnTimeUs = defaultTimeUs;
            MaxLaserOnTimeUs = maxTimeUs;
        }

        /// <summary>
        /// Sets the clamping values for the image mode auto-exposure algorithm in microseconds. To disable auto-exposure,
        /// set <paramref name="minTimeUs"/>, <paramref name="defaultTimeUs"/> and <paramref name="maxTimeUs"/> to the same value.
        /// </summary>
        /// <param name="minTimeUs">Lower bound for the image mode auto-exposure algorithm. Value in microseconds.
        /// Must be smaller than or equal to <paramref name="defaultTimeUs"/>. Default: 10,000μs</param>
        /// <param name="defaultTimeUs">Starting value for the image mode auto-exposure algorithm. Value in microseconds.
        /// Must be smaller than or equal to <paramref name="maxTimeUs"/>. Default: 500,000μs</param>
        /// <param name="maxTimeUs">Upper bound for the image mode auto-exposure algorithm. Value in microseconds.
        /// Must be greater than <paramref name="defaultTimeUs"/>. Default: 1,000,000μs</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="minTimeUs"/> must be less or equal to <paramref name="defaultTimeUs"/><br/>
        /// -or-<br/>
        /// <paramref name="maxTimeUs"/> must be greater or equal to <paramref name="defaultTimeUs"/>
        /// </exception>
        [Obsolete("Camera exposure is only used when getting a diagnostic image.")]
        public void SetCameraExposureTime(uint minTimeUs, uint defaultTimeUs, uint maxTimeUs)
        {
            if (minTimeUs > defaultTimeUs)
            {
                throw new ArgumentException($"{nameof(minTimeUs)} must be less or equal to {nameof(defaultTimeUs)}");
            }

            if (maxTimeUs < defaultTimeUs)
            {
                throw new ArgumentException($"{nameof(maxTimeUs)} must be greater or equal to {nameof(defaultTimeUs)}");
            }

            MinCameraExposureTimeUs = minTimeUs;
            DefaultCameraExposureTimeUs = defaultTimeUs;
            MaxCameraExposureTimeUs = maxTimeUs;
        }

        /// <summary>
        /// <see cref="ScanHeadConfiguration"/> implements <see cref="ICloneable"/>, which is used
        /// to make sure that once a <see cref="ScanHeadConfiguration"/> is set on a <see cref="ScanHead"/>
        /// via <see cref="ScanHead.Configure"/>, it will not change if the original is changed.
        /// </summary>
        /// <returns>A shallow copy of the <see cref="ScanHeadConfiguration"/> object.</returns>
        public object Clone()
        {
            return MemberwiseClone() as ScanHeadConfiguration;
        }

        #endregion
    }
}

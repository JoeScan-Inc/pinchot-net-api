// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Client = joescan.schema.client;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// The various physical limits and features of a <see cref="ScanHead"/>.
    /// </summary>
    public class ScanHeadCapabilities
    {
        #region Public Properties

        /// <summary>
        /// Gets the number of bits used for <see cref="Point2D.Brightness"/> within an <see cref="IProfile"/>.
        /// </summary>
        /// <value>The number of bits.</value>
        public uint CameraBrightnessBitDepth { get; }

        /// <summary>
        /// Gets the maximum image height camera supports.
        /// </summary>
        /// <value>The maximum image height.</value>
        public uint MaxCameraImageHeight { get; }

        /// <summary>
        /// Gets the maximum image width camera supports.
        /// </summary>
        /// <value>The maximum image width.</value>
        public uint MaxCameraImageWidth { get; }

        /// <summary>
        /// Gets the largest scan period in µs supported by product.
        /// </summary>
        /// <value>The largest scan period.</value>
        public uint MaxScanPeriodUs { get; }

        /// <summary>
        /// Gets the smallest scan period in µs supported by product.
        /// </summary>
        /// <value>The smallest scan period.</value>
        public uint MinScanPeriodUs { get; }

        /// <summary>
        /// Gets the number of cameras supported by product.
        /// </summary>
        /// <value>The number of cameras.</value>
        public uint NumCameras { get; }

        /// <summary>
        /// Gets the number of lasers supported by product.
        /// </summary>
        /// <value>The number of lasers.</value>
        public uint NumLasers { get; }

        /// <summary>
        /// Gets the number of encoders supported by product.
        /// </summary>
        /// <value>The number of encoders.</value>
        public uint MaxSupportedEncoders { get; }

        #endregion

        #region Lifecycle

        internal ScanHeadCapabilities(Client::ScanHeadSpecificationT spec)
        {
            CameraBrightnessBitDepth = 8;
            MaxCameraImageHeight = spec.MaxCameraRows;
            MaxCameraImageWidth = spec.MaxCameraColumns;
            MaxScanPeriodUs = spec.MaxScanPeriodUs;
            MinScanPeriodUs = spec.MinScanPeriodUs;
            NumCameras = spec.NumberOfCameras;
            NumLasers = spec.NumberOfLasers;
            MaxSupportedEncoders = 1;
        }

        #endregion
    }
}

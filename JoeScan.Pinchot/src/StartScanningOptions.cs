// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

namespace JoeScan.Pinchot
{
    /// <summary>
    /// A set of options for configuring how a <see cref="ScanSystem"/> should scan.
    /// </summary>
    /// <seealso cref="ScanSystem.StartScanning(StartScanningOptions)"/>
    public class StartScanningOptions
    {
        #region Backing Fields

        private DataFormat format;

        #endregion

        #region Public Properties

        /// <summary>
        /// The time it takes to gather all <see cref="IProfile"/>s from the scheduled phase elements in the phase table.
        /// </summary>
        /// <value>The scan period in microseconds.</value>
        public uint PeriodUs { get; set; }

        /// <summary>
        /// The format of the data contained in an <see cref="IProfile"/>.
        /// </summary>
        /// <value>The data format.</value>
        public DataFormat Format
        {
            get => format;
            set
            {
                format = value;
                AllFormat = (AllDataFormat)value;
            }
        }

        /// <summary>
        /// The mode that determines how <see cref="IProfile"/>s are gathered and consumed.
        /// </summary>
        /// <value>The scanning mode.</value>
        public ScanningMode Mode { get; set; }

        /// <summary>
        /// Gets or sets the rate at which <see cref="IProfile"/>s are generated when the
        /// <see cref="ScanSystem"/> is in an idle state. An idle state occurs when the
        /// <see cref="Encoder.Main"/> value has not changed for 1 second.
        /// <br/>
        /// This value can be set to 0 to completely stop <see cref="IProfile"/>s from being
        /// generated when in an idle state.
        /// </summary>
        /// <remarks>
        /// The idle scan period should be a multiple of <see cref="PeriodUs"/>.
        /// </remarks>
        /// <value>
        /// The idle scan period in microseconds. If set to <see langword="null"/>, idle scanning
        /// is disabled.
        /// </value>
        public uint? IdlePeriodUs { get; set; }

        #endregion

        #region Internal Properties

        /// <summary>
        /// This property should be used internally as <see cref="Format"/> is only used for public API purposes.
        /// </summary>
        internal AllDataFormat AllFormat { get; set; }

        /// <summary>
        /// The time in the future that the scan head should start scanning.
        /// This is used to synchronize multiple scan heads to avoid rollover bugs.
        /// </summary>
        internal ulong StartScanningTimeNs { get; set; }

        #endregion
    }
}

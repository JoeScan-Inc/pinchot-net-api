// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections.Generic;
using Client = joescan.schema.client;
using Server = joescan.schema.server;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Scan head status data.
    /// </summary>
    /// <remarks>
    /// This class contains the status data reported from a <see cref="ScanHead"/>.
    /// Use <see cref="ScanHead.RequestStatus"/> to get an updated status.
    /// </remarks>
    /// <seealso cref="ScanHead.RequestStatus"/>
    public class ScanHeadStatus
    {
        #region Public Properties

        /// <summary>
        /// Gets the system global time in nanoseconds of the last status update.
        /// </summary>
        /// <value>The system global time in nanoseconds of the last status update.</value>
        public ulong GlobalTimeNs { get; internal set; }

        /// <summary>
        /// Gets the encoder positions at the time of the last status update.
        /// </summary>
        /// <value>A <see cref="IDictionary{TKey,TValue}"/> of encoder positions at the
        /// time of the last status update.</value>
        public IDictionary<Encoder, long> EncoderValues { get; internal set; }

        /// <summary>
        /// Gets the total number of profiles sent during the last scan sequence (between calls to
        /// <see cref="ScanSystem.StartScanning(uint, DataFormat, ScanningMode)"/> and <see cref="ScanSystem.StopScanning"/>).
        /// </summary>
        /// <value>The total number of profiles sent during the last scan sequence (between calls to
        /// <see cref="ScanSystem.StartScanning(uint, DataFormat, ScanningMode)"/> and <see cref="ScanSystem.StopScanning"/>).</value>
        public uint ProfilesSentCount { get; internal set; }

        /// <summary>
        /// Gets the temperature sensor measurements in Celsius at the time of the last status update.
        /// </summary>
        /// <value>A <see cref="IDictionary{TKey,TValue}"/> of temperature sensor measurements in Celsius
        /// at the time of the last status update.</value>
        public IDictionary<TemperatureSensor, float> Temperatures { get; internal set; }

        #endregion

        #region Internal Properties

        /// <summary>
        /// Gets the number of valid encoders.
        /// </summary>
        /// <value>The number of valid encoders.</value>
        internal uint NumValidEncoders { get; set; }

        /// <summary>
        /// Gets the cameras that the server detected.
        /// </summary>
        /// <value>Cameras that the server detected.</value>
        internal IList<Camera> DetectedCameras { get; set; }

        /// <summary>
        /// Gets the total number of packets sent during the last scan period.
        /// </summary>
        /// <value>The number of packets sent.</value>
        internal uint NumPacketsSent { get; set; }

        /// <summary>
        /// Gets the total number of pixels seen by the cameras' scan windows.
        /// </summary>
        /// <value>The number of pixels.</value>
        internal IDictionary<Camera, uint> PixelsInWindow { get; set; }

        /// <summary>
        /// Gets the smallest scan period possible with the current configuration in µs. Consumers of the
        /// API should instead use <see cref="ScanSystem.GetMinScanPeriod"/>.
        /// </summary>
        /// <value>The smallest scan period possible with the current configuration in µs.</value>
        internal uint MinScanPeriodUs { get; }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Deserialize a scan head status packet from a FlatBuffer <see cref="Server::StatusDataT"/> type
        /// </summary>
        /// <exception cref="VersionCompatibilityException">Thrown if the scan head version
        /// is incompatible with the client API</exception>"
        internal ScanHeadStatus(Server::StatusDataT data, Client::ScanHeadSpecificationT spec)
        {
            MinScanPeriodUs = data.MinScanPeriodNs / 1000;
            GlobalTimeNs = data.GlobalTimeNs;
            NumPacketsSent = data.NumPacketsSent;
            ProfilesSentCount = data.NumProfilesSent;

            NumValidEncoders = (uint)data.Encoders.Count;
            EncoderValues = new Dictionary<Encoder, long>();
            for (int i = 0; i < NumValidEncoders; ++i)
            {
                long val = data.Encoders[i];
                EncoderValues.Add((Encoder)i, val);
            }

            DetectedCameras = new List<Camera>();
            PixelsInWindow = new Dictionary<Camera, uint>();
            Temperatures = new Dictionary<TemperatureSensor, float>();
            foreach (var cameraData in data.CameraData)
            {
                var camera = (Camera)spec.CameraPortToId[(int)cameraData.Port];
                DetectedCameras.Add(camera);
                PixelsInWindow.Add(camera, cameraData.PixelsInWindow);
                Temperatures.Add((TemperatureSensor)camera, cameraData.Temperature);
            }
        }

        #endregion
    }
}

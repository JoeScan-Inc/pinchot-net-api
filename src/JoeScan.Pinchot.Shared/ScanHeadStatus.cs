// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Scan head status data.
    /// </summary>
    /// <remarks>
    /// The <see cref="ScanHeadStatus"/> class provides properties for getting status data reported
    /// from a physical scan head such as the maximum possible scan rate and number of profiles sent.<br/>
    /// Status data is not updated while a scan head is scanning.
    /// </remarks>
    public class ScanHeadStatus
    {
        #region Public Properties

        /// <summary>
        /// Gets the system global time in nanoseconds of the last status update.
        /// </summary>
        /// <value>The system global time in nanoseconds of the last status update.</value>
        public long GlobalTime { get; internal set; }

        /// <summary>
        /// Gets the encoder positions at the time of the last status update.
        /// </summary>
        /// <value>A <see cref="IDictionary{TKey,TValue}"/> of encoder positions at the
        /// time of the last status update.</value>
        public IDictionary<Encoder, long> EncoderValues { get; internal set; }

        /// <summary>
        /// Gets the total number of profiles sent during the last scan period (between calls to
        /// <see cref="ScanSystem.StartScanning"/> and <see cref="ScanSystem.StopScanning"/>).
        /// </summary>
        /// <value>The total number of profiles sent during the last scan period (between calls to
        /// <see cref="ScanSystem.StartScanning"/> and <see cref="ScanSystem.StopScanning"/>).</value>
        public int ProfilesSentCount { get; internal set; }

        /// <summary>
        /// Gets the temperature sensor measurements in degrees C at the time of the last status update.
        /// </summary>
        /// <value>A <see cref="IDictionary{TKey,TValue}"/> of temperature sensor measurements in degrees C
        /// at the time of the last status update.</value>
        public IDictionary<TemperatureSensor, float> Temperatures { get; internal set; }

        /// <summary>
        /// Gets the <see cref="ScanHeadVersionInformation"/> of the firmware on the physical scan head.
        /// </summary>
        /// <value>The <see cref="ScanHeadVersionInformation"/> of the firmware on the physical scan head.</value>
        public ScanHeadVersionInformation FirmwareVersion { get; }

        #endregion

        #region Internal Properties

        /// <summary>
        /// Gets the packet header.
        /// </summary>
        /// <value>The packet header.</value>
        internal PacketHeader Header { get; }

        /// <summary>
        /// Gets the number of valid encoders.
        /// </summary>
        /// <value>The number of valid encoders.</value>
        internal int NumValidEncoders { get; set; }

        /// <summary>
        /// Gets the number of valid cameras on the scan head.
        /// </summary>
        /// <value>The number of valid cameras.</value>
        internal int NumValidCameras { get; set; }

        /// <summary>
        /// Gets the scan head IP address.
        /// </summary>
        /// <value>The scan head IP address</value>
        [Obsolete("This property is to be removed. Use ScanHead.IPAddress instead.")]
        internal IPAddress ScanHeadIPAddress { get; set; }

        /// <summary>
        /// Gets the scan head serial number.
        /// </summary>
        /// <value>The scan head serial number.</value>
        internal int ScanHeadSerialNumber { get; set; }

        /// <summary>
        /// Gets the ID of the scan sync device paired to the scan head.
        /// </summary>
        /// <value>The scan sync ID.</value>
        internal ushort ScanSyncID { get; set; }

        /// <summary>
        /// Gets the UDP port of the client connected to the scan head.
        /// </summary>
        /// <value>The client UDP port.</value>
        internal ushort ClientUdpPort { get; set; }

        /// <summary>
        /// Gets the IP address of the client connected to the scan head.
        /// </summary>
        /// <value>The client IP address.</value>
        internal IPAddress ClientIPAddress { get; set; }

        /// <summary>
        /// Gets the total number of UDP packets sent during the last scan period.
        /// </summary>
        /// <value>The number of packets sent.</value>
        internal int NumPacketsSent { get; set; }

        /// <summary>
        /// Gets the total number of pixels seen by the cameras' scan windows.
        /// </summary>
        /// <value>The number of pixels.</value>
        // TODO: remember to change set accessor to internal if access is changed to public
        internal IDictionary<Camera, int> PixelsInWindow { get; set; }

        /// <summary>
        /// The minimum amount of data necessary to be considered a valid Status packet.
        /// </summary>
        internal static int MinimumValidPacketSize =>
            Marshal.SizeOf(typeof(PacketHeader)) + Marshal.SizeOf(typeof(ScanHeadVersionInformation));

        /// <summary>
        /// Gets the highest scan rate possible with the current configuration in Hz. Consumers of the
        /// API should instead use <see cref="ScanSystem.GetMaxScanRate"/>.
        /// </summary>
        /// <value>The highest scan rate possible with the current configuration in Hz.</value>
        internal int MaxScanRate { get; private set; }

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        internal uint Reserved0 { get; private set; }

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        internal uint Reserved1 { get; private set; }

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        internal uint Reserved2 { get; private set; }

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        internal uint Reserved3 { get; private set; }

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        internal uint Reserved4 { get; private set; }

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        internal uint Reserved5 { get; private set; }

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        internal uint Reserved6 { get; private set; }

        /// <summary>
        /// Reserved for future use.
        /// </summary>
        internal uint Reserved7 { get; private set; }

        #endregion

        #region Lifecycle

        internal ScanHeadStatus()
        {
        }

        /// <summary>
        /// Deserialize a scan head status packet from a raw byte stream.
        /// </summary>
        /// <exception cref="VersionCompatibilityException">Thrown if the scan head version
        /// is incompatible with the client API</exception>"
        /// <param name="buf"></param>
        internal ScanHeadStatus(byte[] buf)
        {
            int idx = 0;

            // The header and version data should never change to maintain backwards compatibility
            Header = new PacketHeader
            {
                Magic = NetworkByteUnpacker.ExtractUShortFromNetworkBuffer(buf, ref idx),
                Size = NetworkByteUnpacker.ExtractByteFromNetworkBuffer(buf, ref idx),
                Type = (ScanPacketType)NetworkByteUnpacker.ExtractByteFromNetworkBuffer(buf, ref idx)
            };

            FirmwareVersion = new ScanHeadVersionInformation
            {
                Major = NetworkByteUnpacker.ExtractUIntFromNetworkBuffer(buf, ref idx),
                Minor = NetworkByteUnpacker.ExtractUIntFromNetworkBuffer(buf, ref idx),
                Patch = NetworkByteUnpacker.ExtractUIntFromNetworkBuffer(buf, ref idx),
                Commit = NetworkByteUnpacker.ExtractUIntFromNetworkBuffer(buf, ref idx),
                Product =
                    (ProductType)NetworkByteUnpacker.ExtractUShortFromNetworkBuffer(buf, ref idx),
                Flags = NetworkByteUnpacker.ExtractUShortFromNetworkBuffer(buf, ref idx)
            };

            // Minor versions can be differ, but differences in
            // major versions are considered to be incompatible
            if (FirmwareVersion.Major != VersionInformation.Major)
            {
                throw new VersionCompatibilityException(FirmwareVersion);
            }

            // Static Data
            ScanHeadSerialNumber = NetworkByteUnpacker.ExtractIntFromNetworkBuffer(buf, ref idx);
            MaxScanRate = NetworkByteUnpacker.ExtractIntFromNetworkBuffer(buf, ref idx);
            ScanHeadIPAddress = NetworkByteUnpacker.ExtractIPAddressFromNetworkBuffer(buf, ref idx);
            ClientIPAddress = NetworkByteUnpacker.ExtractIPAddressFromNetworkBuffer(buf, ref idx);
            ClientUdpPort = NetworkByteUnpacker.ExtractUShortFromNetworkBuffer(buf, ref idx);
            ScanSyncID = NetworkByteUnpacker.ExtractUShortFromNetworkBuffer(buf, ref idx);
            GlobalTime = NetworkByteUnpacker.ExtractLongFromNetworkBuffer(buf, ref idx);
            NumPacketsSent = NetworkByteUnpacker.ExtractIntFromNetworkBuffer(buf, ref idx);
            ProfilesSentCount = NetworkByteUnpacker.ExtractIntFromNetworkBuffer(buf, ref idx);
            NumValidEncoders = NetworkByteUnpacker.ExtractByteFromNetworkBuffer(buf, ref idx);
            NumValidCameras = NetworkByteUnpacker.ExtractByteFromNetworkBuffer(buf, ref idx);

            Reserved0 = NetworkByteUnpacker.ExtractUIntFromNetworkBuffer(buf, ref idx);
            Reserved1 = NetworkByteUnpacker.ExtractUIntFromNetworkBuffer(buf, ref idx);
            Reserved2 = NetworkByteUnpacker.ExtractUIntFromNetworkBuffer(buf, ref idx);
            Reserved3 = NetworkByteUnpacker.ExtractUIntFromNetworkBuffer(buf, ref idx);
            Reserved4 = NetworkByteUnpacker.ExtractUIntFromNetworkBuffer(buf, ref idx);
            Reserved5 = NetworkByteUnpacker.ExtractUIntFromNetworkBuffer(buf, ref idx);
            Reserved6 = NetworkByteUnpacker.ExtractUIntFromNetworkBuffer(buf, ref idx);
            Reserved7 = NetworkByteUnpacker.ExtractUIntFromNetworkBuffer(buf, ref idx);

            // Variable Length Data
            EncoderValues = new Dictionary<Encoder, long>(NumValidEncoders);
            foreach (int i in Enumerable.Range(0, NumValidEncoders))
            {
                Encoder encoder = (Encoder)Enum.ToObject(typeof(Encoder), i);
                var encoderValue = NetworkByteUnpacker.ExtractLongFromNetworkBuffer(buf, ref idx);
                EncoderValues.Add(encoder, encoderValue);
            }

            PixelsInWindow = new Dictionary<Camera, int>(NumValidCameras);
            foreach (int i in Enumerable.Range(0, NumValidCameras))
            {
                Camera camera = (Camera)Enum.ToObject(typeof(Camera), i);
                var pixels = NetworkByteUnpacker.ExtractIntFromNetworkBuffer(buf, ref idx);
                PixelsInWindow.Add(camera, pixels);
            }

            Temperatures = new Dictionary<TemperatureSensor, float>(NumValidCameras);
            foreach (int i in Enumerable.Range(0, NumValidCameras))
            {
                var temperatureSensor = (TemperatureSensor)Enum.ToObject(typeof(TemperatureSensor), i);
                var temperature = NetworkByteUnpacker.ExtractFloatFromNetworkBuffer(buf, ref idx);
                Temperatures.Add(temperatureSensor, temperature);
            }
        }

        #endregion
    }
}

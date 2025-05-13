// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Client = joescan.schema.client;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// A complete system of <see cref="ScanHead"/>s.
    /// </summary>
    /// <remarks>
    /// This class represents a complete scan system. It contains a collection of
    /// <see cref="ScanHead"/> objects, and provides properties and methods for adding <see cref="ScanHead"/>s,
    /// accessing the <see cref="ScanHead"/>s, connecting/disconnecting to/from the <see cref="ScanHead"/>s, and
    /// starting/stopping scanning on the <see cref="ScanHead"/>s.
    /// </remarks>
    /// @ingroup Connecting
    public partial class ScanSystem : IDisposable
    {
        #region Private Fields

        private readonly ConcurrentDictionary<uint, ScanHead> idToScanHead = new ConcurrentDictionary<uint, ScanHead>();
        private BlockingCollection<IProfile>[] profileBuffers = Array.Empty<BlockingCollection<IProfile>>();
        private CancellationTokenSource keepAliveTokenSource;
        private CancellationToken keepAliveToken;
        private CancellationTokenSource heartBeatTokenSource;
        private CancellationToken heartBeatToken;
        private bool disposed;
        private uint currentSequence = 1;
        private int profilesPerFrame;
        private ScanSystemDirtyStateFlags dirtyFlags;
        private bool heartBeatRunning;

        /// <summary>
        /// The maximum number of profiles that can be queued before
        /// a frame is forcefully taken to avoid falling behind.
        /// </summary>
        private const int FrameThreshold = 50;

        /// <summary>
        /// The amount of time cameras start exposing before the laser turns on. This needs to be accounted for
        /// by both the phase table and the min scan period since they are set relative to laser on times. If ignored,
        /// a scheduler tick could happen while a camera is exposing if the scan period is set aggressively.
        /// </summary>
        private const uint CameraStartEarlyOffsetNs = 9500;

        /// <summary>
        /// The timeout value in milliseconds used for the heartbeat mechanism.
        /// </summary>
        /// <remarks>
        /// The heartbeat mechanism is used to monitor the connection status with scan heads. If a scan head
        /// does not respond within the specified timeout period, it is considered disconnected.
        /// This feature is only available for scan heads running firmware version 16.3.0 or higher.
        /// </remarks>
        private const int HeartBeatTimeoutMs = 2200;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets a value indicating whether the scan system is actively scanning.
        /// </summary>
        /// <value>
        /// A value indicating whether the scan system is actively scanning.
        /// </value>
        /// <seealso cref="StartScanning(uint, DataFormat, ScanningMode)"/>
        /// <seealso cref="StartScanning(StartScanningOptions)"/>
        public bool IsScanning => ScanHeads.Count != 0 && ScanHeads.All(s => s.IsScanning);

        /// <summary>
        /// Gets a value indicating whether all <see cref="ScanHeads"/> have established network connection.
        /// </summary>
        /// <value>
        /// A value indicating whether all <see cref="ScanHeads"/> have established network connection.
        /// </value>
        /// @ingroup Connecting
        public bool IsConnected => ScanHeads.Count != 0 && ScanHeads.All(s => s.IsConnected);

        /// <summary>
        /// Obtains the configuration state of the scan system. If `true`, the system's configuration has
        /// already been sent to the scan head via <see cref="PreSendConfiguration"/>.
        /// If `false`, the configuration will be sent when <see cref="StartScanning(uint, DataFormat, ScanningMode)"/> is called and there
        /// will be a time penalty before receiving profiles.
        /// </summary>
        /// <value>
        /// A <see cref="bool"/> value indicating whether the scan system is configured (<see langword="true"/>) or not (<see langword="false"/>).
        /// </value>
        public bool IsConfigured => ScanHeads.Count != 0 && ScanHeads.All(s => !s.IsDirty()) && !IsDirty();

        /// <summary>
        /// Gets a read-only collection of <see cref="ScanHead"/>s belonging to the scan system.
        /// </summary>
        /// <value>A <see cref="IReadOnlyCollection{T}"/> of <see cref="ScanHead"/>s belonging to the scan system.</value>
        public IReadOnlyCollection<ScanHead> ScanHeads => idToScanHead.Values.ToList().AsReadOnly();

        /// <summary>
        /// Gets the units that this scan system and all associated <see cref="ScanHead"/>s will use
        /// for configuration and returned data. Can only be set when a scan system is created.
        /// </summary>
        /// <value>The units of the scan system.</value>
        public ScanSystemUnits Units { get; }

        /// <summary>
        /// Gets the idle scan period set by <see cref="StartScanning(StartScanningOptions)"/>.
        /// </summary>
        /// <value>
        /// The idle scan period in microseconds. If the value is <see langword="null"/>,
        /// idle scanning is disabled.
        /// </value>
        /// <seealso cref="StartScanningOptions.IdlePeriodUs"/>
        public uint? IdlePeriodUs { get; private set; }

        #endregion

        #region Internal Properties

        internal Client::ConnectionType ConnectionType { get; set; }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Initializes a new instance of the scan system class.
        /// </summary>
        /// <param name="units">
        /// The units that this scan system and
        /// all associated <see cref="ScanHead"/>s will use.
        /// </param>
        public ScanSystem(ScanSystemUnits units)
        {
            if (units == ScanSystemUnits.Invalid)
            {
                ThrowArgumentException("Invalid units.", nameof(units));
            }

            ResolutionPresets.Load();
            Units = units;
            DiscoverDevices();

            FlagDirty(ScanSystemDirtyStateFlags.AllDirty);
        }

        /// <summary>
        /// Releases the managed and unmanaged resources used by the scan system.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the scan system and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">Whether being disposed explicitly (true) or due to a finalizer (false).</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                foreach (var scanHead in ScanHeads)
                {
                    scanHead?.Dispose();
                }

                StopHeartBeat();
                keepAliveTokenSource?.Cancel();
                keepAliveTokenSource?.Dispose();
                scanSyncReceiver?.Dispose();
            }

            disposed = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a <see cref="ScanHead"/> and adds it to <see cref="ScanHeads"/>.
        /// </summary>
        /// <param name="serialNumber">The serial number of the scan head.</param>
        /// <param name="id">The ID to associate with the <see cref="ScanHead"/>.</param>
        /// <returns>The created <see cref="ScanHead"/>.</returns>
        /// <remarks>
        /// scan system must not be connected. Verify <see cref="IsConnected"/>
        /// is `false` and/or call <see cref="Disconnect"/> before calling ths method.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is `true`.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is `true`.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <see cref="ScanHeads"/> already contains a <see cref="ScanHead"/> with <paramref name="serialNumber"/>.<br/>
        /// -or-<br/>
        /// <see cref="ScanHeads"/> already contains a <see cref="ScanHead"/> with <paramref name="id"/>.
        /// </exception>
        public ScanHead CreateScanHead(uint serialNumber, uint id)
        {
            if (IsConnected)
            {
                ThrowInvalidOperationException("Can not add scan head while connected.");
            }

            if (IsScanning)
            {
                ThrowInvalidOperationException("Can not add scan head while scanning.");
            }

            if (idToScanHead.Values.Any(sh => sh.SerialNumber == serialNumber))
            {
                ThrowArgumentException($"Scan head with serial number \"{serialNumber}\" is already managed.");
            }

            if (idToScanHead.ContainsKey(id))
            {
                ThrowArgumentException("ID is already assigned to another scan head.");
            }

            if (!discoveries.ContainsKey(serialNumber))
            {
                // Scan head was not found on initial discovery, try again
                DiscoverDevices();
                if (!discoveries.ContainsKey(serialNumber))
                {
                    ThrowArgumentException($"Scan head {serialNumber} cannot be found on network.");
                }
            }

            var discovery = discoveries[serialNumber];
            var scanHead = new ScanHead(this, discovery, serialNumber, id);
            idToScanHead[id] = scanHead;
            profileBuffers = idToScanHead.Values.Select(sh => sh.Profiles).ToArray();
            return scanHead;
        }

        /// <summary>
        /// Gets a <see cref="ScanHead"/> by serial number.
        /// </summary>
        /// <param name="serialNumber">The serial number of the desired <see cref="ScanHead"/>.</param>
        /// <returns>The <see cref="ScanHead"/>.</returns>
        /// <exception cref="ArgumentException"><see cref="ScanHeads"/> does not contain a <see cref="ScanHead"/>
        /// with specified <paramref name="serialNumber"/>.</exception>
        public ScanHead GetScanHeadBySerialNumber(uint serialNumber)
        {
            var scanHead = idToScanHead.Values.FirstOrDefault(sh => sh.SerialNumber == serialNumber);
            if (scanHead is null)
            {
                ThrowArgumentException($"Scan head with serial number {serialNumber} is not managed.",
                    nameof(serialNumber));
            }

            return scanHead;
        }

        /// <summary>
        /// Gets a <see cref="ScanHead"/> by ID.
        /// </summary>
        /// <param name="id">The ID of the desired <see cref="ScanHead"/>.</param>
        /// <returns>The <see cref="ScanHead"/>.</returns>
        /// <exception cref="ArgumentException"><see cref="ScanHeads"/> does not contain a <see cref="ScanHead"/>
        /// with specified <paramref name="id"/>.</exception>
        public ScanHead GetScanHeadByID(uint id)
        {
            if (!idToScanHead.ContainsKey(id))
            {
                ThrowArgumentException($"Scan head with ID {id} is not managed.", nameof(id));
            }

            return idToScanHead[id];
        }

        /// <summary>
        /// Prepares the scan system to begin scanning. If connected, this
        /// function will send all of the necessary configuration data to all of the
        /// scan heads. Provided that no changes are made to any of the scan heads
        /// associated with the scan system, the API will skip sending this data to the
        /// scan heads when calling <see cref="StartScanning(uint, DataFormat, ScanningMode)"/> and allow scanning to start faster.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>This method is invoked automatically when <see cref="Connect"/> is successful.</description></item>
        /// <item><description>If not manually called between a successful connection and scanning,
        /// it will be called automatically in <see cref="StartScanning(uint, DataFormat, ScanningMode)"/>.</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsScanning"/> is <see langword="true"/>.
        /// -or-<br/>
        /// <see cref="IsConnected"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// A loss of communication with any scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <seealso cref="IsConfigured"/>
        public void PreSendConfiguration()
        {
            if (!IsConnected)
            {
                ThrowInvalidOperationException("Can not configure scan system while disconnected.");
            }

            if (IsScanning)
            {
                ThrowInvalidOperationException("Can not configure scan system while scanning.");
            }

            if (!IsConfigured)
            {
                Parallel.ForEach(ScanHeads, sh =>
                {
                    if (!(sh is Phaser))
                    {
                        if (sh.IsDirty(ScanHeadDirtyStateFlags.Window))
                        {
                            sh.SendAllWindows();
                            sh.StoreAllAlignments();
                        }

                        if (sh.IsDirty(ScanHeadDirtyStateFlags.ExclusionMask))
                        {
                            sh.SendAllExclusionMasks();
                        }

                        if (sh.IsDirty(ScanHeadDirtyStateFlags.BrightnessCorrection))
                        {
                            sh.SendAllBrightnessCorrections();
                        }
                    }

                    if (IsDirty(ScanSystemDirtyStateFlags.ScanSyncMapping))
                    {
                        // only send if user has set a mapping (main encoder must be set)
                        if (encoderToScanSyncMapping.TryGetValue(Encoder.Main, out _))
                        {
                            sh.SendScanSyncMapping(encoderToScanSyncMapping);
                        }
                    }

                    sh.RequestStatus();
                    sh.ClearDirty();
                });

                UpdateCameraLaserConfigurations();
            }

            ClearDirty();
        }

        // TODO: wait for connect should be optional
        /// <summary>
        /// Attempts to connect to all <see cref="ScanHeads"/>s.
        /// </summary>
        /// <param name="connectTimeout">The connection timeout period.</param>
        /// <returns>A <see cref="IReadOnlyCollection{T}"/> of <see cref="ScanHead"/>s
        /// that did not successfully connect.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="ScanHeads"/> does not contain any <see cref="ScanHead"/>s.<br/>
        /// -or-<br/>
        /// <see cref="IsConnected"/> is `true`.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is `true`.<br/>
        /// -or-<br/>
        /// One or more <see cref="ScanHead"/>s were not seen on the network.
        /// </exception>
        /// <exception cref="VersionCompatibilityException">
        /// A scan head has a firmware version that is incompatible with the API.
        /// </exception>
        public IReadOnlyCollection<ScanHead> Connect(TimeSpan connectTimeout)
        {
            if (ScanHeads.Count == 0)
            {
                ThrowInvalidOperationException("No scan heads in scan system.");
            }

            if (IsConnected)
            {
                ThrowInvalidOperationException("Already connected.");
            }

            if (IsScanning)
            {
                ThrowInvalidOperationException("Already scanning.");
            }

            foreach (var sh in ScanHeads)
            {
                if (!discoveries.ContainsKey(sh.SerialNumber))
                {
                    ThrowInvalidOperationException($"Scan head {sh.SerialNumber} not seen on the network.");
                }

                var discovery = discoveries[sh.SerialNumber];
                if (!discovery.IsCompatibleWithApi())
                {
                    ThrowVersionCompatibilityException(discovery.Version);
                }

                // update IP address as it could've changed from the time the scan head object
                // was created (physical scan head was unplugged/plugged back in, scan system
                // is being loaded from JSON, etc.)
                sh.IpAddress = discovery.IpAddress;
                sh.ClientIpAddress = discovery.ClientIpAddress;
                sh.Version = discovery.Version;
            }

            Parallel.ForEach(ScanHeads, sh => sh.Connect(ConnectionType, connectTimeout));

            if (IsConnected)
            {
                foreach (var sh in ScanHeads)
                {
                    if (sh is Phaser)
                    {
                        continue;
                    }

                    var goodCameras = sh.CachedStatus.DetectedCameras;
                    string badCameras = string.Join(",", sh.Cameras.Except(goodCameras));
                    if (badCameras.Any())
                    {
                        ThrowInvalidOperationException(
                            $"Couldn't detect cameras: {badCameras}!\n" +
                            "Something might be broken internally!");
                    }
                }

                PreSendConfiguration();
            }

            if (ScanHeads.All(sh => sh.IsVersionCompatible(16, 3, 0)))
            {
                StartHeartBeat();
            }

            return ScanHeads.Where(sh => !sh.IsConnected).ToList().AsReadOnly();
        }

        /// <summary>
        /// Disconnects from all <see cref="ScanHeads"/>s.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is `false`.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is `true`.
        /// </exception>
        public void Disconnect()
        {
            if (!IsConnected)
            {
                ThrowInvalidOperationException("Attempting to disconnect when not connected.");
            }

            if (IsScanning)
            {
                ThrowInvalidOperationException("Can not disconnect while still scanning.");
            }

            StopHeartBeat();

            foreach (var s in ScanHeads)
            {
                s.Disconnect();
            }

            FlagDirty(ScanSystemDirtyStateFlags.AllDirty);
        }

        /// <summary>
        /// Starts scanning on all <see cref="ScanHeads"/>.
        /// </summary>
        /// <remarks>
        /// scan system must be connected. Verify <see cref="IsConnected"/>
        /// is `true` and/or call <see cref="Connect"/> before calling ths method.<br/>
        /// <br/>
        /// All existing <see cref="IProfile"/>s will be cleared from all <see cref="ScanHeads"/>
        /// when calling this method. Ensure that all data from the previous scan that is desired
        /// is read out before calling this method.<br/>
        /// <br/>
        /// The <paramref name="periodUs"/> is the period in which each individual scan head will
        /// generate profiles.
        /// </remarks>
        /// <param name="periodUs">The scan period in microseconds.</param>
        /// <param name="dataFormat">The <see cref="DataFormat"/>.</param>
        /// <param name="mode">The <see cref="ScanningMode"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is `false`.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is `true`.<br/>
        /// -or-<br/>
        /// There are no phases or phaseable elements in the phase table.<br/>
        /// -or-<br/>
        /// There are duplicate elements from the same scan head in the phase table.<br/>
        /// -or-<br/>
        /// A loss of communication with any scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Requested scan period <paramref name="periodUs"/> is invalid.
        /// </exception>
        /// <exception cref="VersionCompatibilityException">
        /// A scan head has a firmware version that is incompatible with frame
        /// scanning (when <paramref name="mode"/> is <see cref="ScanningMode.Frame"/>).
        /// </exception>
        /// <seealso cref="StartScanning(StartScanningOptions)"/>
        /// <seealso cref="StopScanning"/>
        public void StartScanning(uint periodUs, DataFormat dataFormat, ScanningMode mode = ScanningMode.Profile)
        {
            var opts = new StartScanningOptions
            {
                PeriodUs = periodUs,
                Format = dataFormat,
                Mode = mode,
            };

            StartScanning(opts);
        }

        /// <summary>
        /// Starts scanning on all <see cref="ScanHead"/>s.
        /// </summary>
        /// <remarks>
        /// All existing <see cref="IProfile"/>s and <see cref="IFrame"/>s will be cleared from all <see cref="ScanHead"/>s
        /// when calling this method. Ensure that all data from the previous scan that is desired
        /// is read out before calling this method.
        /// </remarks>
        /// <param name="options">The scan options.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is <see langword="false"/>.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is <see langword="true"/>.<br/>
        /// -or-<br/>
        /// There are no phases or phaseable elements in the phase table.<br/>
        /// -or-<br/>
        /// There are duplicate elements from the same scan head in the phase table.<br/>
        /// -or-<br/>
        /// A loss of communication with any scan head occurred, usually caused by a network or power issue.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Requested scan period <see cref="StartScanningOptions.PeriodUs"/> is invalid.
        /// </exception>
        /// <exception cref="VersionCompatibilityException">
        /// A <see cref="ScanHead"/> has a <see cref="ScanHead.Version"/> that is incompatible with frame
        /// scanning (when <see cref="StartScanningOptions.Mode"/> is <see cref="ScanningMode.Frame"/>).
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="options"/> is <see langword="null"/>.
        /// </exception>
        /// <seealso cref="StartScanning(uint, DataFormat, ScanningMode)"/>
        /// <seealso cref="StopScanning"/>
        public void StartScanning(StartScanningOptions options)
        {
            if (options == null)
            {
                ThrowArgumentNullException(nameof(options));
            }

            if (!IsConnected)
            {
                ThrowInvalidOperationException("Attempting to start scanning when not connected.");
            }

            if (IsScanning)
            {
                ThrowInvalidOperationException("Attempting to start scanning while already scanning.");
            }

            if (NumberOfPhases == 0 || phaseTable.Any(p => p.Elements.Count == 0))
            {
                ThrowInvalidOperationException("Cannot start scanning without a phase table.");
            }

            IdlePeriodUs = options.IdlePeriodUs;

            PreSendConfiguration();

            long minScanPeriodUs = GetMinScanPeriod();

            if (options.PeriodUs < minScanPeriodUs)
            {
                ThrowArgumentOutOfRangeException(
                    "Scan period is smaller than the minimum allowed for the system. " +
                    $"Requested {options.PeriodUs}µs but minimum is {minScanPeriodUs}µs.");
            }

            if (options.Mode == ScanningMode.Frame)
            {
                foreach (var sh in ScanHeads)
                {
                    if (!sh.IsVersionCompatible(16, 2, 0))
                    {
                        ThrowVersionCompatibilityException("Frame scanning is only compatible with scan heads on firmware version 16.2.0 or greater.");
                    }
                }

                var phaseElements = phaseTable.SelectMany(p => p.Elements);
                foreach (var group in phaseElements.GroupBy(p => p.ScanHead))
                {
                    var scanHead = group.Key;
                    var validElements = group.Select(g => new CameraLaserPair(scanHead, g.CameraPort, g.LaserPort));
                    scanHead.QueueManager.SetValidCameraLaserPairs(validElements);
                }

                profilesPerFrame = ScanHeads.Sum(sh => sh.QueueManager.NumQueues);

                bool hasDuplicatePhaseElements = phaseTable.SelectMany(p => p.Elements)
                                                 .GroupBy(e => new { e.ScanHead.ID, e.LaserPort, e.CameraPort })
                                                 .Any(g => g.Count() > 1);
                if (hasDuplicatePhaseElements)
                {
                    ThrowInvalidOperationException("Duplicate element in phase table.");
                }

                currentSequence = 1;
            }

            // The API sets the time to start scanning to avoid a rollover bug that
            // can occur within the firmware. This value was picked arbitrarily and
            // tested to make sure it always sets a time in the future. If there is
            // no encoder, the time will be set to 0 which will cause the scan heads
            // to determine their start time independently.
            const ulong startScanningOffsetNs = 22_000_000;
            ulong lastTimestampNs = 0;

            // If any heads have firwmare lower than 16.3.0, we need to get the main ScanSync encoder
            // timestamp the old way. This is due to the ScanSync mapping feature requiring a new TCP
            // message,which is not available in older firmware.
            if (ScanHeads.Any(sh => !sh.IsVersionCompatible(16, 3, 0)))
            {
                // Get the ScanSyncs found on the network
                var activeScanSyncs = scanSyncReceiver.GetScanSyncs();

                // If no ScanSyncs are found, we are not going to add anything to the offset
                if (activeScanSyncs.Count != 0)
                {
                    // If two encoders are present on the network, the lowest
                    // serial number is considered to be the "main" one
                    // and the other one is considered to be the "auxiliary"
                    var mainScanSync = activeScanSyncs.OrderBy(s => s.Key).First();
                    var scanSyncData = mainScanSync.Value;
                    lastTimestampNs = scanSyncData.EncoderTimestampNs;
                }
            }
            else
            {
                var mapping = GetScanSyncMapping();

                // if there is a main ScanSync, get the most recent timestamp from it
                if (mapping.Count > 0)
                {
                    uint mainScanSyncSerial = mapping.First().Value;
                    if (scanSyncReceiver.TryGetScanSyncData(mainScanSyncSerial, out var data))
                    {
                        lastTimestampNs = data.EncoderTimestampNs;
                    }
                    else
                    {
                        ThrowInvalidOperationException($"ScanSync {mainScanSyncSerial} is not found on the network.");
                    }
                }
            }

            options.StartScanningTimeNs = lastTimestampNs != 0 ? lastTimestampNs + startScanningOffsetNs : 0;
            Parallel.ForEach(ScanHeads, scanHead => scanHead.StartScanning(options));

            keepAliveTokenSource = new CancellationTokenSource();
            keepAliveToken = keepAliveTokenSource.Token;

            // If any scan head is not compatible with the heartbeat mechanism,
            // start the keep alive loop.
            // Otherwise, the heartbeat message will be used for client-side
            // connection monitoring and scan-server keep alive during scanning.
            if (ScanHeads.Any(sh => !sh.IsVersionCompatible(16, 3, 0)))
            {
                Task.Run(KeepAliveLoop, keepAliveToken);
            }
        }

        /// <summary>
        /// Stops scanning on all <see cref="ScanHead"/>s.
        /// </summary>
        /// <remarks>
        /// Physical scan heads will take approximately 0.5-1.0 seconds to stop scanning after <see cref="StopScanning"/>
        /// is called. <see cref="IProfile"/>s will remain in the profile buffers until they are either consumed
        /// or <see cref="StartScanning(uint, DataFormat, ScanningMode)"/> is called.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsScanning"/> is `false`.
        /// </exception>
        public void StopScanning()
        {
            if (!IsScanning)
            {
                ThrowInvalidOperationException("Attempting to stop scanning when not scanning.");
            }

            Parallel.ForEach(ScanHeads, scanHead => scanHead.StopScanning());
            keepAliveTokenSource.Cancel();
        }

        /// <summary>
        /// Gets the minimum scan period allowed by the scan system in microseconds.
        /// </summary>
        /// <returns>The minimum scan period allowed by the scan system in microseconds.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is `false`.<br/>
        /// -or-<br/>
        /// The phase table is empty.<br/>
        /// -or-<br/>
        /// A loss of communication with any scan head occurred, usually caused by a network or power issue.
        /// </exception>
        public uint GetMinScanPeriod()
        {
            if (!IsConnected)
            {
                ThrowInvalidOperationException("Not connected.");
            }

            if (phaseTable.Count == 0 || phaseTable.All(p => p.Elements.Count == 0))
            {
                ThrowInvalidOperationException("Cannot request minimum scan period without creating a phase table.");
            }

            // ensure configuration is sent to scan head so the min scan period is accurate
            PreSendConfiguration();

            long phaseTableDurationNs = CalculatePhaseDurations().Sum(d => d);
            return (uint)Math.Ceiling(phaseTableDurationNs / 1000.0);
        }

        /// <summary>
        /// Tries to take an <see cref="IProfile"/> from any profile queue in <see cref="ScanHeads"/>,
        /// blocking if all the queues are empty.
        /// </summary>
        /// <param name="profile">The dequeued <see cref="IProfile"/>.</param>
        /// <param name="token">Token to observe.</param>
        /// <returns><see langword="true"/> if a profile was taken else <see langword="false"/>.</returns>
        /// <remarks>The profile queues are not guarenteed to be taken from equally or in any order.</remarks>
        public bool TakeAnyProfile(out IProfile profile, CancellationToken token = default)
        {
            // returns the index of the item taken or -1 if no item was taken
            int idx = BlockingCollection<IProfile>.TakeFromAny(profileBuffers, out profile, token);
            return idx != -1;
        }

        /// <summary>
        /// Tries to take an <see cref="IProfile"/> from any profile queue in <see cref="ScanHeads"/>.
        /// </summary>
        /// <param name="profile">The dequeued <see cref="IProfile"/>.</param>
        /// <param name="timeout">The time to wait for a profile when all the queues are empty.</param>
        /// <param name="token">Token to observe.</param>
        /// <returns><see langword="true"/> if a profile was taken else <see langword="false"/>.</returns>
        /// <remarks>The profile queues are not guarenteed to be taken from equally or in any order.</remarks>
        public bool TryTakeAnyProfile(out IProfile profile, TimeSpan timeout = default, CancellationToken token = default)
        {
            // returns the index of the item taken or -1 if no item was taken
            int idx = BlockingCollection<IProfile>.TryTakeFromAny(profileBuffers, out profile, timeout.Milliseconds, token);
            return idx != -1;
        }

        /// <summary>
        /// Tries to take an <see cref="IFrame"/>, blocking if one isn't ready.
        /// </summary>
        /// <param name="token">The <see cref="CancellationToken"/> to observe.</param>
        /// <returns>The dequeued <see cref="IFrame"/>.</returns>
        /// <exception cref="OperationCanceledException">
        /// <paramref name="token"/> gets canceled.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="ScanHead.IsConnected"/> is <see langword="false"/> for any <see cref="ScanHead"/> when a
        /// timeout occurs while waiting for a frame. This indicates a loss of communication with the scan head, either
        /// by a possible network or power issue.
        /// </exception>
        public IFrame TakeFrame(CancellationToken token = default)
        {
            TryTakeFrame(out var frame, TimeSpan.MaxValue, token);

            // "Take" methods canonically throw an exception when a
            // token is canceled rather than gracefully returning
            if (token.IsCancellationRequested)
            {
                ThrowOperationCanceledException(token);
            }

            return frame;
        }

        /// <summary>
        /// Tries to take an <see cref="IFrame"/>.
        /// </summary>
        /// <param name="frame">The dequeued <see cref="IFrame"/> or <see langword="null"/>.</param>
        /// <param name="timeout">Time to wait for an <see cref="IFrame"/> to be taken.</param>
        /// <param name="token">The <see cref="CancellationToken"/> to observe.</param>
        /// <returns>
        /// <see langword="true"/> if an <see cref="IFrame"/> was successfully taken,
        /// otherwise <see langword="false"/> if <paramref name="timeout"/> elapsed or
        /// <paramref name="token"/> was canceled.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="ScanHead.IsConnected"/> is <see langword="false"/> for any <see cref="ScanHead"/> when a
        /// timeout occurs while waiting for a frame. This indicates a loss of communication with the scan head, either
        /// by a possible network or power issue.
        /// </exception>
        public bool TryTakeFrame(out IFrame frame, TimeSpan timeout = default, CancellationToken token = default)
        {
            if (!WaitForFrame(timeout, token))
            {
                frame = null;
                return false;
            }

            frame = new Frame(currentSequence, profilesPerFrame);
            var profileSpan = (frame as Frame).ProfileSpan;

            bool isComplete = true;
            int idx = 0;
            foreach (var sh in ScanHeads)
            {
                int len = sh.QueueManager.NumQueues;
                var slots = profileSpan.Slice(idx, len);
                isComplete &= sh.QueueManager.Dequeue(slots, currentSequence);
                if (sh.Orientation == ScanHeadOrientation.CableIsDownstream)
                {
                    slots.Reverse();
                }

                idx += len;
            }

            (frame as Frame).IsComplete = isComplete;

            currentSequence++;
            return true;
        }

        /// <summary>
        /// Empties the internal client side software buffers used to store frames.
        /// <para>
        /// Under normal scanning conditions where the application consumes frames as they become available,
        /// this function will not be needed. Its use is to be found in cases where the application fails to
        /// consume frames after some time and the number of buffered frames becomes more than the application
        /// can consume and only the most recent scan data is desired.
        /// </para>
        /// <para>
        /// When operating in profile scanning mode, use <see cref="ScanHead.ClearProfiles"/> instead.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Frames are automatically cleared when <see cref="StartScanning(uint, DataFormat, ScanningMode)"/> is called.
        /// </remarks>
        public void ClearFrames()
        {
            foreach (var sh in ScanHeads)
            {
                sh.QueueManager.Clear();
            }
        }

        #endregion

        #region Internal Methods

        internal void StartScanningSubpixel(uint periodUs, SubpixelDataFormat dataFormat)
        {
            var opts = new StartScanningOptions
            {
                PeriodUs = periodUs,
                Mode = ScanningMode.Profile,
                AllFormat = (AllDataFormat)dataFormat,
            };

            StartScanning(opts);
        }

        /// <summary>
        /// Removes a <see cref="ScanHead"/> object from use by reference.
        /// </summary>
        /// <param name="scanHead">An object reference to the scan head to remove.</param>
        internal void RemoveScanHead(ScanHead scanHead)
        {
            if (IsConnected)
            {
                ThrowInvalidOperationException("Can not remove scan head while connected.");
            }

            if (IsScanning)
            {
                ThrowInvalidOperationException("Can not remove scan head while scanning.");
            }

            if (!idToScanHead.ContainsKey(scanHead.ID))
            {
                ThrowArgumentException("Scan head is not managed.");
            }

            if (idToScanHead.TryRemove(scanHead.ID, out scanHead))
            {
                scanHead?.Dispose();
            }
            else
            {
                ThrowInvalidOperationException("Failed to remove scan head.");
            }

            profileBuffers = idToScanHead.Values.Select(sh => sh.Profiles).ToArray();
            ClearPhaseTable();
        }

        /// <summary>
        /// Removes all created <see cref="ScanHead"/> objects from use.
        /// </summary>
        internal void RemoveAllScanHeads()
        {
            if (IsConnected)
            {
                ThrowInvalidOperationException("Can not remove scan head while connected.");
            }

            if (IsScanning)
            {
                ThrowInvalidOperationException("Can not remove scan head while scanning.");
            }

            foreach (var scanHead in ScanHeads)
            {
                scanHead?.Dispose();
            }

            idToScanHead.Clear();
            profileBuffers = default;
            ClearPhaseTable();
        }

        internal void StopSystem()
        {
            keepAliveTokenSource?.Cancel();

            foreach (var sh in ScanHeads)
            {
                try { sh.StopScanning(); } catch { }
                try { sh.Disconnect(); } catch { }
            }
        }

        #endregion

        #region Private Methods

        private bool WaitForFrame(TimeSpan timeout, CancellationToken token = default)
        {
            var start = DateTime.Now;
            while (!token.IsCancellationRequested)
            {
                uint minSeq = uint.MaxValue;
                int maxSize = 0;

                foreach (var sh in ScanHeads)
                {
                    if (sh is Phaser phaser )
                    {
                        bool skipSequence = true;
                        foreach (var strobe in phaser.StrobeConfigurations)
                        {
                            if (currentSequence % strobe.Value.FramesPerStrobe == 0)
                            {
                                skipSequence = false;
                            }
                        }
                        if (skipSequence)
                        {
                            continue;
                        }
                    }
                    

                    var stats = sh.QueueManager.GetStats();
                    if (minSeq > stats.MinSeq) { minSeq = stats.MinSeq; }
                    if (maxSize < stats.MaxSize) { maxSize = stats.MaxSize; }

                    // if queue is empty, check to see if the scan head is still connected
                    if (stats.MaxSize == 0 && !sh.IsConnected)
                    {
                        StopSystem();
                        ThrowInvalidOperationException($"Scan head {sh.SerialNumber} is not connected, possible network or power error.");
                    }
                }

                if (minSeq >= currentSequence || maxSize >= FrameThreshold)
                {
                    // frame is ready, update the sequence number in case we
                    // fell behind and need to catch up but it should almost
                    // always just be the next monotonic number
                    currentSequence = minSeq;
                    return true;
                }

                if (DateTime.Now - start > timeout)
                {
                    // timeout
                    return false;
                }

                // the actual time slept for depends on the system clock
                // which typically has a resolution of around 10-15 ms
                Thread.Sleep(10);
            }

            return false;
        }

        /// <summary>
        /// Gets the IP of every IPv4 NIC on the host machine
        /// </summary>
        private static IEnumerable<IPAddress> GetNicIpAddresses()
        {
            var ips = new List<IPAddress>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                var props = ni.GetIPProperties();
                try
                {
                    var interfaceProperties = props.GetIPv4Properties();
                    if (NetworkInterface.LoopbackInterfaceIndex == interfaceProperties.Index)
                    {
                        continue;
                    }
                }
                catch (NetworkInformationException)
                {
                    // see issue #2399
                    // the exception is thrown in .NET9 and up, when the interface does not have an IPv4 address.
                    // we can ignore this and continue to the next interface.
                   continue;
                }

                foreach (var addrInfo in props.UnicastAddresses)
                {
                    if (addrInfo.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    if (!ips.Contains(addrInfo.Address))
                    {
                        ips.Add(addrInfo.Address);
                    }
                }
            }

            return ips;
        }

        /// <summary>
        /// Sends periodic keep alive messages to the scan head. Should only be spun up
        /// when scanning, and should be killed (cancel token) when scanning stops.
        /// </summary>
        private async Task KeepAliveLoop()
        {
            // The server will keep itself scanning as long as it can send profile data
            // over TCP. This keep alive is really only needed to get scan head's to
            // recover in the event that they fail to send and go into idle state.
            const int keepAliveIntervalMs = 1000;

            try
            {
                while (IsScanning)
                {
                    await Task.Delay(keepAliveIntervalMs, keepAliveToken).ConfigureAwait(false);
                    foreach (var scanHead in ScanHeads)
                    {
                        scanHead.KeepAlive();
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Initializes and starts the heartbeat mechanism for all connected <see cref="ScanHead"/>s.
        /// </summary>
        /// <remarks>
        /// The heartbeat feature is only available for scan heads running firmware version 16.3.0 or higher.
        /// </remarks>
        private void StartHeartBeat()
        {
            if (ScanHeads.All(sh => sh.IsVersionCompatible(16, 3, 0)))
            {
                StopHeartBeat();
                heartBeatTokenSource = new CancellationTokenSource();
                heartBeatToken = heartBeatTokenSource.Token;
                Parallel.ForEach(ScanHeads, scanHead =>
                {
                    if (scanHead.IsConnected)
                    {
                        scanHead.SetHeartBeatTimeout(HeartBeatTimeoutMs);
                    }
                });

                Task.Run(HeartBeatLoop, heartBeatToken);
                heartBeatRunning = true;
            }
        }

        /// <summary>
        /// Stops the heartbeat mechanism for all connected <see cref="ScanHead"/>s.
        /// </summary>
        /// <remarks>
        /// The heartbeat feature is only available for scan heads running firmware version 16.3.0 or higher.
        /// </remarks>
        private void StopHeartBeat()
        {
            if (!heartBeatRunning)
            {
                return;
            }

            if (ScanHeads.All(sh => sh.IsVersionCompatible(16, 3, 0)))
            {
                heartBeatTokenSource?.Cancel();
                heartBeatTokenSource?.Dispose();
                Parallel.ForEach(ScanHeads, scanHead => scanHead.SetHeartBeatTimeout(0));
                heartBeatRunning = false;
            }
        }

        /// <summary>
        /// Periodically sends heartbeat requests to all connected <see cref="ScanHead"/>s to ensure they remain responsive.
        /// </summary>
        /// <remarks>
        /// This method runs in a loop, sending heartbeat requests at regular intervals defined by <see cref="SendIntervalMs"/>.
        /// The actual timeout is set in <see cref="ScanHead.SetHeartBeatTimeout"/>.
        /// The loop continues as long as the scan system is connected. If the operation is canceled, the loop exits gracefully.
        /// If there is an IOException, it's safe to assume IsConnected will be false since the socket will have been closed.
        /// </remarks>
        private async Task HeartBeatLoop()
        {
            const int sendIntervalMs = 250;
            try
            {
                while (IsConnected)
                {
                    // This delay is used to control the frequency of heartbeat requests.
                    await Task.Delay(sendIntervalMs, heartBeatToken).ConfigureAwait(false);

                    foreach (var scanHead in ScanHeads)
                    {
                        try
                        {
                            // This call will perform a TCP send, followed by a TCP read
                            // to check if the scan head is still alive. It is a blocking call,
                            // hence the previous throttle using sendIntervalMs.
                            scanHead.GetHeartBeat();
                        }
                        catch (IOException)
                        {
                            // Individual scan head failure should not cause the entire system to fail.
                            // If a scan head fails, it will be marked as disconnected and a separate 
                            // exception will be thrown from that ScanHead instance
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // ignored
            }
        }

        private void UpdateCameraLaserConfigurations()
        {
            foreach (var sh in ScanHeads)
            {
                sh.CameraLaserConfigurations.Clear();
            }

            uint currPhaseEndOffsetNs = 0;
            var durationsNs = CalculatePhaseDurations();

            foreach ((var phase, int phaseNumber) in phaseTable.Select((p, it) => (p, it)))
            {
                currPhaseEndOffsetNs += durationsNs[phaseNumber];

                foreach (var element in phase.Elements)
                {
                    var scanHead = element.ScanHead;

                    uint maxGroups = scanHead.Specification.MaxConfigurationGroups;
                    if (scanHead.CameraLaserConfigurations.Count >= maxGroups)
                    {
                        ThrowInvalidOperationException($"Scan head {scanHead.ID} cannot have more than {maxGroups} camera laser configurations.");
                    }

                    var camera = scanHead.CameraPortToId(element.CameraPort);
                    var laser = scanHead.LaserPortToId(element.LaserPort);
                    var pair = new CameraLaserPair(camera, laser);
                    var conf = element.Configuration ?? scanHead.Configuration;

                    // TODO: Create a new configuration object for strobes
                    var clc = new Client::CameraLaserConfigurationT
                    {
                        CameraPort = element.CameraPort,
                        LaserPort = element.LaserPort,
                        LaserOnTimeMinNs = element.IsStrobe ? element.StrobeDurationNs : conf.MinLaserOnTimeUs * 1000,
                        LaserOnTimeDefNs = element.IsStrobe ? element.StrobeDurationNs : conf.DefaultLaserOnTimeUs * 1000,
                        LaserOnTimeMaxNs = element.IsStrobe ? element.StrobeDurationNs : conf.MaxLaserOnTimeUs * 1000,
                        ScanEndOffsetNs = currPhaseEndOffsetNs,
                        CameraOrientation = scanHead.GetCameraOrientation(pair)
                    };

                    scanHead.CameraLaserConfigurations.Add(clc);
                }
            }
        }

        private bool IsDirty()
        {
            return dirtyFlags != ScanSystemDirtyStateFlags.Clean;
        }

        private bool IsDirty(ScanSystemDirtyStateFlags flag)
        {
            return dirtyFlags.HasFlag(flag);
        }

        private void FlagDirty(ScanSystemDirtyStateFlags flag)
        {
            if (flag.Equals(ScanSystemDirtyStateFlags.Clean))
            {
                return;
            }

            dirtyFlags |= flag;
        }

        private void ClearDirty()
        {
            dirtyFlags = ScanSystemDirtyStateFlags.Clean;
        }

        private void ThrowArgumentException(string exceptionMessage, string paramName = null)
        {
            StopHeartBeat();
            throw new ArgumentException(exceptionMessage, paramName);
        }

        private void ThrowArgumentNullException(string exceptionMessage)
        {
            StopHeartBeat();
            throw new ArgumentNullException(exceptionMessage);
        }

        private void ThrowInvalidOperationException(string exceptionMessage)
        {
            StopHeartBeat();
            throw new InvalidOperationException(exceptionMessage);
        }

        private void ThrowVersionCompatibilityException(string exceptionMessage)
        {
            StopHeartBeat();
            throw new VersionCompatibilityException(exceptionMessage);
        }

        private void ThrowVersionCompatibilityException(ScanHeadVersionInformation version)
        {
            StopHeartBeat();
            throw new VersionCompatibilityException(version);
        }

        private void ThrowArgumentOutOfRangeException(string exceptionMessage, string paramName = null)
        {
            StopHeartBeat();
            throw new ArgumentOutOfRangeException(paramName, exceptionMessage);
        }

        private void ThrowOperationCanceledException(CancellationToken token)
        {
            StopHeartBeat();
            throw new OperationCanceledException(token);
        }
        #endregion
    }
}

// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
    public partial class ScanSystem : IDisposable
    {
        #region Private Fields

        private readonly ConcurrentDictionary<uint, ScanHead> idToScanHead = new ConcurrentDictionary<uint, ScanHead>();
        private Thread statsThread;
        private readonly Stopwatch timeBase = Stopwatch.StartNew();
        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken token;
        private CancellationTokenSource keepAliveTokenSource;
        private CancellationToken keepAliveToken;
        private readonly ConcurrentDictionary<uint, CommStatsEventArgs> previousCommStats = new ConcurrentDictionary<uint, CommStatsEventArgs>();
        private bool disposed;

        /// <summary>
        /// The amount of time cameras start exposing before the laser turns on. This needs to be accounted for
        /// by both the phase table and the min scan period since they are set relative to laser on times. If ignored,
        /// a scheduler tick could happen while a camera is exposing if the scan period is set aggressively.
        /// </summary>
        private const uint CameraStartEarlyOffsetNs = 9500;

        #endregion

        #region Events

        internal event EventHandler<CommStatsEventArgs> StatsEvent;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets a value indicating whether the scan system is actively scanning.
        /// </summary>
        /// <value>
        /// A value indicating whether the scan system is actively scanning.
        /// </value>
        public bool IsScanning { get; private set; }

        /// <summary>
        /// Gets a value indicating whether all <see cref="ScanHeads"/> have established network connection.
        /// </summary>
        /// <value>
        /// A value indicating whether all <see cref="ScanHeads"/> have established network connection.
        /// </value>
        public bool IsConnected => EnabledHeads.Any() && EnabledHeads.All(s => s.IsConnected);

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

        #endregion

        #region Internal Properties

        internal Client::ConnectionType ConnectionType { get; set; }

        internal IEnumerable<ScanHead> EnabledHeads => ScanHeads.Where(sh => sh.Enabled);

        /// <summary>
        /// Aggregated network communication statistics for all <see cref="Pinchot.ScanHead"/> objects.
        /// </summary>
        internal CommStatsEventArgs CommStats { get; private set; } = new CommStatsEventArgs();

        internal bool CommStatsEnabled;

        internal double EncoderPulseInterval { get; set; }

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
            EncoderPulseInterval = 1.0;
            Units = units;
            DiscoverDevices();
        }

        /// <nodoc/>
        ~ScanSystem()
        {
            Dispose(false);
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

                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
                keepAliveTokenSource?.Cancel();
                keepAliveTokenSource?.Dispose();
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
                throw new InvalidOperationException("Can not add scan head while connected.");
            }

            if (IsScanning)
            {
                throw new InvalidOperationException("Can not add scan head while scanning.");
            }

            if (idToScanHead.Values.Any(sh => sh.SerialNumber == serialNumber))
            {
                throw new ArgumentException($"Scan head with serial number \"{serialNumber}\" is already managed.");
            }

            if (idToScanHead.ContainsKey(id))
            {
                throw new ArgumentException("ID is already assigned to another scan head.");
            }

            if (!discoveries.ContainsKey(serialNumber))
            {
                // Scan head was not found on initial discovery, try again
                DiscoverDevices();
                if (!discoveries.ContainsKey(serialNumber))
                {
                    throw new ArgumentException($"Scan head {serialNumber} cannot be found on network.");
                }
            }

            var discovery = discoveries[serialNumber];
            var scanHead = new ScanHead(this, discovery, serialNumber, id);
            idToScanHead[id] = scanHead;
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
                throw new ArgumentException($"Scan head with serial number {serialNumber} is not managed.",
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
                throw new ArgumentException($"Scan head with ID {id} is not managed.", nameof(id));
            }

            return idToScanHead[id];
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
        /// <see cref="ScanHeads"/> does not contain any enabled <see cref="ScanHead"/>s.<br/>
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
                throw new InvalidOperationException("No scan heads in scan system.");
            }

            if (!EnabledHeads.Any())
            {
                throw new InvalidOperationException("No scan heads are enabled.");
            }

            if (IsConnected)
            {
                throw new InvalidOperationException("Already connected.");
            }

            if (IsScanning)
            {
                throw new InvalidOperationException("Already scanning.");
            }

            foreach (var sh in EnabledHeads)
            {
                if (!discoveries.ContainsKey(sh.SerialNumber))
                {
                    throw new InvalidOperationException($"Scan head {sh.SerialNumber} not seen on the network.");
                }

                var discovery = discoveries[sh.SerialNumber];
                if (!discovery.IsCompatibleWithApi())
                {
                    throw new VersionCompatibilityException(discovery.Version);
                }

                // update IP address as it could've changed from the time the scan head object
                // was created (physical scan head was unplugged/plugged back in, scan system
                // is being loaded from JSON, etc.)
                sh.IpAddress = discovery.IpAddress;
                sh.ClientIpAddress = discovery.ClientIpAddress;
                sh.Version = discovery.Version;
            }

            Parallel.ForEach(EnabledHeads, sh =>
            {
                sh.Connect(ConnectionType, connectTimeout);
                sh.StatsEvent += UpdateCommStats;
            });

            if (IsConnected)
            {
                Parallel.ForEach(EnabledHeads, sh =>
                {
                    if (sh.IsVersionCompatible(16, 1, 0))
                    {
                        sh.SendAllExclusionMasks();
                        sh.SendAllBrightnessCorrections();
                    }

                    sh.SendAllWindows();
                    sh.RequestStatus();
                });

                foreach (var sh in EnabledHeads)
                {
                    if (sh.CachedStatus.NumValidCameras != sh.Cameras.Count())
                    {
                        var goodCameras = sh.CachedStatus.PixelsInWindow.Keys;
                        string badCameras = string.Join(",", sh.Cameras.Except(goodCameras));
                        throw new InvalidOperationException(
                            $"Couldn't detect cameras: {badCameras}!\n" +
                            "Something might be broken internally!");
                    }
                }

                cancellationTokenSource = new CancellationTokenSource();
                token = cancellationTokenSource.Token;
                if (CommStatsEnabled)
                {
                    CommStats = new CommStatsEventArgs();
                    previousCommStats.Clear();
                    statsThread = new Thread(StatsThread) { Priority = ThreadPriority.Lowest, IsBackground = true };
                    statsThread.Start();
                }
            }

            return EnabledHeads.Where(sh => !sh.IsConnected).ToList().AsReadOnly();
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
                throw new InvalidOperationException("Attempting to disconnect when not connected.");
            }

            if (IsScanning)
            {
                throw new InvalidOperationException("Can not disconnect while still scanning.");
            }

            foreach (var s in EnabledHeads)
            {
                s.Disconnect();
            }
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
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is `false`.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is `true`.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Requested scan period <paramref name="periodUs"/> is invalid.
        /// </exception>
        public void StartScanning(uint periodUs, DataFormat dataFormat)
        {
            StartScanning(periodUs, (AllDataFormat)dataFormat);
        }

        /// <summary>
        /// Stops scanning on all <see cref="ScanHeads"/>.
        /// </summary>
        /// <remarks>
        /// Physical scan heads will take approximately 0.5-1.0 seconds to stop scanning after <see cref="StopScanning"/>
        /// is called. <see cref="IProfile"/>s will remain in the profile buffers until they are either consumed
        /// or <see cref="StartScanning(uint, DataFormat)"/> is called.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsScanning"/> is `false`.
        /// </exception>
        public void StopScanning()
        {
            if (!IsScanning)
            {
                throw new InvalidOperationException("Attempting to stop scanning when not scanning.");
            }

            foreach (var scanHead in EnabledHeads)
            {
                scanHead.StopScanning();
            }

            IsScanning = false;
            keepAliveTokenSource.Cancel();
        }

        /// <summary>
        /// Gets the minimum scan period allowed by the scan system in microseconds.
        /// </summary>
        /// <returns>The minimum scan period allowed by the scan system in microseconds.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is `false`.<br/>
        /// -or-<br/>
        /// The phase table is empty.
        /// </exception>
        public uint GetMinScanPeriod()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected.");
            }

            if (phaseTable.Count == 0 || phaseTable.All(p => p.Elements.Count == 0))
            {
                throw new InvalidOperationException("Cannot request minimum scan period without creating a phase table.");
            }

            // user can send scan window after connecting now so we need to check the
            // scan head to see what the min period is
            Parallel.ForEach(EnabledHeads, sh =>
            {
                sh.RequestStatus();
            });

            uint phaseTableDurationUs = (uint)CalculatePhaseDurations().Sum(d => d);
            uint cameraOffsetUs = (uint)Math.Ceiling(CameraStartEarlyOffsetNs / 1000.0);

            return phaseTableDurationUs + cameraOffsetUs;
        }

        #endregion

        #region Internal Methods

        internal void StartScanningSubpixel(uint periodUs, SubpixelDataFormat dataFormat)
        {
            StartScanning(periodUs, (AllDataFormat)dataFormat);
        }

        /// <summary>
        /// Removes a <see cref="ScanHead"/> object from use by reference.
        /// </summary>
        /// <param name="scanHead">An object reference to the scan head to remove.</param>
        internal void RemoveScanHead(ScanHead scanHead)
        {
            if (IsConnected)
            {
                throw new InvalidOperationException("Can not remove scan head while connected.");
            }

            if (IsScanning)
            {
                throw new InvalidOperationException("Can not remove scan head while scanning.");
            }

            if (!idToScanHead.ContainsKey(scanHead.ID))
            {
                throw new ArgumentException("Scan head is not managed.");
            }

            if (idToScanHead.TryRemove(scanHead.ID, out scanHead))
            {
                scanHead?.Dispose();
            }
            else
            {
                throw new InvalidOperationException("Failed to remove scan head.");
            }

            ClearPhaseTable();
        }

        /// <summary>
        /// Removes all created <see cref="ScanHead"/> objects from use.
        /// </summary>
        internal void RemoveAllScanHeads()
        {
            if (IsConnected)
            {
                throw new InvalidOperationException("Can not remove scan head while connected.");
            }

            if (IsScanning)
            {
                throw new InvalidOperationException("Can not remove scan head while scanning.");
            }

            foreach (var scanHead in EnabledHeads)
            {
                scanHead?.Dispose();
            }

            idToScanHead.Clear();
            ClearPhaseTable();
        }

        /// <summary>
        /// Iterates through all <see cref="ScanHead" /> objects that were told to scan through
        /// the <see cref="StartScanning(uint, DataFormat)"/> method and tries to remove the first available profile
        /// while observing a cancellation token.
        /// </summary>
        /// <param name="profile">The profile to be removed from the collection.</param>
        /// <param name="token">Cancellation token to observe.</param>
        /// <returns><c>true</c> if a profile was successfully taken, <c>false</c> otherwise.</returns>
        internal bool TryTakeNextProfile(out IProfile profile, CancellationToken token)
        {
            return TryTakeNextProfile(out profile, TimeSpan.FromMilliseconds(-1), token);
        }

        /// <summary>
        /// Iterates through all <see cref="ScanHead" /> objects that were told to scan through
        /// the <see cref="StartScanning(uint, DataFormat)"/> method and tries to remove the first available profile
        /// in the specified time period while observing a cancellation token.
        /// </summary>
        /// <param name="profile">The profile to be removed from the collection.</param>
        /// <param name="timeout">An object that represents the number of milliseconds to wait,
        /// or an object that represents -1 milliseconds to wait indefinitely.</param>
        /// <param name="token">Cancellation token to observe.</param>
        /// <returns><c>true</c> if a profile was successfully taken, <c>false</c> otherwise.</returns>
        internal bool TryTakeNextProfile(out IProfile profile, TimeSpan timeout, CancellationToken token)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            profile = new Profile();
            while (!token.IsCancellationRequested)
            {
                // TODO: this should be improved to ensure all scan heads are taken from
                foreach (var scanHead in EnabledHeads)
                {
                    if (scanHead.Profiles.TryTake(out profile, 0, token))
                    {
                        return true;
                    }
                }

                if (!timeout.Equals(TimeSpan.FromMilliseconds(-1)) && (stopwatch.Elapsed > timeout))
                {
                    return false;
                }
            }

            return false;
        }

        internal void SaveScanHeads(FileInfo fileInfo)
        {
            if (fileInfo is null)
            {
                throw new ArgumentNullException(nameof(fileInfo));
            }

            if (!Directory.Exists(fileInfo.DirectoryName))
            {
                Directory.CreateDirectory(fileInfo.DirectoryName);
            }

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            using var file = File.CreateText(fileInfo.FullName);
            var serializer = new JsonSerializer() { Formatting = Formatting.Indented };
            serializer.Serialize(file, ScanHeads);
#else
            using (var file = File.CreateText(fileInfo.FullName))
            {
                var serializer = new JsonSerializer() { Formatting = Formatting.Indented };
                serializer.Serialize(file, ScanHeads);
            }
#endif
        }

        internal ICollection<ScanHead> LoadScanHeads(FileInfo fileInfo)
        {
            if (fileInfo is null)
            {
                throw new ArgumentNullException(nameof(fileInfo));
            }

            if (!File.Exists(fileInfo.FullName))
            {
                throw new ArgumentException($"{fileInfo.FullName} does not exist.");
            }

            RemoveAllScanHeads();
            var scanHeadsImported = JsonConvert.DeserializeObject<ICollection<ScanHead>>(File.ReadAllText(fileInfo.FullName));
            foreach (var scanHead in scanHeadsImported)
            {
                scanHead.SetScanSystem(this);
                idToScanHead[scanHead.ID] = scanHead;
            }

            return scanHeadsImported;
        }

        #endregion

        #region Private Methods

        private void StartScanning(uint periodUs, AllDataFormat dataFormat)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Attempting to start scanning when not connected.");
            }

            if (IsScanning)
            {
                throw new InvalidOperationException("Attempting to start scanning while already scanning.");
            }

            long minScanPeriodUs = GetMinScanPeriod();
            if (periodUs < minScanPeriodUs)
            {
                throw new ArgumentOutOfRangeException(nameof(periodUs),
                    "Scan period is smaller than the minimum allowed for the system. " +
                    $"Requested {periodUs}µs but minimum is {minScanPeriodUs}µs.");
            }

            foreach (var scanHead in EnabledHeads)
            {
                scanHead.CameraLaserConfigurations.Clear();
            }

            var durationsUs = CalculatePhaseDurations();
            uint currPhaseEndOffsetNs = CameraStartEarlyOffsetNs;

            foreach ((var phase, int phaseNumber) in phaseTable.Select((p, it) => (p, it)))
            {
                currPhaseEndOffsetNs += durationsUs[phaseNumber] * 1000;

                foreach (var element in phase.Elements)
                {
                    var scanHead = element.ScanHead;

                    uint maxGroups = scanHead.Specification.MaxConfigurationGroups;
                    if (scanHead.CameraLaserConfigurations.Count >= maxGroups)
                    {
                        throw new InvalidOperationException($"Scan head {scanHead.ID} cannot have more than {maxGroups} camera laser configurations.");
                    }

                    var camera = scanHead.CameraPortToId(element.CameraPort);
                    var laser = scanHead.LaserPortToId(element.LaserPort);
                    var pair = new CameraLaserPair(camera, laser);
                    var conf = element.Configuration ?? scanHead.Configuration;
                    var clc = new Client::CameraLaserConfigurationT
                    {
                        CameraPort = element.CameraPort,
                        LaserPort = element.LaserPort,
                        LaserOnTimeMinNs = conf.MinLaserOnTimeUs * 1000,
                        LaserOnTimeDefNs = conf.DefaultLaserOnTimeUs * 1000,
                        LaserOnTimeMaxNs = conf.MaxLaserOnTimeUs * 1000,
                        ScanEndOffsetNs = currPhaseEndOffsetNs,
                        CameraOrientation = scanHead.GetCameraOrientation(pair)
                    };

                    scanHead.CameraLaserConfigurations.Add(clc);
                }
            }

            foreach (var scanHead in EnabledHeads)
            {
                scanHead.StartScanning(periodUs, dataFormat);
            }

            IsScanning = true;
            keepAliveTokenSource = new CancellationTokenSource();
            keepAliveToken = keepAliveTokenSource.Token;
            Task.Run(KeepAliveLoop, keepAliveToken);
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
                if (NetworkInterface.LoopbackInterfaceIndex == props.GetIPv4Properties()?.Index)
                {
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

        private void UpdateCommStats(object sender, CommStatsEventArgs args)
        {
            try
            {
                if (!previousCommStats.ContainsKey(args.ID))
                {
                    previousCommStats[args.ID] = new CommStatsEventArgs() { ID = args.ID };
                }

                CommStats.CompleteProfilesReceived += args.CompleteProfilesReceived - previousCommStats[args.ID].CompleteProfilesReceived;
                CommStats.BytesReceived += args.BytesReceived - previousCommStats[args.ID].BytesReceived;
                CommStats.Evicted += args.Evicted - previousCommStats[args.ID].Evicted;
                CommStats.BadPackets += args.BadPackets - previousCommStats[args.ID].BadPackets;
                CommStats.GoodPackets += args.GoodPackets - previousCommStats[args.ID].GoodPackets;

                previousCommStats[args.ID] = args;
            }
            catch
            {
                // TODO-CCP: after removing NLog, this function started throwing exceptions. Probably a race
                // condition. This catch is a bandaid, but seems to work.
            }
        }

        private void StatsThread()
        {
            long lastCheck = timeBase.ElapsedMilliseconds;
            long bytesReceivedSinceLastCheck = 0;
            while (true)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    Task.Delay(200, token).Wait(token);
                    long now = timeBase.ElapsedMilliseconds;
                    CommStats.DataRate = (CommStats.BytesReceived - bytesReceivedSinceLastCheck) * 1000.0 /
                                         (now - lastCheck);

                    StatsEvent.Raise(this, new CommStatsEventArgs()
                    {
                        CompleteProfilesReceived = CommStats.CompleteProfilesReceived,
                        ProfileRate = CommStats.ProfileRate,
                        BytesReceived = CommStats.BytesReceived,
                        Evicted = CommStats.Evicted,
                        DataRate = CommStats.DataRate,
                        BadPackets = CommStats.BadPackets,
                        GoodPackets = CommStats.GoodPackets
                    });

                    lastCheck = now;
                    bytesReceivedSinceLastCheck = CommStats.BytesReceived;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Sends periodic keep alive messages to the scan head. Should only be spun up
        /// when scanning, and should be killed (cancel token) when scanning stops.
        /// </summary>
        private async Task KeepAliveLoop()
        {
            /// The server will keep itself scanning as long as it can send profile data
            /// over TCP. This keep alive is really only needed to get scan head's to
            /// recover in the event that they fail to send and go into idle state.
            const int keepAliveIntervalMs = 1000;

            try
            {
                while (IsScanning)
                {
                    await Task.Delay(keepAliveIntervalMs, keepAliveToken).ConfigureAwait(false);
                    foreach (var scanHead in EnabledHeads)
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

        #endregion
    }
}

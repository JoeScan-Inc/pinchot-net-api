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
using System.Threading;
using System.Threading.Tasks;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// A complete system of <see cref="ScanHead"/>s.
    /// </summary>
    /// <remarks>
    /// The <see cref="ScanSystem"/> class represents a complete scan system. It contains a collection of
    /// <see cref="ScanHead"/> objects, and provides properties and methods for adding/removing <see cref="ScanHead"/>s,
    /// accessing the <see cref="ScanHead"/>s, connecting/disconnecting to/from the <see cref="ScanHead"/>s, and
    /// starting/stopping scanning on the <see cref="ScanHead"/>s.
    /// </remarks>
    public class ScanSystem : IDisposable
    {
        #region Private Fields

        private readonly ConcurrentDictionary<uint, ScanHead> scanHeads = new ConcurrentDictionary<uint, ScanHead>();
        private readonly ConcurrentDictionary<uint, ScanHead> idToScanHeads = new ConcurrentDictionary<uint, ScanHead>();
        private Thread statsThread;
        private readonly Stopwatch timeBase = Stopwatch.StartNew();
        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken token;
        private Dictionary<uint, CommStatsEventArgs> previousCommStats = new Dictionary<uint, CommStatsEventArgs>();
        private bool disposed;

        #endregion

        #region Events

        internal event EventHandler<CommStatsEventArgs> StatsEvent;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets a value indicating whether the <see cref="ScanSystem"/> is actively scanning.
        /// </summary>
        /// <value>
        /// A value indicating whether the <see cref="ScanSystem"/> is actively scanning.
        /// </value>
        public bool IsScanning { get; private set; }

        /// <summary>
        /// Gets a value indicating whether all <see cref="ScanHeads"/> have established network
        /// connection to their associated physical scan heads.
        /// </summary>
        /// <value>
        /// A value indicating whether all <see cref="ScanHeads"/> have established network
        /// connection to their associated physical scan heads.
        /// </value>
        public bool IsConnected => ScanHeads.Any(s => s.Enabled) && ScanHeads.Where(s => s.Enabled).All(s => s.IsConnected);

        /// <summary>
        /// Gets a read-only collection of <see cref="ScanHead"/>s belonging to the <see cref="ScanSystem"/>.
        /// </summary>
        /// <value>A <see cref="IReadOnlyCollection{T}"/> of <see cref="ScanHead"/>s belonging to the <see cref="ScanSystem"/>.</value>
        public IReadOnlyCollection<ScanHead> ScanHeads => scanHeads.Values.ToList();

        #endregion

        #region Internal Properties

        internal static byte SessionId { get; private set; } = 1;

        internal ConnectionType ConnectionType { get; set; }

        /// <summary>
        /// Aggregated network communication statistics for all <see cref="Pinchot.ScanHead"/> objects.
        /// </summary>
        internal CommStatsEventArgs CommStats { get; private set; } = new CommStatsEventArgs();

        internal bool CommStatsEnabled;

        internal short StartColumn { get; private set; }

        internal short EndColumn { get; private set; } = 1455;

        internal double EncoderPulseInterval { get; set; }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Initializes a new instance of the <see cref="ScanSystem"/> class.
        /// </summary>
        public ScanSystem() => EncoderPulseInterval = 1.0;

        /// <nodoc/>
        ~ScanSystem()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases the managed and unmanaged resources used by the <see cref="ScanSystem"/>
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="ScanSystem"/> and optionally releases the managed resources.
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

                cancellationTokenSource?.Dispose();
            }

            disposed = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a <see cref="ScanHead"/> and adds it to <see cref="ScanHeads"/>.
        /// </summary>
        /// <param name="serialNumber">The serial number of the physical scan head.</param>
        /// <param name="id">The ID to associate with the <see cref="ScanHead"/>.</param>
        /// <returns>The created <see cref="ScanHead"/>.</returns>
        /// <remarks>
        /// <see cref="ScanSystem"/> must not be connected. Verify <see cref="IsConnected"/>
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

            if (scanHeads.ContainsKey(serialNumber))
            {
                throw new ArgumentException($"Scan head with serial number \"{serialNumber}\" is already managed.");
            }

            if (scanHeads.Values.Any(s => s.ID == id))
            {
                throw new ArgumentException("ID is already assigned to another scan head.");
            }

            var scanHead = new ScanHead(this, serialNumber, id);
            scanHeads[serialNumber] = scanHead;
            idToScanHeads[id] = scanHead;
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
            if (!scanHeads.ContainsKey(serialNumber))
            {
                throw new ArgumentException($"Scan head with serial number {serialNumber} is not managed.",
                    nameof(serialNumber));
            }

            return scanHeads[serialNumber];
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
            if (!idToScanHeads.ContainsKey(id))
            {
                throw new ArgumentException($"Scan head with ID {id} is not managed.", nameof(id));
            }

            return idToScanHeads[id];
        }

        // TODO: wait for connect should be optional
        /// <summary>
        /// Attempts to connect all <see cref="ScanHeads"/> to their associated physical scan heads.
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
        /// <see cref="IsScanning"/> is `true`.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// A scan head reports that it's connected but no new status messages are being received.
        /// </exception>
        public IReadOnlyCollection<ScanHead> Connect(TimeSpan connectTimeout)
        {
            if (ScanHeads.Count == 0)
            {
                throw new InvalidOperationException("No scan heads in scan system.");
            }

            if (!ScanHeads.Any(s => s.Enabled))
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

            var enabledHeads = ScanHeads.Where(s => s.Enabled);

            foreach (var scanHead in enabledHeads)
            {
                scanHead.StartSenderReceiver(SessionId++, ConnectionType);
                scanHead.StatsEvent += UpdateCommStats;
            }

            // now check if all heads responded
            var connectedHeadIPs = WaitForConnect(enabledHeads, connectTimeout).ToList();

            var mismatches = enabledHeads.Where(s => s.IsVersionMismatched);
            if (mismatches.Any())
            {
                var errs = mismatches.Select(mm =>
                    $"Scan head {mm.SerialNumber} failed to connect: {mm.VersionMismatchReason}");
                throw new InvalidOperationException(string.Join("\n", errs));
            }

            var connectedHeads = enabledHeads.Where(s => connectedHeadIPs.Contains(s.IPAddress)).ToList();
            connectedHeads.ForEach(s => s.SetWindow());

            // Wait for any status messages already in-transit before applying the window to be received
            var transitWait = TimeSpan.FromMilliseconds(100);
            Thread.Sleep(transitWait);
            connectTimeout -= transitWait;

            var waitSpan = TimeSpan.FromMilliseconds(10);
            var oldTimestamps = connectedHeads.ConvertAll(s => s.Status.GlobalTime);
            bool done = false;
            while (!done)
            {
                done = true;
                var newTimestamps = connectedHeads.ConvertAll(s => s.Status.GlobalTime);
                foreach ((long oldTs, long newTs) in oldTimestamps.Zip(newTimestamps, (f,s) => (f,s)))
                {
                    if (oldTs == newTs)
                    {
                        done = false;
                    }
                }

                Thread.Sleep(waitSpan);
                connectTimeout -= waitSpan;
                if (connectTimeout <= TimeSpan.Zero)
                {
                    throw new TimeoutException("Failed to get new status messages from connected heads");
                }
            }

            cancellationTokenSource = new CancellationTokenSource();
            token = cancellationTokenSource.Token;
            if (CommStatsEnabled)
            {
                CommStats = new CommStatsEventArgs();
                previousCommStats = new Dictionary<uint, CommStatsEventArgs>();
                statsThread = new Thread(StatsThread) { Priority = ThreadPriority.Lowest, IsBackground = true };
                statsThread.Start();
            }

            return enabledHeads.Except(connectedHeads).ToList();
        }

        /// <summary>
        /// Disconnects all <see cref="ScanHeads"/> from their associated physical scan heads.
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

            foreach (var s in ScanHeads)
            {
                s.Disconnect();
            }
        }

        /// <summary>
        /// Starts scanning on all <see cref="ScanHeads"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="ScanSystem"/> must be connected. Verify <see cref="IsConnected"/>
        /// is `true` and/or call <see cref="Connect"/> before calling ths method.<br/>
        /// <br/>
        /// All existing <see cref="Profile"/>s will be cleared from all <see cref="ScanHeads"/>
        /// when calling this method. Ensure that all data from the previous scan that is desired
        /// is read out before calling this method.<br/>
        /// <br/>
        /// The <paramref name="rate"/> is the overall scan rate that each individual scan head will
        /// generate profiles. This implies that each camera in a given scan head will be set to an
        /// equal fractional amount; for example, a scan rate of 2000hz for a scan head with two
        /// cameras will cause each camera to run at 1000hz.
        /// </remarks>
        /// <param name="rate">The scan rate in Hz.</param>
        /// <param name="dataFormat">The <see cref="DataFormat"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is `false`.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is `true`.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Requested scan rate <paramref name="rate"/> is greater than <see cref="GetMaxScanRate"/>.
        /// </exception>
        public void StartScanning(double rate, DataFormat dataFormat)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Attempting to start scanning when not connected.");
            }

            if (IsScanning)
            {
                throw new InvalidOperationException("Attempting to start scanning while already scanning.");
            }

            if (rate > GetMaxScanRate())
            {
                string msg =
                    $"Requested scan rate of {rate}Hz is greater than the maximum allowed of {GetMaxScanRate()}Hz";
                throw new ArgumentException(msg);
            }

            foreach (var scanHead in ScanHeads)
            {
                scanHead.StartScanning(rate, (AllDataFormat)dataFormat);
            }

            IsScanning = true;
        }

        /// <summary>
        /// Stops scanning on all <see cref="ScanHeads"/>.
        /// </summary>
        /// <remarks>
        /// Physical scan heads will take approximately 0.5-1.0 seconds to stop scanning after <see cref="StopScanning"/>
        /// is called. <see cref="Profile"/>s will remain in the <see cref="ScanHead"/> profile buffers until they are either consumed
        /// or <see cref="StartScanning"/> is called.
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

            foreach (var scanHead in ScanHeads)
            {
                scanHead.StopScanning();
            }

            IsScanning = false;
        }

        /// <summary>
        /// Gets the maximum scan rate allowed by the <see cref="ScanSystem"/> in Hz.
        /// </summary>
        /// <returns>The maximum scan rate allowed by the <see cref="ScanSystem"/> in Hz.</returns>
        /// <exception cref="InvalidOperationException">
        /// <see cref="IsConnected"/> is `false`.
        /// </exception>
        public double GetMaxScanRate()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Attempting to get max scan rate when not connected.");
            }

            double maxLaserOnTime = 0;
            double minWindowBasedScanRate = double.MaxValue;
            foreach (var scanHead in ScanHeads.Where(s => s.Enabled))
            {
                if (scanHead.Configuration.MaxLaserOnTime > maxLaserOnTime)
                {
                    maxLaserOnTime = scanHead.Configuration.MaxLaserOnTime;
                }

                if (scanHead.Status.MaxScanRate < minWindowBasedScanRate)
                {
                    minWindowBasedScanRate = scanHead.Status.MaxScanRate;
                }
            }

            double minLaserOnTimeBasedScanRate = 1 / (maxLaserOnTime / 1e6);
            double minRateAmongScanHeads = Math.Min(minLaserOnTimeBasedScanRate, minWindowBasedScanRate);
            return Math.Min(minRateAmongScanHeads, Globals.MaxScanRate);
        }

        #endregion

        #region Internal Methods

        internal void SetColumnRange(short startColumn, short endColumn)
        {
            if (IsScanning)
            {
                throw new InvalidOperationException("Can not change column range while scanning");
            }

            if (startColumn == endColumn || startColumn > endColumn || startColumn < 0 || endColumn > 1455)
            {
                throw new ArgumentOutOfRangeException(nameof(startColumn), "Illegal value for start column or end column.");
            }

            StartColumn = startColumn;
            EndColumn = endColumn;
        }

        internal void StartScanningSubpixel(double rate, SubpixelDataFormat dataFormat)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected.");
            }

            if (IsScanning)
            {
                throw new InvalidOperationException("Already scanning.");
            }

            // TODO-CCP: need to determine the bounds
            if (rate < 0.02 || rate > 5000)
            {
                string msg = $"Scan rate {rate} outside of allowed range. Must be between 0.02 and 5000 Hz";
                throw new ArgumentOutOfRangeException(msg);
            }

            foreach (var scanHead in ScanHeads)
            {
                scanHead.StartScanning(rate, (AllDataFormat)dataFormat);
            }

            IsScanning = true;
        }

        internal void StartScanningImage(double rate)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected.");
            }

            if (IsScanning)
            {
                throw new InvalidOperationException("Already scanning.");
            }

            // TODO-CCP: need to determine the bounds
            if (rate < 0.02 || rate > 20)
            {
                string msg = $"Scan rate {rate} outside of allowed range. Must be between 0.02 and 20 Hz";
                throw new ArgumentOutOfRangeException(msg);
            }

            foreach (var scanHead in ScanHeads)
            {
                scanHead.StartScanning(rate, AllDataFormat.Image);
            }

            IsScanning = true;
        }

        /// <summary>
        /// Removes a <see cref="ScanHead"/> object from use by serial number.
        /// </summary>
        /// <param name="serialNumber">The serial number of the scan head to remove.</param>
        internal void RemoveScanHead(uint serialNumber)
        {
            if (IsConnected)
            {
                throw new InvalidOperationException("Can not remove scan head while connected.");
            }

            if (IsScanning)
            {
                throw new InvalidOperationException("Can not remove scan head while scanning.");
            }

            if (!scanHeads.ContainsKey(serialNumber))
            {
                throw new ArgumentException($"Scan head with serial number \"{serialNumber}\" is not managed.");
            }

            RemoveScanHead(scanHeads[serialNumber]);
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

            if (!scanHeads.Values.Contains(scanHead))
            {
                throw new ArgumentException("Scan head is not managed.");
            }

            var s = scanHeads.FirstOrDefault(q => q.Value == scanHead);

            scanHeads.TryRemove(s.Key, out scanHead);
            scanHead?.Dispose();
            idToScanHeads.TryRemove(s.Value.ID, out scanHead);
            scanHead?.Dispose();
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

            foreach (var scanHead in ScanHeads)
            {
                scanHead?.Dispose();
            }

            scanHeads.Clear();
            idToScanHeads.Clear();
        }

        /// <summary>
        /// Iterates through all <see cref="Pinchot.ScanHead" /> objects that were told to scan through
        /// the <see cref="StartScanning" /> method and tries to remove the first available profile
        /// while observing a cancellation token.
        /// </summary>
        /// <param name="profile">The profile to be removed from the collection.</param>
        /// <param name="token">Cancellation token to observe.</param>
        /// <returns><c>true</c> if a profile was successfully taken, <c>false</c> otherwise.</returns>
        internal bool TryTakeNextProfile(out Profile profile, CancellationToken token)
        {
            return TryTakeNextProfile(out profile, TimeSpan.FromMilliseconds(-1), token);
        }

        /// <summary>
        /// Iterates through all <see cref="Pinchot.ScanHead" /> objects that were told to scan through
        /// the <see cref="StartScanning" /> method and tries to remove the first available profile
        /// in the specified time period while observing a cancellation token.
        /// </summary>
        /// <param name="profile">The profile to be removed from the collection.</param>
        /// <param name="timeout">An object that represents the number of milliseconds to wait,
        /// or an object that represents -1 milliseconds to wait indefinitely.</param>
        /// <param name="token">Cancellation token to observe.</param>
        /// <returns><c>true</c> if a profile was successfully taken, <c>false</c> otherwise.</returns>
        internal bool TryTakeNextProfile(out Profile profile, TimeSpan timeout, CancellationToken token)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            profile = new Profile();
            while (!token.IsCancellationRequested)
            {
                // TODO: this should be improved to ensure all scan heads are taken from
                foreach (var scanHead in ScanHeads.Where(s => s.Enabled))
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

            using (var file = File.CreateText(fileInfo.FullName))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(file, ScanHeads);
            }
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
            var scanHeadsImported =
                JsonConvert.DeserializeObject<ICollection<ScanHead>>(File.ReadAllText(fileInfo.FullName));
            foreach (var scanHead in scanHeadsImported)
            {
                scanHead.SetScanSystem(this);
                scanHeads[scanHead.SerialNumber] = scanHead;
                idToScanHeads[scanHead.ID] = scanHead;
            }

            return scanHeadsImported;
        }

        #endregion

        #region Private Methods

        private static IEnumerable<IPAddress> WaitForConnect(IEnumerable<ScanHead> scanHeads, TimeSpan timeout)
        {
            var waitSpan = TimeSpan.FromMilliseconds(10);
            var connectedHeads = new List<IPAddress>();
            while (scanHeads.Count() != connectedHeads.Count)
            {
                foreach (var scanHead in scanHeads)
                {
                    if (scanHead.IsConnected && !connectedHeads.Contains(scanHead.IPAddress))
                    {
                        connectedHeads.Add(scanHead.IPAddress);
                    }
                }

                Thread.Sleep(waitSpan);
                timeout -= waitSpan;
                if (timeout <= TimeSpan.Zero)
                {
                    break;
                }
            }

            return connectedHeads;
        }

        private void UpdateCommStats(object sender, CommStatsEventArgs args)
        {
            try
            {
                if (previousCommStats == null)
                {
                    previousCommStats = new Dictionary<uint, CommStatsEventArgs>();
                }

                if (!previousCommStats.ContainsKey(args.ID))
                {
                    previousCommStats.Add(args.ID, new CommStatsEventArgs() { ID = args.ID });
                }

                CommStats.CompleteProfilesReceived +=
                    (args.CompleteProfilesReceived - previousCommStats[args.ID].CompleteProfilesReceived);
                CommStats.BytesReceived += (args.BytesReceived - previousCommStats[args.ID].BytesReceived);
                CommStats.Evicted += (args.Evicted - previousCommStats[args.ID].Evicted);
                CommStats.BadPackets += (args.BadPackets - previousCommStats[args.ID].BadPackets);
                CommStats.GoodPackets += (args.GoodPackets - previousCommStats[args.ID].GoodPackets);

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

        #endregion
    }
}
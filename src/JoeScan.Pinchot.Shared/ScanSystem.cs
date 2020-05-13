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

        private readonly ConcurrentDictionary<uint, ScanHead>
            idToScanHeads = new ConcurrentDictionary<uint, ScanHead>();

        private static byte sessionId = 1;
        private Thread statsThread;
        private readonly Stopwatch timeBase = Stopwatch.StartNew();
        private CancellationTokenSource cancellationTokenSource = null;
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
        public bool IsConnected => ScanHeads.Count > 0 && ScanHeads.Where(s => s.Enabled).All(s => s.IsConnected);

        /// <summary>
        /// Gets a read-only collection of <see cref="ScanHead"/>s belonging to the <see cref="ScanSystem"/>.
        /// </summary>
        /// <value>A <see cref="IReadOnlyCollection{T}"/> of <see cref="ScanHead"/>s belonging to the <see cref="ScanSystem"/>.</value>
        public IReadOnlyCollection<ScanHead> ScanHeads => scanHeads.Values.ToList();

        #endregion

        #region Internal Properties

        internal static byte SessionId
        {
            get => sessionId;
            private set => sessionId = value;
        }

        internal ConnectionType ConnectionType { get; set; } = ConnectionType.Normal;

        /// <summary>
        /// Aggregated network communication statistics for all <see cref="Pinchot.ScanHead"/> objects.
        /// </summary>
        internal CommStatsEventArgs CommStats { get; private set; } = new CommStatsEventArgs();

        internal bool CommStatsEnabled = false;

        internal short StartColumn { get; private set; }

        internal short EndColumn { get; private set; } = 1455;

        internal double EncoderPulseInterval { get; set; }

        #endregion

        #region Lifecycle

        /// <summary>
        /// Initializes a new instance of the <see cref="ScanSystem"/> class.
        /// </summary>
        public ScanSystem()
        {
            EncoderPulseInterval = 1.0;
        }

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
                return;

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
        /// Creates a <see cref="Pinchot.ScanHead"/> and adds it to <see cref="ScanHeads"/>.
        /// </summary>
        /// <param name="serialNumber">The serial number of the physical scan head.</param>
        /// <param name="id">The ID to associate with the <see cref="ScanHead"/>.</param>
        /// <returns>The created <see cref="Pinchot.ScanHead"/>.</returns>
        /// <remarks>
        /// <see cref="ScanSystem"/> must not be connected. Verify <see cref="IsConnected"/>
        /// is `false` and/or call <see cref="Disconnect"/> before calling ths method.
        /// </remarks>
        /// <exception cref="System.Exception">
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
                throw new Exception("Can not add scan head while connected.");
            }

            if (IsScanning)
            {
                throw new Exception("Can not add scan head while scanning.");
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
        /// Gets a <see cref="Pinchot.ScanHead"/> by serial number.
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
        /// Gets a <see cref="Pinchot.ScanHead"/> by ID.
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

        // TODO-CCP: wait for connect should be optional
        /// <summary>
        /// Attempts to connect all <see cref="ScanHeads"/> to their associated physical scan heads.
        /// </summary>
        /// <param name="connectTimeout">The connection timeout period.</param>
        /// <returns>A <see cref="IReadOnlyCollection{T}"/> of <see cref="Pinchot.ScanHead"/>s
        /// that did not successfully connect.</returns>
        /// <exception cref="System.Exception">
        /// <see cref="ScanHeads"/> does not contain any <see cref="ScanHead"/>s.<br/>
        /// -or-<br/>
        /// <see cref="ScanHeads"/> does not contain any enabled <see cref="ScanHead"/>s.<br/>
        /// -or-<br/>
        /// <see cref="IsConnected"/> is `true`.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is `true`.
        /// </exception>
        public IReadOnlyCollection<ScanHead> Connect(TimeSpan connectTimeout)
        {
            if (!ScanHeads.Any())
            {
                var msg = "No scan heads in scan system.";
                throw new Exception(msg);
            }

            if (!ScanHeads.Any(s => s.Enabled))
            {
                var msg = "No scan heads are enabled.";
                throw new Exception(msg);
            }

            if (IsConnected)
            {
                var msg = "Already connected.";
                throw new Exception(msg);
            }

            if (IsScanning)
            {
                var msg = "Already scanning.";
                throw new Exception(msg);
            }

            // Create new session id and connect to all heads 
            sessionId++;
            CommStats = new CommStatsEventArgs();
            previousCommStats = new Dictionary<uint, CommStatsEventArgs>();
            foreach (var scanHead in ScanHeads)
            {
                if (!scanHead.ValidateConfiguration())
                {
                    throw new Exception("Configuration validation failed for scan head " + scanHead.ID + ".");
                }

                scanHead.StartSenderReceiver(sessionId, ConnectionType);
                scanHead.StatsEvent += UpdateCommStats;
            }

            // now check if all heads responded
            var connectedHeadIPs = WaitForConnect(ScanHeads.Select(s => s.IPAddress), connectTimeout).ToList();

            var mismatches = ScanHeads.Where(s => s.Enabled).Where(s => s.IsVersionMismatched);
            if (mismatches.Any())
            {
                var errs = mismatches.Select(mm =>
                    $"Scan head {mm.SerialNumber} failed to connect: {mm.VersionMismatchReason}");
                var err = string.Join("\n", errs);
                throw new Exception(err);
            }

            var connectedHeads = ScanHeads.Where(s => connectedHeadIPs.Contains(s.IPAddress)).ToList();

            foreach (var s in ScanHeads.Where(s => s.Enabled))
            {
                s.SetWindow();
            }

            // Wait for scan heads to configure the window and report max scan rate
            // via status message.
            Thread.Sleep(1000);

            cancellationTokenSource = new CancellationTokenSource();
            token = cancellationTokenSource.Token;
            if (CommStatsEnabled)
            {
                statsThread = new Thread(StatsThread) { Priority = ThreadPriority.Lowest, IsBackground = true };
                statsThread.Start();
            }

            return ScanHeads.Where(s => s.Enabled).Except(connectedHeads).ToList();
        }

        /// <summary>
        /// Disconnects all <see cref="ScanHeads"/> from their associated physical scan heads.
        /// </summary>
        /// <exception cref="System.Exception">
        /// <see cref="IsConnected"/> is `false`.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is `true`.
        /// </exception>
        public void Disconnect()
        {
            if (!IsConnected)
            {
                throw new Exception("Attempting to disconnect when not connected.");
            }

            if (IsScanning)
            {
                throw new Exception("Can not disconnect while still scanning.");
            }

            // send connect packet to all heads
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
        /// <exception cref="System.Exception">
        /// <see cref="IsConnected"/> is `false`.<br/>
        /// -or-<br/>
        /// <see cref="IsScanning"/> is `true`.
        /// </exception>
        public void StartScanning(double rate, DataFormat dataFormat)
        {
            if (!IsConnected)
            {
                var msg = "Attempting to start scanning on an unconnected scan head.";
                throw new Exception(msg);
            }

            if (IsScanning)
            {
                var msg = "Attempting to start scanning on an already scanning scan head.";
                throw new Exception(msg);
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
        /// <exception cref="Exception">
        /// <see cref="IsScanning"/> is `false`.
        /// </exception>
        public void StopScanning()
        {
            if (!IsScanning)
            {
                var msg = "Attempting to stop scanning when not scanning.";
                throw new Exception(msg);
            }

            foreach (var scanHead in ScanHeads)
            {
                scanHead.StopScanning();
            }

            IsScanning = false;
        }

        #endregion

        #region Internal Methods

        internal void SetColumnRange(short startColumn, short endColumn)
        {
            if (IsScanning)
            {
                throw new Exception("Can not change column range while scanning");
            }

            if (startColumn == endColumn || startColumn > endColumn || startColumn < 0 || endColumn > 1455)
            {
                throw new Exception("Illegal value for start column or end column.");
            }

            StartColumn = startColumn;
            EndColumn = endColumn;
        }

        internal void StartScanningSubpixel(double rate, SubpixelDataFormat dataFormat)
        {
            if (!IsConnected)
            {
                var msg = "Not connected.";
                throw new Exception(msg);
            }

            if (IsScanning)
            {
                var msg = "Already scanning.";
                throw new Exception(msg);
            }

            // TODO-CCP: need to determine the bounds
            if (rate < 0.02 || rate > 5000)
            {
                var msg = $"Scan rate {rate} outside of allowed range. Must be between 0.02 and 5000 Hz";
                throw new Exception(msg);
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
                var msg = "Not connected.";
                throw new Exception(msg);
            }

            if (IsScanning)
            {
                var msg = "Already scanning.";
                throw new Exception(msg);
            }

            // TODO-CCP: need to determine the bounds
            if (rate < 0.02 || rate > 5000)
            {
                var msg = $"Scan rate {rate} outside of allowed range. Must be between 0.02 and 5000 Hz";
                throw new Exception(msg);
            }

            foreach (var scanHead in ScanHeads)
            {
                scanHead.StartScanning(rate, AllDataFormat.Image);
            }

            IsScanning = true;
        }

        /// <summary>
        /// Removes a <see cref="Pinchot.ScanHead"/> object from use by serial number.
        /// </summary>
        /// <param name="serialNumber">The serial number of the scan head to remove.</param>
        internal void RemoveScanHead(uint serialNumber)
        {
            if (IsConnected)
            {
                throw new Exception("Can not remove scan head while connected.");
            }

            if (IsScanning)
            {
                throw new Exception("Can not remove scan head while scanning.");
            }

            if (!scanHeads.ContainsKey(serialNumber))
            {
                throw new Exception($"Scan head with serial number \"{serialNumber}\" is not managed.");
            }

            RemoveScanHead(scanHeads[serialNumber]);
        }

        /// <summary>
        /// Removes a <see cref="Pinchot.ScanHead"/> object from use by reference.
        /// </summary>
        /// <param name="scanHead">An object reference to the scan head to remove.</param>
        internal void RemoveScanHead(ScanHead scanHead)
        {
            if (IsConnected)
            {
                throw new Exception("Can not remove scan head while connected.");
            }

            if (IsScanning)
            {
                throw new Exception("Can not remove scan head while scanning.");
            }

            if (!scanHeads.Values.Contains(scanHead))
            {
                throw new Exception($"Scan head is not managed.");
            }

            var s = scanHeads.FirstOrDefault(q => q.Value == scanHead);

            scanHeads.TryRemove(s.Key, out scanHead);
            scanHead?.Dispose();
            idToScanHeads.TryRemove(s.Value.ID, out scanHead);
            scanHead?.Dispose();
        }

        /// <summary>
        /// Removes all created <see cref="Pinchot.ScanHead"/> objects from use.
        /// </summary>
        internal void RemoveAllScanHeads()
        {
            if (IsConnected)
            {
                throw new Exception("Can not remove scan head while connected.");
            }

            if (IsScanning)
            {
                throw new Exception("Can not remove scan head while scanning.");
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
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                // TODO-CCP: this should be improved to ensure all scan heads are taken from
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
                throw new ArgumentNullException($"{nameof(fileInfo)} argument is null.");
            }

            if (!Directory.Exists(fileInfo.DirectoryName))
            {
                Directory.CreateDirectory(fileInfo.DirectoryName);
            }

            using (StreamWriter file = File.CreateText(fileInfo.FullName))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, ScanHeads);
            }
        }

        internal ICollection<ScanHead> LoadScanHeads(FileInfo fileInfo)
        {
            if (fileInfo is null)
            {
                throw new ArgumentNullException($"{nameof(fileInfo)} argument is null.");
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

        private void OnScanSyncUpdate(object sender, ScanSyncUpdateEvent e)
        {
        }

        private IEnumerable<IPAddress> WaitForConnect(IEnumerable<IPAddress> heads, TimeSpan timeout)
        {
            TimeSpan spent = TimeSpan.Zero;
            TimeSpan waitSpan = TimeSpan.FromMilliseconds(10);
            List<IPAddress> connectedHeads = new List<IPAddress>();
            do
            {
                foreach (var scanHead in ScanHeads.Where(s => s.Enabled))
                {
                    if (scanHead.IsConnected && !connectedHeads.Contains(scanHead.IPAddress))
                    {
                        connectedHeads.Add(scanHead.IPAddress);
                        if (heads.Count() == connectedHeads.Count)
                        {
                            return connectedHeads;
                        }
                    }
                }

                // timed out, check if allotted timeout is up
                Thread.Sleep(waitSpan);
                spent += waitSpan;
                if (spent > timeout)
                {
                    return connectedHeads;
                }
            } while (true);
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
            catch (Exception)
            {
                // TODO-CCP: after removing NLog, this function started throwing exceptions. Probably a race
                // condition. This catch is a bandaid, but seems to work.
            }
        }

        private void StatsThread()
        {
            var lastCheck = timeBase.ElapsedMilliseconds;
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
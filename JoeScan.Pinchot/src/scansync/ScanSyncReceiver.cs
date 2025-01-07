// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace JoeScan.Pinchot
{
    internal class ScanSyncReceiver : IDisposable
    {
        #region Private Fields

        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken token;
        private readonly Thread threadMain;
        private bool disposed;
        private readonly object activeScanSyncsLock = new object();
        private readonly Dictionary<uint, ActiveScanSync> activeScanSyncs = new Dictionary<uint, ActiveScanSync>();

        #endregion

        #region Internal Properties

        internal bool IsRunning { get; private set; }

        #endregion

        #region Events

        internal event EventHandler<ScanSyncUpdateEvent> ScanSyncUpdate;

        #endregion

        #region Lifecycle

        internal ScanSyncReceiver()
        {
            threadMain = new Thread(Listen)
            {
                IsBackground = true
            };

            threadMain.Start();
            IsRunning = true;
        }

        /// <summary>
        /// Releases the managed and unmanaged resources used by the <see cref="ScanSyncReceiver"/>
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="ScanSyncReceiver"/> and optionally
        /// releases the managed resources.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                cancellationTokenSource?.Cancel();
                threadMain?.Join();
                cancellationTokenSource?.Dispose();
            }

            disposed = true;
        }

        #endregion

        #region Internal Methods

        internal Dictionary<uint, ScanSyncData> GetScanSyncs()
        {
            lock (activeScanSyncsLock)
            {
                return activeScanSyncs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ScanSync);
            }
        }

        internal bool TryGetScanSyncData(uint serial, out ScanSyncData data)
        {
            lock (activeScanSyncsLock)
            {
                bool success = activeScanSyncs.TryGetValue(serial, out var activeScanSync);
                data = success ? activeScanSync.ScanSync : default;
                return success;
            }
        }

        #endregion

        #region Private Methods

        private void Listen()
        {
            cancellationTokenSource = new CancellationTokenSource();
            token = cancellationTokenSource.Token;

            var receiverClient = new UdpClient();
            receiverClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            receiverClient.Client.Bind(new IPEndPoint(IPAddress.Any, Globals.ScanSyncClientPort));

            // this callback will kill the socket when the
            // token was canceled, which is the only way to get out
            // of the blocking udpClient.Receive()
            token.Register(() => receiverClient.Close());

            int eventCounter = 0;
            IPEndPoint ipEndPoint = null;

            const int scanSyncUpdatePeriodMs = 1;
            const int eventPeriodMs = 1000;
            const int timeoutMs = 1000;

            const int eventTriggerCount = eventPeriodMs / scanSyncUpdatePeriodMs;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    byte[] buf = receiverClient.Receive(ref ipEndPoint);
                    if (!ScanSyncData.IsValidPacketSize(buf))
                    {
                        continue;
                    }

                    var pkt = new ScanSyncData(buf, ipEndPoint.Address);

                    lock (activeScanSyncsLock)
                    {
                        // update or add to active scan syncs
                        if (activeScanSyncs.TryGetValue(pkt.SerialNumber, out var a))
                        {
                            a.ScanSync = pkt;
                            a.LastUpdateTick = Environment.TickCount;
                        }
                        else
                        {
                            activeScanSyncs.Add(pkt.SerialNumber, new ActiveScanSync
                            {
                                ScanSync = pkt,
                                LastUpdateTick = Environment.TickCount
                            });
                        }
                    }

                    // check if any ScanSyncs have timed out, and if so, remove them
                    var timedOut = activeScanSyncs
                                    .Where(ss => Environment.TickCount - ss.Value.LastUpdateTick > timeoutMs)
                                    .ToList();

                    if (timedOut.Count > 0)
                    {
                        lock (activeScanSyncsLock)
                        {
                            foreach (var to in timedOut)
                            {
                                activeScanSyncs.Remove(to.Key);
                            }
                        }
                    }

                    // trigger update event if needed
                    if (ScanSyncUpdate != null && eventCounter++ == eventTriggerCount)
                    {
                        eventCounter = 0;
                        var scanSyncs = activeScanSyncs.Select(ss => ss.Value.ScanSync).ToList();
                        if (scanSyncs.Count > 0)
                        {
                            ScanSyncUpdate?.Invoke(this, new ScanSyncUpdateEvent(scanSyncs));
                        }
                    }
                }
            }
            catch { }

            IsRunning = false;
        }

        #endregion
    }
}
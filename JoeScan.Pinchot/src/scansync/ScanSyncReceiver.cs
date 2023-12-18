// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
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
        private readonly object dataLock = new object();

        #endregion

        #region Backing Fields

        private ScanSyncData latestData = new ScanSyncData();

        #endregion

        #region Internal Properties

        internal bool IsRunning { get; private set; }

        /// <summary>
        /// The most recently received ScanSync data.
        /// </summary>
        internal ScanSyncData LatestData
        {
            get
            {
                lock (dataLock)
                {
                    return latestData;
                }
            }
        }

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
        /// <param name="disposing"></param>
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
            IPEndPoint iPEndPoint = null;

            const int scanSyncUpdatePeriodMs = 1;
            const int eventPeriodMs = 1000;
            const int eventTriggerCount = eventPeriodMs / scanSyncUpdatePeriodMs;

            while (true)
            {
                try
                {
                    token.ThrowIfCancellationRequested();

                    byte[] buf = receiverClient.Receive(ref iPEndPoint);
                    if (!ScanSyncData.IsValidPacketSize(buf))
                    {
                        continue;
                    }

                    var pkt = new ScanSyncData(buf);

                    // currently only support 1 encoder (main)
                    // if two encoders are present on the network, the lowest
                    // serial number is considered to be the "main" one
                    if (pkt.SerialNumber <= LatestData.SerialNumber || LatestData.SerialNumber == 0)
                    {
                        lock (dataLock)
                        {
                            latestData = pkt;
                        }

                        if (eventCounter++ == eventTriggerCount)
                        {
                            ScanSyncUpdate?.Invoke(this, new ScanSyncUpdateEvent(latestData));
                            eventCounter = 0;
                        }
                    }
                }
                catch (Exception)
                {
                    break;
                }
            }

            IsRunning = false;
        }

        #endregion
    }
}
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
    internal class ScanSyncReceiverThread : IDisposable
    {
        #region Private Fields

        private long bytesReceived;
        private long goodPackets;
        private long badPackets;
        private readonly UdpClient receiverClient;
        private IPEndPoint groupEP;
        private CancellationTokenSource cancellationTokenSource = null;
        private CancellationToken token;
        private Thread threadMain;
        private int counter = 0;
        private bool disposed;

        #endregion Private Fields

        #region Internal Properties

        internal int EventUpdateFrequency { get; set; } = 1000;

        #endregion Internal Properties

        #region Events

        internal event EventHandler<ScanSyncUpdateEvent> ScanSyncUpdate;

        #endregion Events

        #region Lifecycle

        internal ScanSyncReceiverThread()
        {
            receiverClient = new UdpClient(new IPEndPoint(IPAddress.Any, Globals.ScanSyncClientPort));
            groupEP = new IPEndPoint(IPAddress.Any, Globals.ScanSyncServerPort);
        }

        /// <nodoc/>
        ~ScanSyncReceiverThread()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases the managed and unmanaged resources used by the <see cref="ScanSyncReceiverThread"/>
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="ScanSyncReceiverThread"/> and optionally
        /// releases the managed resources.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                if (cancellationTokenSource != null)
                {
                    threadMain.Join();
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource?.Dispose();
                }

                receiverClient?.Dispose();
            }

            disposed = true;
        }

        #endregion Lifecycle

        #region Internal Methods

        internal void Start()
        {
            if (cancellationTokenSource == null)
            {
                cancellationTokenSource = new CancellationTokenSource();
                token = cancellationTokenSource.Token;
                bytesReceived = 0L;
                goodPackets = 0L;
                badPackets = 0L;
                threadMain = new Thread(ThreadMain) { Priority = ThreadPriority.BelowNormal };
                threadMain.IsBackground = true; // will not keep app alive
                threadMain.Start();
            }
            else
            {
                // no exception - the  end user isn't even supposed to see this class
            }
        }

        internal void Stop()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                threadMain.Join();
                cancellationTokenSource = null;
            }
        }

        #endregion Internal Methods

        #region Private Methods

        private void ThreadMain()
        {
            bytesReceived = 0;
            // this callback will kill the socket when the
            // token was canceled, which is the only way to get out
            // of the blocking udpClient.Receive()
            token.Register(() => receiverClient.Close());
            for (; ; )
            {
                if (token.IsCancellationRequested)
                {
                    // ScanSyncReceiverThread got cancellation notice. Exiting cleanly.
                    break;
                }

                try
                {
                    // raw scansync packet
                    var rsp = receiverClient.Receive(ref groupEP);
                    goodPackets++;
                    bytesReceived += rsp.Length;
                    if (counter++ == EventUpdateFrequency)
                    {
                        var pckt = new ScanSyncPacket(rsp);
                        ScanSyncUpdate.Raise(this, new ScanSyncUpdateEvent(pckt.ScanSyncData));
                        counter = 0;
                    }
                }
                catch (ArgumentException)
                {
                    badPackets++;
                }
                catch (OperationCanceledException)
                {
                    // perfectly normal, nothing to see here
                    break;
                }
                catch (SocketException)
                {
                    // we get here if we call Close() on the UdpClient
                    // while it is in the Receive(0 call. Apparently
                    // the only way to abort a Receive call is to
                    // close the underlying Socket.
                    break;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    // Receive failed.
                    break;
                }
            }
        }

        #endregion Private Methods
    }
}
// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace JoeScan.Pinchot
{
    internal class ScanHeadSenderReceiver : IDisposable
    {
        #region Private Fields

        private const long ConnectionCheckTime = 500;

        private readonly ScanHead scanHead;

        private AllDataFormat dataFormat;
        private short startColumn;
        private short endColumn;
        private double scanRate;

        private long bytesReceived;
        private long goodPackets;
        private long evictedForTimeout;
        private long evictedForNextSeq;
        private long lastReceivedPacketTime = long.MinValue;

        private IPEndPoint scanHeadDataIpEndPoint;
        private readonly UdpClient receiveUdpClient;
        private readonly UdpClient sendUdpClient;

        private readonly CancellationTokenSource cancellationTokenSource = null;
        private readonly CancellationToken token;

        private readonly Thread sendMain;
        private readonly Thread receiveMain;
        private readonly Thread statsThread;
        private ProfileAssembler profileAssembler;

        // TODO: discard all packets from old session id
        private volatile byte sessionId;

        // this queue holds the packets that are fire-and-forget
        private readonly ConcurrentQueue<byte[]> outgoingPackets = new ConcurrentQueue<byte[]>();

        // this packet is to be continually sent during scanning
        private byte[] scanRequestPacket;

        // communication statistics
        private readonly CommStatsEventArgs commStats = new CommStatsEventArgs();

        // unblocks the sender thread so that requests go out immediately
        private readonly AutoResetEvent evt = new AutoResetEvent(false);

        // used to timestamp data packets internally
        private readonly Stopwatch timeBase = Stopwatch.StartNew();

        private readonly TimeSpan scanRequestInterval = TimeSpan.FromSeconds(0.5);
        private bool isRunning;
        private bool disposed;

        private readonly object scanRequestPacketLock = new object();

        #endregion

        #region Events

        internal event EventHandler<CommStatsEventArgs> StatsEvent;

        #endregion

        #region Internal Properties

        internal IPEndPoint LocalReceiveIpEndPoint => (IPEndPoint)receiveUdpClient.Client.LocalEndPoint;

        internal bool IsConnected => lastReceivedPacketTime != long.MinValue &&
                                     (timeBase.ElapsedMilliseconds - lastReceivedPacketTime) < ConnectionCheckTime;

        internal bool ProfileBufferOverflowed => profileAssembler?.ProfileBufferOverflowed ?? false;

        internal long CompleteProfilesReceivedCount { get; private set; }

        internal long IncompleteProfilesReceivedCount => evictedForNextSeq;

        internal long BadPacketsCount { get; private set; }

        internal bool IsVersionMismatched = false;

        internal string VersionMismatchReason = null;

        #endregion

        #region Lifecycle

        internal ScanHeadSenderReceiver(ScanHead scanHead, bool enableCommStats)
        {
            this.scanHead = scanHead;

            receiveUdpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0))
                { Client = { ReceiveBufferSize = Globals.DefaultUdpBufferSize } };
            sendUdpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));

            cancellationTokenSource = new CancellationTokenSource();
            token = cancellationTokenSource.Token;
            token.Register(() => receiveUdpClient.Close());
            token.Register(() => sendUdpClient.Close());
            receiveMain = new Thread(ReceiveMain) { Priority = ThreadPriority.Highest, IsBackground = true };
            receiveMain.Start();
            sendMain = new Thread(SendMain) { Priority = ThreadPriority.Normal, IsBackground = true };
            sendMain.Start();

            if (enableCommStats)
            {
                // IsBackground = false will not keep app alive
                statsThread = new Thread(StatsThread) { Priority = ThreadPriority.Lowest, IsBackground = true };
                statsThread.Start();
            }
        }

        /// <nodoc/>
        ~ScanHeadSenderReceiver()
        {
            Dispose(false);
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                if (cancellationTokenSource != null)
                {
                    cancellationTokenSource.Cancel();
                    evt.Set();
                    receiveMain?.Join();
                    sendMain?.Join();
                    statsThread?.Join();
                    cancellationTokenSource?.Dispose();
                }

                sendUdpClient?.Dispose();
                receiveUdpClient?.Dispose();
                evt?.Dispose();
            }

            disposed = true;
        }

        #endregion

        #region Internal Methods

        internal void Start(byte sessionId, ConnectionType connType)
        {
            this.sessionId = sessionId;
            bytesReceived = 0L;
            goodPackets = 0L;
            BadPacketsCount = 0L;
            evictedForTimeout = 0L;
            evictedForNextSeq = 0L;
            var validNics = GetValidNics();

            foreach (IPAddress possibleEndPoint in validNics)
            {
                byte[] connectMessage = new BroadcastConnectPacket(sessionId,
                    (short)((IPEndPoint)receiveUdpClient.Client.LocalEndPoint).Port, scanHead.SerialNumber, connType,
                    possibleEndPoint).Raw;
                IPEndPoint local = new IPEndPoint(possibleEndPoint, 0);
                UdpClient udpc = new UdpClient();
                udpc.Client.Bind(local);
                udpc.Connect(new IPEndPoint(IPAddress.Broadcast, Globals.ScanServerDataPort));
                udpc.Send(connectMessage, connectMessage.Length);
                udpc.Close();
            }

            isRunning = true;
        }

        internal void Disconnect()
        {
            Send(new DisconnectPacket().Raw);
        }

        internal void Stop()
        {
            isRunning = false;
        }

        internal void SetWindow()
        {
            Send(CreateWindowRectangularRequest(Camera.Camera0));
            Send(CreateWindowRectangularRequest(Camera.Camera1));
        }

        internal void StartScanning(double scanRate, AllDataFormat dataFormat, short startColumn, short endColumn)
        {
            this.dataFormat = dataFormat;
            this.scanRate = scanRate;
            this.startColumn = startColumn;
            this.endColumn = endColumn;
            profileAssembler = new ProfileAssembler(scanHead.Profiles, dataFormat, scanHead.Alignment);
            ClearScanRequests();
            lock (scanRequestPacketLock)
            {
                scanRequestPacket = CreateScanRequest();
            }

            evt.Set();
        }

        internal void ClearScanRequests()
        {
            lock (scanRequestPacketLock)
            {
                scanRequestPacket = null;
            }
        }

        #endregion

        #region Private Methods

        private void Send(byte[] packet)
        {
            outgoingPackets.Enqueue(packet);
            evt.Set(); // signal to waiting thread (the sender)
        }

        private void SendMain()
        {
            for (;;)
            {
                try
                {
                    // first send all waiting packets that are not scan requests
                    // block here for specified time
                    var wasSignaled = evt.WaitOne(scanRequestInterval);

                    token.ThrowIfCancellationRequested();

                    if (scanHeadDataIpEndPoint == null) continue;

                    if (wasSignaled)
                    {
                        while (outgoingPackets.Count > 0)
                        {
                            if (outgoingPackets.TryDequeue(out var toSend))
                            {
                                sendUdpClient.Send(toSend, toSend.Length, scanHeadDataIpEndPoint);
                            }
                        }
                    }

                    // now handle periodic scan requests
                    lock (scanRequestPacketLock)
                    {
                        if (scanRequestPacket != null)
                        {
                            sendUdpClient.Send(scanRequestPacket, scanRequestPacket.Length, scanHeadDataIpEndPoint);
                        }
                    }
                }
                // TODO: with switch to AutoResetEvent instead of a blocking wait, this may no longer be needed
                catch (OperationCanceledException)
                {
                    // perfectly normal, nothing to see here
                    break;
                }
                catch (SocketException exception)
                {
                    // Do nothing, we might have lost connection, but we want to keep trying as long as requested
                }
            }
        }

        private void ReceiveMain()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);

            bytesReceived = 0;
            // this callback will kill the socket when the
            // token was canceled, which is the only way to get out
            // of the blocking udpClient.Receive()
            var demuxerDict = new Dictionary<int, ProfileFragments>();
            for (;;)
            {
                try
                {
                    token.ThrowIfCancellationRequested();

                    // next call blocks!
                    var raw = receiveUdpClient.Receive(ref ep);

                    if (!isRunning)
                    {
                        continue; // we ignore everything
                    }

                    var from = ep.Address;
                    var header = new PacketHeader(raw);

                    if (header.Magic == 0xFACD) // Data packet
                    {
                        lastReceivedPacketTime = timeBase.ElapsedMilliseconds;
                        goodPackets++;
                        bytesReceived += raw.Length;
                        var p = new DataPacket(raw, timeBase.ElapsedMilliseconds);
                        // handle the de-multiplexing
                        if (p.NumParts == 1) // one part-datagram
                        {
                            // hand straight over to workers
                            profileAssembler.AssembleProfiles(new ProfileFragments(p, timeBase.ElapsedMilliseconds));
                            continue;
                        }

                        // source is a composite of scan head, camera and laser, we use it to identify packets from the same head/camera/laser combo
                        var id = p.Source;
                        if (!demuxerDict.ContainsKey(id))
                        {
                            // first time we see a packet from this source
                            demuxerDict[id] = new ProfileFragments(p, timeBase.ElapsedMilliseconds);
                        }
                        else
                        {
                            if (demuxerDict[id].Timestamp == p.Timestamp)
                            {
                                // the timestamp on this packet is the same as is the dict, so it must belong to the same  profile
                                demuxerDict[id].Add(p);
                                if (demuxerDict[id].Complete)
                                {
                                    CompleteProfilesReceivedCount++;
                                    // hand it off to a processor thread, we're done with it.
                                    profileAssembler.AssembleProfiles(demuxerDict[id]);
                                    demuxerDict.Remove(id);
                                    // but also record the Id in a fixed size queue, so that stragglers and
                                    // duplicates don't create a mess
                                    // TODO: add a done queue
                                }
                            }
                            else
                            {
                                // the timestamp on the current packet is newer than on the existing
                                // set of packets in the dictionary, which means we either received out of order or
                                // a packet got lost
                                // If we got the next in sequence, we now consider the previous profile done
                                // hand it off to a processor thread, since we're done with it.
                                profileAssembler.AssembleProfiles(demuxerDict[id]);

                                demuxerDict.Remove(id);

                                evictedForNextSeq++;
                                demuxerDict[id] = new ProfileFragments(p, timeBase.ElapsedMilliseconds);
                            }

                            // here we would check for timeouts, but that is tricky since the Receive call blocks.
                            // We may need to de-couple receiving from assembling.
                        }
                    }
                    else if (header.Magic == 0xFACE) // Non-data packets
                    {
                        switch (header.Type)
                        {
                            case ScanPacketType.Status:
                                try
                                {
                                    scanHead.Status = new StatusPacket(raw, from).ScanHeadStatus;
                                }
                                catch (VersionCompatibilityException e)
                                {
                                    IsVersionMismatched = true;
                                    VersionMismatchReason = e.Message;

                                    // Versions are not compatible, try to send a disconnect before bailing
                                    CreateIPEndPoint(from);
                                    Disconnect();
                                    Stop();
                                    break;
                                }

                                if (scanHead.SerialNumber == scanHead.Status.ScanHeadSerialNumber &&
                                    scanHeadDataIpEndPoint == null)
                                {
                                    CreateIPEndPoint(from);
                                }

                                if (scanHeadDataIpEndPoint != null)
                                {
                                    lastReceivedPacketTime = timeBase.ElapsedMilliseconds;
                                }

                                break;
                            default:
                                // Unknown command
                                BadPacketsCount++;
                                break;
                        }
                    }
                    else
                    {
                        // Wrong signature for received packet: expected 0xFACE or 0xFACD
                        BadPacketsCount++;
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Time to break out of receive loop
                    break;
                }
                catch (SocketException)
                {
                    // We get here if we call Close() on the UdpClient
                    // while it is in the Receive(0 call. Apparently
                    // the only way to abort a Receive call is to
                    // close the underlying Socket.
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Time to break out of receive loop
                    break;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    BadPacketsCount++;
                }
            }
        }

        private void CreateIPEndPoint(IPAddress address)
        {
            scanHeadDataIpEndPoint = new IPEndPoint(address, Globals.ScanServerDataPort);
            scanHead.IPAddress = address.MapToIPv4();
        }

        // Iterates over every NIC, virtual and non-virtual and finds
        // IPv4 capable ones
        private static IEnumerable<IPAddress> GetValidNics()
        {
            var validNics = new List<IPAddress>();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(i => i.OperationalStatus == OperationalStatus.Up
                            && i.SupportsMulticast == true);

            foreach (var ni in interfaces)
            {
                var ipv4Props = ni.GetIPProperties().GetIPv4Properties();
                if (NetworkInterface.LoopbackInterfaceIndex != ipv4Props?.Index)
                {
                    var uips = ni.GetIPProperties().UnicastAddresses
                        .Where(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork);
                    foreach (var uip in uips)
                    {
                        if (!validNics.Contains(uip.Address))
                        {
                            validNics.Add(new IPAddress(uip.Address.GetAddressBytes()));
                        }
                    }
                }
            }

            return validNics;
        }

        private byte[] CreateWindowRectangularRequest(Camera camera)
        {
            var size = 8 + 16 * scanHead.Window.WindowConstraints.Count;
            var raw = new byte[size];
            raw[0] = 0xFA;
            raw[1] = 0xCE;
            raw[2] = (byte)size;
            raw[3] = (byte)ScanPacketType.Window;
            raw[4] = (byte)camera;
            for (int i = 0; i < scanHead.Window.WindowConstraints.Count; i++)
            {
                var p1Prime = scanHead.Alignment[camera].MillToCamera(scanHead.Window.WindowConstraints[i].X1,
                    scanHead.Window.WindowConstraints[i].Y1, 0);
                var p2Prime = scanHead.Alignment[camera].MillToCamera(scanHead.Window.WindowConstraints[i].X2,
                    scanHead.Window.WindowConstraints[i].Y2, 0);
                var p1 = scanHead.Alignment[camera].Orientation == ScanHeadOrientation.CableIsUpstream
                    ? p1Prime
                    : p2Prime;
                var p2 = scanHead.Alignment[camera].Orientation == ScanHeadOrientation.CableIsUpstream
                    ? p2Prime
                    : p1Prime;
                Array.Copy(
                    BitConverter.GetBytes(
                        IPAddress.HostToNetworkOrder((int)(p1.X * 1000.0))), 0, raw,
                    8 + i * 16, 4);
                Array.Copy(
                    BitConverter.GetBytes(
                        IPAddress.HostToNetworkOrder((int)(p1.Y * 1000.0))), 0, raw,
                    12 + i * 16, 4);
                Array.Copy(
                    BitConverter.GetBytes(
                        IPAddress.HostToNetworkOrder((int)(p2.X * 1000.0))), 0, raw,
                    16 + i * 16, 4);
                Array.Copy(
                    BitConverter.GetBytes(
                        IPAddress.HostToNetworkOrder((int)(p2.Y * 1000.0))), 0, raw,
                    20 + i * 16, 4);
            }

            return raw;
        }

        private byte[] CreateScanRequest()
        {
            // get the number of data types requested, the length of the steps array is a good proxy
            var stepArray = ResolutionPresets.GetStep(dataFormat);

            var raw = new byte[74 + stepArray.Length * 2];
            raw[0] = 0xFA;
            raw[1] = 0xCE;
            raw[2] = 12;
            raw[3] = (byte)ScanPacketType.StartScanning;
            raw[4] = raw[5] = raw[6] = raw[7] = 0;
            var p = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)LocalReceiveIpEndPoint.Port));
            raw[8] = p[0];
            raw[9] = p[1];
            raw[10] = ScanSystem.SessionId;
            raw[11] = (byte)scanHead.ID;
            //raw[12] = 0x0; //Camera ID
            //raw[13] = 0x0; //Laser ID
            raw[14] = (byte)ExposureMode.Interleaved;
            //raw[15] = 0x0; //Flags

            Array.Copy(
                BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)(scanHead.Configuration.MinLaserOnTime))), 0,
                raw, 16, 4);
            Array.Copy(
                BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)(scanHead.Configuration.DefaultLaserOnTime))),
                0, raw, 20, 4);
            Array.Copy(
                BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)(scanHead.Configuration.MaxLaserOnTime))), 0,
                raw, 24, 4);

            Array.Copy(
                BitConverter.GetBytes(
                    IPAddress.HostToNetworkOrder((int)(scanHead.Configuration.MinCameraExposureTime))), 0, raw, 28, 4);
            Array.Copy(
                BitConverter.GetBytes(
                    IPAddress.HostToNetworkOrder((int)(scanHead.Configuration.DefaultCameraExposureTime))), 0, raw, 32,
                4);
            Array.Copy(
                BitConverter.GetBytes(
                    IPAddress.HostToNetworkOrder((int)(scanHead.Configuration.MaxCameraExposureTime))), 0, raw, 36, 4);

            Array.Copy(
                BitConverter.GetBytes(IPAddress.HostToNetworkOrder(scanHead.Configuration.LaserDetectionThreshold)), 0,
                raw, 40, 4);
            Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(scanHead.Configuration.SaturationThreshold)),
                0, raw, 44, 4);
            Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(scanHead.Configuration.SaturatedPercentage)),
                0, raw, 48, 4);

            Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(scanHead.Configuration.AverageIntensity)), 0,
                raw, 52, 4);

            Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)(1000 / scanRate * 1000))), 0, raw, 56,
                4);
            Array.Copy(
                BitConverter.GetBytes(IPAddress.HostToNetworkOrder((int)(scanHead.Configuration.ScanPhaseOffset))), 0,
                raw, 60, 4);
            Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(int.MaxValue)), 0, raw, 64, 4);

            Array.Copy(
                BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)ResolutionPresets.GetDataType(dataFormat))),
                0, raw, 68, 2);
            Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)startColumn)), 0, raw, 70, 2);
            Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)endColumn)), 0, raw, 72, 2);
            for (int i = 0; i < stepArray.Length; i++)
            {
                Array.Copy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(stepArray[i])), 0, raw, 74 + 2 * i, 2);
            }

            return raw;
        }

        private void StatsThread()
        {
            var lastCheck = timeBase.ElapsedMilliseconds;
            long bytesReceivedSinceLastCheck = 0;
            long profilesReceivedSinceLastCheck = 0;
            while (true)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    Task.Delay(200, token).Wait(token);
                    long now = timeBase.ElapsedMilliseconds;
                    var dataRate = (bytesReceived - bytesReceivedSinceLastCheck) * 1000.0 / (now - lastCheck);
                    var profileRate = (CompleteProfilesReceivedCount - profilesReceivedSinceLastCheck) * 1000 /
                                      (now - lastCheck);

                    commStats.ID = scanHead.ID;
                    commStats.CompleteProfilesReceived = CompleteProfilesReceivedCount;
                    commStats.ProfileRate = profileRate;
                    commStats.BytesReceived = bytesReceived;
                    commStats.Evicted = evictedForTimeout + evictedForNextSeq;
                    commStats.DataRate = dataRate;
                    commStats.BadPackets = BadPacketsCount;
                    commStats.GoodPackets = goodPackets;

                    StatsEvent?.Invoke(this, new CommStatsEventArgs()
                    {
                        ID = commStats.ID,
                        CompleteProfilesReceived = commStats.CompleteProfilesReceived,
                        ProfileRate = commStats.ProfileRate,
                        BytesReceived = commStats.BytesReceived,
                        Evicted = commStats.Evicted,
                        DataRate = commStats.DataRate,
                        BadPackets = commStats.BadPackets,
                        GoodPackets = commStats.GoodPackets
                    });

                    lastCheck = now;
                    bytesReceivedSinceLastCheck = bytesReceived;
                    profilesReceivedSinceLastCheck = CompleteProfilesReceivedCount;
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
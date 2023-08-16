// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Client = joescan.schema.client;
using Server = joescan.schema.server;

namespace JoeScan.Pinchot
{
    internal class ScanHeadSenderReceiver : IDisposable
    {
        #region Private Fields

        private readonly ScanHead scanHead;

        private long bytesReceived;
        private long goodPackets;
        private long evictedForTimeout;

        private readonly byte[] udpReceiveBuffer = new byte[2048];
        private readonly UdpClient udpReceiveClient;
        private readonly Thread udpReceiveThread;

        private readonly TcpClient tcpControlClient;
        private NetworkStream TcpControlStream => tcpControlClient.GetStream();

        private readonly byte[] tcpDataReceiveBuffer = new byte[8192];
        private readonly Memory<byte> tcpDataReceiveMemory;
        private readonly TcpClient tcpDataClient;
        private NetworkStream TcpDataStream => tcpDataClient.GetStream();
        private Thread tcpDataReceiveThread;

        private CancellationTokenSource tokenSource = new CancellationTokenSource();
        private CancellationToken token;

        private ProfileAssembler profileAssembler;
        private uint idleSkipCount;
        private long lastEncoderCount;
        private uint currentSkipCount;

        private readonly CommStatsEventArgs commStats = new CommStatsEventArgs();
        private readonly Thread commStatsThread;

        // used to timestamp data packets internally
        private readonly Stopwatch timeBase = Stopwatch.StartNew();

        private bool disposed;

        private readonly byte[] StartScanningRequest = new Client::MessageClientT() { Type = Client::MessageType.SCAN_START }.SerializeToBinary();
        private readonly byte[] StopScanningRequest = new Client::MessageClientT() { Type = Client::MessageType.SCAN_STOP }.SerializeToBinary();
        private readonly byte[] DisconnectRequest = new Client::MessageClientT() { Type = Client::MessageType.DISCONNECT }.SerializeToBinary();
        private readonly byte[] KeepAliveRequest = new Client::MessageClientT() { Type = Client::MessageType.KEEP_ALIVE }.SerializeToBinary();
        private readonly byte[] StatusRequest = new Client::MessageClientT() { Type = Client::MessageType.STATUS_REQUEST }.SerializeToBinary();

        #endregion

        #region Events

        internal event EventHandler<CommStatsEventArgs> StatsEvent;

        #endregion

        #region Internal Properties

        internal IPEndPoint LocalReceiveIpEndPoint => (IPEndPoint)udpReceiveClient.Client.LocalEndPoint;

        internal bool ProfileBufferOverflowed { get; private set; }

        internal long CompleteProfilesReceivedCount { get; private set; }

        internal long IncompleteProfilesReceivedCount { get; private set; }

        internal long BadPacketsCount { get; private set; }

        internal Exception TcpException { get; private set; }

        #endregion

        #region Lifecycle

        internal ScanHeadSenderReceiver(ScanHead scanHead, bool enableCommStats)
        {
            this.scanHead = scanHead;
            token = tokenSource.Token;

            udpReceiveClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0))
            {
                Client = { ReceiveBufferSize = Globals.ReceiveDataBufferSize }
            };

            tcpControlClient = new TcpClient(new IPEndPoint(scanHead.ClientIpAddress, 0))
            {
                // disable Nagle's algorithm since we are going to be sending tiny header
                // packets with each TCP transmission and don't want that to be delayed
                NoDelay = true
            };

            tcpDataReceiveMemory = new Memory<byte>(tcpDataReceiveBuffer);
            tcpDataClient = new TcpClient(new IPEndPoint(scanHead.ClientIpAddress, 0))
            {
                ReceiveBufferSize = Globals.ReceiveDataBufferSize
            };

            udpReceiveThread = new Thread(UdpReceiveLoop) { Priority = ThreadPriority.Highest, IsBackground = true };
            udpReceiveThread.Start();

            if (enableCommStats)
            {
                commStatsThread = new Thread(CommStatsLoop) { Priority = ThreadPriority.Lowest, IsBackground = true };
                commStatsThread.Start();
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
            {
                return;
            }

            if (disposing)
            {
                tokenSource.Cancel();
                udpReceiveClient.Close();
                tcpControlClient.Close();
                tcpDataClient.Close();
                udpReceiveThread?.Join();
                tcpDataReceiveThread?.Join();
                commStatsThread?.Join();
                tokenSource.Dispose();
            }

            disposed = true;
        }

        #endregion

        #region Internal Methods

        internal bool Connect(Client::ConnectionType connType, TimeSpan timeout)
        {
            bytesReceived = 0L;
            goodPackets = 0L;
            BadPacketsCount = 0L;
            evictedForTimeout = 0L;
            IncompleteProfilesReceivedCount = 0L;

            byte[] message = new Client::MessageClientT()
            {
                Type = Client.MessageType.CONNECT,
                Data = new Client::MessageDataUnion()
                {
                    Type = Client::MessageData.ConnectData,
                    Value = new Client::ConnectDataT()
                    {
                        ConnectionType = connType,
                        ScanHeadId = scanHead.ID,
                        ScanHeadSerial = scanHead.SerialNumber
                    }
                }
            }.SerializeToBinary();

            return Connect(message, timeout);
        }

        internal void Disconnect()
        {
            try
            {
                TcpSend(DisconnectRequest, TcpControlStream);
            }
            catch
            {
                // if the TCP connection was harshly severed then
                // TcpSend would throw an exception, we should
                // ignore this for two reasons:
                // 1) we want to disconnect anyway
                // 2) this would prevent the API from sending a
                //    message to other heads
            }

            tokenSource.Cancel();
        }

        internal void SendWindow(CameraLaserPair pair)
        {
            byte[] windowReq = CreateWindowRectangularRequest(pair);
            TcpSend(windowReq, TcpControlStream);
        }

        internal void SendExclusionMask(CameraLaserPair pair)
        {
            byte[] maskReq = CreateExclusionMaskRequest(pair);
            TcpSend(maskReq, TcpControlStream);
        }

        internal void SendBrightnessCorrection(CameraLaserPair pair)
        {
            byte[] correctionReq = CreateBrightnessCorrectionRequest(pair);
            TcpSend(correctionReq, TcpControlStream);
        }

        internal void StartScanning(uint periodUs, AllDataFormat dataFormat)
        {
            ProfileBufferOverflowed = false;

            profileAssembler = new ProfileAssembler(scanHead, dataFormat);
            idleSkipCount = scanHead.Configuration.IdleScanPeriodUs / periodUs;

            byte[] confReq = CreateScanConfigurationRequest(periodUs, dataFormat);
            TcpSend(confReq, TcpControlStream);
            TcpSend(StartScanningRequest, TcpControlStream);
        }

        internal void StopScanning()
        {
            try
            {
                TcpSend(StopScanningRequest, TcpControlStream);
            }
            catch
            {
                // if the TCP connection was harshly severed then
                // TcpSend would throw an exception, we should
                // ignore this for two reasons:
                // 1) the server is most likely dead in this case
                //    so there is nothing we can do
                // 2) this would prevent the API from sending a
                //    message to other heads
            }
        }

        internal void KeepAlive()
        {
            TcpSend(KeepAliveRequest, TcpControlStream);
        }

        internal Server::StatusDataT RequestStatus()
        {
            TcpSend(StatusRequest, TcpControlStream);
            byte[] buf = TcpRead(TcpControlStream);

            var rsp = Server::MessageServerT.DeserializeFromBinary(buf);
            if (rsp.Type != Server.MessageType.STATUS)
            {
                throw new InvalidOperationException($"Status request returned unexpected type {rsp.Type}");
            }

            return rsp.Data.AsStatusData();
        }

        internal Server::ImageData RequestDiagnosticImage(Client::ImageRequestDataT settings)
        {
            byte[] req = new Client::MessageClientT()
            {
                Type = Client::MessageType.IMAGE_REQUEST,
                Data = new Client::MessageDataUnion()
                {
                    Type = Client::MessageData.ImageRequestData,
                    Value = settings
                }
            }.SerializeToBinary();

            TcpSend(req, TcpControlStream);
            byte[] buf = TcpRead(TcpControlStream);

            // Don't use object API in order to save memory since these messages are fairly large
            var bb = new FlatBuffers.ByteBuffer(buf);
            var rsp = Server::MessageServer.GetRootAsMessageServer(bb);
            if (rsp.DataType != Server.MessageData.ImageData)
            {
                throw new InvalidOperationException($"Image response not the right datatype! ({rsp.DataType})");
            }

            return rsp.DataAsImageData();
        }

        internal Server::ProfileDataT RequestDiagnosticProfile(Client::ProfileRequestDataT requestData)
        {
            byte[] req = new Client::MessageClientT
            {
                Type = Client::MessageType.PROFILE_REQUEST,
                Data = new Client::MessageDataUnion
                {
                    Type = Client::MessageData.ProfileRequestData,
                    Value = requestData
                }
            }.SerializeToBinary();

            TcpSend(req, TcpControlStream);
            byte[] buf = TcpRead(TcpControlStream);

            var rsp = Server::MessageServerT.DeserializeFromBinary(buf);
            if (rsp.Type != Server.MessageType.PROFILE)
            {
                throw new InvalidOperationException($"Profile request returned unexpected type {rsp.Type}");
            }

            return rsp.Data.AsProfileData();
        }

        internal Server::MappleDataT RequestMappleData(Client::MappleRequestDataT request)
        {
            byte[] req = new Client::MessageClientT()
            {
                Type = Client::MessageType.MAPPLE_REQUEST,
                Data = new Client::MessageDataUnion()
                {
                    Type = Client::MessageData.MappleRequestData,
                    Value = request
                }
            }.SerializeToBinary();

            TcpSend(req, TcpControlStream);
            byte[] buf = TcpRead(TcpControlStream);

            var rsp = Server::MessageServerT.DeserializeFromBinary(buf);
            if (rsp.Type != Server.MessageType.MAPPLE_DATA)
            {
                throw new InvalidOperationException($"Mapple data request returned unexpected type {rsp.Type}");
            }

            return rsp.Data.AsMappleData();
        }

        /// <summary>
        /// Send a TCP message to the <paramref name="stream"/> with <paramref name="packet"/> as the payload.
        /// </summary>
        internal static void TcpSend(byte[] packet, NetworkStream stream)
        {
            // Framing packet
            stream.Write(BitConverter.GetBytes(packet.Length), 0, sizeof(int));
            // Payload
            stream.Write(packet, 0, packet.Length);
        }

        /// <summary>
        /// Receives a TCP message from the <paramref name="stream"/>.
        /// </summary>
        internal static byte[] TcpRead(NetworkStream stream)
        {
            // The server first sends a 4-byte message representing the size of the payload in bytes
            // followed by the payload itself in another message. This is needed because TCP packets
            // can be fragmented and a single read isn't guarenteed to get the whole payload.
            const int frameSize = sizeof(int);
            byte[] frameBuf = new byte[frameSize];

            // Get the payload size
            int curr = 0;
            while (curr < frameSize)
            {
                int r = stream.Read(frameBuf, curr, frameSize - curr);
                if (r == 0)
                {
                    throw new Exception("Remote host terminated connection.");
                }

                curr += r;
            }

            int dataSize = BitConverter.ToInt32(frameBuf, 0);
            byte[] buf = new byte[dataSize];

            // Get the payload
            curr = 0;
            while (curr < dataSize)
            {
                int r = stream.Read(buf, curr, dataSize - curr);
                if (r == 0)
                {
                    throw new Exception("Remote host terminated connection.");
                }

                curr += r;
            }

            return buf;
        }

        /// <summary>
        /// Asynchronously send a TCP message to the <paramref name="stream"/> with <paramref name="packet"/> as the payload.
        /// </summary>
        internal static async Task TcpSendAsync(byte[] packet, NetworkStream stream)
        {
            // Framing packet
            await stream.WriteAsync(BitConverter.GetBytes(packet.Length), 0, sizeof(int));
            // Payload
            await stream.WriteAsync(packet, 0, packet.Length);
        }

        /// <summary>
        /// Asynchronously receives a TCP message from the <paramref name="stream"/>.
        /// </summary>
        internal static async Task<byte[]> TcpReadAsync(NetworkStream stream)
        {
            // The server first sends a 4-byte message representing the size of the payload in bytes
            // followed by the payload itself in another message. This is needed because TCP packets
            // can be fragmented and a single read isn't guarenteed to get the whole payload.
            const int frameSize = sizeof(int);
            byte[] frameBuf = new byte[frameSize];

            // Get the payload size
            int curr = 0;
            while (curr < frameSize)
            {
                int r = await stream.ReadAsync(frameBuf, curr, frameSize - curr);
                if (r == 0)
                {
                    throw new Exception("Remote host terminated connection.");
                }

                curr += r;
            }

            int dataSize = BitConverter.ToInt32(frameBuf, 0);
            byte[] buf = new byte[dataSize];

            // Get the payload
            curr = 0;
            while (curr < dataSize)
            {
                int r = await stream.ReadAsync(buf, curr, dataSize - curr);
                if (r == 0)
                {
                    throw new Exception("Remote host terminated connection.");
                }

                curr += r;
            }

            return buf;
        }

        #endregion

        #region Private Methods

        private bool Connect(byte[] connectMsg, TimeSpan timeout)
        {
            if (scanHead.IpAddress == null)
            {
                return false;
            }

            var controlResult = tcpControlClient.BeginConnect(scanHead.IpAddress, Globals.ScanServerControlPort, null, null);
            var dataResult = tcpDataClient.BeginConnect(scanHead.IpAddress, Globals.ScanServerDataPort, null, null);

            foreach (var h in new WaitHandle[] { controlResult.AsyncWaitHandle, dataResult.AsyncWaitHandle })
            {
                if (!h.WaitOne(timeout))
                {
                    return false;
                }
            }

            try
            {
                tcpControlClient.EndConnect(controlResult);
                tcpDataClient.EndConnect(dataResult);
            }
            catch (SocketException e)
            {
                // TODO: Move the `catch` up higher in the stack
                throw new InvalidOperationException(
                    $"Scan head {scanHead.SerialNumber} was discovered but connection was refused. Try rebooting?" +
                    $"\nSocket error {e.SocketErrorCode}.");
            }

            TcpSend(connectMsg, TcpControlStream);

            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;

            tcpDataReceiveThread = new Thread(TcpDataReceiveLoop) { Priority = ThreadPriority.Highest, IsBackground = true };
            tcpDataReceiveThread.Start();

            return true;
        }

        private void QueueProfile(Profile profile)
        {
            if (scanHead.Configuration.MinimumEncoderTravel != 0)
            {
                long currentEncoderCount = profile.EncoderValues[Encoder.Main];
                long encoderTravel = currentEncoderCount - lastEncoderCount;
                lastEncoderCount = currentEncoderCount;

                if (encoderTravel < scanHead.Configuration.MinimumEncoderTravel)
                {
                    if (idleSkipCount == 0)
                    {
                        return;
                    }
                    else if (idleSkipCount == currentSkipCount)
                    {
                        currentSkipCount = 0;
                    }
                    else
                    {
                        ++currentSkipCount;
                        return;
                    }
                }
            }

            var profiles = scanHead.Profiles;
            if (!profiles.TryAdd(profile))
            {
                ProfileBufferOverflowed = true;
                profiles.TryTake(out _);
                profiles.TryAdd(profile);
            }
        }

        private async void TcpDataReceiveLoop()
        {
            bytesReceived = 0;
            int lastID = 0;
            ulong lastTimestamp = 0;
            Profile profile = null;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    // read data size
                    int expected = sizeof(int);
                    int current = 0;

                    while (current < expected)
                    {
                        try
                        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                            current += await TcpDataStream.ReadAsync(tcpDataReceiveMemory[current..expected], token).ConfigureAwait(false);
#else
                            current += await TcpDataStream.ReadAsync(tcpDataReceiveBuffer, current, expected - current, token).ConfigureAwait(false);
#endif
                        }
                        catch (IOException e) when (e.InnerException is SocketException se)
                        {
                            switch (se.SocketErrorCode)
                            {
                                case SocketError.Interrupted:
                                case SocketError.TryAgain:
                                case SocketError.WouldBlock:
                                    continue;
                                default:
                                    throw;
                            }
                        }
                    }

                    // read data
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                    expected = BitConverter.ToInt32(tcpDataReceiveMemory[0..expected].Span);
#else
                    expected = BitConverter.ToInt32(tcpDataReceiveBuffer, 0);
#endif

                    current = 0;
                    while (current < expected)
                    {
                        try
                        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
                            current += await TcpDataStream.ReadAsync(tcpDataReceiveMemory[current..expected], token).ConfigureAwait(false);
#else
                            current += await TcpDataStream.ReadAsync(tcpDataReceiveBuffer, current, expected - current, token).ConfigureAwait(false);
#endif
                        }
                        catch (IOException e) when (e.InnerException is SocketException se)
                        {
                            switch (se.SocketErrorCode)
                            {
                                case SocketError.Interrupted:
                                case SocketError.TryAgain:
                                case SocketError.WouldBlock:
                                    continue;
                                default:
                                    throw;
                            }
                        }
                    }

                    byte[] packet = tcpDataReceiveBuffer;
                    ushort magic = (ushort)((packet[0] << 8) + packet[1]);
                    if (magic != Globals.DataMagic)
                    {
                        BadPacketsCount++;
                        continue;
                    }

                    var dataHeader = new DataPacketHeader(packet);

                    if (profile == null)
                    {
                        profile = profileAssembler.CreateNewProfile(dataHeader);
                    }
                    else if (dataHeader.Source != lastID || dataHeader.TimestampNs != lastTimestamp)
                    {
                        ++IncompleteProfilesReceivedCount;
                        QueueProfile(profile);
                        profile = profileAssembler.CreateNewProfile(dataHeader);
                    }

                    bool isComplete = profileAssembler.ProcessPacket(profile, dataHeader, packet);
                    goodPackets++;

                    if (isComplete)
                    {
                        ++CompleteProfilesReceivedCount;
                        QueueProfile(profile);
                        profile = null;
                    }

                    lastID = dataHeader.Source;
                    lastTimestamp = dataHeader.TimestampNs;
                }
            }
            catch (Exception e)
            {
                // TODO: Handle this in a better way
                // We can either try to reconnect or disconnect
                // and let the user know something happened.
                TcpException = e;
            }
        }

        private void UdpReceiveLoop()
        {
            EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
            bytesReceived = 0;
            int lastID = 0;
            ulong lastTimestamp = 0;
            Profile profile = null;

            while (true)
            {
                try
                {
                    token.ThrowIfCancellationRequested();

                    bytesReceived += udpReceiveClient.Client.ReceiveFrom(udpReceiveBuffer, ref ep);
                    var packet = udpReceiveBuffer.AsSpan();

                    ushort magic = (ushort)((packet[0] << 8) + packet[1]);
                    if (magic == Globals.DataMagic) // Data packet
                    {
                        var dataHeader = new DataPacketHeader(packet);

                        if (profile == null)
                        {
                            profile = profileAssembler.CreateNewProfile(dataHeader);
                        }
                        else if (dataHeader.Source != lastID || dataHeader.TimestampNs != lastTimestamp)
                        {
                            ++IncompleteProfilesReceivedCount;
                            QueueProfile(profile);
                            profile = profileAssembler.CreateNewProfile(dataHeader);
                        }

                        bool isComplete = profileAssembler.ProcessPacket(profile, dataHeader, packet);
                        goodPackets++;

                        if (isComplete)
                        {
                            ++CompleteProfilesReceivedCount;
                            QueueProfile(profile);
                            profile = null;
                        }

                        lastID = dataHeader.Source;
                        lastTimestamp = dataHeader.TimestampNs;
                    }
                    else
                    {
                        // Unknown command
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
                catch (Exception)
                {
                    BadPacketsCount++;
                }
            }
        }

        private byte[] CreateWindowRectangularRequest(CameraLaserPair pair)
        {
            var constraints = new List<Client::ConstraintT>();
            var window = scanHead.Windows[pair];
            var alignment = scanHead.Alignments[pair];

            foreach (var (wc0, wc1) in window.WindowConstraints)
            {
                var p0Prime = alignment.MillToCamera(wc0.X, wc0.Y, 0);
                var p1Prime = alignment.MillToCamera(wc1.X, wc1.Y, 0);
                bool isCableDownstream = alignment.Orientation == ScanHeadOrientation.CableIsDownstream;
                var p0 = isCableDownstream ? p0Prime : p1Prime;
                var p1 = isCableDownstream ? p1Prime : p0Prime;
                constraints.Add(new Client::ConstraintT()
                {
                    X0 = (long)p0.X,
                    Y0 = (long)p0.Y,
                    X1 = (long)p1.X,
                    Y1 = (long)p1.Y
                });
            }

            var message = new Client::MessageClientT()
            {
                Type = Client::MessageType.WINDOW_CONFIGURATION,
                Data = new Client::MessageDataUnion()
                {
                    Type = Client::MessageData.WindowConfigurationData,
                    Value = new Client::WindowConfigurationDataT()
                    {
                        CameraPort = scanHead.CameraIdToPort(pair.Camera),
                        LaserPort = scanHead.LaserIdToPort(pair.Laser),
                        Constraints = constraints
                    }
                }
            };

            return message.SerializeToBinary();
        }

        private byte[] CreateExclusionMaskRequest(CameraLaserPair pair)
        {
            var mask = scanHead.ExclusionMasks[pair];

            var message = new Client::MessageClientT
            {
                Type = Client::MessageType.EXCLUSION_MASK,
                Data = new Client::MessageDataUnion
                {
                    Type = Client.MessageData.ExclusionMaskData,
                    Value = new Client::ExclusionMaskDataT
                    {
                        CameraPort = scanHead.CameraIdToPort(pair.Camera),
                        LaserPort = scanHead.LaserIdToPort(pair.Laser),
                        Mask = mask.GetMask().ToList()
                    }
                }
            };

            return message.SerializeToBinary();
        }

        private byte[] CreateBrightnessCorrectionRequest(CameraLaserPair pair)
        {
            var correction = scanHead.BrightnessCorrections[pair];

            var message = new Client::MessageClientT
            {
                Type = Client::MessageType.BRIGHTNESS_CORRECTION,
                Data = new Client::MessageDataUnion
                {
                    Type = Client.MessageData.BrightnessCorrectionData,
                    Value = new Client::BrightnessCorrectionDataT
                    {
                        CameraPort = scanHead.CameraIdToPort(pair.Camera),
                        LaserPort = scanHead.LaserIdToPort(pair.Laser),
                        ImageOffset = correction.Offset,
                        ScaleFactors = correction.GetScaleFactors(),
                    }
                }
            };

            return message.SerializeToBinary();
        }

        private byte[] CreateScanConfigurationRequest(uint periodUs, AllDataFormat dataFormat)
        {
            var conf = scanHead.Configuration;
            var message = new Client::MessageClientT()
            {
                Type = Client::MessageType.SCAN_CONFIGURATION,
                Data = new Client::MessageDataUnion()
                {
                    Type = Client::MessageData.ScanConfigurationData,
                    Value = new Client::ScanConfigurationDataT()
                    {
                        CameraLaserConfigurations = scanHead.CameraLaserConfigurations,
                        DataStride = ResolutionPresets.GetStep(dataFormat),
                        DataTypeMask = (uint)ResolutionPresets.GetDataType(dataFormat),
                        LaserDetectionThreshold = conf.LaserDetectionThreshold,
                        SaturationPercent = conf.SaturationPercentage,
                        SaturationThreshold = conf.SaturationThreshold,
                        ScanPeriodNs = periodUs * 1000,
                        UdpPort = (ushort)LocalReceiveIpEndPoint.Port,
                    }
                }
            };

            return message.SerializeToBinary();
        }

        private void CommStatsLoop()
        {
            long lastCheck = timeBase.ElapsedMilliseconds;
            long bytesReceivedSinceLastCheck = 0;
            long profilesReceivedSinceLastCheck = 0;
            while (true)
            {
                try
                {
                    token.WaitHandle.WaitOne(200);
                    token.ThrowIfCancellationRequested();
                    long now = timeBase.ElapsedMilliseconds;
                    double dataRate = (bytesReceived - bytesReceivedSinceLastCheck) * 1000.0 / (now - lastCheck);
                    long profileRate = (CompleteProfilesReceivedCount - profilesReceivedSinceLastCheck) * 1000 /
                                      (now - lastCheck);

                    commStats.ID = scanHead.ID;
                    commStats.CompleteProfilesReceived = CompleteProfilesReceivedCount;
                    commStats.ProfileRate = profileRate;
                    commStats.BytesReceived = bytesReceived;
                    commStats.Evicted = evictedForTimeout + IncompleteProfilesReceivedCount;
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

// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
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

        private bool disposed;
        private static readonly object tcp_send_lock = new object();

        private readonly byte[] StopScanningRequest = new Client::MessageClientT() { Type = Client::MessageType.SCAN_STOP }.SerializeToBinary();
        private readonly byte[] DisconnectRequest = new Client::MessageClientT() { Type = Client::MessageType.DISCONNECT }.SerializeToBinary();
        private readonly byte[] KeepAliveRequest = new Client::MessageClientT() { Type = Client::MessageType.KEEP_ALIVE }.SerializeToBinary();
        private readonly byte[] StatusRequest = new Client::MessageClientT() { Type = Client::MessageType.STATUS_REQUEST }.SerializeToBinary();

        #endregion

        #region Internal Properties

        internal bool IsConnected { get; private set; }

        internal bool IsScanning { get; private set; }

        internal bool ProfileBufferOverflowed { get; private set; }

        internal long CompleteProfilesReceivedCount { get; private set; }

        internal long IncompleteProfilesReceivedCount { get; private set; }

        internal long BadPacketsCount { get; private set; }

        internal ScanningMode Mode { get; set; }

        #endregion

        #region Lifecycle

        internal ScanHeadSenderReceiver(ScanHead scanHead)
        {
            this.scanHead = scanHead;
            token = tokenSource.Token;

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
                Close();
                tcpDataReceiveThread?.Join();
                tokenSource.Dispose();
            }

            disposed = true;
        }

        #endregion

        #region Internal Methods

        internal bool Connect(Client::ConnectionType connType, TimeSpan timeout)
        {
            if (scanHead.IpAddress == null)
            {
                Close();
                return false;
            }

            var controlResult = tcpControlClient.BeginConnect(scanHead.IpAddress, Globals.ScanServerControlPort, null, null);
            var dataResult = tcpDataClient.BeginConnect(scanHead.IpAddress, Globals.ScanServerDataPort, null, null);

            foreach (var h in new WaitHandle[] { controlResult.AsyncWaitHandle, dataResult.AsyncWaitHandle })
            {
                if (!h.WaitOne(timeout))
                {
                    Close();
                    return false;
                }
            }

            try
            {
                tcpControlClient.EndConnect(controlResult);
                tcpDataClient.EndConnect(dataResult);
            }
            catch (Exception e)
            {
                Close();
                throw new IOException($"Scan head {scanHead.SerialNumber} has IP but connection was refused. Try power cycling the scan head.", e);
            }

            BadPacketsCount = 0L;
            IncompleteProfilesReceivedCount = 0L;

            byte[] connectMsg = new Client::MessageClientT()
            {
                Type = Client.MessageType.CONNECT,
                Data = new Client::MessageDataUnion()
                {
                    Type = Client::MessageData.ConnectData,
                    Value = new Client::ConnectDataT()
                    {
                        ConnectionType = connType,
                        ScanHeadId = scanHead.ID,
                        ScanHeadSerial = scanHead.SerialNumber,
                        Notes = new List<string>() { $".NET API {VersionInformation.Version}" }
                    }
                }
            }.SerializeToBinary();

            TcpSendControl(connectMsg);

            tokenSource = new CancellationTokenSource();
            token = tokenSource.Token;

            tcpDataReceiveThread = new Thread(TcpDataReceiveLoop)
            {
                Priority = ThreadPriority.Highest,
                IsBackground = true
            };

            IsConnected = true;
            tcpDataReceiveThread.Start();
            return true;
        }

        internal void Disconnect()
        {
            IsConnected = false;

            // try to send disconnect message while ignoring any exceptions
            // since we are going to close the connection anyway
            try { TcpSendControl(DisconnectRequest); } catch { }
            try { tokenSource?.Cancel(); } catch { }
        }

        /// <summary>
        /// Close the data and control clients without sending a disconnect message.
        /// This should be used when a TCP error is detected to completely close the
        /// connection and stop any further communications to the scan head.
        /// </summary>
        internal void Close()
        {
            IsConnected = false;
            try { tokenSource?.Cancel(); } catch { }
            try { tcpControlClient?.Close(); } catch { }
            try { tcpDataClient?.Close(); } catch { }
        }

        internal void SendWindow(CameraLaserPair pair)
        {
            byte[] windowReq = CreateWindowRectangularRequest(pair);
            TcpSendControl(windowReq);
        }

        internal void SendExclusionMask(CameraLaserPair pair)
        {
            byte[] maskReq = CreateExclusionMaskRequest(pair);
            TcpSendControl(maskReq);
        }

        internal void SendBrightnessCorrection(CameraLaserPair pair)
        {
            byte[] correctionReq = CreateBrightnessCorrectionRequest(pair);
            TcpSendControl(correctionReq);
        }

        internal void SendAlignmentStoreData(CameraLaserPair pair)
        {
            byte[] alignmentReq = CreateAlignmentStoreRequest(pair);
            TcpSendControl(alignmentReq);
        }

        internal void SendCorrectionStoreData(CameraLaserPair pair, double x, double y, double roll, List<string> notes = null)
        {
            byte[] correctionReq = CreateCorrectionStoreRequest(pair, x, y, roll, notes);
            TcpSendControl(correctionReq);
        }

        internal void StartScanning(uint periodUs, AllDataFormat dataFormat, ulong startTimeNs)
        {
            ProfileBufferOverflowed = false;

            profileAssembler = new ProfileAssembler(scanHead, dataFormat);
            idleSkipCount = scanHead.Configuration.IdleScanPeriodUs / periodUs;

            byte[] confReq = CreateScanConfigurationRequest(periodUs, dataFormat);
            TcpSendControl(confReq);
            byte[] startReq = CreateStartScanningRequest(startTimeNs);
            TcpSendControl(startReq);

            IsScanning = true;
        }

        internal void StopScanning()
        {
            IsScanning = false;

            try
            {
                TcpSendControl(StopScanningRequest);
            }
            catch
            {
                // if the TCP connection was harshly severed then
                // TcpSendControl would throw an exception, we should
                // ignore this for two reasons:
                // 1) the server is most likely dead in this case
                //    so there is nothing we can do
                // 2) this would prevent the API from sending a
                //    message to other heads
            }
        }

        internal void KeepAlive()
        {
            TcpSendControl(KeepAliveRequest);
        }

        internal Server::StatusDataT RequestStatus()
        {
            TcpSendControl(StatusRequest);
            byte[] buf = TcpReadControl();

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

            TcpSendControl(req);
            byte[] buf = TcpReadControl();

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

            TcpSendControl(req);
            byte[] buf = TcpReadControl();

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

            TcpSendControl(req);
            byte[] buf = TcpReadControl();

            var rsp = Server::MessageServerT.DeserializeFromBinary(buf);
            if (rsp.Type != Server.MessageType.MAPPLE_DATA)
            {
                throw new InvalidOperationException($"Mapple data request returned unexpected type {rsp.Type}");
            }

            return rsp.Data.AsMappleData();
        }

        /// <summary>
        /// Send a TCP message on the <see cref="TcpControlStream"/> with <paramref name="packet"/> as the payload.
        /// </summary>
        /// <param name="packet">The data to send.</param>
        /// <exception cref="IOException">
        /// Thrown when the TCP message fails to send.
        /// </exception>
        internal void TcpSendControl(byte[] packet)
        {
            try
            {
                TcpSend(packet, TcpControlStream);
            }
            catch (Exception e)
            {
                Close();
                throw new IOException($"Scan head {scanHead.SerialNumber} failed to send TCP message, possible network or power issue.", e);
            }
        }

        /// <summary>
        /// Send a TCP message to the <paramref name="stream"/> with <paramref name="packet"/> as the payload.
        /// </summary>
        /// <param name="packet">The data to send.</param>
        /// <param name="stream">The stream to send the data to.</param>
        /// <exception cref="IOException">
        /// Thrown when the TCP message fails to send.
        /// </exception>
        internal static void TcpSend(byte[] packet, NetworkStream stream)
        {
            // we need to lock to ensure that the framing packet and the
            // payload are sent together because multiple threads could
            // be sending commands at the same time (keep alive, async ops, etc)
            lock (tcp_send_lock)
            {
                // Framing packet
                stream.Write(BitConverter.GetBytes(packet.Length), 0, sizeof(int));
                // Payload
                stream.Write(packet, 0, packet.Length);
            }
        }

        /// <summary>
        /// Receives a TCP message from the <see cref="TcpControlStream"/>.
        /// </summary>
        /// <exception cref="IOException">
        /// Thrown when remote host terminates connection.
        /// </exception>
        internal byte[] TcpReadControl()
        {
            try
            {
                return TcpRead(TcpControlStream);
            }
            catch (Exception e)
            {
                Close();
                throw new IOException($"Scan head {scanHead.SerialNumber} failed to read TCP message, possible network or power issue.", e);
            }
        }

        /// <summary>
        /// Receives a TCP message from the <paramref name="stream"/>.
        /// </summary>
        /// <exception cref="IOException">
        /// Thrown when remote host terminates connection.
        /// </exception>
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
                    throw new IOException("Remote host terminated connection.");
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
                    throw new IOException("Remote host terminated connection.");
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

            // TODO: unify profile and frame queueing
            if (Mode == ScanningMode.Profile)
            {
                var profiles = scanHead.Profiles;
                if (!profiles.TryAdd(profile))
                {
                    ProfileBufferOverflowed = true;
                    profiles.TryTake(out _);
                    profiles.TryAdd(profile);
                }
            }
            else if (Mode == ScanningMode.Frame)
            {
                scanHead.QueueManager.EnqueueProfile(profile);
            }
            else
            {
                throw new InvalidOperationException($"Unhandled queueing strategy for {Mode}");
            }
        }

        private async void TcpDataReceiveLoop()
        {
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
            catch (OperationCanceledException) { }
            catch
            {
                Close();
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
                        ScanPeriodNs = periodUs * 1000
                    }
                }
            };

            return message.SerializeToBinary();
        }

        private byte[] CreateStartScanningRequest(ulong startTimeNs)
        {
            var message = new Client::MessageClientT()
            {
                Type = Client::MessageType.SCAN_START,
                Data = new Client::MessageDataUnion()
                {
                    Type = Client::MessageData.ScanStartData,
                    Value = new Client::ScanStartDataT()
                    {
                        StartTimeNs = startTimeNs
                    }
                }
            };

            return message.SerializeToBinary();
        }

        private byte[] CreateAlignmentStoreRequest(CameraLaserPair pair, List<string> additionalNotes = null)
        {
            var alignment = scanHead.Alignments[pair];

            var notes = new List<string>() { $".NET API {VersionInformation.Version}" };
            if (additionalNotes != null)
            {
                notes.AddRange(additionalNotes);
            }

            var message = new Client::MessageClientT
            {
                Type = Client.MessageType.STORE_INFO,
                Data = new Client.MessageDataUnion
                {
                    Type = Client.MessageData.StoreInfoData,
                    Value = new Client.StoreInfoDataT
                    {
                        Type = Client.StoreType.ALIGNMENT,
                        Notes = notes,
                        TimestampS = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Data = new Client.StoreDataUnion
                        {
                            Type = Client.StoreData.StoreAlignmentData,
                            Value = new Client.StoreAlignmentDataT
                            {
                                CameraPort = scanHead.CameraIdToPort(pair.Camera),
                                LaserPort = scanHead.LaserIdToPort(pair.Laser),
                                YOffset = alignment.ShiftY,
                                XOffset = alignment.ShiftX,
                                Roll = alignment.Roll
                            }
                        }
                    }
                }
            };

            return message.SerializeToBinary();
        }

        private byte[] CreateCorrectionStoreRequest(CameraLaserPair pair, double x, double y, double roll, List<string> additionalNotes = null)
        {
            var notes = new List<string>() { $".NET API {VersionInformation.Version}" };
            if (additionalNotes != null)
            {
                notes.AddRange(additionalNotes);
            }

            var message = new Client::MessageClientT
            {
                Type = Client.MessageType.STORE_INFO,
                Data = new Client.MessageDataUnion
                {
                    Type = Client.MessageData.StoreInfoData,
                    Value = new Client.StoreInfoDataT
                    {
                        Type = Client.StoreType.MAPPLE_CORRECTION,
                        Notes = notes,
                        TimestampS = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Data = new Client.StoreDataUnion
                        {
                            Type = Client.StoreData.StoreMappleCorrectionData,
                            Value = new Client.StoreMappleCorrectionDataT
                            {
                                CameraPort = scanHead.CameraIdToPort(pair.Camera),
                                LaserPort = scanHead.LaserIdToPort(pair.Laser),
                                XOffset = x,
                                YOffset = y,
                                Roll = roll
                            }
                        }
                    }
                }
            };

            return message.SerializeToBinary();
        }

        #endregion
    }
}

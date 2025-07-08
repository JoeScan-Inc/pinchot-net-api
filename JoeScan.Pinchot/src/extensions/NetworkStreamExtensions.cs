using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace JoeScan.Pinchot
{
    internal static class NetworkStreamExtensions
    {
        /// <summary>
        /// Sends a framed packet over the network stream. The packet is prefixed with a 4-byte length header
        /// followed by the payload. The caller is responsible for locking the stream so it isn't interrupted
        /// by other threads.
        /// </summary>
        /// <param name="stream">The stream to send the packet to.</param>
        /// <param name="packet">The payload.</param>
        internal static void SendFramedPacket(this NetworkStream stream, byte[] packet)
        {
            // Framing packet
            stream.Write(BitConverter.GetBytes(packet.Length), 0, sizeof(int));
            // Payload
            stream.Write(packet, 0, packet.Length);
        }

        /// <summary>
        /// Receives a framed packet over the network stream. The packet is prefixed with a 4-byte
        /// length header followed by the payload. The caller is responsible for locking the stream
        /// so it isn't interrupted by other threads.
        /// </summary>
        /// <param name="stream">The stream to receive the packet from.</param>
        /// <exception cref="IOException">If the read failed due to severed connection.</exception>
        internal static byte[] ReceiveFramedPacket(this NetworkStream stream)
        {
            // The server first sends a 4-byte message representing the size of the payload in bytes
            // followed by the payload itself in another message. This is needed because TCP packets
            // can be fragmented and a single read isn't guaranteed to get the whole payload.
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
        /// Sends and receives a framed packet over the network stream. The packet is prefixed with a 4-byte
        /// length header followed by the payload. The caller is responsible for locking the stream so it
        /// isn't interrupted by other threads.
        /// </summary>
        /// <param name="stream">The stream to send and receive the packet.</param>
        /// <param name="packet">The payload.</param>
        /// <exception cref="IOException">If the read failed due to severed connection.</exception>
        internal static byte[] SendAndReceiveFramedPacket(this NetworkStream stream, byte[] packet)
        {
            SendFramedPacket(stream, packet);
            return ReceiveFramedPacket(stream);
        }

        /// <summary>
        /// Sends a framed packet over the network stream. The packet is prefixed with a 4-byte length header
        /// followed by the payload. The caller is responsible for locking the stream so it isn't interrupted
        /// by other threads.
        /// </summary>
        /// <param name="stream">The stream to send the packet to.</param>
        /// <param name="packet">The payload.</param>
        internal static async Task SendFramedPacketAsync(this NetworkStream stream, byte[] packet)
        {
            // Framing packet
            await stream.WriteAsync(BitConverter.GetBytes(packet.Length), 0, sizeof(int));
            // Payload
            await stream.WriteAsync(packet, 0, packet.Length);
        }

        /// <summary>
        /// Receives a framed packet over the network stream. The packet is prefixed with a 4-byte length
        /// header followed by the payload. The caller is responsible for locking the stream so it isn't
        /// interrupted by other threads.
        /// </summary>
        /// <param name="stream">The stream to receive the packet from.</param>
        /// <returns>The packet.</returns>
        /// <exception cref="IOException">If the read failed due to severed connection.</exception>
        internal static async Task<byte[]> ReceiveFramedPacketAsync(this NetworkStream stream)
        {
            // The server first sends a 4-byte message representing the size of the payload in bytes
            // followed by the payload itself in another message. This is needed because TCP packets
            // can be fragmented and a single read isn't guaranteed to get the whole payload.
            const int frameSize = sizeof(int);
            byte[] frameBuf = new byte[frameSize];

            // Get the payload size
            int curr = 0;
            while (curr < frameSize)
            {
                int r = await stream.ReadAsync(frameBuf, curr, frameSize - curr);
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
                int r = await stream.ReadAsync(buf, curr, dataSize - curr);
                if (r == 0)
                {
                    throw new IOException("Remote host terminated connection.");
                }

                curr += r;
            }

            return buf;
        }

        /// <summary>
        /// Sends and receives a framed packet over the network stream. The packet is prefixed with a 4-byte
        /// length header followed by the payload. The caller is responsible for locking the stream so it
        /// isn't interrupted by other threads.
        /// </summary>
        /// <param name="stream">The stream to send and receive the packet.</param>
        /// <param name="packet">The payload.</param>
        /// <exception cref="IOException">If the read failed due to severed connection.</exception>
        internal static async Task<byte[]> SendAndReceiveFramedPacketAsync(this NetworkStream stream, byte[] packet)
        {
            // First, send the payload
            await SendFramedPacketAsync(stream, packet).ConfigureAwait(false);
            return await ReceiveFramedPacketAsync(stream).ConfigureAwait(false);
        }
    }
}

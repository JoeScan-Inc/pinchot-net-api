// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client = joescan.schema.client;
using Server = joescan.schema.server;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Image data from a scan head.
    /// </summary>
    /// <remarks>
    /// The camera image class provides properties and methods for accessing the information
    /// contained in an image received from a scan head. The properties include the image pixel data,
    /// timestamp, encoder values, and other properties.
    /// </remarks>
    public class CameraImage
    {
        #region Private Fields

        private const short ProfileMagic = 0xCBD;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the ID of the <see cref="ScanHead"/> the camera image originates from.
        /// </summary>
        /// <value>The ID of the <see cref="ScanHead"/> the camera image originates from.</value>
        public uint ScanHeadID { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Pinchot.Camera"/> the camera image originates from.
        /// </summary>
        /// <value>The <see cref="Pinchot.Camera"/> the camera image originates from.</value>
        public Camera Camera { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Pinchot.Laser"/> used to generate the camera image.
        /// </summary>
        /// <value>The <see cref="Pinchot.Laser"/> used to generate the camera image.</value>
        public Laser Laser { get; internal set; }

        /// <summary>
        /// Gets the time of the scan head in nanoseconds when the camera image was generated.
        /// </summary>
        /// <value>The time of the scan head in nanoseconds when the camera image was generated.</value>
        public ulong Timestamp { get; internal set; }

        /// <summary>
        /// Gets the encoder positions when the camera image was generated.
        /// </summary>
        /// <value>A <see cref="IDictionary{TKey,TValue}"/> of encoder positions when the camera image was generated.</value>
        public IDictionary<Encoder, long> EncoderValues { get; internal set; }

        /// <summary>
        /// Gets the camera exposure time in microseconds used to generate the camera image.
        /// </summary>
        /// <value>The camera exposure time in microseconds used to generate the camera image.</value>
        public uint ExposureTimeUs { get; internal set; }

        /// <summary>
        /// Gets the laser on time in microseconds used to generate the camera image.
        /// </summary>
        /// <value>The laser on time in microseconds used to generate the camera image.</value>
        public uint LaserOnTimeUs { get; internal set; }

        /// <summary>
        /// Gets the byte array representing the pixel data.
        /// Pixel data is one <see cref="byte"/> per pixel, <see cref="Width"/> pixels per row,
        /// <see cref="Height"/> rows per image.
        /// </summary>
        /// <value>The array of <see cref="byte"/>s representing the pixel data.</value>
        public byte[] Data { get; internal set; }

        /// <summary>
        /// Gets the width of the image in pixels.
        /// </summary>
        /// <value>The width of the image in pixels.</value>
        public uint Width { get; internal set; }

        /// <summary>
        /// Gets the height of the image in pixels.
        /// </summary>
        /// <value>The height of the image in pixels.</value>
        public uint Height { get; internal set; }

        #endregion

        #region Interal Properties

        /// <summary>
        /// Gets the subpixel and brightness data at the specified
        /// column or <see langword="null"/> if none exists.
        /// </summary>
        internal Server::Peaks? GetPeak(int column)
        {
            return ImageData.Peaks(column);
        }

        /// <summary>
        /// Length of <see cref="GetPeak(int)"/>.
        /// </summary>
        internal int PeaksLength => ImageData.PeaksLength;

        /// <summary>
        /// Raw flatbuffer image data from server.
        /// </summary>
        internal Server::ImageData ImageData { get; }

        #endregion

        #region Lifecycle

        internal CameraImage(Server::ImageData imgData, Client::ScanHeadSpecificationT spec)
        {
            ImageData = imgData;
            Camera = (Camera)spec.CameraPortToId[(int)imgData.CameraPort];
            Laser = (Laser)spec.LaserPortToId[(int)imgData.LaserPort];
            ExposureTimeUs = imgData.CameraExposureNs / 1000;
            LaserOnTimeUs = imgData.LaserOnTimeNs / 1000;
            Height = imgData.Height;
            Width = imgData.Width;
            Data = imgData.GetPixelsArray();
            EncoderValues = new Dictionary<Encoder, long>(imgData.EncodersLength);
            for (int i = 0; i < imgData.EncodersLength; ++i)
            {
                EncoderValues.Add((Encoder)i, imgData.Encoders(i));
            }
        }

        internal CameraImage(BinaryReader reader)
        {
            if (reader.ReadInt16() != ProfileMagic)
            {
                throw new ArgumentException("Wrong magic header for profile");
            }

            ScanHeadID = reader.ReadUInt32();
            _ = (DataFormat)reader.ReadInt32(); // unused data format field, keep here to retain ability to read older images
            Camera = (Camera)reader.ReadInt32();
            Laser = (Laser)reader.ReadInt32();
            Timestamp = reader.ReadUInt64();
            int numberOfEncoders = reader.ReadInt32();
            EncoderValues = new Dictionary<Encoder, long>(numberOfEncoders);
            for (int i = 0; i < numberOfEncoders; i++)
            {
                EncoderValues[(Encoder)i] = reader.ReadInt64();
            }

            ExposureTimeUs = reader.ReadUInt32();
            LaserOnTimeUs = reader.ReadUInt32();
            Width = reader.ReadUInt32();
            Height = reader.ReadUInt32();
            int numberOfPixels = reader.ReadInt32();
            Data = new byte[numberOfPixels];
            for (int i = 0; i < numberOfPixels; i++)
            {
                Data[i] = reader.ReadByte();
            }
        }

        #endregion

        #region Internal Methods

        internal void WriteToBinaryWriter(BinaryWriter bw)
        {
            bw.Write(ProfileMagic);
            bw.Write(ScanHeadID);
            bw.Write(0); // unused data format field, keep here to stay consistent with older images
            bw.Write((int)Camera);
            bw.Write((int)Laser);
            bw.Write(Timestamp);
            bw.Write(EncoderValues.Count);
            foreach (long val in EncoderValues.Values)
            {
                bw.Write(val);
            }

            bw.Write(ExposureTimeUs);
            bw.Write(LaserOnTimeUs);
            bw.Write(Width);
            bw.Write(Height);
            byte[] dataArray = Data.ToArray();
            bw.Write(dataArray.Length);
            foreach (byte pixel in dataArray)
            {
                bw.Write(pixel);
            }
        }

        #endregion
    }
}
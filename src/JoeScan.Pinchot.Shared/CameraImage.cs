// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JoeScan.Pinchot
{
    /// <summary>
    /// Image data from a scan head.
    /// </summary>
    /// <remarks>
    /// The <see cref="CameraImage"/> class provides properties and methods for accessing the information
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
        /// Gets the ID of the <see cref="ScanHead"/> the <see cref="CameraImage"/> originates from.
        /// </summary>
        /// <value>The ID of the <see cref="ScanHead"/> the <see cref="CameraImage"/> originates from.</value>
        public uint ScanHeadID { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Camera"/> the <see cref="CameraImage"/> originates from.
        /// </summary>
        /// <value>The <see cref="Camera"/> the <see cref="CameraImage"/> originates from.</value>
        public Camera Camera { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Laser"/> used to generate the <see cref="CameraImage"/>.
        /// </summary>
        /// <value>The <see cref="Laser"/> used to generate the <see cref="CameraImage"/>.</value>
        public Laser Laser { get; internal set; }

        /// <summary>
        /// Gets the time of the scan head in nanoseconds when the <see cref="CameraImage"/> was generated.
        /// </summary>
        /// <value>The time of the scan head in nanoseconds when the <see cref="CameraImage"/> was generated.</value>
        public long Timestamp { get; internal set; }

        /// <summary>
        /// Gets the encoder positions when the <see cref="CameraImage"/> was generated.
        /// </summary>
        /// <value>A <see cref="IDictionary{TKey,TValue}"/> of encoder positions when the <see cref="CameraImage"/> was generated.</value>
        public IDictionary<Encoder, long> EncoderValues { get; internal set; }

        /// <summary>
        /// Gets the camera exposure time in microseconds used to generate the <see cref="CameraImage"/>.
        /// </summary>
        /// <value>The camera exposure time in microseconds used to generate the <see cref="CameraImage"/>.</value>
        public double ExposureTime { get; internal set; }

        /// <summary>
        /// Gets the laser on time in microseconds used to generate the <see cref="CameraImage"/>.
        /// </summary>
        /// <value>The laser on time in microseconds used to generate the <see cref="CameraImage"/>.</value>
        public double LaserOnTime { get; internal set; }

        /// <summary>
        /// Gets the <see cref="DataFormat"/> of the <see cref="CameraImage"/>.
        /// </summary>
        /// <value>The <see cref="DataFormat"/> of the <see cref="CameraImage"/>.</value>
        public DataFormat DataFormat { get; internal set; }

        /// <summary>
        /// Gets the <see cref="IList{T}"/> of <see cref="byte"/>s representing the pixel data.
        /// Pixel data is one <see cref="byte"/> per pixel, <see cref="Width"/> pixels per row,
        /// <see cref="Height"/> rows per image.
        /// </summary>
        /// <value>The <see cref="IList{T}"/> of <see cref="byte"/>s representing the pixel data.</value>
        public IList<byte> Data { get; internal set; }

        /// <summary>
        /// Gets the width of the image in pixels.
        /// </summary>
        /// <value>The width of the image in pixels.</value>
        public int Width { get; internal set; }

        /// <summary>
        /// Gets the height of the image in pixels.
        /// </summary>
        /// <value>The height of the image in pixels.</value>
        public int Height { get; internal set; }

        #endregion

        #region Lifecycle

        internal CameraImage(Profile profile)
        {
            ScanHeadID = profile.ScanHeadID;
            Camera = profile.Camera;
            DataFormat = profile.DataFormat;
            EncoderValues = profile.EncoderValues;
            Laser = profile.Laser;
            LaserOnTime = profile.LaserOnTime;
            ExposureTime = profile.ExposureTime;
            Data = profile.Image;
            Width = Globals.CameraImageDataMaxWidth;
            Height = Globals.CameraImageDataMaxHeight;
            Timestamp = profile.Timestamp;
        }

        internal CameraImage(BinaryReader reader)
        {
            if (reader.ReadInt16() != ProfileMagic)
            {
                throw new ArgumentException("Wrong magic header for profile");
            }

            ScanHeadID = (uint)reader.ReadInt32();
            DataFormat = (DataFormat)reader.ReadInt32();
            Camera = (Camera)reader.ReadInt32();
            Laser = (Laser)reader.ReadInt32();
            Timestamp = reader.ReadInt64();
            var numberOfEncoders = reader.ReadInt32();
            EncoderValues = new Dictionary<Encoder, long>(numberOfEncoders);
            for (int i = 0; i < numberOfEncoders; i++)
            {
                EncoderValues[(Encoder)i] = reader.ReadInt64();
            }

            ExposureTime = reader.ReadInt32();
            LaserOnTime = reader.ReadInt32();
            Width = reader.ReadInt32();
            Height = reader.ReadInt32();
            var numberOfPixels = reader.ReadInt32();
            Data = new byte[numberOfPixels];
            for (var i = 0; i < numberOfPixels; i++)
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
            bw.Write((int)DataFormat);
            bw.Write((int)Camera);
            bw.Write((int)Laser);
            bw.Write(Timestamp);
            bw.Write(EncoderValues.Count);
            foreach (var val in EncoderValues.Values)
            {
                bw.Write(val);
            }

            bw.Write((int)ExposureTime);
            bw.Write((int)LaserOnTime);
            bw.Write(Width);
            bw.Write(Height);
            var dataArray = Data.ToArray();
            bw.Write(dataArray.Length);
            foreach (var pixel in dataArray)
            {
                bw.Write(pixel);
            }
        }

        #endregion
    }
}
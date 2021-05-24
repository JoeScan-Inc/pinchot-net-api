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
    /// Profile data from a scan head.
    /// </summary>
    /// <remarks>
    /// The <see cref="Profile"/> class provides properties and methods for accessing the information
    /// contained in a profile received from a scan head. The properties include the raw <see cref="Point2D"/> data,
    /// timestamp, encoder values, and other properties. Methods for retrieving only valid <see cref="Point2D"/>
    /// data are also provided.
    /// </remarks>
    public class Profile : ICloneable
    {
        #region Private Fields

        private const short ProfileMagic = 0xCBD;

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the ID of the <see cref="ScanHead"/> the <see cref="Profile"/> originates from.
        /// </summary>
        /// <value>The ID of the <see cref="ScanHead"/> the <see cref="Profile"/> originates from.</value>
        public uint ScanHeadID { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Camera"/> the <see cref="Profile"/> originates from.
        /// </summary>
        /// <value>The <see cref="Camera"/> the <see cref="Profile"/> originates from.</value>
        public Camera Camera { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Laser"/> used to generate the <see cref="Profile"/>.
        /// </summary>
        /// <value>The <see cref="Laser"/> used to generate the <see cref="Profile"/>.</value>
        public Laser Laser { get; internal set; }

        /// <summary>
        /// Gets the time of the scan head in nanoseconds when the <see cref="Profile"/> was generated.
        /// </summary>
        /// <value>The time of the scan head in nanoseconds when the <see cref="Profile"/> was generated.</value>
        public long Timestamp { get; internal set; }

        /// <summary>
        /// Gets the encoder positions when the <see cref="Profile"/> was generated.
        /// </summary>
        /// <value>A <see cref="IDictionary{TKey,TValue}"/> of encoder positions when the <see cref="Profile"/> was generated.</value>
        public IDictionary<Encoder, long> EncoderValues { get; internal set; }

        /// <summary>
        /// Gets the laser on time in microseconds used to generate the <see cref="Profile"/>.
        /// </summary>
        /// <value>The laser on time in microseconds used to generate the <see cref="Profile"/>.</value>
        public double LaserOnTime { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Point2D"/> data for the <see cref="Profile"/>, including invalid points.
        /// </summary>
        /// <value>A <see cref="Span{T}"/> of <see cref="Point2D"/> data for the <see cref="Profile"/>.</value>
        public Span<Point2D> RawPoints => RawPointsMemory.Span;

        /// <summary>
        /// Gets the number of valid <see cref="Point2D"/>s in <see cref="RawPoints"/>.
        /// </summary>
        /// <value>The number of valid <see cref="Point2D"/>s in <see cref="RawPoints"/>.</value>
        public int ValidPointCount { get; internal set; }

        /// <summary>
        /// Gets the <see cref="DataFormat"/> of the <see cref="Profile"/>.
        /// </summary>
        /// <value>The <see cref="DataFormat"/> of the <see cref="Profile"/>.</value>
        public DataFormat DataFormat => (DataFormat)AllDataFormat;

        #endregion

        #region Internal Properies

        internal Memory<Point2D> RawPointsMemory { get; set; }

        internal IList<byte> Image { get; set; }

        /// <summary>
        /// Gets the Point2D data. XY values in sub pixels.
        /// </summary>
        internal IList<Point2D> CameraCoordinates { get; set; }

        internal int SourceID
        {
            get { return (int)ScanHeadID << 3 | (int)Camera << 2 | (int)Laser; }
        }

        internal AllDataFormat AllDataFormat { get; set; }

        /// <summary>
        /// Gets the camera exposure time in microseconds used to generate the <see cref="Profile"/>.
        /// </summary>
        /// <value>The camera exposure time in microseconds used to generate the <see cref="Profile"/>.</value>
        internal double ExposureTime { get; set; }

        #endregion

        #region Lifecycle

        internal Profile()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the valid <see cref="Point2D"/>s in the <see cref="Profile"/>.
        /// </summary>
        /// <returns>A <see cref="IEnumerable{Point2D}"/> of the valid <see cref="Point2D"/>s in the <see cref="Profile"/>.</returns>
        public IEnumerable<Point2D> GetValidXYPoints()
        {
            return RawPoints.ToArray().Where(q => !double.IsNaN(q.Y));
        }

        /// <summary>
        /// Gets the valid <see cref="Point2D"/>s in the <see cref="Profile"/>.
        /// </summary>
        /// <param name="validPoints">A <see cref="Span{T}"/> of <see cref="Point2D"/> that is the
        /// storage location for the valid <see cref="Point2D"/>s. Must be of length greater than
        /// or equal to <see cref="ValidPointCount"/>.</param>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="validPoints"/> is less than <see cref="ValidPointCount"/>.
        /// </exception>
        public void GetValidXYPoints(Span<Point2D> validPoints)
        {
            if (validPoints.Length < ValidPointCount)
            {
                throw new ArgumentException(
                    $"Length of {nameof(validPoints)} is less than number of valid points: {ValidPointCount}",
                    nameof(validPoints));
            }

            var i = 0;
            foreach (var point in RawPoints)
            {
                if (double.IsNaN(point.Y)) continue;

                validPoints[i++] = point;
            }
        }

        /// <summary>
        /// <see cref="Profile"/> implements <see cref="ICloneable"/>.
        /// </summary>
        /// <returns>A shallow copy of the <see cref="Profile"/> object.</returns>
        public object Clone()
        {
            return MemberwiseClone();
        }

        #endregion

        #region Internal Methods

        internal Profile(BinaryReader reader)
            : this()
        {
            if (reader.ReadInt16() != ProfileMagic)
            {
                throw new ArgumentException("Wrong magic header for profile");
            }

            ScanHeadID = (uint)reader.ReadInt32();
            Camera = (Camera)reader.ReadInt32();
            Laser = (Laser)reader.ReadInt32();
            Timestamp = reader.ReadInt64();
            var numberOfEncoders = reader.ReadInt32();
            EncoderValues = new Dictionary<Encoder, long>(numberOfEncoders);
            for (int i = 0; i < numberOfEncoders; i++)
            {
                EncoderValues[(Encoder)i] = reader.ReadInt64();
            }

            LaserOnTime = reader.ReadInt32();
            var numberPoints = reader.ReadInt32();
            var rawPointsArray = new Point2D[numberPoints];
            for (var i = 0; i < numberPoints; i++)
            {
                var x = reader.ReadDouble();
                var y = reader.ReadDouble();
                var brightness = reader.ReadInt32();
                rawPointsArray[i] = new Point2D(x, y, brightness);
            }

            RawPointsMemory = new Memory<Point2D>(rawPointsArray);
            var numSubpixelValues = reader.ReadInt32();
            CameraCoordinates = new Point2D[numSubpixelValues];
            for (int i = 0; i < numSubpixelValues; i++)
            {
                var x = reader.ReadDouble();
                var y = reader.ReadDouble();
                var brightness = reader.ReadInt32();
                CameraCoordinates[i] = new Point2D(x, y, brightness);
            }

            ValidPointCount = this.GetValidXYPoints().Count();
        }

        internal void WriteToBinaryWriter(BinaryWriter bw)
        {
            bw.Write(ProfileMagic);
            bw.Write(ScanHeadID);
            bw.Write((int)Camera);
            bw.Write((int)Laser);
            bw.Write(Timestamp);
            bw.Write(EncoderValues.Count);
            foreach (var val in EncoderValues.Values)
            {
                bw.Write(val);
            }

            bw.Write((int)LaserOnTime);
            var rawPointsArray = RawPoints.ToArray();
            bw.Write(rawPointsArray.Length);
            foreach (var pt in rawPointsArray)
            {
                bw.Write(pt.X);
                bw.Write(pt.Y);
                bw.Write(pt.Brightness);
            }

            if (CameraCoordinates != null)
            {
                bw.Write(CameraCoordinates.Count);
                foreach (var sc in CameraCoordinates)
                {
                    bw.Write(sc.X * 1000);
                    bw.Write(sc.Y * 1000);
                    bw.Write(sc.Brightness);
                }
            }
            else
            {
                bw.Write(0); // no camera coordinates
            }
        }

        internal void WriteMappleData(BinaryWriter bw)
        {
            bw.Write(ProfileMagic);
            bw.Write(ScanHeadID);
            bw.Write((int)Camera);
            bw.Write((int)Laser);
            bw.Write(Timestamp);
            bw.Write(EncoderValues.Count);
            foreach (var val in EncoderValues.Values)
            {
                bw.Write(val);
            }

            bw.Write(LaserOnTime);
            bw.Write(CameraCoordinates.Count);
            foreach (var cc in CameraCoordinates)
            {
                bw.Write((ushort)cc.X);
            }

            foreach (var cc in CameraCoordinates)
            {
                bw.Write((byte)cc.Brightness);
            }
        }

        #endregion
    }
}
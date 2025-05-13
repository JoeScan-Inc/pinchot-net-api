// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server = joescan.schema.server;

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
    internal sealed class Profile : IProfile, ICloneable
    {
        #region Private Fields

        private const short ProfileMagic = 0xCBD;

        #endregion

        #region IProfile Implementation

        /// <inheritdoc/>
        public uint ScanHeadID { get; internal set; }

        /// <inheritdoc/>
        public Camera Camera { get; internal set; }

        /// <inheritdoc/>
        public Laser Laser { get; internal set; }

        /// <inheritdoc/>
        public ulong TimestampNs { get; internal set; }

        /// <inheritdoc/>
        public IDictionary<Encoder, long> EncoderValues { get; internal set; }

        /// <inheritdoc/>
        public ushort LaserOnTimeUs { get; internal set; }

        /// <inheritdoc/>
        public Span<Point2D> RawPoints => RawPointsMemory.Span;

        /// <inheritdoc/>
        public uint ValidPointCount { get; internal set; }

        /// <inheritdoc/>
        public DataFormat DataFormat => (DataFormat)AllDataFormat;

        /// <inheritdoc/>
        public uint SequenceNumber { get; internal set; }

        /// <inheritdoc/>
        public uint PacketsReceived { get; internal set; }

        /// <inheritdoc/>
        public uint PacketsExpected { get; internal set; }

        /// <inheritdoc/>
        public ProfileFlags Flags { get; internal set; }

        /// <inheritdoc/>
        public IEnumerable<Point2D> GetValidXYPoints()
        {
            int stride = GetDataStride();
            for (int i = 0; i < RawPoints.Length; i += stride)
            {
                var p = RawPoints[i];
                if (p.IsValid)
                {
                    yield return p;
                }
            }
        }

        /// <inheritdoc/>
        public void GetValidXYPoints(Span<Point2D> validPoints)
        {
            if (validPoints.Length < ValidPointCount)
            {
                throw new ArgumentException(
                    $"Length of {nameof(validPoints)} is less than number of valid points: {ValidPointCount}",
                    nameof(validPoints));
            }

            int validIdx = 0;
            int stride = GetDataStride();
            for (int i = 0; i < RawPoints.Length; i += stride)
            {
                var p = RawPoints[i];
                if (p.IsValid)
                {
                    validPoints[validIdx++] = p;
                }
            }
        }

        #endregion

        #region ICloneable Implementation

        /// <summary>
        /// <see cref="IProfile"/> implements <see cref="ICloneable"/>.
        /// </summary>
        /// <returns>A shallow copy of the <see cref="IProfile"/> object.</returns>
        public object Clone()
        {
            return MemberwiseClone();
        }

        #endregion

        #region Internal Properies

        internal Memory<Point2D> RawPointsMemory { get; set; }

        internal IList<byte> Image { get; set; }

        /// <summary>
        /// Gets the Point2D data. XY values in sub pixels.
        /// </summary>
        internal IList<Point2D> CameraCoordinates { get; set; }

        internal int SourceID { get; set; }

        internal AllDataFormat AllDataFormat { get; set; }

        // TODO: Remove this property
        /// <summary>
        /// Gets the camera exposure time in microseconds used to generate the <see cref="Profile"/>.
        /// </summary>
        /// <value>The camera exposure time in microseconds used to generate the <see cref="Profile"/>.</value>
        internal uint ExposureTimeUs { get; set; }

        #endregion

        #region Lifecycle

        internal Profile()
        {
        }

        internal Profile(ScanHead scanHead, Server::ProfileDataT profileData)
        {
            ScanHeadID = scanHead.ID;
            Camera = scanHead.CameraPortToId(profileData.CameraPort);
            Laser = scanHead.LaserPortToId(profileData.LaserPort);
            ExposureTimeUs = profileData.CameraExposureNs / 1000;
            LaserOnTimeUs = (ushort)(profileData.LaserOnTimeNs / 1000);
            ValidPointCount = profileData.ValidPoints;
            TimestampNs = profileData.TimestampNs;
            AllDataFormat = AllDataFormat.XYBrightnessFull;

            EncoderValues = new Dictionary<Encoder, long>();
            for (int i = 0; i < profileData.Encoders.Count; ++i)
            {
                EncoderValues[Encoder.Main + i] = profileData.Encoders[i];
            }

            var points = new Point2D[profileData.Points.Count];
            RawPointsMemory = new Memory<Point2D>(points);

            for (int i = 0; i < profileData.Points.Count; ++i)
            {
                var point = profileData.Points[i];
                if (point.X == Globals.ServerProfileDataInvalidXY || point.Y == Globals.ServerProfileDataInvalidXY)
                {
                    points[i].X = Globals.ProfileDataInvalidXY;
                    points[i].Y = Globals.ProfileDataInvalidXY;
                    points[i].Brightness = Globals.ProfileDataInvalidBrightness;
                }
                else
                {
                    var pair = new CameraLaserPair(Camera, Laser);
                    var alignment = scanHead.Alignments[pair];
                    points[i] = alignment.CameraToMill(point.X, point.Y, point.Brightness);
                }
            }
        }

        #endregion

        #region Internal Methods

        internal int GetDataStride()
        {
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            return AllDataFormat switch
            {
                AllDataFormat.XYFull => 1,
                AllDataFormat.XYBrightnessFull => 1,
                AllDataFormat.Subpixel => 1,
                AllDataFormat.SubpixelBrightnessFull => 1,
                AllDataFormat.XYBrightnessHalf => 2,
                AllDataFormat.XYHalf => 2,
                AllDataFormat.XYQuarter => 4,
                AllDataFormat.XYBrightnessQuarter => 4,
                _ => throw new InvalidOperationException($"Unknown data stride for {AllDataFormat}")
            };
#else
            switch (AllDataFormat)
            {
                case AllDataFormat.XYFull:
                case AllDataFormat.XYBrightnessFull:
                case AllDataFormat.Subpixel:
                case AllDataFormat.SubpixelBrightnessFull:
                    return 1;
                case AllDataFormat.XYBrightnessHalf:
                case AllDataFormat.XYHalf:
                    return 2;
                case AllDataFormat.XYQuarter:
                case AllDataFormat.XYBrightnessQuarter:
                    return 4;
                default:
                    throw new InvalidOperationException($"Unknown data stride for {AllDataFormat}");
            }
#endif
        }

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
            TimestampNs = reader.ReadUInt64();
            int numberOfEncoders = reader.ReadInt32();
            EncoderValues = new Dictionary<Encoder, long>(numberOfEncoders);
            for (int i = 0; i < numberOfEncoders; i++)
            {
                EncoderValues[(Encoder)i] = reader.ReadInt64();
            }

            LaserOnTimeUs = (ushort)reader.ReadInt32();
            int numberPoints = reader.ReadInt32();
            var rawPointsArray = new Point2D[numberPoints];
            for (int i = 0; i < numberPoints; i++)
            {
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                int brightness = reader.ReadInt32();
                rawPointsArray[i] = new Point2D(x, y, brightness);
            }

            RawPointsMemory = new Memory<Point2D>(rawPointsArray);
            int numSubpixelValues = reader.ReadInt32();
            CameraCoordinates = new Point2D[numSubpixelValues];
            for (int i = 0; i < numSubpixelValues; i++)
            {
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                int brightness = reader.ReadInt32();
                CameraCoordinates[i] = new Point2D(x, y, brightness);
            }

            // the writer doesn't write the data format, but we can infer it from the data
            bool isHalf = rawPointsArray.Where((_, i) => i % 2 != 0).All(p => p.X == 0 && p.Y == 0 && p.Brightness == 0);
            bool isQuarter = rawPointsArray.Where((_, i) => i % 4 != 0).All(p => p.X == 0 && p.Y == 0 && p.Brightness == 0);
            bool hasBrightness = rawPointsArray.Any(p => p.Brightness != Globals.ProfileDataInvalidBrightness);

            if (isQuarter)
            {
                AllDataFormat = hasBrightness ? AllDataFormat.XYBrightnessQuarter : AllDataFormat.XYQuarter;
            }
            else if (isHalf)
            {
                AllDataFormat = hasBrightness ? AllDataFormat.XYBrightnessHalf : AllDataFormat.XYHalf;
            }
            else
            {
                AllDataFormat = hasBrightness ? AllDataFormat.XYBrightnessFull : AllDataFormat.XYFull;
            }

            ValidPointCount = (uint)GetValidXYPoints().Count();
        }

        internal void WriteToBinaryWriter(BinaryWriter bw)
        {
            bw.Write(ProfileMagic);
            bw.Write(ScanHeadID);
            bw.Write((int)Camera);
            bw.Write((int)Laser);
            bw.Write(TimestampNs);
            bw.Write(EncoderValues.Count);
            foreach (long val in EncoderValues.Values)
            {
                bw.Write(val);
            }

            bw.Write((int)LaserOnTimeUs);
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
            // Mapple generator expects a long
            bw.Write((long)TimestampNs);
            bw.Write(EncoderValues.Count);
            foreach (long val in EncoderValues.Values)
            {
                bw.Write(val);
            }

            // Mapple generator expects a double
            bw.Write((double)LaserOnTimeUs);
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
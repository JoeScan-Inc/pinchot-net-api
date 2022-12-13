// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Linq;

namespace JoeScan.Pinchot
{
    internal class ProfileAssembler
    {
        #region Private Fields

        private readonly ScanHead scanHead;
        private readonly AllDataFormat dataFormat;
        private const int NumProfilesToBuffer = 100;
        private readonly Point2D[] defaultPointsArray;
        private readonly Point2D[] rawPointsArray;
        private int rawPointsArrayIndex;
        private AlignmentParameters alignment;

        #endregion

        #region Lifecycle

        internal ProfileAssembler(ScanHead scanHead, AllDataFormat dataFormat)
        {
            this.dataFormat = dataFormat;
            this.scanHead = scanHead;

            var defaultPoint = new Point2D
            {
                X = Globals.ProfileDataInvalidXY,
                Y = Globals.ProfileDataInvalidXY,
                Brightness = Globals.ProfileDataInvalidBrightness
            };

            // keep copy of default array to save on computation time
            // when raw points array needs to be reset
            defaultPointsArray = Enumerable.Repeat(defaultPoint, NumProfilesToBuffer * Globals.RawProfileDataLength).ToArray();
            rawPointsArray = defaultPointsArray.Clone() as Point2D[];
            rawPointsArrayIndex = 0;
        }

        #endregion

        internal Profile CreateNewProfile(DataPacketHeader header)
        {
            var spec = scanHead.Specification;
            var p = new Profile
            {
                SourceID = header.Source,
                ScanHeadID = header.ScanHeadId,
                Camera = (Camera)spec.CameraPortToId[(int)header.CameraPort],
                Laser = (Laser)spec.LaserPortToId[(int)header.LaserPort],
                Flags = (ProfileFlags)header.Flags,
                TimestampNs = header.TimestampNs,
                LaserOnTimeUs = header.LaserOnTimeUs,
                ExposureTimeUs = header.ExposureTimeUs,
                SequenceNumber = header.SequenceNumber,
                PacketsExpected = header.NumberDatagrams,
                EncoderValues = header.Encoders,
                AllDataFormat = dataFormat,
            };

            if (header.DataType.HasFlag(DataType.Subpixel))
            {
                p.CameraCoordinates = new Point2D[Globals.RawProfileDataLength];
            }

            rawPointsArrayIndex++;
            if (rawPointsArrayIndex >= NumProfilesToBuffer)
            {
                defaultPointsArray.CopyTo(rawPointsArray, 0);
                rawPointsArrayIndex = 0;
            }

            p.RawPointsMemory = new Memory<Point2D>(rawPointsArray,
                rawPointsArrayIndex * Globals.RawProfileDataLength,
                Globals.RawProfileDataLength);

            var pair = new CameraLaserPair(p.Camera, p.Laser);
            alignment = scanHead.Alignments[pair];

            return p;
        }

        internal bool ProcessPacket(Profile profile, DataPacketHeader header, Span<byte> packet)
        {
            var dataTypes = header.DataType;
            ushort startCol = header.StartColumn;
            uint partNum = header.DatagramPosition;
            uint totalParts = header.NumberDatagrams;

            var rawPointsSpan = profile.RawPointsMemory.Span;

            double cameraToMillXX = alignment.CameraToMillXX;
            double cameraToMillXY = alignment.CameraToMillXY;
            double cameraToMillYX = alignment.CameraToMillYX;
            double cameraToMillYY = alignment.CameraToMillYY;
            double shiftX = alignment.ShiftX;
            double shiftY = alignment.ShiftY;

            foreach (var dt in dataTypes.GetFlags())
            {
                var layout = header.FragmentLayouts[dt];
                int numVals = layout.numVals;
                int step = layout.step;
                int srcIdx = layout.offset;

                int inc = (int)(totalParts * step);
                int destIdx = (int)(startCol + (partNum * step));

                switch (dt)
                {
                    case DataType.Brightness:
                        for (int j = 0; j < numVals; j++)
                        {
                            rawPointsSpan[destIdx].Brightness = packet[srcIdx];
                            ++srcIdx;
                            destIdx += inc;
                        }
                        break;
                    case DataType.XY:
                        for (int j = 0; j < numVals; j++)
                        {
                            short xraw = (short)(packet[srcIdx + 1] | (packet[srcIdx] << 8));
                            short yraw = (short)(packet[srcIdx + 3] | (packet[srcIdx + 2] << 8));
                            srcIdx += 4;

                            if (xraw != Globals.ServerProfileDataInvalidXY && yraw != Globals.ServerProfileDataInvalidXY)
                            {
                                profile.ValidPointCount++;
                                rawPointsSpan[destIdx].X = (xraw * cameraToMillXX) - (yraw * cameraToMillXY) + shiftX;
                                rawPointsSpan[destIdx].Y = (xraw * cameraToMillYX) + (yraw * cameraToMillYY) + shiftY;
                            }

                            destIdx += inc;
                        }
                        break;
                    case DataType.Subpixel:
                        for (int j = 0; j < numVals; j++)
                        {
                            short rowPixel = (short)(packet[srcIdx + 1] | (packet[srcIdx] << 8));
                            srcIdx += 2;

                            profile.CameraCoordinates[destIdx] = new Point2D(rowPixel, destIdx, rawPointsSpan[destIdx].Brightness);
                            destIdx += inc;
                        }
                        break;
                    default:
                        throw new InvalidOperationException($"DataType {dt} is unhandled!");
                }
            }

            return ++profile.PacketsReceived == profile.PacketsExpected;
        }
    }
}

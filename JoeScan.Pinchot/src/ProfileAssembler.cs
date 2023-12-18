// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    internal class ProfileAssembler
    {
        #region Private Fields

        private readonly ScanHead scanHead;
        private readonly AllDataFormat dataFormat;
        private const int NumProfilesToBuffer = 100;
        private Point2D[] rawPointsArray;
        private int rawPointsArrayIndex;
        private AlignmentParameters alignment;

        #endregion

        #region Lifecycle

        internal ProfileAssembler(ScanHead scanHead, AllDataFormat dataFormat)
        {
            this.dataFormat = dataFormat;
            this.scanHead = scanHead;

            rawPointsArray = new Point2D[NumProfilesToBuffer * Globals.RawProfileDataLength];
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
                // need to `new` the array in case there are profiles yet to be read out
                // that have references to the previous spots in memory - the memory will
                // then be GC'd when the profile goes out of scope
                rawPointsArray = new Point2D[NumProfilesToBuffer * Globals.RawProfileDataLength];
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

            if (dataTypes.HasFlag(DataType.XY))
            {
                // Brightness is always accompanied by XY data so
                // process them both in the same loop if present
                int bSrc = 0;
                bool hasBrightness = dataTypes.HasFlag(DataType.Brightness);
                if (hasBrightness)
                {
                    var bLayout = header.FragmentLayouts[DataType.Brightness];
                    bSrc = bLayout.offset;
                }

                var xyLayout = header.FragmentLayouts[DataType.XY];
                int xySrc = xyLayout.offset;

                // assume step and number of values is same for brightness and XY layout
                int numVals = xyLayout.numVals;
                int step = xyLayout.step;
                int inc = (int)(totalParts * step);
                int dstIdx = (int)(startCol + (partNum * step));

                for (int j = 0; j < numVals; j++)
                {
                    short xraw = (short)(packet[xySrc + 1] | (packet[xySrc] << 8));
                    short yraw = (short)(packet[xySrc + 3] | (packet[xySrc + 2] << 8));
                    xySrc += 4;

                    int brightness = hasBrightness ? packet[bSrc] : Globals.ProfileDataInvalidBrightness;
                    bSrc++;

                    if (xraw != Globals.ServerProfileDataInvalidXY && yraw != Globals.ServerProfileDataInvalidXY)
                    {
                        profile.ValidPointCount++;
                        alignment.CameraToMill(ref rawPointsSpan[dstIdx], xraw, yraw, brightness);
                    }
                    else
                    {
                        rawPointsSpan[dstIdx].X = Globals.ProfileDataInvalidXY;
                        rawPointsSpan[dstIdx].Y = Globals.ProfileDataInvalidXY;
                        rawPointsSpan[dstIdx].Brightness = Globals.ProfileDataInvalidBrightness;
                    }

                    dstIdx += inc;
                }
            }
            else if (dataTypes.HasFlag(DataType.Subpixel))
            {
                var spLayout = header.FragmentLayouts[DataType.Subpixel];
                int spSrc = spLayout.offset;
                var bLayout = header.FragmentLayouts[DataType.Brightness];
                int bSrc = bLayout.offset;

                int numVals = spLayout.numVals;
                int step = spLayout.step;
                int inc = (int)(totalParts * step);
                int dstIdx = (int)(startCol + (partNum * step));
                for (int j = 0; j < numVals; j++)
                {
                    short rowPixel = (short)(packet[spSrc + 1] | (packet[spSrc] << 8));
                    spSrc += 2;

                    int brightness = packet[bSrc];
                    bSrc++;

                    profile.CameraCoordinates[dstIdx] = new Point2D(rowPixel, dstIdx, brightness);

                    dstIdx += inc;
                }
            }
            else
            {
                throw new InvalidOperationException($"Unhandled data type in {dataTypes.GetFlags()}!");
            }

            return ++profile.PacketsReceived == profile.PacketsExpected;
        }
    }
}

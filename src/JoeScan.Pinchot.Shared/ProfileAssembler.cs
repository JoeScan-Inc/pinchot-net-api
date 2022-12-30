// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace JoeScan.Pinchot
{
    internal class ProfileAssembler
    {
        #region Private Fields

        private readonly AllDataFormat dataFormat;
        private readonly IDictionary<Camera, AlignmentParameters> alignmentParameters;
        private readonly BlockingCollection<Profile> profiles;
        private const int NumProfilesToBuffer = 100;
        private readonly Point2D[] defaultPointsArray;
        private readonly Point2D[] rawPointsArray;
        private int rawPointsArrayIndex;

        #endregion

        #region Internal Properties

        internal bool ProfileBufferOverflowed { get; private set; }

        #endregion

        #region Lifecycle

        internal ProfileAssembler(BlockingCollection<Profile> profiles, AllDataFormat dataFormat,
            IDictionary<Camera, AlignmentParameters> alignmentParameters)
        {
            this.dataFormat = dataFormat;
            this.profiles = profiles;
            this.alignmentParameters = alignmentParameters;

            var defaultPoint = new Point2D
            {
                X = float.NaN,
                Y = float.NaN,
                Brightness = Globals.ProfileDataInvalidBrightness
            };

            // keep copy of default array to save on computation time
            // when raw points array needs to be reset
            defaultPointsArray = Enumerable.Repeat(defaultPoint, NumProfilesToBuffer * Globals.RawProfileDataLength).ToArray();
            rawPointsArray = defaultPointsArray.Clone() as Point2D[];
            rawPointsArrayIndex = 0;
        }

        #endregion

        private Profile CreateNewProfile(DataPacket seedPacket)
        {
            var p = new Profile
            {
                ScanHeadID = (uint)seedPacket.ScanHead,
                Camera = seedPacket.Camera,
                Laser = seedPacket.Laser,
                Timestamp = seedPacket.Timestamp,
                LaserOnTime = seedPacket.LaserOnTime,
                ExposureTime = seedPacket.ExposureTime,
                AllDataFormat = dataFormat,
                EncoderValues = new Dictionary<Encoder, long>(seedPacket.NumEncoderVals)
            };

            for (int i = 0; i < seedPacket.NumEncoderVals; i++)
            {
                p.EncoderValues[(Encoder)i] = seedPacket.EncoderVals[i];
            }

            if (seedPacket.Contents.HasFlag(DataType.IM))
            {
                p.Image = new byte[1456 * 1088];
                p.CameraCoordinates = new Point2D[Globals.RawProfileDataLength];
            }

            if (seedPacket.Contents.HasFlag(DataType.SP))
            {
                p.CameraCoordinates = new Point2D[Globals.RawProfileDataLength];
            }

            return p;
        }

        internal void AssembleProfiles(ProfileFragments fragments)
        {
            var seedPacket = fragments[0];
            var p = CreateNewProfile(seedPacket);

            if (rawPointsArrayIndex >= NumProfilesToBuffer)
            {
                defaultPointsArray.CopyTo(rawPointsArray, 0);
                rawPointsArrayIndex = 0;
            }

            p.RawPointsMemory = new Memory<Point2D>(rawPointsArray, rawPointsArrayIndex * Globals.RawProfileDataLength,
                Globals.RawProfileDataLength);
            var rawPointsSpan = p.RawPointsMemory.Span;

            var tr = alignmentParameters[seedPacket.Camera];
            double sinRoll = tr.SinRoll;
            double cosRoll = tr.CosRoll;
            double cosYaw = tr.CosYaw;
            float shiftX = (float)tr.ShiftX;
            float shiftY = (float)tr.ShiftY;
            float xXCoefficient = (float)(cosYaw * cosRoll / 1000);
            float xYCoefficient = (float)(sinRoll / 1000);
            float yXCoefficient = (float)(cosYaw * sinRoll / 1000);
            float yYCoefficient = (float)(cosRoll / 1000);

            foreach (var currentFragment in fragments)
            {
                var dataTypes = currentFragment.Contents;
                short startCol = currentFragment.StartColumn;
                int partNum = currentFragment.PartNum;
                int totalParts = currentFragment.NumParts;
                byte[] currentFragmentRaw = currentFragment.Raw;

                foreach (var dt in dataTypes.GetFlags())
                {
                    var layout = currentFragment.FragmentLayouts[dt];
                    int numVals = layout.numVals;
                    int step = layout.step;
                    int srcIdx = layout.offset;

                    int inc = totalParts * step;
                    int destIdx = startCol + (partNum * step);

                    switch (dt)
                    {
                        case DataType.LM:
                            for (int j = 0; j < numVals; j++)
                            {
                                rawPointsSpan[destIdx].Brightness = currentFragmentRaw[srcIdx];
                                ++srcIdx;
                                destIdx += inc;
                            }
                            break;
                        case DataType.XY:
                            for (int j = 0; j < numVals; j++)
                            {
                                short xraw = (short)(currentFragmentRaw[srcIdx + 1] | (currentFragmentRaw[srcIdx] << 8));
                                short yraw = (short)(currentFragmentRaw[srcIdx + 3] | (currentFragmentRaw[srcIdx + 2] << 8));
                                srcIdx += 4;

                                if (xraw != Globals.ProfileDataInvalidXY && yraw != Globals.ProfileDataInvalidXY)
                                {
                                    p.ValidPointCount++;
                                    rawPointsSpan[destIdx].X = (xraw * xXCoefficient) - (yraw * xYCoefficient) + shiftX;
                                    rawPointsSpan[destIdx].Y = (xraw * yXCoefficient) + (yraw * yYCoefficient) + shiftY;
                                }

                                destIdx += inc;
                            }
                            break;
                        case DataType.SP:
                            for (int j = 0; j < numVals; j++)
                            {
                                short rowPixel = (short)(currentFragmentRaw[srcIdx + 1] | (currentFragmentRaw[srcIdx] << 8));
                                srcIdx += 2;

                                p.CameraCoordinates[destIdx] = new Point2D(rowPixel, destIdx, rawPointsSpan[destIdx].Brightness);
                                destIdx += inc;
                            }
                            break;
                        case DataType.IM:
                            // TODO: Adapt to use SP type
                            // last packet is subpixel data corresponding to laser line
                            if (partNum == totalParts - 1)
                            {
                                for (int i = 0; i < 1456; i++)
                                {
                                    // TODO: SP data doesn't get sent in network order
                                    int rowPixel = currentFragmentRaw[srcIdx + 1] << 8 | currentFragmentRaw[srcIdx];
                                    int brightness = currentFragmentRaw[srcIdx + 3] << 8 | currentFragmentRaw[srcIdx + 2];
                                    srcIdx += 4;

                                    if (brightness < 0x8000)
                                    {
                                        brightness /= 7;
                                    }
                                    else
                                    {
                                        rowPixel = Globals.ProfileDataInvalidSubpixel;
                                        brightness = Globals.ProfileDataInvalidBrightness;
                                    }

                                    p.CameraCoordinates[i] = new Point2D(rowPixel, i, brightness);
                                }
                                break;
                            }
                            int pos = partNum * 4 * 1456;
                            Array.Copy(currentFragmentRaw, srcIdx, (Array)p.Image, pos, numVals);
                            break;
                    }
                }
            }

            if (!profiles.TryAdd(p))
            {
                ProfileBufferOverflowed = true;
                profiles.TryTake(out _);
                profiles.TryAdd(p);
            }

            rawPointsArrayIndex++;
        }
    }
}
// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace JoeScan.Pinchot
{
    internal class ProfileAssembler
    {
        #region Private Fields

        private readonly AllDataFormat dataFormat;
        private readonly IDictionary<Camera, AlignmentParameters> alignmentParameters;
        private readonly BlockingCollection<Profile> profiles;
        private const int RawPointsArrayCapacity = 100;
        private Point2D[] rawPointsArray;
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
            rawPointsArray = new Point2D[RawPointsArrayCapacity * Globals.RawProfileDataLength];
            rawPointsArrayIndex = 0;
        }

        #endregion

        internal void AssembleProfiles(ProfileFragments fragments)
        {
            var p = new Profile
            {
                // copy common data from the first fragment
                ScanHeadID = (uint)fragments[0].ScanHead,
                Camera = fragments[0].Camera,
                Laser = fragments[0].Laser,
                Timestamp = fragments[0].Timestamp,
                LaserOnTime = fragments[0].LaserOnTime,
                ExposureTime = fragments[0].ExposureTime,
                AllDataFormat = dataFormat,
                EncoderValues = new Dictionary<Encoder, long>(fragments[0].NumEncoderVals)
            };

            // copy encoder vals
            for (int i = 0; i < fragments[0].NumEncoderVals; i++)
            {
                p.EncoderValues[(Encoder)i] = fragments[0].EncoderVals[i];
            }

            if (rawPointsArrayIndex >= RawPointsArrayCapacity)
            {
                rawPointsArray = new Point2D[RawPointsArrayCapacity * Globals.RawProfileDataLength];
                rawPointsArrayIndex = 0;
            }

            p.RawPointsMemory = new Memory<Point2D>(rawPointsArray, rawPointsArrayIndex * Globals.RawProfileDataLength,
                Globals.RawProfileDataLength);
            var rawPointsSpan = p.RawPointsMemory.Span;
            rawPointsSpan.Fill(new Point2D(double.NaN, double.NaN, Globals.ProfileDataInvalidBrightness));
            int validPointCount = 0;

            Point2D[] cameraCoords = null;

            if ((fragments[0].Contents & DataType.IM) > 0)
            {
                // we have image data
                p.Image = new byte[1456 * 1088];
            }

            // helpers local
            int fragmentsCount = fragments.Count;
            var tr = alignmentParameters[fragments[0].Camera];
            double sinRoll = tr.SinRoll;
            double cosRoll = tr.CosRoll;
            double cosYaw = tr.CosYaw;
            double shiftX = tr.ShiftX;
            double shiftY = tr.ShiftY;
            double xXCoefficient = cosYaw * cosRoll / 1000;
            double xYCoefficient = sinRoll / 1000;
            double yXCoefficient = cosYaw * sinRoll / 1000;
            double yYCoefficient = cosRoll / 1000;
            foreach (var dt in DataTypeValues.DataTypes)
            {
                for (int fragmentNumber = 0; fragmentNumber < fragmentsCount; fragmentNumber++)
                {
                    if ((fragments[0].Contents & dt) != 0)
                    {
                        var currentFragment = fragments[fragmentNumber];
                        short startCol = fragments[0].StartColumn;
                        int numVals = currentFragment.FragmentLayouts[dt].numVals;
                        int step = currentFragment.FragmentLayouts[dt].step;
                        int sourcePos = currentFragment.FragmentLayouts[dt].offset;
                        byte[] currentFragmentRaw = currentFragment.Raw;

                        switch (dt)
                        {
                            case DataType.LM:
                                for (int j = 0; j < numVals; j++)
                                {
                                    int destPos = startCol + (j * fragmentsCount + fragmentNumber) * step;
                                    rawPointsSpan[destPos].Brightness = currentFragmentRaw[sourcePos];
                                    sourcePos++;
                                }

                                break;
                            case DataType.XY:
                                for (int j = 0; j < numVals; j++)
                                {
                                    int destPos =
                                        startCol + (j * fragmentsCount + fragmentNumber) *
                                        step; // for looking up brightness
                                    short xraw = (short)(currentFragmentRaw[sourcePos + 1] |
                                                       currentFragmentRaw[sourcePos] << 8);
                                    short yraw = (short)(currentFragmentRaw[sourcePos + 3] |
                                                       currentFragmentRaw[sourcePos + 2] << 8);
                                    // check for invalid value for pt here
                                    if (xraw != Globals.ProfileDataInvalidXY && yraw != Globals.ProfileDataInvalidXY)
                                    {
                                        validPointCount++;
                                        rawPointsSpan[destPos].X =
                                            xraw * xXCoefficient - yraw * xYCoefficient + shiftX;
                                        rawPointsSpan[destPos].Y =
                                            xraw * yXCoefficient + yraw * yYCoefficient + shiftY;
                                    }

                                    sourcePos += 4;
                                }

                                break;
                            case DataType.PW:
                                break;
                            case DataType.VR:
                                break;
                            case DataType.SP:
                                for (int j = 0; j < numVals; j++)
                                {
                                    if (cameraCoords == null)
                                    {
                                        cameraCoords = new Point2D[Globals.RawProfileDataLength];
                                    }

                                    int col = startCol + (j * fragmentsCount + fragmentNumber) * step;
                                    short rowPixel =
                                        IPAddress.NetworkToHostOrder(BitConverter.ToInt16(currentFragment.Raw,
                                            sourcePos));
                                    cameraCoords[col] = (new Point2D(rowPixel, col, rawPointsSpan[col].Brightness));
                                    sourcePos += 2;
                                }

                                break;
                            case DataType.IM:
                                //TODO:: Adapt to use SP type
                                if (fragmentNumber == fragmentsCount - 1)
                                {
                                    if (cameraCoords == null)
                                    {
                                        cameraCoords = new Point2D[Globals.RawProfileDataLength];
                                    }

                                    for (int i = 0; i < 1456; i++)
                                    {
                                        int rowPixel = BitConverter.ToInt16(currentFragment.Raw, sourcePos);
                                        sourcePos += 2;
                                        int brightness = BitConverter.ToInt16(currentFragment.Raw, sourcePos);
                                        sourcePos += 2;
                                        if (brightness < 0x8000)
                                        {
                                            brightness /= 7;
                                        }
                                        else
                                        {
                                            rowPixel = Globals.ProfileDataInvalidSubpixel;
                                            brightness = 0;
                                        }
                                        cameraCoords[i] = new Point2D(rowPixel, i, brightness);
                                    }
                                    break;
                                }

                                for (int j = 0; j < numVals; j++)
                                {
                                    int pos = fragmentNumber * 4 * 1456 + j;
                                    p.Image[pos] = currentFragment.Raw[sourcePos++];
                                }

                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            if (cameraCoords != null)
            {
                p.CameraCoordinates = cameraCoords.ToArray();
            }

            p.ValidPointCount = validPointCount;

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
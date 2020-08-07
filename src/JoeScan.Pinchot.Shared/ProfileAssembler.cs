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
    class ProfileAssembler
    {
        #region Private Fields

        private readonly DataType[] dataTypes;
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

            dataTypes = new DataType[Enum.GetValues(typeof(DataType)).Length];
            var i = 0;
            foreach (DataType dt in Enum.GetValues(typeof(DataType)))
            {
                dataTypes[i++] = dt;
            }

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
            for (var i = 0; i < fragments[0].NumEncoderVals; i++)
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
            var validPointCount = 0;

            Point2D[] cameraCoords = null;

            if ((fragments[0].Contents & DataType.IM) > 0)
            {
                // we have image data
                p.Image = new byte[1456 * 1088];
            }

            // helpers local 
            var fragmentsCount = fragments.Count();
            var tr = alignmentParameters[fragments[0].Camera];
            var sinRoll = tr.SinRoll;
            var cosRoll = tr.CosRoll;
            var cosYaw = tr.CosYaw;
            var shiftX = tr.ShiftX;
            var shiftY = tr.ShiftY;
            var xXCoefficient = cosYaw * cosRoll / 1000;
            var xYCoefficient = sinRoll / 1000;
            var yXCoefficient = cosYaw * sinRoll / 1000;
            var yYCoefficient = cosRoll / 1000;
            foreach (DataType dt in dataTypes)
            {
                for (var fragmentNumber = 0; fragmentNumber < fragmentsCount; fragmentNumber++)
                {
                    if ((fragments[0].Contents & dt) != 0)
                    {
                        var currentFragment = fragments[fragmentNumber];
                        var startCol = fragments[0].StartColumn;
                        var numVals = currentFragment.FragmentLayouts[dt].numVals;
                        var step = currentFragment.FragmentLayouts[dt].step;
                        var sourcePos = currentFragment.FragmentLayouts[dt].offset;
                        var currentFragmentRaw = currentFragment.Raw;

                        switch (dt)
                        {
                            case DataType.LM:
                                for (var j = 0; j < numVals; j++)
                                {
                                    var destPos = startCol + (j * fragmentsCount + fragmentNumber) * step;
                                    rawPointsSpan[destPos].Brightness = currentFragmentRaw[sourcePos];
                                    sourcePos++;
                                }

                                break;
                            case DataType.XY:
                                for (var j = 0; j < numVals; j++)
                                {
                                    var destPos =
                                        startCol + (j * fragmentsCount + fragmentNumber) *
                                        step; // for looking up brightness
                                    var xraw = (short)(currentFragmentRaw[sourcePos + 1] |
                                                       currentFragmentRaw[sourcePos] << 8);
                                    var yraw = (short)(currentFragmentRaw[sourcePos + 3] |
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
                                for (var j = 0; j < numVals; j++)
                                {
                                    if (cameraCoords == null)
                                    {
                                        cameraCoords = new Point2D[Globals.RawProfileDataLength];
                                    }

                                    var col = startCol + (j * fragmentsCount + fragmentNumber) * step;
                                    var rowPixel =
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
                                            brightness = brightness / 7;
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

                                for (var j = 0; j < numVals; j++)
                                {
                                    var pos = fragmentNumber * 4 * 1456 + j;
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
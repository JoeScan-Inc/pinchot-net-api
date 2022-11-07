// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;
using System.ComponentModel;
using System.Globalization;

namespace JoeScan.Pinchot
{
    [TypeConverter(typeof(CameraLaserPairConverter))]
    internal class CameraLaserPair : Tuple<Camera, Laser>
    {
        internal Camera Camera => Item1;
        internal Laser Laser => Item2;

        internal CameraLaserPair(Camera camera, Laser laser)
            : base(camera, laser)
        {
        }
    }

    internal class CameraLaserPairConverter : TypeConverter
    {
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return TypeDescriptor.GetConverter(typeof(Tuple<Camera, Laser>)).CanConvertTo(context, destinationType);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            return TypeDescriptor.GetConverter(typeof(Tuple<Camera, Laser>)).ConvertTo(context, culture, value, destinationType);
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return typeof(string) == sourceType;
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            // Newtonsoft serializes CameraLaserPairs in the form "(Camera#, Laser#)"
            string str = value as string;
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            string[] toks = str[1..^1].Split(",");
#else
            string[] toks = str.Substring(1, str.Length-1).Split(',');
#endif
            var camera = (Camera)Enum.Parse(typeof(Camera), toks[0]);
            var laser = (Laser)Enum.Parse(typeof(Laser), toks[1]);
            return new CameraLaserPair(camera, laser);
        }
    }
}

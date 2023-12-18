// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections.Generic;
using System.Linq;

namespace JoeScan.Pinchot
{
    internal static class ResolutionPresets
    {
        // static classes are only put into memory when they are first used which can take
        // a few milliseconds, calling this function forces the static ctor to run and
        // "pre-loads" the class which saves us time when StartScanning is called
        internal static void Load() { }

        static ResolutionPresets()
        {
            Presets = new Dictionary<AllDataFormat, Preset>()
            {
                { AllDataFormat.XYBrightnessFull, new Preset() { Dt = DataType.XY | DataType.Brightness, Name = "XY & Brightness Full Resolution", Step = 1 } },
                { AllDataFormat.XYBrightnessHalf, new Preset() { Dt = DataType.XY | DataType.Brightness, Name = "XY & Brightness Half Resolution", Step = 2 } },
                { AllDataFormat.XYBrightnessQuarter, new Preset() { Dt = DataType.XY | DataType.Brightness, Name = "XY & Brightness Quarter Resolution", Step = 4 } },
                { AllDataFormat.XYFull, new Preset() { Dt = DataType.XY, Name = "XY Full Resolution", Step = 1 } },
                { AllDataFormat.XYHalf, new Preset() { Dt = DataType.XY, Name = "XY Half Resolution", Step = 2 } },
                { AllDataFormat.XYQuarter, new Preset() { Dt = DataType.XY, Name = "XY Quarter Resolution", Step = 4 } },
                { AllDataFormat.Subpixel, new Preset() { Dt = DataType.Subpixel, Name = "Subpixel Full Resolution", Step = 1 } },
                { AllDataFormat.SubpixelBrightnessFull, new Preset() { Dt = DataType.Subpixel | DataType.Brightness, Name = "Subpixel & Brightness Full Resolution", Step = 1 } }
            };
        }

        private static readonly Dictionary<AllDataFormat, Preset> Presets = new Dictionary<AllDataFormat, Preset>();

        internal static readonly KeyValuePair<AllDataFormat, string>[] DisplayPresets =
            Presets.Select(q => new KeyValuePair<AllDataFormat, string>(q.Key, q.Value.Name)).ToArray();

        internal static string[] GetNames()
        {
            return Presets.Select(q => q.Value.Name).ToArray();
        }

        internal static DataType GetDataType(AllDataFormat p)
        {
            return Presets[p].Dt;
        }

        internal static uint GetStep(AllDataFormat p)
        {
            return Presets[p].Step;
        }

        private class Preset
        {
            public string Name;
            public DataType Dt;
            public uint Step;
        }
    }
}
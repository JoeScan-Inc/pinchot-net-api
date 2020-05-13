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
        private static readonly List<Preset> Presets = new List<Preset>()
        {
            // the steps array must contain the steps in the order that the flag bit is defined in DataType
            new Preset()
            {
                P = AllDataFormat.XYFullLMFull, Dt = DataType.XY | DataType.LM, Name = "XY Full / LM Full",
                Steps = new short[] { 1, 1 }
            },
            new Preset()
            {
                P = AllDataFormat.XYHalfLMFull, Dt = DataType.XY | DataType.LM, Name = "XY Half / LM Full",
                Steps = new short[] { 1, 2 }
            },
            new Preset()
            {
                P = AllDataFormat.XYQuarterLMFull, Dt = DataType.XY | DataType.LM, Name = "XY Quarter / LM Full",
                Steps = new short[] { 1, 4 }
            },

            new Preset()
            {
                P = AllDataFormat.XYFullLMHalf, Dt = DataType.XY | DataType.LM, Name = "XY Full / LM Half",
                Steps = new short[] { 2, 1 }
            },
            new Preset()
            {
                P = AllDataFormat.XYHalfLMHalf, Dt = DataType.XY | DataType.LM, Name = "XY Half / LM Half",
                Steps = new short[] { 2, 2 }
            },
            new Preset()
            {
                P = AllDataFormat.XYQuarterLMHalf, Dt = DataType.XY | DataType.LM, Name = "XY Quarter / LM Half",
                Steps = new short[] { 2, 4 }
            },

            new Preset()
            {
                P = AllDataFormat.XYFullLMQuarter, Dt = DataType.XY | DataType.LM, Name = "XY Full / LM Quarter",
                Steps = new short[] { 4, 1 }
            },
            new Preset()
            {
                P = AllDataFormat.XYHalfLMQuarter, Dt = DataType.XY | DataType.LM, Name = "XY Half / LM Quarter",
                Steps = new short[] { 4, 2 }
            },
            new Preset()
            {
                P = AllDataFormat.XYQuarterLMQuarter, Dt = DataType.XY | DataType.LM, Name = "XY Quarter / LM Quarter",
                Steps = new short[] { 4, 4 }
            },

            new Preset()
            {
                P = AllDataFormat.XYFull, Dt = DataType.XY, Name = "XY Full",
                Steps = new short[] { 1 }
            },
            new Preset()
            {
                P = AllDataFormat.XYHalf, Dt = DataType.XY, Name = "XY Half",
                Steps = new short[] { 2 }
            },
            new Preset()
            {
                P = AllDataFormat.XYQuarter, Dt = DataType.XY, Name = "XY Quarter",
                Steps = new short[] { 4 }
            },
            new Preset()
            {
                P = AllDataFormat.LMFull, Dt = DataType.LM, Name = "LM Full",
                Steps = new short[] { 1 }
            },
            new Preset()
            {
                P = AllDataFormat.LMHalf, Dt = DataType.LM, Name = "LM Half",
                Steps = new short[] { 2 }
            },
            new Preset()
            {
                P = AllDataFormat.LMQuarter, Dt = DataType.LM, Name = "LM Quarter",
                Steps = new short[] { 4 }
            },
            new Preset()
            {
                P = AllDataFormat.Image, Dt = DataType.IM, Name = "Image",
                Steps = new short[] { 1 }
            },
            new Preset()
            {
                P = AllDataFormat.Subpixel, Dt = DataType.SP, Name = "Subpixel",
                Steps = new short[] { 1 }
            },
            new Preset()
            {
                P = AllDataFormat.SubpixelFullLMFull, Dt = DataType.SP | DataType.LM, Name = "Subpixel Full / LM Full",
                Steps = new short[] { 1, 1 }
            }
        };

        internal static readonly KeyValuePair<AllDataFormat, string>[] DisplayPresets =
            Presets.Select(q => new KeyValuePair<AllDataFormat, string>(q.P, q.Name)).ToArray();

        internal static string[] GetNames()
        {
            return Presets.Select(q => q.Name).ToArray();
        }

        internal static DataType GetDataType(AllDataFormat p)
        {
            return Presets[(int)p].Dt;
        }

        internal static short[] GetStep(AllDataFormat p)
        {
            return Presets[(int)p].Steps;
        }

        private class Preset
        {
            public AllDataFormat P;
            public string Name;
            public DataType Dt;
            public short[] Steps;
        }
    }
}
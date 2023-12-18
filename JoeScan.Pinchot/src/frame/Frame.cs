// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System;

namespace JoeScan.Pinchot
{
    internal class Frame : IFrame
    {
        private readonly Memory<IProfile> profiles;

        internal Span<IProfile> ProfileSpan => profiles.Span;

        internal Frame(uint sequence, int profilesCount)
        {
            SequenceNumber = sequence;
            profiles = new Memory<IProfile>(new IProfile[profilesCount]);
        }

        /// <inheritdoc/>
        public ref IProfile this[int index]
        {
            get
            {
                return ref ProfileSpan[index];
            }
        }

        /// <inheritdoc/>
        public int Count => ProfileSpan.Length;

        /// <inheritdoc/>
        public uint SequenceNumber { get; }

        /// <inheritdoc/>
        public bool IsComplete { get; internal set; }
    }
}

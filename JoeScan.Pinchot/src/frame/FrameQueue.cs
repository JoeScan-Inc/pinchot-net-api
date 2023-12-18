// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections.Concurrent;
using System.Linq;

namespace JoeScan.Pinchot
{
    internal class FrameQueue
    {
        private readonly BlockingCollection<IProfile> profiles;

        internal int Count => profiles.Count;
        internal uint LastSequence { get; private set; }
        internal bool QueueOverflowed { get; private set; }

        internal FrameQueue(int bufferSize)
        {
            profiles = new BlockingCollection<IProfile>(new ConcurrentQueue<IProfile>(), bufferSize);
        }

        internal IProfile Peek()
        {
            return profiles.FirstOrDefault();
        }

        internal void Enqueue(IProfile profile)
        {
            if (!profiles.TryAdd(profile))
            {
                QueueOverflowed = true;
                profiles.TryTake(out _);
                profiles.TryAdd(profile);
            }

            LastSequence = profile.SequenceNumber;
        }

        internal IProfile Dequeue()
        {
            return profiles.Take();
        }

        internal void Clear()
        {
            while (profiles.TryTake(out _)) { }
            QueueOverflowed = false;
        }
    }

}

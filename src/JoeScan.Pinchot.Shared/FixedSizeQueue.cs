// Copyright(c) JoeScan Inc. All Rights Reserved.
//
// Licensed under the BSD 3 Clause License. See LICENSE.txt in the project
// root for license information.

using System.Collections.Concurrent;

namespace JoeScan.Pinchot
{
    internal class FixedSizedQueue<T> : BlockingCollection<T>
    {
        internal int Size { get; private set; }

        internal FixedSizedQueue(int size)
        {
            // underlying queue is unbounded, we manually keep the size constant
            Size = size;
        }

        internal void Enqueue(T obj)
        {
            while (Count >= Size)
            {
                T t;
                TryTake(item: out t);
            }

            TryAdd(obj);
        }
    }
}
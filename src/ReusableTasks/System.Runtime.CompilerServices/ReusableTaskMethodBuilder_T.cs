﻿#pragma warning disable RECS0108 // Warns about static fields in generic types
//
// ReusableTaskMethodBuilder_T.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2019 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System.Collections.Generic;
using System.Threading;

using ReusableTasks;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Not intended to be used directly.
    /// </summary>
    public class ReusableTaskMethodBuilder<T>
    {
        static readonly Stack<ReusableTaskMethodBuilder<T>> Cache = new Stack<ReusableTaskMethodBuilder<T>> ();

        /// <summary>
        /// The number of <see cref="ReusableTaskMethodBuilder{T}"/> instances currently in the cache.
        /// </summary>
        public static int CacheCount => Cache.Count;

        /// <summary>
        /// Removes all <see cref="ReusableTaskMethodBuilder{T}"/> instances from the cache.
        /// </summary>
        public static void ClearCache ()
        {
            lock (Cache)
                Cache.Clear ();
        }

        /// <summary>
        /// The maximum number of instances to store in the cache. Defaults to <see langword="512"/>
        /// </summary>
        public static int MaximumCacheSize { get; set; } = 512;

        /// <summary>
        /// Not intended to be used directly. This method returns an object from the cache, or instantiates
        /// and returns a new object if the cache is empty.
        /// </summary>
        /// <returns></returns>
        public static ReusableTaskMethodBuilder<T> Create ()
        {
            lock (Cache)
                return Cache.Count > 0 ? Cache.Pop () : new ReusableTaskMethodBuilder<T> (true);
        }

        /// <summary>
        /// Creates a <see cref="ReusableTask{T}"/> which will be reset after it has been completed and
        /// awaited, but it will not be returned to the cache.
        /// </summary>
        /// <returns></returns>
        internal static ReusableTaskMethodBuilder<T> CreateUncachedResettable ()
            => new ReusableTaskMethodBuilder<T> (false);

        /// <summary>
        /// Places the instance into the cache for re-use. This is invoked implicitly when a <see cref="ReusableTask{T}"/> is awaited.
        /// </summary>
        /// <param name="builder">The instance to place in the cache</param>
        internal static void Release (ReusableTaskMethodBuilder<T> builder)
        {
            builder.Task.Reset ();
            if (builder.Cacheable) {
                lock (Cache)
                    if (Cache.Count < MaximumCacheSize)
                        Cache.Push (builder);
            }
        }

        bool Cacheable { get; }

        public ReusableTask<T> Task { get; }

        ReusableTaskMethodBuilder (bool cacheable)
        {
            Cacheable = cacheable;
            Task = new ReusableTask<T> (this);
        }

        public void SetException(Exception e)
        {
            Task.Result.Exception = e;
        }

        public void SetResult(T result)
        {
            Task.Result.Value = result;
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            StateMachineCache<TStateMachine>.GetOrCreate ()
                .AwaitOnCompleted (ref awaiter, ref stateMachine);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            StateMachineCache<TStateMachine>.GetOrCreate ()
                .AwaitUnsafeOnCompleted (ref awaiter, ref stateMachine);
        }

        public void Start<TStateMachine> (ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            Task.Result.SyncContext = SynchronizationContext.Current;
            stateMachine.MoveNext ();
        }

        public void SetStateMachine (IAsyncStateMachine stateMachine)
        {
        }
    }
}
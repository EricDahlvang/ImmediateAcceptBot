// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace ImmediateAcceptBot.BackgroundQueue
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private Dictionary<string, Queue<Func<CancellationToken, Task>>> _workItems = new Dictionary<string, Queue<Func<CancellationToken, Task>>>();
        private SemaphoreSlim _signal = new SemaphoreSlim(0);

        public void QueueBackgroundWorkItem(string key, Func<CancellationToken, Task> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            lock (_workItems)
            {
                if(_workItems.TryGetValue(key, out var foundQueue))
                {
                    foundQueue.Enqueue(workItem);
                }
                else 
                {
                    var queue = new Queue<Func<CancellationToken, Task>>();
                    queue.Enqueue(workItem);
                    _workItems.Add(key, queue);
                }
            }

            _signal.Release();
        }

        public async Task<IEnumerable<Func<CancellationToken, Task>>> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);

            var dequeued = new List<Func<CancellationToken, Task>>();
            lock (_workItems)
            {
                foreach(var kvp in _workItems.ToImmutableArray())
                {
                    dequeued.Add(kvp.Value.Dequeue());
                    if(kvp.Value.Count == 0)
                    {
                        _workItems.Remove(kvp.Key);
                    }
                }
            }

            return dequeued;
        }
    }
}

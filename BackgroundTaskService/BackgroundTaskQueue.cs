// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ImmediateAcceptBot.BackgroundQueue
{
    /// <summary>
    /// Singleton queue, used to transfer a workitem to the <see cref="HostedTaskService"/>.
    /// </summary>
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private ConcurrentQueue<Func<CancellationToken, Task>> _workItems = new ConcurrentQueue<Func<CancellationToken, Task>>();
        private SemaphoreSlim _signal = new SemaphoreSlim(0);
        
        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
        {
            if (workItem == null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            _workItems.Enqueue(workItem);
            _signal.Release();
        }

        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);

            Func<CancellationToken, Task> dequeued;
            _workItems.TryDequeue(out dequeued);
            return dequeued;
        }
    }
}

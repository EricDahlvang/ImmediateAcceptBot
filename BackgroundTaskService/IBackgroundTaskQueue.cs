// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ImmediateAcceptBot.BackgroundQueue
{
    /// <summary>
    /// Interface for a class used to transfer a workitem to the <see cref="HostedTaskService"/>.
    /// </summary>
    public interface IBackgroundTaskQueue
    {
        void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem);

        Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
    }
}

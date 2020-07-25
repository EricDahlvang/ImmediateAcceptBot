// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImmediateAcceptBot.BackgroundQueue
{
    /// <summary>
    /// BackgroundService implementation used to process work items on background threads.
    /// 
    /// <seealso cref="https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.backgroundservice"/>
    /// </summary>
    public class HostedTaskService : BackgroundService
    {
        private readonly ILogger<HostedTaskService> _logger;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        
        //private readonly ConcurrentBag<Task> _tasks = new ConcurrentBag<Task>();
        
        private readonly ConcurrentDictionary<Func<CancellationToken,Task>, Task> _tasks = new ConcurrentDictionary<Func<CancellationToken, Task>, Task>();
        private readonly IBackgroundTaskQueue _taskQueue;

        public HostedTaskService(IBackgroundTaskQueue taskQueue, ILogger<HostedTaskService> logger)
        {
            _taskQueue = taskQueue;
            _logger = logger;
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping.");

            // Obtain a write lock and do not release it, preventing new tasks from starting
            if (_lock.TryEnterWriteLock(TimeSpan.FromSeconds(60)))
            {
                // Wait for currently running tasks, but only n seconds.
                await Task.WhenAny(Task.WhenAll(_tasks.Values), Task.Delay(TimeSpan.FromSeconds(60)));
            }

            await base.StopAsync(stoppingToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Queued Hosted Service is running.{Environment.NewLine}");
            
            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);
                if (workItem != null)
                {
                    try
                    {
                        // The read lock will not be acquirable if the app is shutting down.
                        // New tasks should not be starting during shutdown.
                        if (_lock.TryEnterReadLock(500))
                        {
                            var task = GetTaskFromWorkItem(workItem, stoppingToken)
                                .ContinueWith(t =>
                                {
                                    // After the work item completes, clear the running tasks of all completed tasks.
                                    foreach (var task in _tasks.Where(tsk => tsk.Value.IsCompleted))
                                    {
                                        _tasks.TryRemove(task.Key, out Task removed);
                                    }
                                });

                            _tasks.TryAdd(workItem, task);
                            task.Start();
                        }
                        else
                        {
                            _logger.LogError("Work item not processed.  Server is shutting down.", nameof(BackgroundProcessing));
                        }
                    }
                    finally
                    {
                        _lock.ExitReadLock();
                    }
                }
            }
        }

        private Task GetTaskFromWorkItem(Func<CancellationToken, Task> workItem, CancellationToken stoppingToken)
        {
            // Start the work item, and return the task
            return Task.Run(
                async () =>
                {
                    try
                    {
                        await workItem(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        // Bot Errors should be processed in the Adapter.OnTurnError.
                        _logger.LogError(ex, "Error occurred executing WorkItem.", nameof(HostedTaskService));
                    }
                }, stoppingToken);
        }
    }
}

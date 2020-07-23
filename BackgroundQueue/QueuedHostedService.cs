// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImmediateAcceptBot.BackgroundQueue
{
    public class QueuedHostedService : BackgroundService
    {
        private readonly ILogger<QueuedHostedService> _logger;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private ConcurrentDictionary<string, Task> _runningTasks = new ConcurrentDictionary<string, Task>();
        
        private bool _shuttingDown = false;

        public QueuedHostedService(IBackgroundTaskQueue taskQueue, ILogger<QueuedHostedService> logger)
        {
            TaskQueue = taskQueue;
            _logger = logger;
        }

        public IBackgroundTaskQueue TaskQueue { get; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Queued Hosted Service is running.{Environment.NewLine}");
            
            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem = await TaskQueue.DequeueAsync(stoppingToken);

                if (workItem != null)
                {
                    try
                    {
                        // Create the task which will execute the work item
                        var task = new Task(async () =>
                        {
                            try
                            {
                                await workItem(stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error occurred executing WorkItem.", nameof(BackgroundProcessing));
                            }
                            finally
                            {
                                // After the work item completes, clear the running tasks
                                // of all completed tasks.
                                var completed = _runningTasks.Where(t => t.Value.IsCompleted);
                                foreach (var complete in completed)
                                {
                                    _runningTasks.TryRemove(complete.Key, out Task removed);
                                }
                            }
                        }, stoppingToken);

                        try
                        {
                            // Do not start the task if the app is shutting down
                            if (_lock.TryEnterReadLock(5) && !_shuttingDown)
                            {
                                _runningTasks.TryAdd(Guid.NewGuid().ToString(), task);
                                task.Start();
                            }
                            else
                            {
                                throw new InvalidOperationException("Server is shutting down.");
                            }
                        }
                        finally
                        {
                            _lock.ExitReadLock();
                        }
                    }
                    catch (Exception ex)
                    when (ex.Message != "Server is shutting down")
                    {
                        _logger.LogError(ex, "Error occurred executing WorkItems.", nameof(BackgroundProcessing));
                    }
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping.");

            // Acquire the lock, preventing new tasks from starting
            if (_lock.TryEnterWriteLock(5000))
            {
                _shuttingDown = true;
                // Wait for currently running tasks, but only 5 seconds
                // since that is the default Stopping timeout.
                await Task.WhenAny(Task.WhenAll(_runningTasks.Values.ToArray()), Task.Delay(5000));
            }

            await base.StopAsync(stoppingToken);
        }
    }
}

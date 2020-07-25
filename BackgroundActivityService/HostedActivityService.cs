// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ImmediateAcceptBot.BackgroundQueue
{
    /// <summary>
    /// BackgroundService implementation used to process activities with claims.
    /// 
    /// <seealso cref="https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.backgroundservice"/>
    /// </summary>
    public class HostedActivityService : BackgroundService
    {
        private readonly ILogger<HostedTaskService> _logger;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private readonly ConcurrentDictionary<ActivityWithClaims, Task> _activitiesProcessing = new ConcurrentDictionary<ActivityWithClaims, Task>();
        private IActivityTaskQueue _activityQueue;
        private readonly ImmediateAcceptAdapter _adapter;
        private readonly Func<IBot> _botCreator;
        private readonly int _shutdownTimeoutSeconds;

        public HostedActivityService(IConfiguration config, Func<IBot> botCreator, ImmediateAcceptAdapter adapter, IActivityTaskQueue activityTaskQueue, ILogger<HostedTaskService> logger)
        {
            _shutdownTimeoutSeconds = config.GetValue<int>("ShutdownTimeoutSeconds");
            _activityQueue = activityTaskQueue;
            _botCreator = botCreator;
            _adapter = adapter;
            _logger = logger;
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping.");

            // Obtain a write lock and do not release it, preventing new tasks from starting
            if (_lock.TryEnterWriteLock(TimeSpan.FromSeconds(60)))
            {
                // Wait for currently running tasks, but only n seconds.
                await Task.WhenAny(Task.WhenAll(_activitiesProcessing.Values), Task.Delay(TimeSpan.FromSeconds(60)));
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
                var activityWithClaims = await _activityQueue.WaitForActivityAsync(stoppingToken);
                if (activityWithClaims != null)
                {
                    try
                    {
                        // The read lock will not be acquirable if the app is shutting down.
                        // New tasks should not be starting during shutdown.
                        if (_lock.TryEnterReadLock(500))
                        {
                            // Create the task which will execute the work item.
                            var task = GetTaskFromWorkItem(activityWithClaims, stoppingToken)
                                .ContinueWith(t =>
                            {
                                // After the work item completes, clear the running tasks of all completed tasks.
                                foreach (var task in _activitiesProcessing.Where(tsk => tsk.Value.IsCompleted))
                                {
                                    _activitiesProcessing.TryRemove(task.Key, out Task removed);
                                }
                            });

                            _activitiesProcessing.TryAdd(activityWithClaims, task);
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

        private Task GetTaskFromWorkItem(ActivityWithClaims activityWithClaims, CancellationToken stoppingToken)
        {
            // Start the work item, and return the task
            return Task.Run(
                async () =>
            {
                try
                {
                    await _adapter.ProcessActivityAsync(activityWithClaims.ClaimsIdentity, activityWithClaims.Activity, _botCreator().OnTurnAsync, stoppingToken);
                }
                catch (Exception ex)
                {
                    // Bot Errors should be processed in the Adapter.OnTurnError.
                    _logger.LogError(ex, "Error occurred executing WorkItem.", nameof(HostedActivityService));
                }
            }, stoppingToken);
        }
    }
}

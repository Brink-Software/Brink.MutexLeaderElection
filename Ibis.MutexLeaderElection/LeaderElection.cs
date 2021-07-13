﻿using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Ibis.MutexLeaderElection
{
    public class LeaderElection : ILeaderElection
    {
        private readonly ILogger<LeaderElection> _logger;
        private readonly TimeSpan _renewInterval;
        private readonly TimeSpan _acquireInterval;
        private readonly IDistributedLock _distributedLock;

        public LeaderElection(ILogger<LeaderElection> logger, IDistributedLock distributedLock) : this(logger, distributedLock, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)) { }

        public LeaderElection(ILogger<LeaderElection> logger, IDistributedLock distributedLock, TimeSpan renewInterval, TimeSpan acquireInterval)
        {
            _logger = logger;
            _renewInterval = renewInterval;
            _acquireInterval = acquireInterval;
            _distributedLock = distributedLock;
        }

        public async Task RunTaskWhenElectedLeaderAsync(Func<CancellationToken, Task> taskToRunWhenElectedLeader, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!await TryAcquireLockOrWait(cancellationToken))
                {
                    continue;
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                
                var leaderTask = taskToRunWhenElectedLeader(cts.Token);
                var renewalTask = KeepRenewingLockAsync(cts.Token);

                await CancelAllWhenAnyCompletes(leaderTask, renewalTask, cts);
            }
        }

        private async Task<bool> TryAcquireLockOrWait(CancellationToken cancellationToken)
        {
            try
            {
                if (!await _distributedLock.TryAcquireLockAsync(cancellationToken))
                {
                    await Task.Delay(_acquireInterval, cancellationToken);
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Task CancelAllWhenAnyCompletes(Task leaderTask, Task renewLeaseTask, CancellationTokenSource cts)
        {
            await Task.WhenAny(leaderTask, renewLeaseTask);
            cts.Cancel();

            var allTasks = Task.WhenAll(leaderTask, renewLeaseTask);
            try
            {
                await Task.WhenAll(allTasks);
            }
            catch (Exception)
            {
                if (allTasks.Exception != null)
                {
                    allTasks.Exception.Handle(ex =>
                    {
                        if (!(ex is OperationCanceledException))
                        {
                            _logger.LogError((int)LoggingEvents.CompletionException, ex, ex.Message);
                        }

                        return true;
                    });
                }
            }

            _logger.LogInformation((int)LoggingEvents.Completed, "Completed");
        }

        private async Task KeepRenewingLockAsync(CancellationToken cancellationToken)
        {
            var renewOffset = new Stopwatch();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    renewOffset.Restart();
                    var isRenewed = await _distributedLock.TryRenewLockAsync(cancellationToken);
                    renewOffset.Stop();

                    if (!isRenewed)
                        return;

                    var timeToWait = _renewInterval - renewOffset.Elapsed;
                    if (timeToWait > TimeSpan.Zero)
                        await Task.Delay(timeToWait, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    await _distributedLock.TryReleaseLockAsync();
                    return;
                }
            }
        }
    }
}
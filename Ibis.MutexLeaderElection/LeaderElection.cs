using Microsoft.Extensions.Logging;
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

        /// <summary>
        /// Create a new instance of LeaderElection with a default lease renewal interval of 3 seconds and a default interval of 3 seconds to try to become elected as the leader
        /// </summary>
        /// <param name="logger">Logger instance to use</param>
        /// <param name="distributedLock">The distributed lock instance to use</param>
        public LeaderElection(ILogger<LeaderElection> logger, IDistributedLock distributedLock) : this(logger, distributedLock, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)) { }

        /// <summary>
        /// Create a new instance of LeaderElection
        /// </summary>
        /// <param name="logger">Logger instance to use</param>
        /// <param name="distributedLock">The distributed lock instance to use</param>
        /// <param name="renewInterval">The period between lease renewals</param>
        /// <param name="acquireInterval">The period between attempts to get elected as the leader</param>
        public LeaderElection(ILogger<LeaderElection> logger, IDistributedLock distributedLock, TimeSpan renewInterval, TimeSpan acquireInterval)
        {
            _logger = logger;
            _renewInterval = renewInterval;
            _acquireInterval = acquireInterval;
            _distributedLock = distributedLock;
        }

        /// <summary>
        /// Try to become leader. If it succeeds run a designated task, otherwise wait and retry to be elected
        /// </summary>
        /// <param name="taskToRunWhenElectedLeader">The task to run once elected as leader</param>
        /// <param name="cancellationToken">Cancellation token which is used to cancel the leader task on shutdown</param>
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
                            _logger.LogError((int)LoggingEvents.CompletedWithException, ex, ex.Message);
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
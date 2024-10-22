using Microsoft.Extensions.Logging.Abstractions;

namespace Ibis.MutexLeaderElection.Tests
{
    public class LeaderElectionTests
    {
        [Fact]
        public async Task SingleCandidateShouldBecomeElected()
        {
            var distributedLock = new DistributedLock(new NullLogger<DistributedLock>(), new DistributedLockOptions
            {
                StorageConnectionString = "UseDevelopmentStorage=true",
                StorageBlobName = Guid.NewGuid().ToString()
            });
            var leaderElection = new LeaderElection(new NullLogger<LeaderElection>(), distributedLock);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var isSelectedLeader = false;

            await leaderElection.RunTaskWhenElectedLeaderAsync(async _ =>
            {
                isSelectedLeader = true;
                cts.Cancel();
            }, cts.Token);

            Assert.True(isSelectedLeader);
        }

        [Fact]
        public async Task OnlyOneCandidateShouldBecomeElected()
        {
            var distributedLock = new DistributedLock(new NullLogger<DistributedLock>(), new DistributedLockOptions
            {
                StorageConnectionString = "UseDevelopmentStorage=true",
                StorageBlobName = Guid.NewGuid().ToString()
            });
            var leaderElectionOne = new LeaderElection(new NullLogger<LeaderElection>(), distributedLock);
            var leaderElectionTwo = new LeaderElection(new NullLogger<LeaderElection>(), distributedLock);

            // Fail if no leader is elected in 2 seconds
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            var leaderOneTask = leaderElectionOne.RunTaskWhenElectedLeaderAsync(EndlessLoop, cts.Token);
            var leaderTwoTask = leaderElectionTwo.RunTaskWhenElectedLeaderAsync(EndlessLoop, cts.Token);

            // Give the system some time to elect a leader
            await Task.Delay(TimeSpan.FromMilliseconds(200));

            Assert.True(leaderElectionOne.IsLeader ^ leaderElectionTwo.IsLeader);

            cts.Cancel();

            await Task.WhenAll(leaderOneTask, leaderTwoTask);

            static async Task EndlessLoop(CancellationToken cancellationToken)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                }
            }
        }
    }
}
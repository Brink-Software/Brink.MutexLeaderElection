using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ibis.MutexLeaderElection.Sample
{
    public class DemoBackgroundService : IHostedService
    {
        private readonly ILogger<BackgroundService> _logger;
        private readonly ILeaderElection _leaderElection;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task _continuousTask = Task.CompletedTask;

        public DemoBackgroundService(ILogger<BackgroundService> logger, ILeaderElection leaderElection)
        {
            _logger = logger;
            _leaderElection = leaderElection;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting DemoBackgroundService");

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _continuousTask = _leaderElection.RunTaskWhenElectedLeaderAsync(WorkerAsync, _cancellationTokenSource.Token);

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping DemoBackgroundService");

            _cancellationTokenSource?.Cancel();

            await _continuousTask;
        }

        public async Task WorkerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Working...");
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }
}
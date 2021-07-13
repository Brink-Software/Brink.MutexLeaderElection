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

    public DemoBackgroundService(ILogger<BackgroundService> logger, ILeaderElection leaderElection)
    {
        _logger = logger;
        _leaderElection = leaderElection;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting DemoBackgroundService");

        await _leaderElection.RunTaskWhenElectedLeaderAsync(WorkerAsync, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping DemoBackgroundService");

        return Task.CompletedTask;
    }

    public async Task WorkerAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Working...");

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }
}
}
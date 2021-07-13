using System.Threading;
using System.Threading.Tasks;

namespace Ibis.MutexLeaderElection
{
    public interface IDistributedLock
    {
        Task<bool> TryAcquireLockAsync(CancellationToken cancellationToken);
        Task<bool> TryRenewLockAsync(CancellationToken cancellationToken);
        Task TryReleaseLockAsync();
    }
}
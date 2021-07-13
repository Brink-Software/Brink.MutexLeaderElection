using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ibis.MutexLeaderElection
{
    public interface ILeaderElection
    {
        Task RunTaskWhenElectedLeaderAsync(Func<CancellationToken, Task> taskToRunWhenElectedLeader, CancellationToken cancellationToken);
    }
}
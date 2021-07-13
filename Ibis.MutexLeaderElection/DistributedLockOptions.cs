using Azure.Core;
using System;

namespace Ibis.MutexLeaderElection
{
    public class DistributedLockOptions
    {
        public Uri? StorageContainerUri { get; set; }
        public string StorageBlobName { get; set; } = "distributedLock";
        public string? StorageContainerName { get; set; } = "locks";
        public string? StorageConnectionString { get; set; }
        public TokenCredential? TokenCredential { get; set; }
        public TimeSpan LockDuration { get; set; } = TimeSpan.FromSeconds(15);
    }
}
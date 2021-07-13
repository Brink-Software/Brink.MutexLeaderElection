using Azure.Core;
using System;

namespace Ibis.MutexLeaderElection
{
    /// <summary>
    /// Provides the configuration of the distributed lock.
    /// Either use the `TokenCredential` and `StorageContainerUri` properties to connect to the Azure Storage Account
    /// or use the `StorageConnectionString` and `StorageContainerName` properties to connect to the Azure Storage Account
    /// </summary>
    public class DistributedLockOptions
    {
        /// <summary>
        /// Uri of the Azure Storage Container that contains the blob that is used to get a lease on
        /// </summary>
        public Uri? StorageContainerUri { get; set; }
        /// <summary>
        /// Name of the Azure Storage Blob  that is used to get a lease on. Default: distributedLock 
        /// </summary>
        public string StorageBlobName { get; set; } = "distributedLock";
        /// <summary>
        /// Name of the Azure Storage Container that contains the blob that is used to get a lease on. Default: locks
        /// </summary>
        public string? StorageContainerName { get; set; } = "locks";
        /// <summary>
        /// Connection string to the Azure Storage Account
        /// </summary>
        public string? StorageConnectionString { get; set; }
        /// <summary>
        /// TokenCredential used to access the Azure Storage Account
        /// </summary>
        public TokenCredential? TokenCredential { get; set; }
        /// <summary>
        /// Duration of the lease. Default: 15 seconds. Must be between 15 and 60 seconds
        /// </summary>
        public TimeSpan LockDuration { get; set; } = TimeSpan.FromSeconds(15);
    }
}
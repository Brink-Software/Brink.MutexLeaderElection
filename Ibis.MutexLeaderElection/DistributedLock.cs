using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ibis.MutexLeaderElection
{
    public class DistributedLock : IDistributedLock
    {
        private string? _lockId;
        private readonly BlobClient _blobClient;
        private readonly ILogger _logger;
        private readonly DistributedLockOptions _distributedLockOptions;
        private readonly BlobContainerClient _containerClient;

        public DistributedLock(ILogger<DistributedLock> logger, DistributedLockOptions distributedLockOptions)
        {
            _logger = logger;
            _distributedLockOptions = distributedLockOptions;

            if (!string.IsNullOrWhiteSpace(_distributedLockOptions.StorageConnectionString))
            {
                _containerClient = new BlobContainerClient(_distributedLockOptions.StorageConnectionString, distributedLockOptions.StorageContainerName);
                _blobClient = _containerClient.GetBlobClient(distributedLockOptions.StorageBlobName);
            }
            else
            {
                _containerClient = new BlobContainerClient(_distributedLockOptions.StorageContainerUri, _distributedLockOptions.TokenCredential);
                _blobClient = _containerClient.GetBlobClient(distributedLockOptions.StorageBlobName);
            }
        }

        public async Task<bool> TryAcquireLockAsync(CancellationToken cancellationToken)
        {
            try
            {
                var blc = _blobClient.GetBlobLeaseClient();
                var leaseResponse = await blc.AcquireAsync(_distributedLockOptions.LockDuration, cancellationToken: cancellationToken);
                _lockId = leaseResponse.Value.LeaseId;
                _logger.LogInformation((int)LoggingEvents.LockAcquired, "Lock acquired. Id: {LockId}", _lockId);

                return true;
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == "BlobNotFound" || ex.ErrorCode == "ContainerNotFound")
            {
                _logger.LogInformation((int)LoggingEvents.FirstLock, "First lock attempt");

                await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
                await _blobClient.UploadAsync(Stream.Null, overwrite: true, cancellationToken);

                return await TryAcquireLockAsync(cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == "LeaseAlreadyPresent")
            {
                _logger.LogDebug((int)LoggingEvents.LockAlreadyTaken, "Lock already acquired");
                return false;
            }
        }

        public async Task<bool> TryRenewLockAsync(CancellationToken cancellationToken)
        {
            if (_lockId == null)
                return false;

            try
            {
                var blc = _blobClient.GetBlobLeaseClient(_lockId);
                await blc.RenewAsync(cancellationToken: cancellationToken);
                _logger.LogDebug((int)LoggingEvents.LockRenewed, "Lock renewed. Id: {LockId}", _lockId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError((int)LoggingEvents.RenewalFailed, ex, "Lock renewal failed. Id: {LockId}", _lockId);
                return false;
            }
        }

        public async Task TryReleaseLockAsync()
        {
            if (_lockId == null)
                return;

            try
            {
                var blc = _blobClient.GetBlobLeaseClient(_lockId);
                await blc.ReleaseAsync();
                _logger.LogInformation((int)LoggingEvents.LockReleased, "Lock released. Id: {LockId}", _lockId);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError((int)LoggingEvents.ReleaseFailed, ex, "Lock release failed. Id: {LockId}", _lockId);
            }
        }
    }
}

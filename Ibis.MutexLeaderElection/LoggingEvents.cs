namespace Ibis.MutexLeaderElection
{
    public enum LoggingEvents
    {
        LockRenewed = 1,
        LockAlreadyTaken,
        FirstLock,
        LockAcquired,
        LockReleased,
        RenewalFailed,
        ReleaseFailed,

        Completed = 100,
        CompletedWithException
    }
}
namespace FileSystemWatcherAlts.Polling
{
    /// <summary>
    /// Defines how FileSystemPoller reports back to the listeners
    /// </summary>
    public enum PollingType
    {
        /// <summary>
        /// Watcher-like behivour. Reports NEWLY created/deleted files.
        /// </summary>
        Watch,
        /// <summary>
        /// Poller-like behivour. Reports ALL existing files in the directory in EVERY poll.
        /// </summary>
        Poll
    }
}
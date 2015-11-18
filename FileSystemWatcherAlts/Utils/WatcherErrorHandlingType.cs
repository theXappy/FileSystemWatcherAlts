using System;

namespace FileSystemWatcherAlts.Utils
{
    /// <summary>
    /// Different approachs to handle a FileSystemWatcher error.
    /// </summary>
    [Flags]
    public enum WatcherErrorHandlingType
    {
        /// <summary>
        /// Forward the error using the Error event
        /// </summary>
        Forward = 0,
        /// <summary>
        /// Do not forward the error using the Error event
        /// </summary>
        Swallow = 1,
        /// <summary>
        /// Refresh the internal watcher
        /// </summary>
        Refresh = 2,
        /// <summary>
        /// Refresh the internal watcher and do not forward the error using the Error event
        /// </summary>
        RefreshAndSwallow = 3,
    }
}
using System;
using System.IO;

namespace FileSystemWatcherAlts
{
    /// <summary>
    /// Defines properties, events and methods for a FileSystemWatcher-like class
    /// </summary>
    public interface IFileSystemWatcher : IDisposable, ICloneable
    {
        bool EnableRaisingEvents { get; set; }
        string Filter { get; set; }
        bool IncludeSubdirectories { get; set; }
        int InternalBufferSize { get; set; }
        NotifyFilters NotifyFilter { get; set; }
        string Path { get; set; }

        event FileSystemEventHandler Changed;
        event FileSystemEventHandler Created;
        event FileSystemEventHandler Deleted;
        event RenamedEventHandler Renamed;
        event ErrorEventHandler Error;
        
        WaitForChangedResult WaitForChanged(WatcherChangeTypes changeType);
        WaitForChangedResult WaitForChanged(WatcherChangeTypes changeType, int timeout);
    }
}
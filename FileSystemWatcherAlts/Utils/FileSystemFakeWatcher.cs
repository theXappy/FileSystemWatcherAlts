using System.IO;

namespace FileSystemWatcherAlts.Utils
{
    /// <summary>
    /// A fake object which implements the IFileSystemWatcher interface.
    /// </summary>
    internal class FileSystemFakeWatcher : IFileSystemWatcher
    {
        #region Properties

        public bool EnableRaisingEvents { get; set; }
        public string Filter { get; set; }
        public bool IncludeSubdirectories { get; set; }
        public int InternalBufferSize { get; set; }
        public NotifyFilters NotifyFilter { get; set; }
        public string Path { get; set; }

        #endregion
        
        #region Events

        public event FileSystemEventHandler Changed
        {
            add { }
            remove { }
        }

        public event FileSystemEventHandler Created
        {
            add { }
            remove { }
        }

        public event FileSystemEventHandler Deleted
        {
            add { }
            remove { }
        }

        public event RenamedEventHandler Renamed
        {
            add { }
            remove { }
        }

        public event ErrorEventHandler Error
        {
            add { }
            remove { }
        }

        #endregion
        
        #region IFileSystemWatcher Methods

        public WaitForChangedResult WaitForChanged(WatcherChangeTypes changeType)
        {
            return new WaitForChangedResult();
        }

        public WaitForChangedResult WaitForChanged(WatcherChangeTypes changeType, int timeout)
        {
            return new WaitForChangedResult();
        }

        #endregion
        
        #region IDisposable Methods

        public void Dispose()
        {
        }

        #endregion

        #region ICloneable Methods

        public object Clone()
        {
            return this;
        }

        #endregion       
    }
}
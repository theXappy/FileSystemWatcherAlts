using System;
using System.IO;

namespace FileSystemWatcherAlts.Wrappers
{
    /// <summary>
    /// Adapts a FileSystemWatcher to make it fit the IFileSystemWatcher interface
    /// </summary>
    public class FileSystemWatcherAdapter : IFileSystemWatcher
    {
        #region Fields

        private FileSystemWatcher _watcher;

        #endregion

        #region Constructors

        public FileSystemWatcherAdapter(FileSystemWatcher watcherToWrap)
        {
            _watcher = watcherToWrap;
            SubscribeToPrivateWatcherEvents();
        }
        public FileSystemWatcherAdapter() : this(new FileSystemWatcher())
        {
        }

        public FileSystemWatcherAdapter(string path) : this(new FileSystemWatcher(path))
        {
        }

        public FileSystemWatcherAdapter(string path, string filter) : this(new FileSystemWatcher(path,filter))
        {
        }

        #endregion

        #region Events

        public event FileSystemEventHandler Changed;
        public event FileSystemEventHandler Created;
        public event FileSystemEventHandler Deleted;
        public event ErrorEventHandler Error;
        public event RenamedEventHandler Renamed;

        #endregion

        #region Proprties
        protected FileSystemWatcher InternalWatcher
        {
            get { return _watcher; }
            set
            {
                UnsubscribeFromPrivateWatcherEvents();
                _watcher = value;
                SubscribeToPrivateWatcherEvents();
            }
        }

        public bool EnableRaisingEvents
        {
            get
            {
                return InternalWatcher.EnableRaisingEvents;
            }
            set
            {
                InternalWatcher.EnableRaisingEvents = value;
            }
        }

        public string Filter
        {
            get
            {
                return InternalWatcher.Filter;
            }
            set
            {
                InternalWatcher.Filter = value;
            }
        }

        public bool IncludeSubdirectories
        {
            get
            {
                return InternalWatcher.IncludeSubdirectories;
            }
            set
            {
                InternalWatcher.IncludeSubdirectories = value;
            }
        }

        public int InternalBufferSize
        {
            get
            {
                return InternalWatcher.InternalBufferSize;
            }
            set
            {
                InternalWatcher.InternalBufferSize = value;
            }
        }

        public NotifyFilters NotifyFilter
        {
            get
            {
                return InternalWatcher.NotifyFilter;
            }
            set
            {
                InternalWatcher.NotifyFilter = value;
            }
        }

        public string Path
        {
            get
            {
                return InternalWatcher.Path;
            }
            set
            {
                InternalWatcher.Path = value;
            }
        }

        #endregion

        #region Watcher Refreshing

        protected void SubscribeToPrivateWatcherEvents()
        {
            if (InternalWatcher == null) return;

            InternalWatcher.Created += OnCreated;
            InternalWatcher.Changed += OnChanged;
            InternalWatcher.Deleted += OnDeleted;
            InternalWatcher.Error += OnError;
            InternalWatcher.Renamed += OnRenamed;
        }

        protected void UnsubscribeFromPrivateWatcherEvents()
        {
            if (InternalWatcher == null) return;

            InternalWatcher.Created -= OnCreated;
            InternalWatcher.Changed -= OnChanged;
            InternalWatcher.Deleted -= OnDeleted;
            InternalWatcher.Error -= OnError;
            InternalWatcher.Renamed -= OnRenamed;
        }

        protected void OnChanged(object sender, FileSystemEventArgs fileSystemEventArgs) => Changed?.Invoke(sender, fileSystemEventArgs);
        protected void OnCreated(object sender, FileSystemEventArgs fileSystemEventArgs) => Created?.Invoke(sender, fileSystemEventArgs);
        protected void OnDeleted(object sender, FileSystemEventArgs fileSystemEventArgs) => Deleted?.Invoke(sender, fileSystemEventArgs);
        protected void OnError(object sender, ErrorEventArgs fileSystemErrorArgs) => Error?.Invoke(sender, fileSystemErrorArgs);
        protected void OnRenamed(object sender, RenamedEventArgs fileSystemEventArgs) => Renamed?.Invoke(sender, fileSystemEventArgs);

        #endregion

        #region Mmethods

        public WaitForChangedResult WaitForChanged(WatcherChangeTypes changeType)
        {
            return InternalWatcher.WaitForChanged(changeType);
        }

        public WaitForChangedResult WaitForChanged(WatcherChangeTypes changeType, int timeout)
        {
            return InternalWatcher.WaitForChanged(changeType, timeout);
        }

        #endregion
        
        #region IDisposeable Methods

        ~FileSystemWatcherAdapter()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            UnsubscribeFromPrivateWatcherEvents();

            if (disposing)
            {
                _watcher.Dispose();
            }
        }
        #endregion

        #region ICloneable Methods

        public object Clone()
        {
            FileSystemWatcher clonedEncapsWatcher = new FileSystemWatcher()
            {
                NotifyFilter = InternalWatcher.NotifyFilter,
                Path = InternalWatcher.Path,
                IncludeSubdirectories = InternalWatcher.IncludeSubdirectories,
                InternalBufferSize = InternalWatcher.InternalBufferSize,
                Filter = InternalWatcher.Filter,
                EnableRaisingEvents = InternalWatcher.EnableRaisingEvents
            };
            return new FileSystemWatcherAdapter(clonedEncapsWatcher);
        }

        #endregion

    }
}
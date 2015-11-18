using System;
using System.IO;

namespace FileSystemWatcherAlts.Wrappers
{
    /// <summary>
    /// An abstract wrapper for an IFilesystemWatcher
    /// </summary>
    public abstract class FileSystemWatcherWrapper : IFileSystemWatcher
    {

        #region Fields

        private IFileSystemWatcher _internalWatcher;
        
        #endregion

        #region Events

        public virtual event FileSystemEventHandler Changed;
        public virtual event FileSystemEventHandler Created;
        public virtual event FileSystemEventHandler Deleted;
        public virtual event ErrorEventHandler Error;
        public virtual event RenamedEventHandler Renamed;

        #endregion

        #region Proprties

        protected IFileSystemWatcher InternalWatcher
        {
            get { return _internalWatcher; }
            set
            {
                UnsubscribeFromInternalWatcherEvents();
                _internalWatcher = value;
                SubscribeToPrivateWatcherEvents();
            }
        }
        
        public virtual bool EnableRaisingEvents
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

        public virtual string Filter
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

        public virtual bool IncludeSubdirectories
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

        public virtual int InternalBufferSize
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

        public virtual NotifyFilters NotifyFilter
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

        public virtual string Path
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

        #region Constructors
        
        protected FileSystemWatcherWrapper(IFileSystemWatcher watcher)
        {
            InternalWatcher = watcher;
        }
        protected FileSystemWatcherWrapper(FileSystemWatcher watcher) : this(new FileSystemWatcherAdapter(watcher))
        {
        }
        protected FileSystemWatcherWrapper() : this(new FileSystemWatcherAdapter())
        {
        }
        protected FileSystemWatcherWrapper(string path) : this(new FileSystemWatcherAdapter(path))
        {
        }
        protected FileSystemWatcherWrapper(string path, string filter) : this(new FileSystemWatcherAdapter(path, filter))
        {
        }

        #endregion

        #region Events related Methods

        // Subscribe/Unsubscribe from wrapped watcher's events
        protected virtual void SubscribeToPrivateWatcherEvents()
        {
            if (InternalWatcher == null) return;

            InternalWatcher.Created += OnCreated;
            InternalWatcher.Changed += OnChanged;
            InternalWatcher.Deleted += OnDeleted;
            InternalWatcher.Error += OnError;
            InternalWatcher.Renamed += OnRenamed;
        }
        protected virtual void UnsubscribeFromInternalWatcherEvents()
        {
            if (InternalWatcher == null) return;

            InternalWatcher.Created -= OnCreated;
            InternalWatcher.Changed -= OnChanged;
            InternalWatcher.Deleted -= OnDeleted;
            InternalWatcher.Error -= OnError;
            InternalWatcher.Renamed -= OnRenamed;
        }

        // Events Invokers
        protected virtual void OnChanged(object sender, FileSystemEventArgs fileSystemEventArgs) => Changed?.Invoke(sender, fileSystemEventArgs);
        protected virtual void OnCreated(object sender, FileSystemEventArgs fileSystemEventArgs) => Created?.Invoke(sender, fileSystemEventArgs);
        protected virtual void OnDeleted(object sender, FileSystemEventArgs fileSystemEventArgs) => Deleted?.Invoke(sender, fileSystemEventArgs);
        protected virtual void OnError(object sender, ErrorEventArgs fileSystemErrorArgs) => Error?.Invoke(sender, fileSystemErrorArgs);
        protected virtual void OnRenamed(object sender, RenamedEventArgs fileSystemEventArgs) => Renamed?.Invoke(sender, fileSystemEventArgs);

        #endregion

        #region Override methods

        public virtual WaitForChangedResult WaitForChanged(WatcherChangeTypes changeType)
        {
            return InternalWatcher.WaitForChanged(changeType);
        }

        public virtual WaitForChangedResult WaitForChanged(WatcherChangeTypes changeType, int timeout)
        {
            return InternalWatcher.WaitForChanged(changeType, timeout);
        }

        #endregion

        #region IDisposable Methods

        ~FileSystemWatcherWrapper()
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
            UnsubscribeFromInternalWatcherEvents();

            if (disposing)
            {
                InternalWatcher.Dispose();
            }
        }

        #endregion

        #region ICloneable Methods

        public abstract object Clone();

        #endregion

    }
}
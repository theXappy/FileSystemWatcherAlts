using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FileSystemWatcherAlts.Polling;
using FileSystemWatcherAlts.Utils;
using FileSystemWatcherAlts.Utils.Extentions;
using FileSystemWatcherAlts.Wrappers;

namespace FileSystemWatcherAlts
{
    [DebuggerDisplay("Path = {Path}, Filter = {Filter}, EnableRaisingEvents = {_enableRaisingEvents}")]
    /// <summary>
    /// A FilleSystemWatcher wrapper which detects common FileSystemWatcher issues and resolves them.
    /// also performs periodic polling to increase reliability.
    /// </summary>
    public class FileSystemOverseer : FileSystemAutoRefreshingWatcher
    {
        #region Fields

        private readonly IFileSystemPoller _poller;

        private bool _enableRaisingEvents;

        private readonly object _reportedFilesLock;
        private readonly HashSet<string> _reportedItems;

        #endregion

        #region Properties

        /// <summary>
        /// Defines a delay (in milliseconds) between processing poller reports.
        /// The main reason to delay such reports is to allow the more descriptive reports of the watcher to be processed.
        /// </summary>
        [Browsable(false)]
        public int PollerReportsDelay { get; set; } = 100;
        
        public override bool EnableRaisingEvents
        {
            get
            {
                return _enableRaisingEvents;
            }
            set
            {
                _enableRaisingEvents = value;
                InternalWatcher.EnableRaisingEvents = value;
                _poller.EnableRaisingEvents = value;
            }
        }
        public override string Filter
        {
            get
            {
                return InternalWatcher.Filter;
            }
            set
            {
                InternalWatcher.Filter = value;
                _poller.Filter = value;
            }
        }
        public override bool IncludeSubdirectories
        {
            get
            {
                return InternalWatcher.IncludeSubdirectories;
            }
            set
            {
                bool lastEREvalue = _enableRaisingEvents;
                _enableRaisingEvents = false;

                InternalWatcher.IncludeSubdirectories = value;
                _poller.IncludeSubdirectories = value;
                _poller.ForcePoll();

                _enableRaisingEvents = lastEREvalue;

            }
        }
        public override int InternalBufferSize
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
        public override NotifyFilters NotifyFilter
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
        public override string Path
        {
            get
            {
                return InternalWatcher.Path;
            }
            set
            {
                bool lastEREvalue = _enableRaisingEvents;
                _enableRaisingEvents = false;

                InternalWatcher.Path = value;
                _poller.Path = value;
                
                _enableRaisingEvents = lastEREvalue;
            }
        }

        #endregion

        #region Constructors

        public FileSystemOverseer(IFileSystemPoller poller, IFileSystemWatcher watcher) : base(watcher)
        {
            _reportedItems = new HashSet<string>();
            _reportedFilesLock = new object();

            InitPollerErrorPolicies();
            
            // Initiating poller
            _poller = poller;
            if(_poller.Path != watcher.Path)
            {
                _poller.Path = watcher.Path;
            }

            EnableRaisingEvents = false;

            _poller.Created += OnCreatedPolled;
            _poller.Deleted += OnDeletedPolled;
            _poller.Error += OnPollerError;

            // Getting initial directory content by forcing a poll
            _poller.PollingType = PollingType.Poll;
            _poller.ForcePoll();

            // For the rest of the Overseer's lifespan, keep the poller as a 'watcher'
            _poller.PollingType = PollingType.Watch;
        }

        public FileSystemOverseer(IFileSystemPoller poller, FileSystemWatcher watcher) : this(poller, new FileSystemWatcherAdapter(watcher))
        {
        }

        public FileSystemOverseer(IFileSystemPoller poller) : this(poller, new FileSystemWatcherAdapter(poller.Path, poller.Filter))
        {
        }

        public FileSystemOverseer(int pollingInterval) : this(new FileSystemPoller(pollingInterval), new FileSystemWatcher())
        {
        }

        public FileSystemOverseer(int pollingInterval, string path) : this(new FileSystemPoller(pollingInterval, path), new FileSystemWatcher(path))
        {
        }

        public FileSystemOverseer(int pollingInterval, string path, string filter) : this(new FileSystemPoller(pollingInterval, path, filter), new FileSystemWatcher(path, filter))
        {
        }

        private void InitPollerErrorPolicies()
        {
            var dirNotFoundPolicy = new WatcherErrorHandlingPolicy(typeof(DirectoryNotFoundException),
                "When the poller indicates a 'directory not found' exception check if it's the main watched directory or sub-dir." +
                "If it's the main directory - refresh the watcher.",
                exception => (exception as DirectoryNotFoundException)?.Path() == Path
                    ? WatcherErrorHandlingType.Refresh | WatcherErrorHandlingType.Swallow
                    : WatcherErrorHandlingType.Forward);

            var unAuthPolicy = new WatcherErrorHandlingPolicy(typeof(UnauthorizedAccessException),
                "When the poller indicates an 'unauthorized access' exception check if it's access was denied to the main watched directory or file/sub-dir." +
                "If it's the main directory - refresh the watcher.",
                exception => (exception as UnauthorizedAccessException)?.Path() == Path
                    ? WatcherErrorHandlingType.Refresh | WatcherErrorHandlingType.Swallow
                    : WatcherErrorHandlingType.Forward);

            AddPolicy(dirNotFoundPolicy);
            AddPolicy(unAuthPolicy);
        }

        #endregion

        #region Event Handling Methods

        // Event handlers for the wrapped watcher and the poller (a delay)
        protected override void OnCreated(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            lock (_reportedFilesLock)
            {
                // If the files was already reported - return 
                if (_reportedItems.Contains(fileSystemEventArgs.FullPath))
                {
                    return;
                }

                // Other wise:
                // 1. Add to reported files set
                _reportedItems.Add(fileSystemEventArgs.FullPath);
            }

            // 2. report to subscribers
            if (!_enableRaisingEvents) return;
            base.OnCreated(sender, fileSystemEventArgs);
        }

        protected override void OnDeleted(object sender, FileSystemEventArgs fileSystemEventArgs)
        {

            lock (_reportedFilesLock)
            {
                // If the files was already reported - return 
                if (!_reportedItems.Contains(fileSystemEventArgs.FullPath))
                {
                    return;
                }


                // Other wise:
                // 1. Try to remove said file. If the removal fails - return
                if (!_reportedItems.Remove(fileSystemEventArgs.FullPath)) return;
            }
            
            // 2. report to subscribers
            if (!_enableRaisingEvents) return;
            base.OnDeleted(sender, fileSystemEventArgs);
        }

        protected override void OnChanged(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            if (!_enableRaisingEvents) return;
            base.OnChanged(sender,fileSystemEventArgs);
        }

        protected override void OnRenamed(object sender, RenamedEventArgs fileSystemEventArgs)
        {
            lock (_reportedFilesLock)
            {
                // If a file with the new name was already reported - return 
                if (_reportedItems.Contains(fileSystemEventArgs.FullPath))
                {
                    return;
                }

                // 1. If the file's old name existed in the storage - remove it
                if (_reportedItems.Contains(fileSystemEventArgs.OldFullPath))
                {
                    _reportedItems.Remove(fileSystemEventArgs.OldFullPath);
                }

                // 2. Add new path to the reportedFiles list
                _reportedItems.Add(fileSystemEventArgs.FullPath);
            }
            
            // 3. report to subscribers
            if (!_enableRaisingEvents) return;
            base.OnRenamed(sender, fileSystemEventArgs);
        }

        protected override void OnError(object sender, ErrorEventArgs e)
        {
            var ex = e.GetException();
            if (ex is InternalBufferOverflowException)
            {
                _poller.ForcePoll();
            }
            
            base.OnError(sender, e);
        }

        // Events raised by the poller will invoke these methods first:
        private void OnCreatedPolled(object sender, FileSystemEventArgs fileSystemEventArgs)
        {
            Task.Delay(PollerReportsDelay).ContinueWith(task => OnCreated(sender, fileSystemEventArgs));
        }

        private void OnDeletedPolled(object sender, FileSystemEventArgs fileSystemEventArgs)
        {

            Task.Delay(PollerReportsDelay).ContinueWith(task => OnDeleted(sender, fileSystemEventArgs));
        }

        private void OnPollerError(object sender, ErrorEventArgs e)
        {
            base.OnError(sender, e);
        }

        #endregion

        #region IDisposeable Methods

        ~FileSystemOverseer()
        {
            Dispose(false);
        }

        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            _poller.Created -= OnCreatedPolled;
            _poller.Deleted -= OnDeletedPolled;
            _poller.Error -= OnPollerError;
            _poller.Dispose();

            if (disposing)
            {
                base.Dispose();
            }
        }

        #endregion

        #region ICloneable Methods

        public override object Clone()
        {
            var clonedPoller = (IFileSystemPoller) _poller.Clone();

            var clonedEncapsWatcher = (IFileSystemWatcher) InternalWatcher.Clone();

            var clonedOverseer = new FileSystemOverseer(clonedPoller, clonedEncapsWatcher)
            { PollerReportsDelay = this.PollerReportsDelay };

            clonedOverseer.ClearPolicies();
            foreach (var policy in ErrorHandlingPolicies)
            {
                clonedOverseer.AddPolicy(policy);
            }

            return clonedOverseer;
        }

        #endregion
    }
}
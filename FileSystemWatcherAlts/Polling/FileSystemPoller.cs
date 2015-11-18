using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FileSystemWatcherAlts.Utils.Extentions;
using PathClass = System.IO.Path;

namespace FileSystemWatcherAlts.Polling
{
    [DebuggerDisplay("Path = {_path}, Filter = {_filter}, Polling = {_pollingTask!=null}, EnableRaisingEvents = {_enableRaisingEvents}")]
    /// <summary>
    /// Polls the file system and raises events when a directory, or file in a directory, changes.
    /// </summary>
    public class FileSystemPoller : IFileSystemWatcher, IFileSystemPoller
    {
        #region Events

        public event FileSystemEventHandler Created;
        public event FileSystemEventHandler Deleted;
        public event ErrorEventHandler Error;

        event FileSystemEventHandler IFileSystemWatcher.Changed
        {
            add
            {
                if(_supressNotSuppotedErrors) return;

                throw new NotImplementedException("Changed events are not supported by FileSystemPoller. If you are trying to wrap the poller use poller.SupressNotSupportedErrors = true to stop this exception from being thrown.");
            }
            remove
            {
            }
        }
        event RenamedEventHandler IFileSystemWatcher.Renamed
        {
            add
            {
                if (_supressNotSuppotedErrors) return;

                throw new NotImplementedException("Renamed events are not supported by FileSystemPoller.  If you are trying to wrap the poller use poller.SupressNotSupportedErrors = true to stop this exception from being thrown.");
            }
            remove
            {
            }
        }

        #endregion

        #region Fields

        // classic watcher properties' backing fields
        /// <summary>
        /// Indicates whether the poller should raise it's events when a new notification is found
        /// </summary>
        private bool _enableRaisingEvents;
        /// <summary>
        /// Indicates what path of the directory the poller should poll from
        /// </summary>
        private string _path;
        /// <summary>
        /// An uppercase version of _path. used for string comparisons (prevents multiple ToUpper calls)
        /// </summary>
        private string _uppercasePath; 
        /// <summary>
        /// Indicates whether the poller should supress it's NotSupported exceptions when subscribing to Changed/Renamed events.
        /// </summary>
        private bool _supressNotSuppotedErrors = false;
        /// <summary>
        /// Indicates whether the poller should poll subdirectories or not.
        /// </summary>
        private bool _includeSubdirectories;


        // basic file watching fields

        /// <summary>
        /// A FileSystemWatcher-like filter for the poller to use
        /// </summary>
        private string _filter;
        /// <summary>
        /// A regex expression created according to the _filter and used to check polled files.
        /// </summary>
        private Regex _regexFilter;
        /// <summary>
        /// Used by the polling thread to signal it has finished the initial polling.
        /// </summary>
        private readonly ManualResetEvent _initialFilesSeen;
        /// <summary>
        /// Collection of files seen in the last poll
        /// </summary>
        private IEnumerable<string> _lastSeenFiles;
        /// <summary>
        /// Collection of directories seen in the last poll
        /// </summary>
        private IEnumerable<string> _lastSeenDirs;
        

        // Polling related fields

        /// <summary>
        /// The task responsible for polling.
        /// </summary>
        private Task _pollingTask;
        /// <summary>
        /// Makes sure only a single thread starts/stops the polling task
        /// </summary>
        private readonly object _pollingTaskLock;
        /// <summary>
        /// Used by the polling thread to wait the timeout between polls. If set the thread stops waiting and continues.
        /// </summary>
        private readonly AutoResetEvent _pollingTimeoutEvent;
        /// <summary>
        /// Used to signal to the polling thread that it should stop execution
        /// </summary>
        private readonly ManualResetEventSlim _pollingEnabledEvent;
        /// <summary>
        /// Used by the polling thread to signal a poll was done sucessfully. The event is set afte EVERY poll.
        /// </summary>
        private readonly ManualResetEventSlim _pollDone;
        
        
        // WaitForChange fields

        /// <summary>
        /// Contains the number of threads waiting for notifications using the WaitForChanged methods
        /// </summary>
        private int _waiters;
        /// <summary>
        /// Used by the polling thread to signal to the waiters that a new notification is available.
        /// </summary>
        private readonly AutoResetEvent _changesWatchingEvent;
        /// <summary>
        /// Latest notification available for the WaitForChanged waiters
        /// </summary>
        private WaitForChangedResult _latestChange;
        /// <summary>
        /// Used to assert only a single thread (waiter/poller) access the _latestChange field at a time.
        /// </summary>
        private readonly object _latestChangeLocker;

        #endregion

        #region Properties

        /// <summary>
        /// Defines whether the poller acts as a 'Watcher' or as a clasic 'Poller'.
        /// </summary>
        public PollingType PollingType { get; set; }

        /// <summary>
        /// Path of the directory to monitor
        /// </summary>
        public string Path
        {
            get { return _path; }
            set
            {
                if (value == null) throw new NullReferenceException(nameof(Path));
                if (value.Length == 0) throw new ArgumentException("Path cannot be an empty string.", nameof(Path));

                StopPollingTask();
                
                // Add the directory seperator character to the value if it's missing
                if (value[value.Length-1] != PathClass.DirectorySeparatorChar)
                {
                    value = value + PathClass.DirectorySeparatorChar;
                }

                _path = value;
                _uppercasePath = _path.ToUpperInvariant();

                StartPollingTask();
            }
        }
        /// <summary>
        /// Whether the poller should raise Created/Deleted/Error events
        /// </summary>
        public bool EnableRaisingEvents
        {
            get { return _enableRaisingEvents; }
            set
            {
                if (String.IsNullOrEmpty(_path))
                {
                    throw new InvalidOperationException("No directory path was provided to the poller. Can not poll.");
                }
                if (!Directory.Exists(_path))
                {
                    throw new InvalidOperationException("Directory path to poll does not exist. Path: "+_path);
                }

                if (value) // settings raising to true
                {
                    _initialFilesSeen.WaitOne(); // waiting for intialization to end
                }
                _enableRaisingEvents = value;
            }
        }

        /// <summary>
        /// A file name filter to monitor the directory with. Files/Directories which does not pass the filter won't be reported.
        /// </summary>
        public string Filter
        {
            get { return _filter; }
            set
            {
                _filter = value;

                if (value == string.Empty)
                {
                    _regexFilter = new Regex(".*");
                }
                else
                {
                    // Turning a filesytsemwatcher filter into a regex filter
                    // abc?.txt -> abc.\.txt
                    // def*.bin    -> def.*\.bin
                    // *.txt        -> .*\.txt
                    // *.*          -> .*\..*
                    _regexFilter = new Regex(Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*"));
                }

            }
        }

        /// <summary>
        /// Whether the poller should also poll in subdirectories of the directory at Path
        /// </summary>
        public bool IncludeSubdirectories
        {
            get { return _includeSubdirectories; }
            set
            {
                StopPollingTask();

                _includeSubdirectories = value;

                StartPollingTask();
            }
        }

        int IFileSystemWatcher.InternalBufferSize { get; set; }
        NotifyFilters IFileSystemWatcher.NotifyFilter { get; set; }

        /// <summary>
        /// The interval to poll at.
        /// </summary>
        public int PollingInterval { get; set; }

        /// <summary>
        /// Prevents the "NotImplementedException" from being thrown when subscribing to the Renamed/Changed events.
        /// Set this to true when trying to wrap the Poller. The events will still not invoke but will allow subscription.
        /// </summary>
        [Browsable(false)]
        public bool SupressNotSupportedErrors
        {
            get { return _supressNotSuppotedErrors; }
            set { _supressNotSuppotedErrors=value; }
        }

        /// <summary>
        /// Whether any threads are currently waiting in one of the WaitForChanged overloads
        /// </summary>
        private bool ReportsExpected => Volatile.Read(ref _waiters) != 0 || EnableRaisingEvents;

        #endregion

        #region Constructors

        public FileSystemPoller(int pollingInterval)
        {
            _path = String.Empty;
            
            _initialFilesSeen = new ManualResetEvent(false);
            _lastSeenFiles = new HashSet<string>();
            _lastSeenDirs = new HashSet<string>();

            Filter = string.Empty;

            _waiters = 0;
            _changesWatchingEvent = new AutoResetEvent(false);
            _latestChangeLocker = new object();
            _latestChange = new WaitForChangedResult();

            _pollingTaskLock = new object();
            PollingInterval = pollingInterval;
            _pollDone = new ManualResetEventSlim(false);
            _pollingTimeoutEvent = new AutoResetEvent(false);
            _pollingEnabledEvent = new ManualResetEventSlim(true);

            PollingType = PollingType.Watch;

        }

        public FileSystemPoller(int pollingInterval, string path) : this(pollingInterval)
        {
            Path = path;
            StartPollingTask();
        }

        public FileSystemPoller(int pollingInterval, string path, string filter) : this(pollingInterval, path)
        {
            Filter = filter;
            StartPollingTask();
        }

        private void StopPollingTask()
        {
            // Check if a polling task even exist
            if (_pollingTask == null) return;

            lock (_pollingTaskLock)
            {
                if (_pollingTask == null) return;

                _initialFilesSeen.Set();
                _pollingEnabledEvent.Reset(); // Signaling for the task to quit
                _pollingTimeoutEvent.Set(); // Trying to speed up the task exit by interupting the 'sleep' period
                if (_pollingTask.Status == TaskStatus.Running)
                {
                    _pollingTask.Wait();
                }
                _pollingTask = null;
            }
        }

        private void StartPollingTask()
        {
            // Check if a no other polling task exists
            if (_pollingTask != null) return;

            lock (_pollingTaskLock)
            {
                if (_pollingTask != null) return;

                _initialFilesSeen.Reset();
                _pollingTimeoutEvent.Reset();
                _pollingEnabledEvent.Set();
                _lastSeenFiles = new Collection<string>();
                _lastSeenDirs = new Collection<string>();
                _pollingTask = Task.Factory.StartNew(Poll, TaskCreationOptions.LongRunning);
            }
        }

        #endregion

        #region Polling Methods

        /// <summary>
        /// Polls the files currently in the directory
        /// </summary>
        private void PollInitialDirContent()
        {
            // Get initial content
            while (!_initialFilesSeen.WaitOne(1))
            {
                // Check if polling was disabled
                if (!_pollingEnabledEvent.Wait(1)) return;

                // Query files in folder
                IEnumerable<string> currentFiles;
                IEnumerable<string> currentFolders;
                if (PollCurrentFiles(out currentFiles) && PollCurrentSubDirs(out currentFolders))
                {
                    _lastSeenFiles = currentFiles;
                    _lastSeenDirs = currentFolders;
                    _initialFilesSeen.Set();
                    return;
                }
                else
                {

                }

                // Check if polling was disabled
                if (!_pollingEnabledEvent.Wait(1)) return;

                // Sleep
                _pollingTimeoutEvent.WaitOne(PollingInterval);
            }
        }

        /// <summary>
        /// Constantly polls the files in the path given. 
        /// </summary>
        private void Poll()
        {

            // Firstly, get an idea of what the folder currently looks like.
            PollInitialDirContent();


            while (true)
            {
                // Check if polling was disabled
                if (!_pollingEnabledEvent.Wait(1)) break;

                // Sleep
                _pollingTimeoutEvent.WaitOne(PollingInterval);

                // Check if polling was disabled while waiting the timeout (which might be long)
                if (!_pollingEnabledEvent.Wait(1)) break;

                // Poll both files and directories in watched folder
                IEnumerable<string> currentFiles;
                IEnumerable<string> currentFolders;
                if (!PollCurrentFiles(out currentFiles) || !PollCurrentSubDirs(out currentFolders))
                {

                    // Polling files or folders failed, continuing to next sleep
                    continue;
                }

                ProcessPolledItems(currentFiles, currentFolders);

                // Inform any 'ForcePoll' threads that the poll finished
                _pollDone.Set();
            }
            
        }

        /// <summary>
        /// Proccess collections of files and folders currently polled and runs checks on them according to the polling type
        /// </summary>
        /// <param name="currentFiles">Files that currently exist under the polled folder</param>
        /// <param name="currentFolders">Folders that currently exist under the polled folder</param>
        private void ProcessPolledItems(IEnumerable<string> currentFiles, IEnumerable<string> currentFolders)
        {
            // Orginazing possible check to run each poll
            List<Action> actionsOnItems;
            if (PollingType == PollingType.Watch)
            {
                actionsOnItems = new List<Action>()
                    {
                        () => ReportCreatedItems(_lastSeenFiles, currentFiles), // Check for new files
                        () => ReportCreatedItems(_lastSeenDirs, currentFolders), // Check for new folders
                        () => ReportDeletedItems(_lastSeenFiles, currentFiles), // Check for deleted files
                        () => ReportDeletedItems(_lastSeenDirs, currentFolders) // Check for deleted folders
                    };
            }
            else // PollingType == PollingType.Poll
            {
                actionsOnItems = new List<Action>()
                    {
                        () => ReportItems(currentFiles,WatcherChangeTypes.Created), // Report current files that match the filter
                        () => ReportItems(currentFolders,WatcherChangeTypes.Created), // Report current directories that match the filter
                    };
            }

            // For each one of the checks above, see if there is a point even running this check (EnableRaisingEvents is true or threads are WaitingForChange-s).
            foreach (Action itemsCheck in actionsOnItems)
            {
                if (ReportsExpected)
                {
                    itemsCheck();
                }
            }

            // Update "last seen files" and "last seen folders"
            _lastSeenFiles = currentFiles;
            _lastSeenDirs = currentFolders;
        }

        /// <summary>
        /// Forces the Poller to poll for files immediatly.
        /// </summary>
        public Task ForcePollAsync(bool returnWhenPolled = false)
        {
            return Task.Factory.StartNew(()=> ForcePoll(returnWhenPolled));
        }

        /// <summary>
        /// Forces the Poller to poll for files immediatly.
        /// </summary>
        public void ForcePoll()
        {
            ForcePoll(returnWhenPolled: true);
        }

        /// <summary>
        /// Forces the Poller to poll for files immediatly.
        /// </summary>
        public void ForcePoll(bool returnWhenPolled)
        {
            _pollingTimeoutEvent.Set();
            if (returnWhenPolled)
            {
                _pollDone.Reset();
                _pollDone.Wait();
            }
        }

        /// <summary>
        /// Polls a collection of file names in the watched folder
        /// </summary>
        /// <param name="currentFiles">Output variable for the files' names</param>
        /// <returns>True if polling succeeded, false otherwise</returns>
        private bool PollCurrentFiles(out IEnumerable<string> currentFiles)
        {

            currentFiles = null;
            try
            {
                currentFiles = DirectoryExtentions.GetFilesInA<Collection<string>>(Path, IncludeSubdirectories);

                return true;
            }
            catch (Exception ex)
            {

                OnError(new ErrorEventArgs(ex));
                return false;
            }
        }

        /// <summary>
        /// Polls a collection of directories names in the watched folder
        /// </summary>
        /// <param name="currentFolders">Output variable for the directories' names</param>
        /// <returns>True if polling succeeded, false otherwise</returns>
        private bool PollCurrentSubDirs(out IEnumerable<string> currentFolders)
        {

            currentFolders = null;
            try
            {
                currentFolders = DirectoryExtentions.GetDirsInA<Collection<string>>(Path, IncludeSubdirectories);

                return true;
            }
            catch (Exception ex)
            {

                OnError(new ErrorEventArgs(ex));
                return false;
            }
        }

        #endregion

        #region Files Examination Methods

        /// <summary>
        /// Compares an old and a new collection of files to see if any old items were removed. Pops the "Deleted" event for each of those items.
        /// </summary>
        /// <param name="originalItems">Set of old items</param>
        /// <param name="currentItems">Set of new items</param>
        private void ReportDeletedItems(IEnumerable<string> originalItems, IEnumerable<string> currentItems)
        {

            // Copy last known items to a new set
            ISet<string> deletedFiles = new HashSet<string>(originalItems);
            // Substract current items
            deletedFiles.ExceptWith(currentItems);

            // Runs the items through the filter and reports matching ones
            ReportItems(deletedFiles, WatcherChangeTypes.Deleted);
        }

        /// <summary>
        /// Compares an old and a new collection of files to see if any new items were added. Pops the "Created" event for each of those items.
        /// </summary>
        /// <param name="originalItems">Set of old items</param>
        /// <param name="currentItems">Set of new items</param>
        private void ReportCreatedItems(IEnumerable<string> originalItems, IEnumerable<string> currentItems)
        {

            // Copy current found items to a new set
            ISet<string> addedItems = new HashSet<string>(currentItems);
            // Substract last seen items
            addedItems.ExceptWith(originalItems);

            // Runs the items through the filter and reports matching ones
            ReportItems(addedItems, WatcherChangeTypes.Created);
        }

        /// <summary>
        /// Checks an enumerable of items with the current filter and reports those who fit.
        /// </summary>
        /// <param name="items">The collection of items (files/folders) to check</param>
        /// <param name="reportType">The type of report to create for those items</param>
        private void ReportItems(IEnumerable<string> items, WatcherChangeTypes reportType)
        {
            foreach (var item in items)
            {
                string itemName = PathClass.GetFileName(item);
                if (!PassesFilter(itemName)) continue;
                string folder = PathClass.GetDirectoryName(item) ?? string.Empty;

                SignalFileChangeForWaiters(reportType, item);

                if (EnableRaisingEvents)
                {
                    FileSystemEventArgs reportArgs = new FileSystemEventArgs(reportType, folder, itemName);
                    switch (reportType)
                    {
                        case WatcherChangeTypes.Created:
                            OnCreated(reportArgs);
                            break;
                        case WatcherChangeTypes.Deleted:
                            OnDeleted(reportArgs);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a single file name/folder name matches the currently set filter
        /// </summary>
        /// <param name="item">File/Folder name</param>
        /// <returns>True if the name matches the filter, false otherwise.</returns>
        private bool PassesFilter(string item)
        {
            // returns whether *the string is not empty* && *the string matches filter*
            return !String.IsNullOrEmpty(item) && _regexFilter.IsMatch(item);
        }

        #endregion

        #region Events Raising Methods

        private void OnCreated(FileSystemEventArgs fileSystemEventArgs)
        {

            Created?.Invoke(this, fileSystemEventArgs);
        }
        private void OnDeleted(FileSystemEventArgs fileSystemEventArgs)
        {

            Deleted?.Invoke(this, fileSystemEventArgs);
        }
        private void OnError(ErrorEventArgs errorEventArgs)
        {

            Error?.Invoke(this, errorEventArgs);
        }

        #endregion

        #region WaitForChanged Methods

        private void SignalFileChangeForWaiters(WatcherChangeTypes type, string filePath)
        {
            if (_waiters == 0) return; // No point signaling if no one is waiting

            // Getting the 'relative path' of the filePath compared to the currently monitored folder path
            string uppercaseFilePath = filePath.ToUpperInvariant();
            var fileNameToReport = uppercaseFilePath.Replace(_uppercasePath, string.Empty);

            lock (_latestChangeLocker)
            {
                _latestChange = new WaitForChangedResult() { ChangeType = type, Name = fileNameToReport };
            }
            _changesWatchingEvent.Set();
        }

        public WaitForChangedResult WaitForChanged(WatcherChangeTypes type)
        {
            if (type == WatcherChangeTypes.Renamed ||
                type == WatcherChangeTypes.Changed ||
                type == (WatcherChangeTypes.Changed | WatcherChangeTypes.Renamed)) // Polling cannot monitor these changes
            {
                throw new NotImplementedException("File System Poller can not monitor \"Rename\" or \"Changed\" file changes.");
            }

            while (true)
            {
                Interlocked.Increment(ref _waiters);
                _changesWatchingEvent.WaitOne();
                Interlocked.Decrement(ref _waiters);

                WaitForChangedResult results;
                lock (_latestChangeLocker)
                {
                    results = _latestChange;
                }
                // Check if the report fits the one the current thread is looking for
                if (type.HasFlag(results.ChangeType))
                {
                    // It does, returning the report.
                    return results;
                }
                else
                {
                    // It doesn't.
                    // allowing a signle other thread to examine it this report:
                    _changesWatchingEvent.Set();
                    // making sure the event is reset when the current thread returns to it. (If a thread is waiting it will exit after the .Set and before the .Reset)
                    _changesWatchingEvent.Reset();
                }
            }
        }

        public WaitForChangedResult WaitForChanged(WatcherChangeTypes type, int timeout)
        {
            if (type == WatcherChangeTypes.Renamed ||
                type == WatcherChangeTypes.Changed ||
                type == (WatcherChangeTypes.Changed | WatcherChangeTypes.Renamed)) // Polling cannot monitor these changes
            {
                throw new NotImplementedException("File System Poller can not monitor \"Rename\" or \"Changed\" item changes.");
            }
            
            // Using this stopwatch to check I'm staying in the method longer then the timeout set
            Stopwatch timeInMethodStopwatch = Stopwatch.StartNew();

            Interlocked.Increment(ref _waiters);
            while (true)
            {
                int remainingTimeToWait = timeout - (int)timeInMethodStopwatch.ElapsedMilliseconds;
                var timedOut = !_changesWatchingEvent.WaitOne(remainingTimeToWait);

                if (timedOut) // wait timed out, exit method.
                {
                    Interlocked.Decrement(ref _waiters);
                    return new WaitForChangedResult() { ChangeType = type, TimedOut = true };
                }

                // wait didn't time out - check results
                WaitForChangedResult results;
                lock (_latestChangeLocker)
                {
                    results = _latestChange;
                }
                // Check if the reported results match the requestsed result type.
                // Otherwise - continue waiting for more changes
                if (type.HasFlag(results.ChangeType))
                {
                    Interlocked.Decrement(ref _waiters);
                    return results;
                }
            }
        }

        #endregion

        #region IDisposeable Methods

        public void Dispose()
        {
            // Canceling polling task
            StopPollingTask();

            // Empty files/folders collections - those might get quite large
            _lastSeenFiles = new Collection<string>();
            _lastSeenDirs = new Collection<string>();
        }

        #endregion

        #region ICloneable Methods

        public object Clone()
        {
            var clonedPoller = new FileSystemPoller(this.PollingInterval, this.Path, this.Filter)
            {
                IncludeSubdirectories = this.IncludeSubdirectories,
                EnableRaisingEvents = this.EnableRaisingEvents,
                PollingType = this.PollingType
            };

            return clonedPoller;
        }

        #endregion
    }
}
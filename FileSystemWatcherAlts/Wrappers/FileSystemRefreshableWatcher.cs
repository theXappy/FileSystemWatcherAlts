using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileSystemWatcherAlts.Utils;
using Polly;

namespace FileSystemWatcherAlts.Wrappers
{
    /// <summary>
    /// An IFileSystemWatcher wrapper which allows refreshing the watcher for when it ceases to work due to a problem.
    /// </summary>
    public class FileSystemRefreshableWatcher : FileSystemWatcherWrapper
    {
        #region Fields

        /// <summary>
        /// A collection of ManualResetEvents of the threads currently waiting for a refresh to finish
        /// </summary>
        private readonly ConcurrentDictionary<Thread,ManualResetEventSlim> _waitingThreadsEvents; 
        /// <summary>
        /// Used to synchronize between different threads that try to refresh the watcher at the same time 
        /// Only the one who successfully entered this object is allowed to refresh.
        /// </summary>
        private readonly object _refreshLock;

        private readonly CancellationTokenSource _refreshTokenSource;

        #endregion

        #region Properties

        /// <summary>
        /// The amount of time in milliseconds to wait between refresh attemps on the watcher.
        /// </summary>
        [Browsable(false)]
        public int RefreshAttempInterval { get; set; } = 500;

        /// <summary>
        /// Wether the watcher is currently refreshing or not.
        /// </summary>
        public bool IsRefreshing { get; private set; }

        #endregion

        #region Events

        public event EventHandler Refreshed;

        #endregion

        #region Constructors

        public FileSystemRefreshableWatcher(IFileSystemWatcher watcher) : base(watcher)
        {
            _refreshTokenSource = new CancellationTokenSource();
            _waitingThreadsEvents = new ConcurrentDictionary<Thread, ManualResetEventSlim>();
            IsRefreshing = false;
            _refreshLock = new object();
        }
        public FileSystemRefreshableWatcher(FileSystemWatcher watcher) : this(new FileSystemWatcherAdapter(watcher))
        {
        }
        public FileSystemRefreshableWatcher() : this(new FileSystemWatcher())
        {
        }
        public FileSystemRefreshableWatcher(string path) : this(new FileSystemWatcher(path))
        {
        }
        public FileSystemRefreshableWatcher(string path, string filter) : this(new FileSystemWatcher(path, filter))
        {
        }

        #endregion

        #region Methods

        /// <summary>
        /// Refreshes the internal FileSystemWatcher asynchronously
        /// </summary>
        /// <returns></returns>
        public Task RefreshAsync(bool returnWhenRefreshed = true)
        {
            return Task.Factory.StartNew(()=>Refresh(returnWhenRefreshed));
        }

        /// <summary>
        /// Refreshes the internal FileSystemWatcher
        /// </summary>
        public void Refresh()
        {
            // when using this synchronous method, the call should make sure to return only when the watcher has been refreshed.
            Refresh(returnWhenRefreshed: true);
        }

        /// <summary>
        /// Refreshes the internal FileSystemWatcher
        /// </summary>
        /// <param name="returnWhenRefreshed">In case another thread is alreayd refreshing, determines wether the thread should return before the refreshing thread finishes or not.</param>
        private void Refresh(bool returnWhenRefreshed)
        {
            // Making sure another thread isn't already refreshing:
            if (!Monitor.TryEnter(_refreshLock))
            {
                // if another thread IS already refreshing - wait for it to finish then return
                if (returnWhenRefreshed)
                {
                    WaitForRefresh();
                }
                return;
            }
            IsRefreshing = true;

            // 1. unsubscribe from old watcher's events.
            UnsubscribeFromInternalWatcherEvents();

            // 2a. Keeping the current internal "EnableRaisingEvents" value
            bool currentEnableRaisingEvents = InternalWatcher.EnableRaisingEvents;
            // 2b. Turning off EnableRaisingEvents to avoid "locking" the watched folder
            InternalWatcher.EnableRaisingEvents = false;

            // 3. Get a new watcher
            IFileSystemWatcher newInternalWatcher = GetReplacementWatcher();
            newInternalWatcher.EnableRaisingEvents = currentEnableRaisingEvents;

            // 4. Disposing of the old watcher
            InternalWatcher.Dispose();

            // 5. Place new watcher in the Internal watcher property
            //    This also registers to the watcher's events
            InternalWatcher = newInternalWatcher;

            // Change state back to "not refreshing"
            IsRefreshing = false;
            // Notify any waiting threads that the refresh is done
            foreach (var waitingThreadEvent in _waitingThreadsEvents.Values)
            {
                waitingThreadEvent.Set();
            }
            _waitingThreadsEvents.Clear();
            Monitor.Exit(_refreshLock);

            // Notify listeners about the refresh.
            Refreshed?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// Gets a replacement for the InternalWatcher
        /// </summary>
        /// <returns>A new IFileSystemWatcher of the same type as the InternalWatcher</returns>
        private IFileSystemWatcher GetReplacementWatcher()
        {
            IFileSystemWatcher newInternalWatcher = null;
            // Swallowing any exceptions that might occure when trying to get a clone of the current watcher
            CancellationToken cToken = _refreshTokenSource.Token;
            Policy.Handle<Exception>()
                  .RetryForever((ex, con) => Thread.Sleep(RefreshAttempInterval))
                  .Execute(() =>
                  {
                      // If the refreshment is cancelled, place a fake as the new watcher and return.
                      if (cToken.IsCancellationRequested)
                      {
                          newInternalWatcher = new FileSystemFakeWatcher();
                          return; //Exits polly's 'Execute' method.
                      }

                      newInternalWatcher = (IFileSystemWatcher) InternalWatcher.Clone();
                      // setting EnableRaisingEvents to true is where exceptions may raise so 
                      // I'm giving this clone a "test drive" before returning it to the Refresh method
                      newInternalWatcher.EnableRaisingEvents = true;
                      newInternalWatcher.EnableRaisingEvents = false;
                  });

            return newInternalWatcher;
        }

        /// <summary>
        /// Blocks the thread while a refresh is in progress
        /// </summary>
        public void WaitForRefresh()
        {
            // Create a reset event and adds it to the waiting threads events list
            ManualResetEventSlim refreshEvent = new ManualResetEventSlim(false);
            _waitingThreadsEvents[Thread.CurrentThread] = refreshEvent;
            refreshEvent.Wait();
        }

        /// <summary>
        /// Blocks the thread while a refresh is in progress
        /// </summary>
        /// <param name="timeout">Maximum amount of time, in ms, to wait for the refresh to finish.</param>
        /// <returns>True if the refresh finished in time, false if the wait timed out.</returns>
        public bool WaitForRefresh(int timeout)
        {
            // Create a reset event and adds it to the waiting threads events list
            ManualResetEventSlim refreshEvent = new ManualResetEventSlim(false);
            _waitingThreadsEvents[Thread.CurrentThread] = refreshEvent;

            var refreshed = refreshEvent.Wait(timeout); // waiting for the refresh
            
            if (!refreshed) // = wait timed out
            {
                // remove the event from the list
                _waitingThreadsEvents.TryRemove(Thread.CurrentThread, out refreshEvent);
            }
            return refreshed;
        }

        #endregion

        #region IDisposeable Methods

        ~FileSystemRefreshableWatcher()
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
            // If the refresher is currently refreshing, cancel the refresh and wait for the refreshing thread to exit
            if (IsRefreshing)
            {
                _refreshTokenSource.Cancel();
                WaitForRefresh();
            }

            if (disposing)
            {
                base.Dispose();
            }
        }

        #endregion

        #region ICloneable Methods

        public override object Clone()
        {
            var clonedInternalWatcher = (IFileSystemWatcher) InternalWatcher.Clone();
            return new FileSystemRefreshableWatcher(clonedInternalWatcher) { RefreshAttempInterval = this.RefreshAttempInterval };
        }

        #endregion
    }
}
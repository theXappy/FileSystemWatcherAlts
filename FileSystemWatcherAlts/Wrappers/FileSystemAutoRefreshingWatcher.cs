using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using FileSystemWatcherAlts.Utils;

namespace FileSystemWatcherAlts.Wrappers
{
    /// <summary>
    /// An IFileSystemWatcher wrapper which automaticly refreshes it when specific errors occurre.
    /// </summary>
    public class FileSystemAutoRefreshingWatcher : FileSystemRefreshableWatcher
    {
        #region Fields

        private List<WatcherErrorHandlingPolicy> _errorHandlingPolicies;

        #endregion

        #region Properties

        public IReadOnlyCollection<WatcherErrorHandlingPolicy> ErrorHandlingPolicies => _errorHandlingPolicies;

        #endregion

        #region Constructor

        public FileSystemAutoRefreshingWatcher()
        {
            InitBasicPolicies();
        }

        public FileSystemAutoRefreshingWatcher(FileSystemWatcher watcher) : base(watcher)
        {
            InitBasicPolicies();
        }

        public FileSystemAutoRefreshingWatcher(IFileSystemWatcher watcher) : base(watcher)
        {
            InitBasicPolicies();
        }

        public FileSystemAutoRefreshingWatcher(string path) : base(path)
        {
            InitBasicPolicies();
        }

        public FileSystemAutoRefreshingWatcher(string path, string filter) : base(path, filter)
        {
            InitBasicPolicies();
        }
        
        public void InitBasicPolicies()
        {
            _errorHandlingPolicies = new List<WatcherErrorHandlingPolicy>();

            var accessDeniedPolicy = new WatcherErrorHandlingPolicy(
                typeof (Win32Exception),
                "When an 'access denied' win32 exception occures, refresh the wrapped watcher.",
                exception =>
                    (exception as Win32Exception)?.NativeErrorCode == 5 ?
                    WatcherErrorHandlingType.RefreshAndSwallow :
                    WatcherErrorHandlingType.Forward);

            var netNameDeletedPolicy = new WatcherErrorHandlingPolicy(
                typeof (Win32Exception),
                "When a 'net name deleted' win32 exception occures, refresh the wrapped watcher.",
                exception =>
                    (exception as Win32Exception)?.NativeErrorCode == 64 ? 
                    WatcherErrorHandlingType.RefreshAndSwallow : 
                    WatcherErrorHandlingType.Forward);

            _errorHandlingPolicies.Add(accessDeniedPolicy);
            _errorHandlingPolicies.Add(netNameDeletedPolicy);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Removes all currently set error handling policies
        /// </summary>
        public void ClearPolicies()
        {
            _errorHandlingPolicies.Clear();
        }

        /// <summary>
        /// Tries to remove a specific error handling policy
        /// </summary>
        /// <param name="pol">The policy to remove</param>
        /// <returns>
        ///     true if policy is successfully removed; otherwise, false. This method also returns
        ///     false if policy was not found in the policies collection.
        /// </returns>
        public bool RemovePolicy(WatcherErrorHandlingPolicy pol)
        {
            return _errorHandlingPolicies.Remove(pol);
        }

        /// <summary>
        /// Adds an error handling policy
        /// </summary>
        /// <param name="pol"></param>
        public void AddPolicy(WatcherErrorHandlingPolicy pol)
        {
            _errorHandlingPolicies.Add(pol);
        }

        /// <summary>
        /// Inoked when the wrapped watcher throws an exception. The exception is tested with the existing policies
        /// and handled according to the tests results.
        /// </summary>
        /// <param name="sender">Raiser of the event</param>
        /// <param name="e">Error event args</param>
        protected override void OnError(object sender, ErrorEventArgs e)
        {
            Exception ex = e.GetException();
            Type exType = ex.GetType();
            WatcherErrorHandlingType exHandling = WatcherErrorHandlingType.Forward;
            
            // Testing all relevant policies according to the exception type
            foreach (var relevantPolicy in ErrorHandlingPolicies.Where(policy => policy.ExceptionType == exType))
            {
                exHandling |= relevantPolicy.Test(ex);
            }

            // Check the policies test results.

            //  If ANY of the policies requested a refresh - a refresh will be invoked
            if (exHandling.HasFlag(WatcherErrorHandlingType.Refresh))
            {
                // Tries to refresh. If a refresh is already in progress, the thread returns.
                var refreshTask = RefreshAsync(returnWhenRefreshed: false);
            }

            //  If NONE of the policies requested a swallow - the error will be forwarded
            //  (if any of them DID request a swallow, the error will be swallowed)
            if (!exHandling.HasFlag(WatcherErrorHandlingType.Swallow))
            {
                base.OnError(sender, e);
            }
        }

        #endregion

        #region ICloneable Methods

        public override object Clone()
        {
            IFileSystemWatcher clonedEncapsWatcher = InternalWatcher.Clone() as IFileSystemWatcher;
            FileSystemAutoRefreshingWatcher clonedAutoRefreshingWatcher = new FileSystemAutoRefreshingWatcher(clonedEncapsWatcher);
            // Add current refresher's policies to the cloned one
            clonedAutoRefreshingWatcher.ClearPolicies();
            foreach (var policy in _errorHandlingPolicies)
            {
                clonedAutoRefreshingWatcher.AddPolicy(policy);
            }
            return clonedAutoRefreshingWatcher;
        }

        #endregion
    }
}
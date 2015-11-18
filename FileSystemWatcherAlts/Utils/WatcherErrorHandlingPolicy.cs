using System;

namespace FileSystemWatcherAlts.Utils
{
    /// <summary>
    /// Defines a policy for handling an error a FileSystemWatcher might report.
    /// </summary>
    public struct WatcherErrorHandlingPolicy
    {
        #region Properties

        /// <summary>
        /// The type of exceptions this policy is testing
        /// </summary>
        public Type ExceptionType { get; set; }

        /// <summary>
        /// A description about the policy
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// A test to run for each exception of <see cref="ExceptionType"/> type to determine how the error should be handled.
        /// </summary>
        public Func<Exception, WatcherErrorHandlingType> Test { get; set; }

        #endregion
        
        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="exceptionType">The exception type to enforce the policy on</param>
        /// <param name="description">Literal descirption of the policy</param>
        /// <param name="test">A test to run for each exception of <paramref name="exceptionType"/> type to determine how the error should be handled.</param>
        public WatcherErrorHandlingPolicy(Type exceptionType, string description,
            Func<Exception, WatcherErrorHandlingType> test)
        {
            ExceptionType = exceptionType;
            Description = description;
            Test = test;
        }

        #endregion
    }
}
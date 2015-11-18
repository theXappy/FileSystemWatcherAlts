using System;
using System.IO;

namespace FileSystemWatcherAlts.Utils.Extentions
{
    internal static class ExceptionsExtentions
    {
        /// <summary>
        /// Extracts the path of the directory in the DirectoryNotFoundException
        /// </summary>
        /// <param name="ex">The exception</param>
        /// <returns>The path of the directory</returns>
        internal static string Path(this DirectoryNotFoundException ex)
        {
            return GetPathFromMessage(ex.Message);
        }

        /// <summary>
        /// Extracts the path of the directory in the UnauthorizedAccessException
        /// </summary>
        /// <param name="ex">The exception</param>
        /// <returns>The path of the directory</returns>
        internal static string Path(this UnauthorizedAccessException ex)
        {
            return GetPathFromMessage(ex.Message);
        }

        private static string GetPathFromMessage(string exMessage)
        {
            int startIndex = exMessage.IndexOf('\'') + 1;
            int endIndex = exMessage.LastIndexOf('\'');
            int length = endIndex - startIndex;

            // Here I assert that atleast 2 apostrophe exist in the message
            if (length < 0) return string.Empty;

            return exMessage.Substring(startIndex, length);
        }
    }
}
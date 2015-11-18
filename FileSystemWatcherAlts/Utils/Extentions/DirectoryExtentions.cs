using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileSystemWatcherAlts.Utils.Extentions
{
    internal static class DirectoryExtentions
    {
        /// <summary>
        /// Gets paths of the files under a directory in a given collection type.
        /// </summary>
        /// <typeparam name="T">The type of strings collection to get the paths as.</typeparam>
        /// <param name="directory">The directory to look for files.</param>
        /// <param name="includeSubDirectories">Whether to include sub-directories files or not.</param>
        /// <returns>A collection of file paths found under <paramref name="directory"/>.</returns>
        internal static T GetFilesInA<T>(string directory, bool includeSubDirectories = false) where T : ICollection<string>, new()
        {
            T output = new T();
            output.AddRange(Directory.GetFiles(directory));
            if (includeSubDirectories)
            {
                Stack<string> subDirsStack = new Stack<string>(Directory.GetDirectories(directory));
                while (subDirsStack.Any())
                {
                    // Get next sub-dir
                    string nextDir = subDirsStack.Pop();
                    // Get the dir's sub-dir and push them into the stack
                    subDirsStack.PushRange(Directory.GetDirectories(nextDir));
                    // Get the files in the subfolder and union them with the currentFiles set
                    var filesInSubfolder = Directory.GetFiles(nextDir);
                    output.AddRange(filesInSubfolder);
                }
            }
            else
            {
                var filesInSubfolder = Directory.GetFiles(directory);
                output.AddRange(filesInSubfolder);
            }
            return output;
        }

        /// <summary>
        /// Gets paths of the sub-directories under a directory in a given collection type.
        /// </summary>
        /// <typeparam name="T">The type of strings collection to get the paths as.</typeparam>
        /// <param name="directory">The directory to look for sub-directories.</param>
        /// <param name="includeSubDirectories">Whether to include sub-directories' sub-directories or not.</param>
        /// <returns>A collection of directories paths found under <paramref name="directory"/>.</returns>
        internal static T GetDirsInA<T>(string directory, bool includeSubDirectories = false) where T : ICollection<string>, new()
        {
            T output = new T();
            if (includeSubDirectories)
            {
                Stack<string> subDirsStack = new Stack<string>(Directory.GetDirectories(directory));
                while (subDirsStack.Any())
                {
                    // Get next sub-dir
                    string nextDir = subDirsStack.Pop();
                    IEnumerable<string> nextSubDirs = Directory.GetDirectories(nextDir);
                    // Get the dir's sub-dir and push them into the stack
                    subDirsStack.PushRange(nextSubDirs);
                    // Get the files in the subfolder and union them with the currentFiles set
                    output.AddRange(nextSubDirs);
                }
            }
            else
            {
                IEnumerable<string> nextSubDirs = Directory.GetDirectories(directory);
                output.AddRange(nextSubDirs);
            }
            return output;
        }
    }
}
using System;
using System.IO;
using System.Threading.Tasks;

namespace FileSystemWatcherAlts.Polling
{
    public interface IFileSystemPoller : IDisposable, ICloneable
    {
        bool EnableRaisingEvents { get; set; }
        string Filter { get; set; }
        bool IncludeSubdirectories { get; set; }
        string Path { get; set; }
        int PollingInterval { get; set; }
        PollingType PollingType { get; set; }
        
        event FileSystemEventHandler Created;
        event FileSystemEventHandler Deleted;
        event ErrorEventHandler Error;

        Task ForcePollAsync(bool returnWhenPolled);
        void ForcePoll();
    }
}
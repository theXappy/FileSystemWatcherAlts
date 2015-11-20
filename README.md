![Icon](https://github.com/Wootness/FileSystemWatcherAlts/blob/master/FileSystemWatcherAlts/Icon/noun_160432_cc.png?raw=true)


## FileSystemWatcherAlts

###### _Resolving your file watching trust issues since 2015._


### What is this?
A collection of alternatives to the System.IO.FileSystemWatcher class.

### What's wrong with System.IO.FileSystemWatcher?
As you can see with a quick <a href="https://www.google.co.il/search?q=FileSystemWatcher+problem">google search</a> - FilesystemWatcher is far from perfect.

In fact, it is problematic enough that Microsoft gave it an "Error" event so you can react when it encounters a problem.
To put it in simple words, there are 2 main issues with it:

1. It misses file changes when under heavy load, rendering it unreliable.
2. Some events causes it to completely break and stop monitoring.

### Show me what you got
The alternatives offered are:

1. **FileSystemPoller** - Periodicly polls for file system changes.
2. **FileSystemRefreshableWatcher** - A watcher wrapper. Allows the user to restart a broken watcher.
3. **FileSystemAutoRefreshingWatcher** - A watcher wrapper. *Automatically* restarts a watcher if it breaks.
4. **FileSystemOverseer** - A watcher wrapper. Automatically restarts a watcher if it breaks and uses a backup poller for increased reliability.

### Which one should I use?
The table  <a href="https://github.com/Wootness/FileSystemWatcherAlts/blob/master/AltComparison.md" target="_blank">over here</a> shows you the upsides and downsides of FileSystemWatcher and my alternatives.
Choose the one that fits your requirements.

### Usage

All the alternatives in this library implements an Interface called "IFileSystemWatcher"
It defines methods and events corresponding to the ones in System.IO.FileSystemWatcher.
If your project is already using FileSytemWatcher you change this:

```C#
FileSystemWatcher sysMonitor = new FileSystemWatcher(@"C:\");
```

To any of those:

```C#
IFileSystemWatcher sysMonitor = new FileSystemRefreshableWatcher(@"C:\");
IFileSystemWatcher sysMonitor = new FileSystemAutoRefreshingWatcher(@"C:\");
IFileSystemWatcher sysMonitor = new FileSystemPoller(pollingInterval: 500,path: @"C:\");
IFileSystemWatcher sysMonitor = new FileSystemOverseer(pollingInterval: 500, path: @"C:\");
```  

Subscribe to the Created/Deleted/Renamed/Changed events, set EnableRaisingEvents to true and you are good to go!


### Thanks
Icon: <a href="https://thenounproject.com/term/binoculars/160432/" target="_blank">Binoculars</a> designed by <a href="https://thenounproject.com/grega.cresnar/" target="_blank">Gregor Crešnar</a> from The Noun Project

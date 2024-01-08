# ⚠️ This project is not maintained.
I suggest seeking other solutions like this one:  
https://petermeinl.wordpress.com/2015/05/18/tamed-filesystemwatcher/ 

![Icon](https://github.com/Wootness/FileSystemWatcherAlts/blob/master/FileSystemWatcherAlts/Icon/noun_160432_cc.png?raw=true)
# FileSystemWatcherAlts[![Build status](https://ci.appveyor.com/api/projects/status/skjg78b5rr4xysam?svg=true)](https://ci.appveyor.com/project/Wootness/filesystemwatcheralts)

###### _Resolving your files watching trust issues since 2015._


## What Is This?
A collection of alternatives to the System.IO.FileSystemWatcher class.

## What's Wrong With System.IO.FileSystemWatcher?
As you can see with a quick <a href="https://www.google.co.il/search?q=FileSystemWatcher+problem">google search</a> - FilesystemWatcher is far from perfect.
In fact, it is problematic enough that Microsoft gave it an "Error" event so you can react when it encounters a problem.
To put it in simple words, there are 2 main issues with it:

1. It misses file changes when under heavy load, rendering it unreliable.
2. It stops working under certain circumstances.

## Show Me What You Got
The alternatives offered are:

1. `FileSystemPoller` - Periodicly polls for file system changes.
2. `FileSystemRefreshableWatcher` - A watcher wrapper. Allows the user to restart a broken watcher.
3. `FileSystemAutoRefreshingWatcher` - A watcher wrapper. *Automatically* restarts a watcher if it breaks.
4. `FileSystemOverseer` - A watcher wrapper. Automatically restarts a watcher if it breaks and uses a backup poller for increased reliability.

The table  <a href="https://github.com/Wootness/FileSystemWatcherAlts/blob/master/AltComparison.md" target="_blank">over here</a> shows you the upsides and downsides of FileSystemWatcher and my alternatives.
Choose the one that fits your requirements.

## Usage

All alternatives in this library implements an interface called **"IFileSystemWatcher"**
It defines methods and events corresponding to the ones in System.IO.FileSystemWatcher.
If your project is already utilizing FileSytemWatcher you can simply change this:

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

Then use Created/Deleted/Renamed/Changed events as you would with FileSystemWatcher.


## Thanks
Icon: <a href="https://thenounproject.com/term/binoculars/160432/" target="_blank">Binoculars</a> designed by <a href="https://thenounproject.com/grega.cresnar/" target="_blank">Gregor Crešnar</a> from The Noun Project

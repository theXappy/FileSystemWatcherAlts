 |Class|FileSystemWatcher|FileSystemRefreshableWatcher|FileSystemAutoRefreshingWatcher|FileSystemPoller|FileSystemOverseer
------------- | -------------| -------------|-------------| -------------|-------------| -------------
Supported Events|Created|TRUE|TRUE|TRUE|TRUE|TRUE
 |Deleted|TRUE|TRUE|TRUE|TRUE|TRUE
 |Changed|TRUE|TRUE|TRUE|FALSE|TRUE*
 |Renamed|TRUE|TRUE|TRUE|FALSE|TRUE*
 |Error|TRUE|TRUE|TRUE|TRUE|TRUE
Issues Recovery|Remote Host Disconnected|Breaks|User can refresh|Triggers watcher refresh|Continues when available|Triggers watcher refresh
 |Remote Folder Deleted|Breaks|User can refresh|Triggers watcher refresh|Continues when available|Triggers watcher refresh
 |Internal Buffer Overflow|Misses Files|Misses Files|Misses Files|No effect|Triggers polling
 |Local Folder Deleted|Breaks|User can refresh|User can refresh|Continues when available|Triggers watcher refresh


\* Supported while the internal buffer doesn't overflow. Falls back to Created/Deleted events instead of renames if it does.

A few rules of thumb:

* If you are not expecting heavy loads, consider using FileSystemAutoRefreshingWatcher. It provides the most reliability and lowest overhead for that case.
* If you need "Renamed" or "Changed" update events the FileSystemPoller can't help you as it doesn't monitor them.
* If you need reliability at all costs use FileSytemOverseer. It's overhead is higher than the other alternatives because it utilizes both a FileSystemAutoRefreshingWatcher and a FileSystemPoller
but the combination of both gurantees you the most immediate and precise reports in this library.
LogWatch
========

LogWatch allows you to view log files.

## Features
* Designed to handle large log files (hundreds of megabytes, it doesn't load all the data into memory)
* Live file streaming support (view files that are currently being updated by the logger)
* Provides quick jump to a next Trace/Debug/Info/Warn/Error/Fatal record functionality
* Search using simple text search or regular expressions (press Ctrol+F to activate)

When using file source LogWatch sets a monitor on the file and provides live updates on the records.

## TODO
* bookmarks
* format exception in the record view
* compute statistics automatically when reading the file (no need to click on the button)
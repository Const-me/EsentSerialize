This demo project demonstrates how to implement a file system on the top of ESENT database.

Don't be scared with many source files in the project.
Most of them implement various GUI features, windows shell integration, or related utilities.

See "FileSystem/EfsEntry.cs" for the file system entry record class,
and "FileSystem/EFS.cs" for the static class implementing some file system operations.

The storage overhead is about 10% (more while adding files because of the transaction logs).
The throughput is adequate: write rate might be better (about 7-8 megabytes / second on my old Core 2 Duo PC), the read rate is pretty good ( 60 megabytes / second on my old PC ).

To start, copy & paste some folder in the main window of the app.

Drag & drop only works from inside out, use the clipboard to move data into the ESENT file system.

This project is just a demo / proof of concept, don't expect high code quality.
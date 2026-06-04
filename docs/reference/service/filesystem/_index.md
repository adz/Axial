---
title: "Services FileSystem"
weight: 30
---

This page shows the file-system service package. `IFileSystem` models common `System.IO.File`, `Directory`, `Path`, text, byte, stream, metadata, and timestamp operations as an explicit workflow service. Keep workflow code typed against the service contract, provide `FileSystem.live` only at the edge, and replace it with a deterministic implementation in tests. File-system helpers classify thrown platform exceptions into `FileSystemError` so workflow errors stay typed instead of escaping as ordinary exceptions.

## Service

- [`FileSystem.IFileSystem`](./t-filesystem-ifilesystem.md): Provides access to common file, directory, and path operations.
- [`FileSystem.FileSystemError`](./t-filesystem-filesystemerror.md):

## Errors

- [`FileSystem.FileSystemError.fromException`](./m-filesystem-filesystemerror-fromexception.md): Classifies an exception raised by a file-system operation.
- [`FileSystem.FileSystemError.describe`](./m-filesystem-filesystemerror-describe.md): Formats a human-readable description for a file-system error.

## Text and bytes

- [`FileSystem.FileSystem.readAllText`](./m-filesystem-filesystem-readalltext.md): Reads all text through an explicit file-system service.
- [`FileSystem.FileSystem.readAllTextWithEncoding`](./m-filesystem-filesystem-readalltextwithencoding.md): Reads all text with the specified encoding through an explicit file-system service.
- [`FileSystem.FileSystem.readAllTextAsync`](./m-filesystem-filesystem-readalltextasync.md): Asynchronously reads all text through an explicit file-system service.
- [`FileSystem.FileSystem.readAllLines`](./m-filesystem-filesystem-readalllines.md): Reads all lines through an explicit file-system service.
- [`FileSystem.FileSystem.readAllLinesWithEncoding`](./m-filesystem-filesystem-readalllineswithencoding.md): Reads all lines with the specified encoding through an explicit file-system service.
- [`FileSystem.FileSystem.readAllLinesAsync`](./m-filesystem-filesystem-readalllinesasync.md): Asynchronously reads all lines through an explicit file-system service.
- [`FileSystem.FileSystem.readAllBytes`](./m-filesystem-filesystem-readallbytes.md): Reads all bytes through an explicit file-system service.
- [`FileSystem.FileSystem.readAllBytesAsync`](./m-filesystem-filesystem-readallbytesasync.md): Asynchronously reads all bytes through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllText`](./m-filesystem-filesystem-writealltext.md): Writes all text through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllTextWithEncoding`](./m-filesystem-filesystem-writealltextwithencoding.md): Writes all text with the specified encoding through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllTextAsync`](./m-filesystem-filesystem-writealltextasync.md): Asynchronously writes all text through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllLines`](./m-filesystem-filesystem-writealllines.md): Writes all lines through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllLinesWithEncoding`](./m-filesystem-filesystem-writealllineswithencoding.md): Writes all lines with the specified encoding through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllLinesAsync`](./m-filesystem-filesystem-writealllinesasync.md): Asynchronously writes all lines through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllBytes`](./m-filesystem-filesystem-writeallbytes.md): Writes all bytes through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllBytesAsync`](./m-filesystem-filesystem-writeallbytesasync.md): Asynchronously writes all bytes through an explicit file-system service.
- [`FileSystem.FileSystem.appendAllText`](./m-filesystem-filesystem-appendalltext.md): Appends all text through an explicit file-system service.
- [`FileSystem.FileSystem.appendAllTextWithEncoding`](./m-filesystem-filesystem-appendalltextwithencoding.md): Appends all text with the specified encoding through an explicit file-system service.
- [`FileSystem.FileSystem.appendAllTextAsync`](./m-filesystem-filesystem-appendalltextasync.md): Asynchronously appends all text through an explicit file-system service.
- [`FileSystem.FileSystem.appendAllLines`](./m-filesystem-filesystem-appendalllines.md): Appends all lines through an explicit file-system service.
- [`FileSystem.FileSystem.appendAllLinesWithEncoding`](./m-filesystem-filesystem-appendalllineswithencoding.md): Appends all lines with the specified encoding through an explicit file-system service.

## Files and streams

- [`FileSystem.FileSystem.fileExists`](./m-filesystem-filesystem-fileexists.md): Checks file existence through an explicit file-system service.
- [`FileSystem.FileSystem.exists`](./m-filesystem-filesystem-exists.md): Checks file existence through an explicit file-system service.
- [`FileSystem.FileSystem.deleteFile`](./m-filesystem-filesystem-deletefile.md): Deletes a file through an explicit file-system service.
- [`FileSystem.FileSystem.copyFile`](./m-filesystem-filesystem-copyfile.md): Copies a file through an explicit file-system service.
- [`FileSystem.FileSystem.moveFile`](./m-filesystem-filesystem-movefile.md): Moves a file through an explicit file-system service.
- [`FileSystem.FileSystem.openFile`](./m-filesystem-filesystem-openfile.md): Opens a file with the specified mode through an explicit file-system service.
- [`FileSystem.FileSystem.openFileWithAccess`](./m-filesystem-filesystem-openfilewithaccess.md): Opens a file with the specified mode and access through an explicit file-system service.
- [`FileSystem.FileSystem.openFileWithShare`](./m-filesystem-filesystem-openfilewithshare.md): Opens a file with the specified mode, access, and sharing behavior through an explicit file-system service.
- [`FileSystem.FileSystem.openRead`](./m-filesystem-filesystem-openread.md): Opens a file for reading through an explicit file-system service.
- [`FileSystem.FileSystem.openText`](./m-filesystem-filesystem-opentext.md): Opens a text reader through an explicit file-system service.
- [`FileSystem.FileSystem.openWrite`](./m-filesystem-filesystem-openwrite.md): Opens a file for writing through an explicit file-system service.
- [`FileSystem.FileSystem.createFile`](./m-filesystem-filesystem-createfile.md): Creates or overwrites a file through an explicit file-system service.
- [`FileSystem.FileSystem.createText`](./m-filesystem-filesystem-createtext.md): Creates a text writer through an explicit file-system service.
- [`FileSystem.FileSystem.appendText`](./m-filesystem-filesystem-appendtext.md): Creates an append text writer through an explicit file-system service.

## File metadata

- [`FileSystem.FileSystem.getFileAttributes`](./m-filesystem-filesystem-getfileattributes.md): Gets file attributes through an explicit file-system service.
- [`FileSystem.FileSystem.setFileAttributes`](./m-filesystem-filesystem-setfileattributes.md): Sets file attributes through an explicit file-system service.
- [`FileSystem.FileSystem.getFileCreationTime`](./m-filesystem-filesystem-getfilecreationtime.md): Gets file creation time through an explicit file-system service.
- [`FileSystem.FileSystem.getFileCreationTimeUtc`](./m-filesystem-filesystem-getfilecreationtimeutc.md): Gets file creation time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.setFileCreationTime`](./m-filesystem-filesystem-setfilecreationtime.md): Sets file creation time through an explicit file-system service.
- [`FileSystem.FileSystem.setFileCreationTimeUtc`](./m-filesystem-filesystem-setfilecreationtimeutc.md): Sets file creation time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.getFileLastAccessTime`](./m-filesystem-filesystem-getfilelastaccesstime.md): Gets file last access time through an explicit file-system service.
- [`FileSystem.FileSystem.getFileLastAccessTimeUtc`](./m-filesystem-filesystem-getfilelastaccesstimeutc.md): Gets file last access time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.setFileLastAccessTime`](./m-filesystem-filesystem-setfilelastaccesstime.md): Sets file last access time through an explicit file-system service.
- [`FileSystem.FileSystem.setFileLastAccessTimeUtc`](./m-filesystem-filesystem-setfilelastaccesstimeutc.md): Sets file last access time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.getFileLastWriteTime`](./m-filesystem-filesystem-getfilelastwritetime.md): Gets file last write time through an explicit file-system service.
- [`FileSystem.FileSystem.getFileLastWriteTimeUtc`](./m-filesystem-filesystem-getfilelastwritetimeutc.md): Gets file last write time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.setFileLastWriteTime`](./m-filesystem-filesystem-setfilelastwritetime.md): Sets file last write time through an explicit file-system service.
- [`FileSystem.FileSystem.setFileLastWriteTimeUtc`](./m-filesystem-filesystem-setfilelastwritetimeutc.md): Sets file last write time in UTC through an explicit file-system service.

## Directories

- [`FileSystem.FileSystem.directoryExists`](./m-filesystem-filesystem-directoryexists.md): Checks directory existence through an explicit file-system service.
- [`FileSystem.FileSystem.createDirectory`](./m-filesystem-filesystem-createdirectory.md): Creates a directory through an explicit file-system service.
- [`FileSystem.FileSystem.deleteDirectory`](./m-filesystem-filesystem-deletedirectory.md): Deletes a directory through an explicit file-system service.
- [`FileSystem.FileSystem.moveDirectory`](./m-filesystem-filesystem-movedirectory.md): Moves a directory through an explicit file-system service.
- [`FileSystem.FileSystem.enumerateFiles`](./m-filesystem-filesystem-enumeratefiles.md): Enumerates files through an explicit file-system service.
- [`FileSystem.FileSystem.getFiles`](./m-filesystem-filesystem-getfiles.md): Gets files through an explicit file-system service.
- [`FileSystem.FileSystem.enumerateDirectories`](./m-filesystem-filesystem-enumeratedirectories.md): Enumerates directories through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectories`](./m-filesystem-filesystem-getdirectories.md): Gets directories through an explicit file-system service.
- [`FileSystem.FileSystem.enumerateFileSystemEntries`](./m-filesystem-filesystem-enumeratefilesystementries.md): Enumerates files and directories through an explicit file-system service.
- [`FileSystem.FileSystem.getFileSystemEntries`](./m-filesystem-filesystem-getfilesystementries.md): Gets files and directories through an explicit file-system service.
- [`FileSystem.FileSystem.getLogicalDrives`](./m-filesystem-filesystem-getlogicaldrives.md): Gets logical drives through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryRoot`](./m-filesystem-filesystem-getdirectoryroot.md): Gets the directory root through an explicit file-system service.
- [`FileSystem.FileSystem.getParent`](./m-filesystem-filesystem-getparent.md): Gets the parent directory through an explicit file-system service.
- [`FileSystem.FileSystem.getCurrentDirectory`](./m-filesystem-filesystem-getcurrentdirectory.md): Gets the current working directory through an explicit file-system service.
- [`FileSystem.FileSystem.setCurrentDirectory`](./m-filesystem-filesystem-setcurrentdirectory.md): Sets the current working directory through an explicit file-system service.

## Directory metadata

- [`FileSystem.FileSystem.getDirectoryCreationTime`](./m-filesystem-filesystem-getdirectorycreationtime.md): Gets directory creation time through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryCreationTimeUtc`](./m-filesystem-filesystem-getdirectorycreationtimeutc.md): Gets directory creation time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.setDirectoryCreationTime`](./m-filesystem-filesystem-setdirectorycreationtime.md): Sets directory creation time through an explicit file-system service.
- [`FileSystem.FileSystem.setDirectoryCreationTimeUtc`](./m-filesystem-filesystem-setdirectorycreationtimeutc.md): Sets directory creation time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryLastAccessTime`](./m-filesystem-filesystem-getdirectorylastaccesstime.md): Gets directory last access time through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryLastAccessTimeUtc`](./m-filesystem-filesystem-getdirectorylastaccesstimeutc.md): Gets directory last access time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.setDirectoryLastAccessTime`](./m-filesystem-filesystem-setdirectorylastaccesstime.md): Sets directory last access time through an explicit file-system service.
- [`FileSystem.FileSystem.setDirectoryLastAccessTimeUtc`](./m-filesystem-filesystem-setdirectorylastaccesstimeutc.md): Sets directory last access time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryLastWriteTime`](./m-filesystem-filesystem-getdirectorylastwritetime.md): Gets directory last write time through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryLastWriteTimeUtc`](./m-filesystem-filesystem-getdirectorylastwritetimeutc.md): Gets directory last write time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.setDirectoryLastWriteTime`](./m-filesystem-filesystem-setdirectorylastwritetime.md): Sets directory last write time through an explicit file-system service.
- [`FileSystem.FileSystem.setDirectoryLastWriteTimeUtc`](./m-filesystem-filesystem-setdirectorylastwritetimeutc.md): Sets directory last write time in UTC through an explicit file-system service.

## Paths

- [`FileSystem.FileSystem.combine`](./m-filesystem-filesystem-combine.md): Combines path segments through an explicit file-system service.
- [`FileSystem.FileSystem.changeExtension`](./m-filesystem-filesystem-changeextension.md): Changes a path extension through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryName`](./m-filesystem-filesystem-getdirectoryname.md): Gets the directory name for a path through an explicit file-system service.
- [`FileSystem.FileSystem.getInvalidFileNameChars`](./m-filesystem-filesystem-getinvalidfilenamechars.md): Gets invalid file-name characters through an explicit file-system service.
- [`FileSystem.FileSystem.getInvalidPathChars`](./m-filesystem-filesystem-getinvalidpathchars.md): Gets invalid path characters through an explicit file-system service.
- [`FileSystem.FileSystem.getExtension`](./m-filesystem-filesystem-getextension.md): Gets the extension for a path through an explicit file-system service.
- [`FileSystem.FileSystem.getFileName`](./m-filesystem-filesystem-getfilename.md): Gets the file name for a path through an explicit file-system service.
- [`FileSystem.FileSystem.getFileNameWithoutExtension`](./m-filesystem-filesystem-getfilenamewithoutextension.md): Gets the file name without extension for a path through an explicit file-system service.
- [`FileSystem.FileSystem.getFullPath`](./m-filesystem-filesystem-getfullpath.md): Gets the full path through an explicit file-system service.
- [`FileSystem.FileSystem.getPathRoot`](./m-filesystem-filesystem-getpathroot.md): Gets the path root through an explicit file-system service.
- [`FileSystem.FileSystem.getRelativePath`](./m-filesystem-filesystem-getrelativepath.md): Gets a relative path through an explicit file-system service.
- [`FileSystem.FileSystem.getTempPath`](./m-filesystem-filesystem-gettemppath.md): Gets the temporary directory path through an explicit file-system service.
- [`FileSystem.FileSystem.getTempFileName`](./m-filesystem-filesystem-gettempfilename.md): Creates a temporary file through an explicit file-system service and returns its path.
- [`FileSystem.FileSystem.getRandomFileName`](./m-filesystem-filesystem-getrandomfilename.md): Gets a random file name through an explicit file-system service.
- [`FileSystem.FileSystem.hasExtension`](./m-filesystem-filesystem-hasextension.md): Checks whether a path has an extension through an explicit file-system service.
- [`FileSystem.FileSystem.endsInDirectorySeparator`](./m-filesystem-filesystem-endsindirectoryseparator.md): Checks whether a path ends in a directory separator through an explicit file-system service.
- [`FileSystem.FileSystem.trimEndingDirectorySeparator`](./m-filesystem-filesystem-trimendingdirectoryseparator.md): Trims one trailing directory separator through an explicit file-system service.
- [`FileSystem.FileSystem.isPathFullyQualified`](./m-filesystem-filesystem-ispathfullyqualified.md): Checks whether a path is fully qualified through an explicit file-system service.
- [`FileSystem.FileSystem.isPathRooted`](./m-filesystem-filesystem-ispathrooted.md): Checks whether a path is rooted through an explicit file-system service.

## Implementations

- [`FileSystem.FileSystem.live`](./p-filesystem-filesystem-live.md): Creates a live file-system service backed by <a href="https://learn.microsoft.com/dotnet/api/system.io.file">File</a>, <a href="https://learn.microsoft.com/dotnet/api/system.io.directory">Directory</a>, and <a href="https://learn.microsoft.com/dotnet/api/system.io.path">Path</a>.
- [`FileSystem.FileSystem.layer`](./p-filesystem-filesystem-layer.md): Builds the live file-system service as a layer.

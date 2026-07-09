---
title: "Services FileSystem"
weight: 30
---

This page shows the file-system service package. `IFileSystem` models common `System.IO.File`, `Directory`, `Path`, text, byte, stream, metadata, and timestamp operations as an explicit workflow service. Keep workflow code typed against the service contract, provide `FileSystem.live` only at the edge, and replace it with a deterministic implementation in tests. File-system helpers classify thrown platform exceptions into `FileSystemError` so workflow errors stay typed instead of escaping as ordinary exceptions.

## Service

- [`Flow.FileSystem.IFileSystem`](./t-flow-filesystem-ifilesystem.md): Provides access to common file, directory, and path operations.
- [`Flow.FileSystem.FileSystemError`](./t-flow-filesystem-filesystemerror.md): Describes a meaningful file-system failure.

## Errors

- [`Flow.FileSystem.FileSystemError.fromException`](./errors/m-flow-filesystem-filesystemerror-fromexception.md): Classifies an exception raised by a file-system operation.
- [`Flow.FileSystem.FileSystemError.describe`](./errors/m-flow-filesystem-filesystemerror-describe.md): Formats a human-readable description for a file-system error.

## Text and bytes

- [`Flow.FileSystem.readAllText`](./text-and-bytes/m-flow-filesystem-filesystem-readalltext.md): Reads all text through an explicit file-system service.
- [`Flow.FileSystem.readAllTextWithEncoding`](./text-and-bytes/m-flow-filesystem-filesystem-readalltextwithencoding.md): Reads all text with the specified encoding through an explicit file-system service.
- [`Flow.FileSystem.readAllTextAsync`](./text-and-bytes/m-flow-filesystem-filesystem-readalltextasync.md): Asynchronously reads all text through an explicit file-system service.
- [`Flow.FileSystem.readAllLines`](./text-and-bytes/m-flow-filesystem-filesystem-readalllines.md): Reads all lines through an explicit file-system service.
- [`Flow.FileSystem.readAllLinesWithEncoding`](./text-and-bytes/m-flow-filesystem-filesystem-readalllineswithencoding.md): Reads all lines with the specified encoding through an explicit file-system service.
- [`Flow.FileSystem.readAllLinesAsync`](./text-and-bytes/m-flow-filesystem-filesystem-readalllinesasync.md): Asynchronously reads all lines through an explicit file-system service.
- [`Flow.FileSystem.readAllBytes`](./text-and-bytes/m-flow-filesystem-filesystem-readallbytes.md): Reads all bytes through an explicit file-system service.
- [`Flow.FileSystem.readAllBytesAsync`](./text-and-bytes/m-flow-filesystem-filesystem-readallbytesasync.md): Asynchronously reads all bytes through an explicit file-system service.
- [`Flow.FileSystem.writeAllText`](./text-and-bytes/m-flow-filesystem-filesystem-writealltext.md): Writes all text through an explicit file-system service.
- [`Flow.FileSystem.writeAllTextWithEncoding`](./text-and-bytes/m-flow-filesystem-filesystem-writealltextwithencoding.md): Writes all text with the specified encoding through an explicit file-system service.
- [`Flow.FileSystem.writeAllTextAsync`](./text-and-bytes/m-flow-filesystem-filesystem-writealltextasync.md): Asynchronously writes all text through an explicit file-system service.
- [`Flow.FileSystem.writeAllLines`](./text-and-bytes/m-flow-filesystem-filesystem-writealllines.md): Writes all lines through an explicit file-system service.
- [`Flow.FileSystem.writeAllLinesWithEncoding`](./text-and-bytes/m-flow-filesystem-filesystem-writealllineswithencoding.md): Writes all lines with the specified encoding through an explicit file-system service.
- [`Flow.FileSystem.writeAllLinesAsync`](./text-and-bytes/m-flow-filesystem-filesystem-writealllinesasync.md): Asynchronously writes all lines through an explicit file-system service.
- [`Flow.FileSystem.writeAllBytes`](./text-and-bytes/m-flow-filesystem-filesystem-writeallbytes.md): Writes all bytes through an explicit file-system service.
- [`Flow.FileSystem.writeAllBytesAsync`](./text-and-bytes/m-flow-filesystem-filesystem-writeallbytesasync.md): Asynchronously writes all bytes through an explicit file-system service.
- [`Flow.FileSystem.appendAllText`](./text-and-bytes/m-flow-filesystem-filesystem-appendalltext.md): Appends all text through an explicit file-system service.
- [`Flow.FileSystem.appendAllTextWithEncoding`](./text-and-bytes/m-flow-filesystem-filesystem-appendalltextwithencoding.md): Appends all text with the specified encoding through an explicit file-system service.
- [`Flow.FileSystem.appendAllTextAsync`](./text-and-bytes/m-flow-filesystem-filesystem-appendalltextasync.md): Asynchronously appends all text through an explicit file-system service.
- [`Flow.FileSystem.appendAllLines`](./text-and-bytes/m-flow-filesystem-filesystem-appendalllines.md): Appends all lines through an explicit file-system service.
- [`Flow.FileSystem.appendAllLinesWithEncoding`](./text-and-bytes/m-flow-filesystem-filesystem-appendalllineswithencoding.md): Appends all lines with the specified encoding through an explicit file-system service.

## Files and streams

- [`Flow.FileSystem.fileExists`](./files-and-streams/m-flow-filesystem-filesystem-fileexists.md): Checks file existence through an explicit file-system service.
- [`Flow.FileSystem.exists`](./files-and-streams/m-flow-filesystem-filesystem-exists.md): Checks file existence through an explicit file-system service.
- [`Flow.FileSystem.deleteFile`](./files-and-streams/m-flow-filesystem-filesystem-deletefile.md): Deletes a file through an explicit file-system service.
- [`Flow.FileSystem.copyFile`](./files-and-streams/m-flow-filesystem-filesystem-copyfile.md): Copies a file through an explicit file-system service.
- [`Flow.FileSystem.moveFile`](./files-and-streams/m-flow-filesystem-filesystem-movefile.md): Moves a file through an explicit file-system service.
- [`Flow.FileSystem.openFile`](./files-and-streams/m-flow-filesystem-filesystem-openfile.md): Opens a file with the specified mode through an explicit file-system service.
- [`Flow.FileSystem.openFileWithAccess`](./files-and-streams/m-flow-filesystem-filesystem-openfilewithaccess.md): Opens a file with the specified mode and access through an explicit file-system service.
- [`Flow.FileSystem.openFileWithShare`](./files-and-streams/m-flow-filesystem-filesystem-openfilewithshare.md): Opens a file with the specified mode, access, and sharing behavior through an explicit file-system service.
- [`Flow.FileSystem.openRead`](./files-and-streams/m-flow-filesystem-filesystem-openread.md): Opens a file for reading through an explicit file-system service.
- [`Flow.FileSystem.openText`](./files-and-streams/m-flow-filesystem-filesystem-opentext.md): Opens a text reader through an explicit file-system service.
- [`Flow.FileSystem.openWrite`](./files-and-streams/m-flow-filesystem-filesystem-openwrite.md): Opens a file for writing through an explicit file-system service.
- [`Flow.FileSystem.createFile`](./files-and-streams/m-flow-filesystem-filesystem-createfile.md): Creates or overwrites a file through an explicit file-system service.
- [`Flow.FileSystem.createText`](./files-and-streams/m-flow-filesystem-filesystem-createtext.md): Creates a text writer through an explicit file-system service.
- [`Flow.FileSystem.appendText`](./files-and-streams/m-flow-filesystem-filesystem-appendtext.md): Creates an append text writer through an explicit file-system service.

## File metadata

- [`Flow.FileSystem.getFileAttributes`](./file-metadata/m-flow-filesystem-filesystem-getfileattributes.md): Gets file attributes through an explicit file-system service.
- [`Flow.FileSystem.setFileAttributes`](./file-metadata/m-flow-filesystem-filesystem-setfileattributes.md): Sets file attributes through an explicit file-system service.
- [`Flow.FileSystem.getFileCreationTime`](./file-metadata/m-flow-filesystem-filesystem-getfilecreationtime.md): Gets file creation time through an explicit file-system service.
- [`Flow.FileSystem.getFileCreationTimeUtc`](./file-metadata/m-flow-filesystem-filesystem-getfilecreationtimeutc.md): Gets file creation time in UTC through an explicit file-system service.
- [`Flow.FileSystem.setFileCreationTime`](./file-metadata/m-flow-filesystem-filesystem-setfilecreationtime.md): Sets file creation time through an explicit file-system service.
- [`Flow.FileSystem.setFileCreationTimeUtc`](./file-metadata/m-flow-filesystem-filesystem-setfilecreationtimeutc.md): Sets file creation time in UTC through an explicit file-system service.
- [`Flow.FileSystem.getFileLastAccessTime`](./file-metadata/m-flow-filesystem-filesystem-getfilelastaccesstime.md): Gets file last access time through an explicit file-system service.
- [`Flow.FileSystem.getFileLastAccessTimeUtc`](./file-metadata/m-flow-filesystem-filesystem-getfilelastaccesstimeutc.md): Gets file last access time in UTC through an explicit file-system service.
- [`Flow.FileSystem.setFileLastAccessTime`](./file-metadata/m-flow-filesystem-filesystem-setfilelastaccesstime.md): Sets file last access time through an explicit file-system service.
- [`Flow.FileSystem.setFileLastAccessTimeUtc`](./file-metadata/m-flow-filesystem-filesystem-setfilelastaccesstimeutc.md): Sets file last access time in UTC through an explicit file-system service.
- [`Flow.FileSystem.getFileLastWriteTime`](./file-metadata/m-flow-filesystem-filesystem-getfilelastwritetime.md): Gets file last write time through an explicit file-system service.
- [`Flow.FileSystem.getFileLastWriteTimeUtc`](./file-metadata/m-flow-filesystem-filesystem-getfilelastwritetimeutc.md): Gets file last write time in UTC through an explicit file-system service.
- [`Flow.FileSystem.setFileLastWriteTime`](./file-metadata/m-flow-filesystem-filesystem-setfilelastwritetime.md): Sets file last write time through an explicit file-system service.
- [`Flow.FileSystem.setFileLastWriteTimeUtc`](./file-metadata/m-flow-filesystem-filesystem-setfilelastwritetimeutc.md): Sets file last write time in UTC through an explicit file-system service.

## Directories

- [`Flow.FileSystem.directoryExists`](./directories/m-flow-filesystem-filesystem-directoryexists.md): Checks directory existence through an explicit file-system service.
- [`Flow.FileSystem.createDirectory`](./directories/m-flow-filesystem-filesystem-createdirectory.md): Creates a directory through an explicit file-system service.
- [`Flow.FileSystem.deleteDirectory`](./directories/m-flow-filesystem-filesystem-deletedirectory.md): Deletes a directory through an explicit file-system service.
- [`Flow.FileSystem.moveDirectory`](./directories/m-flow-filesystem-filesystem-movedirectory.md): Moves a directory through an explicit file-system service.
- [`Flow.FileSystem.enumerateFiles`](./directories/m-flow-filesystem-filesystem-enumeratefiles.md): Enumerates files through an explicit file-system service.
- [`Flow.FileSystem.getFiles`](./directories/m-flow-filesystem-filesystem-getfiles.md): Gets files through an explicit file-system service.
- [`Flow.FileSystem.enumerateDirectories`](./directories/m-flow-filesystem-filesystem-enumeratedirectories.md): Enumerates directories through an explicit file-system service.
- [`Flow.FileSystem.getDirectories`](./directories/m-flow-filesystem-filesystem-getdirectories.md): Gets directories through an explicit file-system service.
- [`Flow.FileSystem.enumerateFileSystemEntries`](./directories/m-flow-filesystem-filesystem-enumeratefilesystementries.md): Enumerates files and directories through an explicit file-system service.
- [`Flow.FileSystem.getFileSystemEntries`](./directories/m-flow-filesystem-filesystem-getfilesystementries.md): Gets files and directories through an explicit file-system service.
- [`Flow.FileSystem.getLogicalDrives`](./directories/m-flow-filesystem-filesystem-getlogicaldrives.md): Gets logical drives through an explicit file-system service.
- [`Flow.FileSystem.getDirectoryRoot`](./directories/m-flow-filesystem-filesystem-getdirectoryroot.md): Gets the directory root through an explicit file-system service.
- [`Flow.FileSystem.getParent`](./directories/m-flow-filesystem-filesystem-getparent.md): Gets the parent directory through an explicit file-system service.
- [`Flow.FileSystem.getCurrentDirectory`](./directories/m-flow-filesystem-filesystem-getcurrentdirectory.md): Gets the current working directory through an explicit file-system service.
- [`Flow.FileSystem.setCurrentDirectory`](./directories/m-flow-filesystem-filesystem-setcurrentdirectory.md): Sets the current working directory through an explicit file-system service.

## Directory metadata

- [`Flow.FileSystem.getDirectoryCreationTime`](./directory-metadata/m-flow-filesystem-filesystem-getdirectorycreationtime.md): Gets directory creation time through an explicit file-system service.
- [`Flow.FileSystem.getDirectoryCreationTimeUtc`](./directory-metadata/m-flow-filesystem-filesystem-getdirectorycreationtimeutc.md): Gets directory creation time in UTC through an explicit file-system service.
- [`Flow.FileSystem.setDirectoryCreationTime`](./directory-metadata/m-flow-filesystem-filesystem-setdirectorycreationtime.md): Sets directory creation time through an explicit file-system service.
- [`Flow.FileSystem.setDirectoryCreationTimeUtc`](./directory-metadata/m-flow-filesystem-filesystem-setdirectorycreationtimeutc.md): Sets directory creation time in UTC through an explicit file-system service.
- [`Flow.FileSystem.getDirectoryLastAccessTime`](./directory-metadata/m-flow-filesystem-filesystem-getdirectorylastaccesstime.md): Gets directory last access time through an explicit file-system service.
- [`Flow.FileSystem.getDirectoryLastAccessTimeUtc`](./directory-metadata/m-flow-filesystem-filesystem-getdirectorylastaccesstimeutc.md): Gets directory last access time in UTC through an explicit file-system service.
- [`Flow.FileSystem.setDirectoryLastAccessTime`](./directory-metadata/m-flow-filesystem-filesystem-setdirectorylastaccesstime.md): Sets directory last access time through an explicit file-system service.
- [`Flow.FileSystem.setDirectoryLastAccessTimeUtc`](./directory-metadata/m-flow-filesystem-filesystem-setdirectorylastaccesstimeutc.md): Sets directory last access time in UTC through an explicit file-system service.
- [`Flow.FileSystem.getDirectoryLastWriteTime`](./directory-metadata/m-flow-filesystem-filesystem-getdirectorylastwritetime.md): Gets directory last write time through an explicit file-system service.
- [`Flow.FileSystem.getDirectoryLastWriteTimeUtc`](./directory-metadata/m-flow-filesystem-filesystem-getdirectorylastwritetimeutc.md): Gets directory last write time in UTC through an explicit file-system service.
- [`Flow.FileSystem.setDirectoryLastWriteTime`](./directory-metadata/m-flow-filesystem-filesystem-setdirectorylastwritetime.md): Sets directory last write time through an explicit file-system service.
- [`Flow.FileSystem.setDirectoryLastWriteTimeUtc`](./directory-metadata/m-flow-filesystem-filesystem-setdirectorylastwritetimeutc.md): Sets directory last write time in UTC through an explicit file-system service.

## Paths

- [`Flow.FileSystem.combine`](./paths/m-flow-filesystem-filesystem-combine.md): Combines path segments through an explicit file-system service.
- [`Flow.FileSystem.changeExtension`](./paths/m-flow-filesystem-filesystem-changeextension.md): Changes a path extension through an explicit file-system service.
- [`Flow.FileSystem.getDirectoryName`](./paths/m-flow-filesystem-filesystem-getdirectoryname.md): Gets the directory name for a path through an explicit file-system service.
- [`Flow.FileSystem.getInvalidFileNameChars`](./paths/m-flow-filesystem-filesystem-getinvalidfilenamechars.md): Gets invalid file-name characters through an explicit file-system service.
- [`Flow.FileSystem.getInvalidPathChars`](./paths/m-flow-filesystem-filesystem-getinvalidpathchars.md): Gets invalid path characters through an explicit file-system service.
- [`Flow.FileSystem.getExtension`](./paths/m-flow-filesystem-filesystem-getextension.md): Gets the extension for a path through an explicit file-system service.
- [`Flow.FileSystem.getFileName`](./paths/m-flow-filesystem-filesystem-getfilename.md): Gets the file name for a path through an explicit file-system service.
- [`Flow.FileSystem.getFileNameWithoutExtension`](./paths/m-flow-filesystem-filesystem-getfilenamewithoutextension.md): Gets the file name without extension for a path through an explicit file-system service.
- [`Flow.FileSystem.getFullPath`](./paths/m-flow-filesystem-filesystem-getfullpath.md): Gets the full path through an explicit file-system service.
- [`Flow.FileSystem.getPathRoot`](./paths/m-flow-filesystem-filesystem-getpathroot.md): Gets the path root through an explicit file-system service.
- [`Flow.FileSystem.getRelativePath`](./paths/m-flow-filesystem-filesystem-getrelativepath.md): Gets a relative path through an explicit file-system service.
- [`Flow.FileSystem.getTempPath`](./paths/m-flow-filesystem-filesystem-gettemppath.md): Gets the temporary directory path through an explicit file-system service.
- [`Flow.FileSystem.getTempFileName`](./paths/m-flow-filesystem-filesystem-gettempfilename.md): Creates a temporary file through an explicit file-system service and returns its path.
- [`Flow.FileSystem.getRandomFileName`](./paths/m-flow-filesystem-filesystem-getrandomfilename.md): Gets a random file name through an explicit file-system service.
- [`Flow.FileSystem.hasExtension`](./paths/m-flow-filesystem-filesystem-hasextension.md): Checks whether a path has an extension through an explicit file-system service.
- [`Flow.FileSystem.endsInDirectorySeparator`](./paths/m-flow-filesystem-filesystem-endsindirectoryseparator.md): Checks whether a path ends in a directory separator through an explicit file-system service.
- [`Flow.FileSystem.trimEndingDirectorySeparator`](./paths/m-flow-filesystem-filesystem-trimendingdirectoryseparator.md): Trims one trailing directory separator through an explicit file-system service.
- [`Flow.FileSystem.isPathFullyQualified`](./paths/m-flow-filesystem-filesystem-ispathfullyqualified.md): Checks whether a path is fully qualified through an explicit file-system service.
- [`Flow.FileSystem.isPathRooted`](./paths/m-flow-filesystem-filesystem-ispathrooted.md): Checks whether a path is rooted through an explicit file-system service.

## Implementations

- [`Flow.FileSystem.live`](./implementations/p-flow-filesystem-filesystem-live.md): Creates a live file-system service backed by <a href="https://learn.microsoft.com/dotnet/api/system.io.file">File</a>, <a href="https://learn.microsoft.com/dotnet/api/system.io.directory">Directory</a>, and <a href="https://learn.microsoft.com/dotnet/api/system.io.path">Path</a>.
- [`Flow.FileSystem.layer`](./implementations/p-flow-filesystem-filesystem-layer.md): Builds the live file-system service as a layer.

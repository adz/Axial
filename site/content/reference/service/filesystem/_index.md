---
title: "Services FileSystem"
weight: 30
type: docs
---

This page shows the file-system service package. `IFileSystem` models common `System.IO.File`, `Directory`, `Path`, text, byte, stream, metadata, and timestamp operations as an explicit workflow service. Keep workflow code typed against the service contract, provide `FileSystem.live` only at the edge, and replace it with a deterministic implementation in tests. File-system helpers classify thrown platform exceptions into `FileSystemError` so workflow errors stay typed instead of escaping as ordinary exceptions.

## Service

- [`FileSystem.IFileSystem`](./t-filesystem-ifilesystem.md): Provides access to common file, directory, and path operations.
- [`FileSystem.FileSystemError`](./t-filesystem-filesystemerror.md):

## Errors

- [`FileSystem.FileSystemError.fromException`](./errors/m-filesystem-filesystemerror-fromexception.md): Classifies an exception raised by a file-system operation.
- [`FileSystem.FileSystemError.describe`](./errors/m-filesystem-filesystemerror-describe.md): Formats a human-readable description for a file-system error.

## Text and bytes

- [`FileSystem.FileSystem.readAllText`](./text-and-bytes/m-filesystem-filesystem-readalltext.md): Reads all text through an explicit file-system service.
- [`FileSystem.FileSystem.readAllTextWithEncoding`](./text-and-bytes/m-filesystem-filesystem-readalltextwithencoding.md): Reads all text with the specified encoding through an explicit file-system service.
- [`FileSystem.FileSystem.readAllTextAsync`](./text-and-bytes/m-filesystem-filesystem-readalltextasync.md): Asynchronously reads all text through an explicit file-system service.
- [`FileSystem.FileSystem.readAllLines`](./text-and-bytes/m-filesystem-filesystem-readalllines.md): Reads all lines through an explicit file-system service.
- [`FileSystem.FileSystem.readAllLinesWithEncoding`](./text-and-bytes/m-filesystem-filesystem-readalllineswithencoding.md): Reads all lines with the specified encoding through an explicit file-system service.
- [`FileSystem.FileSystem.readAllLinesAsync`](./text-and-bytes/m-filesystem-filesystem-readalllinesasync.md): Asynchronously reads all lines through an explicit file-system service.
- [`FileSystem.FileSystem.readAllBytes`](./text-and-bytes/m-filesystem-filesystem-readallbytes.md): Reads all bytes through an explicit file-system service.
- [`FileSystem.FileSystem.readAllBytesAsync`](./text-and-bytes/m-filesystem-filesystem-readallbytesasync.md): Asynchronously reads all bytes through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllText`](./text-and-bytes/m-filesystem-filesystem-writealltext.md): Writes all text through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllTextWithEncoding`](./text-and-bytes/m-filesystem-filesystem-writealltextwithencoding.md): Writes all text with the specified encoding through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllTextAsync`](./text-and-bytes/m-filesystem-filesystem-writealltextasync.md): Asynchronously writes all text through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllLines`](./text-and-bytes/m-filesystem-filesystem-writealllines.md): Writes all lines through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllLinesWithEncoding`](./text-and-bytes/m-filesystem-filesystem-writealllineswithencoding.md): Writes all lines with the specified encoding through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllLinesAsync`](./text-and-bytes/m-filesystem-filesystem-writealllinesasync.md): Asynchronously writes all lines through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllBytes`](./text-and-bytes/m-filesystem-filesystem-writeallbytes.md): Writes all bytes through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllBytesAsync`](./text-and-bytes/m-filesystem-filesystem-writeallbytesasync.md): Asynchronously writes all bytes through an explicit file-system service.
- [`FileSystem.FileSystem.appendAllText`](./text-and-bytes/m-filesystem-filesystem-appendalltext.md): Appends all text through an explicit file-system service.
- [`FileSystem.FileSystem.appendAllTextWithEncoding`](./text-and-bytes/m-filesystem-filesystem-appendalltextwithencoding.md): Appends all text with the specified encoding through an explicit file-system service.
- [`FileSystem.FileSystem.appendAllTextAsync`](./text-and-bytes/m-filesystem-filesystem-appendalltextasync.md): Asynchronously appends all text through an explicit file-system service.
- [`FileSystem.FileSystem.appendAllLines`](./text-and-bytes/m-filesystem-filesystem-appendalllines.md): Appends all lines through an explicit file-system service.
- [`FileSystem.FileSystem.appendAllLinesWithEncoding`](./text-and-bytes/m-filesystem-filesystem-appendalllineswithencoding.md): Appends all lines with the specified encoding through an explicit file-system service.

## Files and streams

- [`FileSystem.FileSystem.fileExists`](./files-and-streams/m-filesystem-filesystem-fileexists.md): Checks file existence through an explicit file-system service.
- [`FileSystem.FileSystem.exists`](./files-and-streams/m-filesystem-filesystem-exists.md): Checks file existence through an explicit file-system service.
- [`FileSystem.FileSystem.deleteFile`](./files-and-streams/m-filesystem-filesystem-deletefile.md): Deletes a file through an explicit file-system service.
- [`FileSystem.FileSystem.copyFile`](./files-and-streams/m-filesystem-filesystem-copyfile.md): Copies a file through an explicit file-system service.
- [`FileSystem.FileSystem.moveFile`](./files-and-streams/m-filesystem-filesystem-movefile.md): Moves a file through an explicit file-system service.
- [`FileSystem.FileSystem.openFile`](./files-and-streams/m-filesystem-filesystem-openfile.md): Opens a file with the specified mode through an explicit file-system service.
- [`FileSystem.FileSystem.openFileWithAccess`](./files-and-streams/m-filesystem-filesystem-openfilewithaccess.md): Opens a file with the specified mode and access through an explicit file-system service.
- [`FileSystem.FileSystem.openFileWithShare`](./files-and-streams/m-filesystem-filesystem-openfilewithshare.md): Opens a file with the specified mode, access, and sharing behavior through an explicit file-system service.
- [`FileSystem.FileSystem.openRead`](./files-and-streams/m-filesystem-filesystem-openread.md): Opens a file for reading through an explicit file-system service.
- [`FileSystem.FileSystem.openText`](./files-and-streams/m-filesystem-filesystem-opentext.md): Opens a text reader through an explicit file-system service.
- [`FileSystem.FileSystem.openWrite`](./files-and-streams/m-filesystem-filesystem-openwrite.md): Opens a file for writing through an explicit file-system service.
- [`FileSystem.FileSystem.createFile`](./files-and-streams/m-filesystem-filesystem-createfile.md): Creates or overwrites a file through an explicit file-system service.
- [`FileSystem.FileSystem.createText`](./files-and-streams/m-filesystem-filesystem-createtext.md): Creates a text writer through an explicit file-system service.
- [`FileSystem.FileSystem.appendText`](./files-and-streams/m-filesystem-filesystem-appendtext.md): Creates an append text writer through an explicit file-system service.

## File metadata

- [`FileSystem.FileSystem.getFileAttributes`](./file-metadata/m-filesystem-filesystem-getfileattributes.md): Gets file attributes through an explicit file-system service.
- [`FileSystem.FileSystem.setFileAttributes`](./file-metadata/m-filesystem-filesystem-setfileattributes.md): Sets file attributes through an explicit file-system service.
- [`FileSystem.FileSystem.getFileCreationTime`](./file-metadata/m-filesystem-filesystem-getfilecreationtime.md): Gets file creation time through an explicit file-system service.
- [`FileSystem.FileSystem.getFileCreationTimeUtc`](./file-metadata/m-filesystem-filesystem-getfilecreationtimeutc.md): Gets file creation time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.setFileCreationTime`](./file-metadata/m-filesystem-filesystem-setfilecreationtime.md): Sets file creation time through an explicit file-system service.
- [`FileSystem.FileSystem.setFileCreationTimeUtc`](./file-metadata/m-filesystem-filesystem-setfilecreationtimeutc.md): Sets file creation time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.getFileLastAccessTime`](./file-metadata/m-filesystem-filesystem-getfilelastaccesstime.md): Gets file last access time through an explicit file-system service.
- [`FileSystem.FileSystem.getFileLastAccessTimeUtc`](./file-metadata/m-filesystem-filesystem-getfilelastaccesstimeutc.md): Gets file last access time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.setFileLastAccessTime`](./file-metadata/m-filesystem-filesystem-setfilelastaccesstime.md): Sets file last access time through an explicit file-system service.
- [`FileSystem.FileSystem.setFileLastAccessTimeUtc`](./file-metadata/m-filesystem-filesystem-setfilelastaccesstimeutc.md): Sets file last access time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.getFileLastWriteTime`](./file-metadata/m-filesystem-filesystem-getfilelastwritetime.md): Gets file last write time through an explicit file-system service.
- [`FileSystem.FileSystem.getFileLastWriteTimeUtc`](./file-metadata/m-filesystem-filesystem-getfilelastwritetimeutc.md): Gets file last write time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.setFileLastWriteTime`](./file-metadata/m-filesystem-filesystem-setfilelastwritetime.md): Sets file last write time through an explicit file-system service.
- [`FileSystem.FileSystem.setFileLastWriteTimeUtc`](./file-metadata/m-filesystem-filesystem-setfilelastwritetimeutc.md): Sets file last write time in UTC through an explicit file-system service.

## Directories

- [`FileSystem.FileSystem.directoryExists`](./directories/m-filesystem-filesystem-directoryexists.md): Checks directory existence through an explicit file-system service.
- [`FileSystem.FileSystem.createDirectory`](./directories/m-filesystem-filesystem-createdirectory.md): Creates a directory through an explicit file-system service.
- [`FileSystem.FileSystem.deleteDirectory`](./directories/m-filesystem-filesystem-deletedirectory.md): Deletes a directory through an explicit file-system service.
- [`FileSystem.FileSystem.moveDirectory`](./directories/m-filesystem-filesystem-movedirectory.md): Moves a directory through an explicit file-system service.
- [`FileSystem.FileSystem.enumerateFiles`](./directories/m-filesystem-filesystem-enumeratefiles.md): Enumerates files through an explicit file-system service.
- [`FileSystem.FileSystem.getFiles`](./directories/m-filesystem-filesystem-getfiles.md): Gets files through an explicit file-system service.
- [`FileSystem.FileSystem.enumerateDirectories`](./directories/m-filesystem-filesystem-enumeratedirectories.md): Enumerates directories through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectories`](./directories/m-filesystem-filesystem-getdirectories.md): Gets directories through an explicit file-system service.
- [`FileSystem.FileSystem.enumerateFileSystemEntries`](./directories/m-filesystem-filesystem-enumeratefilesystementries.md): Enumerates files and directories through an explicit file-system service.
- [`FileSystem.FileSystem.getFileSystemEntries`](./directories/m-filesystem-filesystem-getfilesystementries.md): Gets files and directories through an explicit file-system service.
- [`FileSystem.FileSystem.getLogicalDrives`](./directories/m-filesystem-filesystem-getlogicaldrives.md): Gets logical drives through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryRoot`](./directories/m-filesystem-filesystem-getdirectoryroot.md): Gets the directory root through an explicit file-system service.
- [`FileSystem.FileSystem.getParent`](./directories/m-filesystem-filesystem-getparent.md): Gets the parent directory through an explicit file-system service.
- [`FileSystem.FileSystem.getCurrentDirectory`](./directories/m-filesystem-filesystem-getcurrentdirectory.md): Gets the current working directory through an explicit file-system service.
- [`FileSystem.FileSystem.setCurrentDirectory`](./directories/m-filesystem-filesystem-setcurrentdirectory.md): Sets the current working directory through an explicit file-system service.

## Directory metadata

- [`FileSystem.FileSystem.getDirectoryCreationTime`](./directory-metadata/m-filesystem-filesystem-getdirectorycreationtime.md): Gets directory creation time through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryCreationTimeUtc`](./directory-metadata/m-filesystem-filesystem-getdirectorycreationtimeutc.md): Gets directory creation time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.setDirectoryCreationTime`](./directory-metadata/m-filesystem-filesystem-setdirectorycreationtime.md): Sets directory creation time through an explicit file-system service.
- [`FileSystem.FileSystem.setDirectoryCreationTimeUtc`](./directory-metadata/m-filesystem-filesystem-setdirectorycreationtimeutc.md): Sets directory creation time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryLastAccessTime`](./directory-metadata/m-filesystem-filesystem-getdirectorylastaccesstime.md): Gets directory last access time through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryLastAccessTimeUtc`](./directory-metadata/m-filesystem-filesystem-getdirectorylastaccesstimeutc.md): Gets directory last access time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.setDirectoryLastAccessTime`](./directory-metadata/m-filesystem-filesystem-setdirectorylastaccesstime.md): Sets directory last access time through an explicit file-system service.
- [`FileSystem.FileSystem.setDirectoryLastAccessTimeUtc`](./directory-metadata/m-filesystem-filesystem-setdirectorylastaccesstimeutc.md): Sets directory last access time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryLastWriteTime`](./directory-metadata/m-filesystem-filesystem-getdirectorylastwritetime.md): Gets directory last write time through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryLastWriteTimeUtc`](./directory-metadata/m-filesystem-filesystem-getdirectorylastwritetimeutc.md): Gets directory last write time in UTC through an explicit file-system service.
- [`FileSystem.FileSystem.setDirectoryLastWriteTime`](./directory-metadata/m-filesystem-filesystem-setdirectorylastwritetime.md): Sets directory last write time through an explicit file-system service.
- [`FileSystem.FileSystem.setDirectoryLastWriteTimeUtc`](./directory-metadata/m-filesystem-filesystem-setdirectorylastwritetimeutc.md): Sets directory last write time in UTC through an explicit file-system service.

## Paths

- [`FileSystem.FileSystem.combine`](./paths/m-filesystem-filesystem-combine.md): Combines path segments through an explicit file-system service.
- [`FileSystem.FileSystem.changeExtension`](./paths/m-filesystem-filesystem-changeextension.md): Changes a path extension through an explicit file-system service.
- [`FileSystem.FileSystem.getDirectoryName`](./paths/m-filesystem-filesystem-getdirectoryname.md): Gets the directory name for a path through an explicit file-system service.
- [`FileSystem.FileSystem.getInvalidFileNameChars`](./paths/m-filesystem-filesystem-getinvalidfilenamechars.md): Gets invalid file-name characters through an explicit file-system service.
- [`FileSystem.FileSystem.getInvalidPathChars`](./paths/m-filesystem-filesystem-getinvalidpathchars.md): Gets invalid path characters through an explicit file-system service.
- [`FileSystem.FileSystem.getExtension`](./paths/m-filesystem-filesystem-getextension.md): Gets the extension for a path through an explicit file-system service.
- [`FileSystem.FileSystem.getFileName`](./paths/m-filesystem-filesystem-getfilename.md): Gets the file name for a path through an explicit file-system service.
- [`FileSystem.FileSystem.getFileNameWithoutExtension`](./paths/m-filesystem-filesystem-getfilenamewithoutextension.md): Gets the file name without extension for a path through an explicit file-system service.
- [`FileSystem.FileSystem.getFullPath`](./paths/m-filesystem-filesystem-getfullpath.md): Gets the full path through an explicit file-system service.
- [`FileSystem.FileSystem.getPathRoot`](./paths/m-filesystem-filesystem-getpathroot.md): Gets the path root through an explicit file-system service.
- [`FileSystem.FileSystem.getRelativePath`](./paths/m-filesystem-filesystem-getrelativepath.md): Gets a relative path through an explicit file-system service.
- [`FileSystem.FileSystem.getTempPath`](./paths/m-filesystem-filesystem-gettemppath.md): Gets the temporary directory path through an explicit file-system service.
- [`FileSystem.FileSystem.getTempFileName`](./paths/m-filesystem-filesystem-gettempfilename.md): Creates a temporary file through an explicit file-system service and returns its path.
- [`FileSystem.FileSystem.getRandomFileName`](./paths/m-filesystem-filesystem-getrandomfilename.md): Gets a random file name through an explicit file-system service.
- [`FileSystem.FileSystem.hasExtension`](./paths/m-filesystem-filesystem-hasextension.md): Checks whether a path has an extension through an explicit file-system service.
- [`FileSystem.FileSystem.endsInDirectorySeparator`](./paths/m-filesystem-filesystem-endsindirectoryseparator.md): Checks whether a path ends in a directory separator through an explicit file-system service.
- [`FileSystem.FileSystem.trimEndingDirectorySeparator`](./paths/m-filesystem-filesystem-trimendingdirectoryseparator.md): Trims one trailing directory separator through an explicit file-system service.
- [`FileSystem.FileSystem.isPathFullyQualified`](./paths/m-filesystem-filesystem-ispathfullyqualified.md): Checks whether a path is fully qualified through an explicit file-system service.
- [`FileSystem.FileSystem.isPathRooted`](./paths/m-filesystem-filesystem-ispathrooted.md): Checks whether a path is rooted through an explicit file-system service.

## Implementations

- [`FileSystem.FileSystem.live`](./implementations/p-filesystem-filesystem-live.md): Creates a live file-system service backed by <a href="https://learn.microsoft.com/dotnet/api/system.io.file">File</a>, <a href="https://learn.microsoft.com/dotnet/api/system.io.directory">Directory</a>, and <a href="https://learn.microsoft.com/dotnet/api/system.io.path">Path</a>.
- [`FileSystem.FileSystem.layer`](./implementations/p-filesystem-filesystem-layer.md): Builds the live file-system service as a layer.

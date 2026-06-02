---
title: "Services FileSystem"
weight: 30
type: docs
---

This page shows the file-system service package. `IFileSystem` names the small set of file operations currently supported by FsFlow examples and workflows: reading text, writing text, and existence checks. Keep workflow code typed against the service contract and choose the live or test implementation at provisioning time.

## Service

- [`FileSystem.IFileSystem`](./t-filesystem-ifilesystem.md): Provides synchronous access to file system operations.

## Helpers

- [`FileSystem.FileSystem.readAllText`](./m-filesystem-filesystem-readalltext.md): Reads all text through an explicit file-system service.
- [`FileSystem.FileSystem.writeAllText`](./m-filesystem-filesystem-writealltext.md): Writes all text through an explicit file-system service.
- [`FileSystem.FileSystem.exists`](./m-filesystem-filesystem-exists.md): Checks file existence through an explicit file-system service.
- [`FileSystem.FileSystem.live`](./p-filesystem-filesystem-live.md): Creates a live file-system service backed by <a href="https://learn.microsoft.com/dotnet/api/system.io.file">File</a>.
- [`FileSystem.FileSystem.layer`](./p-filesystem-filesystem-layer.md): Builds the live file-system service as a layer.


---
title: "Flow.FileSystem.FileSystemError"
linkTitle: "FileSystemError"
weight: 1001
type: docs
---

Describes a meaningful file-system failure.

## Signature

<div class="fsdocs-usage">
<code>type FileSystemError</code>
</div>

## Union Cases

| Case | Description |
| --- | --- |
| `FileNotFound` | A file was not found. |
| `DirectoryNotFound` | A directory was not found. |
| `AlreadyExists` | The requested path already exists. |
| `Unauthorized` | The caller is not authorized to access the requested path. |
| `InvalidPath` | The requested path was invalid. |
| `PathTooLong` | The requested path was too long for the platform. |
| `Canceled` | The operation was canceled. |
| `Io` | The operation failed with a general I/O error. |
| `Unsupported` | The operation is not supported by the current platform or path shape. |
| `Unexpected` | An unexpected exception escaped a file-system operation. |

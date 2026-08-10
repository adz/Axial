---
title: Resources
description: Acquiring something that must be released, within one flow.
---

# Resources

When a flow opens something that must be closed, pair the two so the close cannot be skipped.

```fsharp
open System.IO
open System.Threading.Tasks
open Axial

let readFirstLine path =
    Flow.acquireReleaseWith
        (Flow.succeed (File.OpenText path))
        (fun reader _ ->
            reader.Dispose()
            Task.CompletedTask)
        (fun reader ->
            flow {
                return! reader.ReadLineAsync()
            })
```

Three arguments: **acquire**, **release**, and **use**. The release runs after the use flow finishes — whether it
succeeded, failed with a typed error, died with a defect, or was interrupted. There is no path through the flow that
skips it.

That covers the common case: the resource's lifetime is one expression, and `use` / `use!` inside `flow { }` covers
it too when the lifetime matches a lexical block.

A resource sometimes has to outlive the expression that acquired it — acquired in one subflow, used by several
others, released only when the whole execution finishes. That is a *scope*, and it is covered in
[scopes and resources](/advanced/scopes-and-resources.html).

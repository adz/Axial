---
title: Resources
description: Acquiring values owned by a Flow scope.
---

# Resources

Use `use` or `use!` when one `flow { }` body owns an `IDisposable` for its whole lifetime:

```fsharp
open System.IO
open Axial

let readFirstLineLexically path =
    flow {
        use reader = File.OpenText path
        return! ColdTask(fun _ -> reader.ReadLineAsync())
    }
```

`use` disposes a value that already exists. `use!` first runs a Flow to obtain the disposable, then owns it for the
rest of the body:

```fsharp
let readFirstLineWithUseBang path =
    flow {
        use! reader = Flow.succeed (File.OpenText path)
        return! ColdTask(fun _ -> reader.ReadLineAsync())
    }
```

Both are **lexical**: the compiler disposes the resource when control leaves the body governed by the binding, even
when `return!` fails or the flow is interrupted. Prefer them when one `flow { }` body owns the resource for its whole
lifetime; use a Flow scope when ownership must span functions, subflows, or fibers.

```fsharp
open System.IO
open Axial

let readFirstLine path =
    Flow.scoped (
        Flow.scopeAcquireRelease
            (Flow.succeed (File.OpenText path))
            (fun reader ->
                Flow.delay (fun () ->
                    reader.Dispose()
                    Flow.succeed ()))
        |> Flow.bind (fun reader ->
            flow {
                return! ColdTask(fun _ -> reader.ReadLineAsync())
            }))
```

`Flow.scopeAcquireRelease` acquires the value and registers its release with the current scope. The release is a flow
that cannot fail with a typed error; it runs without interruption, with the environment captured at acquisition.
`Flow.scoped` creates the local ownership boundary and closes it after success, typed failure, defect, or
interruption.

Both examples open the file with `File.OpenText` to keep the focus on ownership and cleanup. That is an ambient
.NET call, not the `IFileSystem` service: it is not interruptible, and I/O failures are not modeled as a typed
`FileSystemError`. For the recommended pattern — an explicit filesystem dependency with a typed error channel — see
[FileSystem](/filesystem/filesystem.html).

For reusable acquisition descriptions and direct registration of disposables or finalizers, see
[scopes and resources](/scopes/index.html).

## Cleanup for one expression

When cleanup belongs to one flow rather than to a resource, attach it to the flow itself:

- `Flow.ensuring finalizer` runs `finalizer` after the flow, however it ends.
- `Flow.onExit handler` passes the flow's `Exit` to `handler`, for cleanup that depends on how the flow ended.
- `Flow.onInterrupt handler` runs `handler` only when the flow is interrupted.

The handler runs without the flow's cancellation, so the interruption that ended the flow cannot cut the cleanup short.
The handler cannot fail with a typed error. If the flow succeeded, a defect in the handler replaces the success with
that defect; if the flow failed, the defect is appended to the original cause as `Cause.Then`, keeping the flow's own
cause first.

```fsharp transcript
> open Axial.PlatformService;;
> (flow {
-     let log = ResizeArray<string>()
-     let note text : Flow<unit, Never, unit> = Flow.delay (fun () -> log.Add text; Flow.ok ())
-     let! worker =
-         Flow.never<unit, Never, unit>
-         |> Flow.onInterrupt (note "interrupted")
-         |> Flow.ensuring (note "closed")
-         |> Flow.fork
-     let! _ = Fiber.interrupt worker
-     let! _ = Flow.ok 1 |> Flow.onInterrupt (note "not logged") |> Flow.ensuring (note "closed")
-     return List.ofSeq log
- } : Flow<unit, Never, string list>)
- |> Flow.run ();;
val it: Exit<string list,Never> = Success ["interrupted"; "closed"; "closed"]
```

The first worker never finished on its own, so interrupting it ran both handlers in order. The second flow succeeded,
so only its `ensuring` finalizer ran. `Flow.never` is a flow that only ends when it is interrupted, such as a service
that waits for shutdown.


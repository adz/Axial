---
title: Resources
description: Acquiring values owned by a Flow scope.
---

# Resources

Use `use` or `use!` when one `flow { }` body owns an `IDisposable` for its whole lifetime:

```fsharp
let readFirstLineLexically path =
    flow {
        use reader = File.OpenText path
        return! ColdTask(fun _ -> reader.ReadLineAsync())
    }
```

Use a Flow scope when ownership must span functions, subflows, or fibers:

```fsharp
open System.IO
open System.Threading.Tasks
open Axial

let readFirstLine path =
    Flow.scoped (
        Flow.scopeAcquireRelease
            (Flow.succeed (File.OpenText path))
            (fun reader _ ->
                reader.Dispose()
                Task.CompletedTask)
        |> Flow.bind (fun reader ->
            flow {
                return! ColdTask(fun _ -> reader.ReadLineAsync())
            }))
```

`Flow.scopeAcquireRelease` acquires the value and registers its release with the current scope. `Flow.scoped` creates
the local ownership boundary and closes it after success, typed failure, defect, or interruption.

For reusable acquisition descriptions and direct registration of disposables or finalizers, see
[scopes and resources](/scopes/index.html).

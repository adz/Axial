---
title: Creating Flows
description: Create successful, failed, Task-backed, and Async-backed Flow descriptions.
---

# Creating Flows

`Flow.succeed` creates a description that succeeds with a value, and `Flow.fail` one that fails with an expected,
typed error. Neither runs until `Flow.run` starts it:

```fsharp transcript
> let greeting () : Flow<string> = Flow.succeed "Hello";;
> Flow.run () (greeting ());;
val it: Exit<string,Never> = Success "Hello"

> type LoadError = UserNotFound;;
> let missing () : Flow<LoadError, string> = Flow.fail UserNotFound;;
> Flow.run () (missing ());;
val it: Exit<string,LoadError> = Failure (Fail UserNotFound)
```

Use `Flow.fromTask` or `Flow.fromAsync` when the operation comes from a Task- or Async-returning API and thrown
exceptions are defects. Use an `attempt` constructor when thrown exceptions are expected interop failures that callers
should handle:

```fsharp transcript
> let path () = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "axial-docs-message.txt");;
> System.IO.File.WriteAllText(path (), "Hello from a file");;
val it: unit = ()

> let readText (file: string) : Flow<string> = Flow.fromTask (fun token -> System.IO.File.ReadAllTextAsync(file, token));;
> Flow.run () (readText (path ()));;
val it: Exit<string,Never> = Success "Hello from a file"

> match Flow.run () (readText (path () + ".missing")) with Exit.Failure (Cause.Die error) -> error.GetType().Name | _ -> "";;
val it: string = "FileNotFoundException"

> let attemptRead (file: string) : ExnFlow<string> = Flow.attemptTask (fun token -> System.IO.File.ReadAllTextAsync(file, token));;
> match Flow.run () (attemptRead (path () + ".missing")) with Exit.Failure (Cause.Fail error) -> error.GetType().Name | _ -> "";;
val it: string = "FileNotFoundException"
```

The missing file shows the difference. `fromTask` reports the exception as a defect (`Cause.Die`); `attemptTask`
places it in the typed error channel (`Cause.Fail`), where the caller is expected to handle it.

The [Task and Async interop guide](/the-flow-type/task-async-interop.html) covers cancellation and
all supported carriers.

## Go Further

- [Flow construction reference](/api/) lists every constructor and
  conversion.
- [Defects](/error-handling/defects.html) explains when an exception should remain a defect and
  when an attempt constructor is appropriate.

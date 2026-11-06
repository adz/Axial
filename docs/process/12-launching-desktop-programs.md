# Open an editor, file manager or URL

A desktop application often hands work to another program: open a file in an editor, show a folder in the file
manager, or open a URL in the browser. These launches differ from the rest of `Axial.Process`: the caller does not
wait for the child, does not capture its output, and does not own its lifetime. Closing the application leaves the
editor open.

- `Process.launch command` starts one command and returns once it has started.
- `Process.shellOpen target` opens a path or URL with the operating system's default handler: `ShellExecute` on
  Windows, `open` on macOS and `xdg-open` elsewhere.

Both return `Flow<'env, ProcessError, unit>` and go through the environment's `IProcess` service, so a failure to start
is the typed `ProcessError.StartFailed` and a test can assert the launch without starting anything.

```fsharp prepare
open Axial
open Axial.Process

let shouldEqual expected actual =
    if actual <> expected then failwithf "Expected %A but got %A" expected actual
```

```fsharp
let openInEditor (path: string) =
    Process.commandArgs "code" [ "--goto"; path ] |> Process.launch

let revealFolder (folder: string) = Process.shellOpen folder
```

`Process.launch` applies the command's executable, arguments, working directory and environment variables. The child
inherits the caller's standard handles; output routing, capture, timeouts and success codes do not apply, because
nothing waits for the child. Interrupting the flow after the launch does not stop the child.

`Process.shellOpen` succeeds once the opener has started. On Linux and macOS the opener exits after handing the target
on, and a target with no registered handler is not reported. To reveal a file, open its folder.

## Commands that are scripts on Windows

Some editors install a script rather than an executable, for example `code.cmd` on Windows. Starting it directly fails
with `StartFailed`; run it through `cmd.exe`:

```fsharp
let editorCommand (isWindows: bool) (path: string) =
    if isWindows then Process.commandArgs "cmd.exe" [ "/c"; "code"; path ]
    else Process.commandArgs "code" [ path ]
```

## Test a launch

`Process.test ()` records every launch in `Launches` and succeeds by default. Set `LaunchHandler` to simulate a
failure:

```fsharp run
let host = ProcessHostEnvironment.test ()

openInEditor "notes.txt" |> Flow.run host.Environment |> shouldEqual (Exit.Success ())

match List.ofSeq host.Process.Launches with
| [ ProcessLaunch.Command command ] -> Process.arguments command |> shouldEqual [ "--goto"; "notes.txt" ]
| _ -> failwith "expected one launch"

host.Process.LaunchHandler <-
    Some(fun _ -> Flow.fail (ProcessError.StartFailed { Command = "xdg-open"; Message = "not installed" }))

match revealFolder "/var/log" |> Flow.run host.Environment with
| Exit.Failure(Cause.Fail(ProcessError.StartFailed failure)) -> failure.Message |> shouldEqual "not installed"
| _ -> failwith "expected StartFailed"
```

A custom `IProcess` implements `Launch` alongside `Execute`. A host that cannot start desktop programs, such as a
browser worker, returns `ProcessError.StartFailed` from `Launch`.

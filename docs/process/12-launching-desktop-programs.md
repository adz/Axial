# Open an editor, file manager or URL

A desktop application often hands work to another program: open a file in an editor, show a folder in the file
manager, or open a URL in the browser. These launches differ from the rest of `Axial.Process`: the caller does not
wait for the child, does not capture its output, and does not own its lifetime. Closing the application leaves the
editor open.

- `Process.launch command` starts one command and returns once it has started.
- `Process.shellOpen target` opens a path or URL with the operating system's default handler: `ShellExecute` on
  Windows, `open` on macOS and `xdg-open` elsewhere.

Both return `Flow<'env, ProcessError, unit>` and go through the environment's `IProcess` service, so a failure to start
is the typed `ProcessError.StartFailed` and a test can assert the launch without starting anything. Building either
flow never throws; an invalid target is a typed failure when the flow runs.

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
nothing waits for the child. Interrupting the flow after the launch does not stop the child. A console program gets
its own window on Windows. Arguments go to the executable as a list, never through a shell, so a path with spaces or
`&` in it is one argument.

`Process.shellOpen` succeeds once the opener has started. On Linux and macOS the opener exits after handing the target
on, and a target with no registered handler is not reported. To reveal a file, open its folder.

## Opening untrusted targets

Opening a target can run a program. `ShellExecute` runs an `.exe`, `.bat` or `.cmd` file, `open` runs an `.app`
bundle or an executable script, and `xdg-open` hands the target to whatever the desktop associates with it. Treat a
path or URL that comes from a file, a network response or another user as untrusted: check that it is what the
application means to open, for example an `https:` URL or a folder the application owns, before passing it to
`Process.shellOpen`.

An opener reads a leading `-` as an option, so `Process.shellOpen` refuses an empty target or one that starts with
`-` with `ProcessError.ShellOpenRejected`, and starts nothing. Prefix a relative path with `./`.

```fsharp
let openDocsPage (url: string) =
    match Uri.TryCreate(url, UriKind.Absolute) with
    | true, uri when uri.Scheme = "https" -> Process.shellOpen uri.AbsoluteUri
    | _ -> Flow.fail (ProcessError.ShellOpenRejected { Target = url; Reason = "not an https URL" })
```

## Editors installed as scripts on Windows

Some editors install a script rather than an executable, for example `code.cmd` on Windows. Starting the script
directly fails with `StartFailed`. Do not wrap it in `cmd.exe /c`: `cmd.exe` parses the whole command line again and
treats `&`, `|`, `^` and `%` in a file name as shell syntax, so an untrusted path can run other commands. Start the
executable the script runs instead, which receives the path as one argument. `code.cmd` in
`Microsoft VS Code\bin` runs `Code.exe` in the folder above it:

```fsharp
let editorCommand (codeExecutable: string option) (path: string) =
    match codeExecutable with
    | Some executable -> Process.commandArgs executable [ path ]
    | None -> Process.commandArgs "code" [ path ]
```

## Test a launch

`Process.test ()` records every launch in `Launches` and succeeds by default. Set `LaunchHandler` to simulate a
failure:

```fsharp run
let host = ProcessHostEnvironment.test ()

openInEditor "notes.txt"
|> Flow.run host.Environment
|> shouldEqual (Exit.Success ())

match List.ofSeq host.Process.Launches with
| [ ProcessLaunch.Command command ] ->
    Process.arguments command |> shouldEqual [ "--goto"; "notes.txt" ]
| _ -> failwith "expected one launch"

let notInstalled = ProcessError.StartFailed { Command = "xdg-open"; Message = "not installed" }
host.Process.LaunchHandler <- Some(fun _ -> Flow.fail notInstalled)

match revealFolder "/var/log" |> Flow.run host.Environment with
| Exit.Failure(Cause.Fail(ProcessError.StartFailed failure)) ->
    failure.Message |> shouldEqual "not installed"
| _ -> failwith "expected StartFailed"

match Process.shellOpen "--version" |> Flow.run host.Environment with
| Exit.Failure(Cause.Fail(ProcessError.ShellOpenRejected rejection)) ->
    rejection.Target |> shouldEqual "--version"
| _ -> failwith "expected ShellOpenRejected"
```

A custom `IProcess` implements `Launch` alongside `Execute`. A host that cannot start desktop programs, such as a
browser worker, returns `ProcessError.StartFailed` from `Launch`.

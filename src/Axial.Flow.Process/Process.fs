namespace Axial.Flow.Process

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Globalization
open System.Text
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.Flow.PlatformService
open Axial.Flow.Console
open Axial.Flow.FileSystem

/// Identifies one standard output channel.
[<RequireQualifiedAccess>]
type OutputChannel = StdOut | StdErr

/// Selects arbitrary decoded chunks or complete decoded lines for streamed events.
[<RequireQualifiedAccess>]
type OutputFraming = Chunks | Lines

/// Supplies bytes to the first process stage.
[<RequireQualifiedAccess>]
type InputSource =
    | Empty
    | Text of string
    | Bytes of byte array
    | File of path: string
    | Read of read: (unit -> Async<byte array>)
    | Produce of produce: ((byte array -> Async<unit>) -> Async<unit>)

/// Receives bytes from a process topology. Capture limits are measured in bytes.
[<RequireQualifiedAccess>]
type OutputTarget =
    | Capture
    | CaptureTail of maxBytes: int
    | Console
    | Inherit
    | Discard
    | File of path: string
    | AppendFile of path: string
    | Callback of write: (byte array -> Async<unit>)
    | Sink of write: (byte array -> Async<unit>) * complete: (unit -> Async<unit>)
    | Tee of OutputTarget list

/// Internal configuration for one safely tokenized external command.
type internal CommandDefinition =
    internal
        { FileName: string
          Arguments: string list
          RedactedArguments: Map<int, string>
          WorkingDirectory: string option
          Environment: Map<string, string option>
          Encoding: Encoding
          SuccessCodes: Set<int> }

/// An immutable description of one command or a connected process topology and its execution policy.
type ProcessSpec =
    internal
        { Commands: CommandDefinition list
          StdIn: InputSource
          StdOut: OutputTarget
          StdErr: OutputTarget
          Connections: (int * int * bool) list
          Leaves: Set<int>
          MergeStdErr: bool
          Framing: OutputFraming
          Timeout: TimeSpan option }

/// Exact captured bytes plus their decoded text view and truncation status.
type CapturedOutput =
    { Text: string
      Bytes: byte array
      Truncated: bool }

/// A timestamped decoded output event attributed to one specification stage.
type ProcessOutput =
    { Stage: int
      Channel: OutputChannel
      Text: string
      Timestamp: DateTimeOffset }

/// The redacted command, exit decision, timing, and diagnostic stderr tail for one stage.
type StageResult =
    { Stage: int
      Command: string
      ExitCode: int
      Succeeded: bool
      StartedAt: DateTimeOffset
      Duration: TimeSpan
      StdErrTail: CapturedOutput }

/// The complete structured transcript returned by a process execution.
type ProcessResult =
    { ExitCode: int
      ExitCodes: int list
      StdOut: string
      StdErr: string
      StdOutCapture: CapturedOutput
      StdErrCapture: CapturedOutput
      Stages: StageResult list
      StartedAt: DateTimeOffset
      Duration: TimeSpan }

/// A redacted, serializable description of work that would be executed.
type ProcessPlan =
    { Commands: string list
      StdIn: string
      StdOut: string
      StdErr: string
      Connections: (int * int * bool) list
      MergeStdErr: bool
      Framing: OutputFraming
      Timeout: TimeSpan option }

/// Values emitted by a native process FlowStream.
[<RequireQualifiedAccess>]
type ProcessEvent =
    | Output of ProcessOutput
    | Completed of ProcessResult

/// Diagnostic details for a process that could not be started.
type ProcessStartFailure = { Command: string; Message: string }

/// Diagnostic details for an elapsed process deadline.
type ProcessTimeout = { Specification: string; Timeout: TimeSpan }

/// Diagnostic details for caller-initiated process cancellation.
type ProcessCancellation = { Message: string }

/// Diagnostic details for an unsuccessful process stage.
type StageFailure = { Stage: StageResult; Result: ProcessResult }

/// Diagnostic details for a process I/O failure.
type ProcessIoFailure = { Message: string }

/// A recoverable process startup, cancellation, stage, or I/O failure.
[<RequireQualifiedAccess>]
type ProcessError =
    | StartFailed of ProcessStartFailure
    | TimedOut of ProcessTimeout
    | Canceled of ProcessCancellation
    | StageFailed of StageFailure
    | IoFailed of ProcessIoFailure

/// Interprets process specifications as lazy Axial workflows for a concrete host platform.
type IProcess =
    /// Returns a lazy workflow that runs the specification when composed into a Flow runtime.
    abstract Run : specification: ProcessSpec -> Flow<unit, ProcessError, ProcessResult>
    /// Returns a lazy, backpressured stream of output and completion events.
    abstract Stream : specification: ProcessSpec -> FlowStream<unit, ProcessError, ProcessEvent>

type private AsyncRendezvous<'value>() =
    let gate = obj()
    let mutable pending: ('value * (unit -> unit)) option = None
    let mutable taker: ('value -> unit) option = None

    member _.Put(value: 'value) : Async<unit> =
        Async.FromContinuations(fun (success, error, _) ->
            let deliver =
                lock gate (fun () ->
                    match taker, pending with
                    | Some take, None -> taker <- None; Choice1Of2 take
                    | None, None -> pending <- Some(value, success); Choice2Of2()
                    | _ -> error (InvalidOperationException "Only one process event producer is supported."); Choice2Of2())
            match deliver with
            | Choice1Of2 take -> take value; success ()
            | Choice2Of2 () -> ())

    member _.Take() : Async<'value> =
        Async.FromContinuations(fun (success, error, _) ->
            let buffered =
                lock gate (fun () ->
                    match pending, taker with
                    | Some(value, acknowledge), None -> pending <- None; Some(value, acknowledge)
                    | None, None -> taker <- Some success; None
                    | _ -> error (InvalidOperationException "Only one process event consumer is supported."); None)
            match buffered with
            | Some(value, acknowledge) -> success value; acknowledge ()
            | None -> ())

type private ProcessStreamSession =
    { Events: AsyncRendezvous<Result<ProcessEvent, ProcessError>> }

type private ProcessStreamState =
    | NotStarted
    | Running of ProcessStreamSession
    | Finished

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module ProcessError =
    /// Formats a process error with stage-aware diagnostic context.
    /// <example><code>error |&gt; ProcessError.describe</code></example>
    let describe = function
        | ProcessError.StartFailed failure -> $"Could not start '{failure.Command}': {failure.Message}"
        | ProcessError.TimedOut failure -> $"Process specification '{failure.Specification}' timed out after {failure.Timeout}."
        | ProcessError.Canceled failure -> $"Process execution was canceled: {failure.Message}"
        | ProcessError.StageFailed failure ->
            let stage = failure.Stage
            let diagnostic = if stage.StdErrTail.Text = "" then "" else Environment.NewLine + stage.StdErrTail.Text
            $"Stage {stage.Stage} ({stage.Command}) exited with code {stage.ExitCode}.{diagnostic}"
        | ProcessError.IoFailed failure -> $"Process I/O failed: {failure.Message}"

    /// Returns a suitable host exit code for a process failure.
    /// <example><code>Environment.ExitCode &lt;- ProcessError.exitCode error</code></example>
    let exitCode = function
        | ProcessError.StageFailed failure -> failure.Stage.ExitCode
        | ProcessError.TimedOut _ -> 124
        | ProcessError.Canceled _ -> 130
        | ProcessError.StartFailed _
        | ProcessError.IoFailed _ -> 1

#if !FABLE_COMPILER
type private CaptureBuffer(limit: int option, tail: bool) =
    let gate = obj()
    let mutable truncated = false
    let mutable tailBytes = Array.empty<byte>
    let stream = new MemoryStream()
    let enabled = limit <> Some -1

    member _.Append(chunk: byte array) =
        if enabled then
            lock gate (fun () ->
                match limit with
                | None -> stream.Write(chunk, 0, chunk.Length)
                | Some maximum when maximum <= 0 -> truncated <- truncated || chunk.Length > 0
                | Some maximum when tail ->
                    let combined = Array.append tailBytes chunk
                    if combined.Length > maximum then
                        truncated <- true
                        tailBytes <- combined[combined.Length - maximum ..]
                    else tailBytes <- combined
                | Some maximum ->
                    let remaining = maximum - int stream.Length
                    if remaining <= 0 then truncated <- truncated || chunk.Length > 0
                    else
                        let count = min remaining chunk.Length
                        stream.Write(chunk, 0, count)
                        truncated <- truncated || count < chunk.Length)

    member _.Finish(encoding: Encoding) =
        lock gate (fun () ->
            let bytes = if tail then tailBytes else stream.ToArray()
            { Text = encoding.GetString bytes; Bytes = bytes; Truncated = truncated })
#endif

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Process =
    let private one command =
        { Commands = [ command ]; StdIn = InputSource.Empty
          StdOut = OutputTarget.Capture; StdErr = OutputTarget.Capture
          Connections = []; Leaves = Set.singleton 0
          MergeStdErr = false; Framing = OutputFraming.Chunks; Timeout = None }

    let private mapSingle transform (specification: ProcessSpec) =
        match specification.Commands with
        | [ command ] -> { specification with Commands = [ transform command ] }
        | _ -> invalidArg (nameof specification) "This operation configures one command and must be applied before commands are connected."

    let private single (specification: ProcessSpec) =
        match specification.Commands with
        | [ command ] -> command
        | _ -> invalidArg (nameof specification) "The supplied process specification must contain exactly one command."

    /// Creates a runnable, safely tokenized one-command process specification.
    /// <example><code>Process.command "git" [ "status"; "--short" ] |&gt; Process.run</code></example>
    let command fileName arguments =
        if String.IsNullOrWhiteSpace fileName then invalidArg (nameof fileName) "A command file name cannot be empty."
        { FileName = fileName; Arguments = arguments; RedactedArguments = Map.empty
          WorkingDirectory = None; Environment = Map.empty
          Encoding = Encoding.UTF8; SuccessCodes = Set.singleton 0 }
        |> one

    /// Returns the executable name of a one-command specification.
    let fileName specification = (single specification).FileName
    /// Returns the real argument values of a one-command specification.
    let arguments specification = (single specification).Arguments
    /// Returns the configured working directory of a one-command specification.
    let tryWorkingDirectory specification = (single specification).WorkingDirectory
    /// Returns environment overrides of a one-command specification.
    let environmentVariables specification = (single specification).Environment
    /// Returns accepted exit codes of a one-command specification.
    let acceptedExitCodes specification = (single specification).SuccessCodes

    /// Appends one ordinary argument.
    /// <example><code>command |&gt; Process.arg "--verbose"</code></example>
    let arg value specification = specification |> mapSingle (fun command -> { command with Arguments = command.Arguments @ [ value ] })

    /// Adds an argument whose value is replaced with <c>***</c> in rendered commands and transcripts.
    let secretArg value specification =
        specification |> mapSingle (fun command ->
            let index = command.Arguments.Length
            { command with Arguments = command.Arguments @ [ value ]; RedactedArguments = command.RedactedArguments.Add(index, "***") })

    /// Sets the working directory. <example><code>command |&gt; Process.workingDirectory repo</code></example>
    let workingDirectory path specification = specification |> mapSingle (fun command -> { command with WorkingDirectory = Some path })
    /// Sets an environment override. <example><code>command |&gt; Process.environment "CI" "true"</code></example>
    let environment name value specification = specification |> mapSingle (fun command -> { command with Environment = command.Environment.Add(name, Some value) })
    /// Removes an inherited environment variable. <example><code>command |&gt; Process.removeEnvironment "TOKEN"</code></example>
    let removeEnvironment name specification = specification |> mapSingle (fun command -> { command with Environment = command.Environment.Add(name, None) })
    /// Selects text decoding for this stage. <example><code>command |&gt; Process.encoding Encoding.Latin1</code></example>
    let encoding value specification = specification |> mapSingle (fun command -> { command with Encoding = value })

    /// Replaces the set of exit codes considered successful for this command.
    let successCodes values specification =
        let codes = Set.ofSeq values
        if Set.isEmpty codes then invalidArg (nameof values) "At least one successful exit code is required."
        specification |> mapSingle (fun command -> { command with SuccessCodes = codes })

    let private quote (value: string) =
        if value.Length > 0 && value |> Seq.forall (fun c -> Char.IsLetterOrDigit c || "-._/:=@+".Contains c) then value
        else "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""

    /// Renders one command definition with secret arguments redacted.
    let private renderCommand command =
        command.Arguments
        |> List.mapi (fun index value ->
            match command.RedactedArguments |> Map.tryFind index with
            | Some display -> display
            | None -> quote value)
        |> fun args -> String.concat " " (quote command.FileName :: args)

    /// Renders a redacted shell-like description of the complete process specification.
    let render (specification: ProcessSpec) = specification.Commands |> List.map renderCommand |> String.concat " | "

    /// Connects the current stdout to the next one-command specification's stdin.
    let pipe (next: ProcessSpec) (source: ProcessSpec) : ProcessSpec =
        let nextCommand = single next
        let target = source.Commands.Length
        let connections = source.Leaves |> Seq.map (fun stage -> stage, target, false) |> Seq.toList
        { source with Commands = source.Commands @ [ nextCommand ]; Connections = source.Connections @ connections; Leaves = Set.singleton target }
    /// Connects both stdout and stderr from the current final stage to the next command's stdin.
    let pipeBoth (next: ProcessSpec) (source: ProcessSpec) : ProcessSpec =
        if source.Leaves.Count <> 1 then invalidArg (nameof source) "pipeBothTo requires one current producer."
        let nextCommand = single next
        let target = source.Commands.Length
        let stage = Set.minElement source.Leaves
        { source with Commands = source.Commands @ [ nextCommand ]; Connections = source.Connections @ [ stage, target, true ]; Leaves = Set.singleton target }
    /// Creates a fan-in topology whose producers may be connected to one downstream command.
    let merge commands : ProcessSpec =
        match Seq.toList commands with
        | [] -> invalidArg (nameof commands) "A merge requires at least one command."
        | specifications ->
            let commands = specifications |> List.map single
            { Commands = commands; StdIn = InputSource.Empty
              StdOut = OutputTarget.Capture; StdErr = OutputTarget.Capture
              Connections = []; Leaves = Set.ofList [ 0 .. commands.Length - 1 ]
              MergeStdErr = false; Framing = OutputFraming.Lines; Timeout = None }
    /// Supplies stdin to the first stage.
    let stdin source (specification: ProcessSpec) =
        match specification.StdIn with
        | InputSource.Empty -> ()
        | _ -> invalidArg (nameof source) "A process topology can have only one primary input source."
        { specification with StdIn = source }
    /// Configures final stdout handling. <example><code>specification |&gt; Process.stdout OutputTarget.Console</code></example>
    let stdout destination (specification: ProcessSpec) =
        let rec validate = function
            | OutputTarget.CaptureTail maximum when maximum < 0 -> invalidArg (nameof destination) "Capture size cannot be negative."
            | OutputTarget.Tee items when items |> List.exists (function OutputTarget.Inherit -> true | _ -> false) ->
                invalidArg (nameof destination) "True inherited handles cannot be combined with tee targets."
            | OutputTarget.Tee items -> List.iter validate items
            | _ -> ()
        validate destination
        { specification with StdOut = destination }
    /// Configures combined stderr handling. <example><code>specification |&gt; Process.stderr (OutputTarget.CaptureTail 65536)</code></example>
    let stderr destination (specification: ProcessSpec) =
        let rec validate = function
            | OutputTarget.CaptureTail maximum when maximum < 0 -> invalidArg (nameof destination) "Capture size cannot be negative."
            | OutputTarget.Tee items when items |> List.exists (function OutputTarget.Inherit -> true | _ -> false) ->
                invalidArg (nameof destination) "True inherited handles cannot be combined with tee targets."
            | OutputTarget.Tee items -> List.iter validate items
            | _ -> ()
        validate destination
        { specification with StdErr = destination }
    /// Routes final stderr through the final stdout targets, like the intent of <c>2&gt;&amp;1</c>.
    let mergeStderr (specification: ProcessSpec) = { specification with MergeStdErr = true }
    /// Selects chunk or line event framing. <example><code>specification |&gt; Process.framing OutputFraming.Lines</code></example>
    let framing value (specification: ProcessSpec) : ProcessSpec = { specification with Framing = value }
    /// Sets the maximum execution time for the complete process topology.
    /// <example><code>specification |&gt; Process.timeout (TimeSpan.FromSeconds 30.0)</code></example>
    let timeout after (specification: ProcessSpec) : ProcessSpec =
        if after <= TimeSpan.Zero then invalidArg (nameof after) "A process timeout must be greater than zero."
        { specification with Timeout = Some after }
    /// Returns a redacted execution plan without starting a process.
    let plan (specification: ProcessSpec) : ProcessPlan =
        let input =
            match specification.StdIn with
            | InputSource.Empty -> "empty"
            | InputSource.Text text -> $"text ({text.Length} characters)"
            | InputSource.Bytes bytes -> $"bytes ({bytes.Length} bytes)"
            | InputSource.File path -> $"file: {path}"
            | InputSource.Read _ -> "asynchronous read"
            | InputSource.Produce _ -> "asynchronous producer"
        let rec target = function
            | OutputTarget.Capture -> "capture"
            | OutputTarget.CaptureTail maximum -> $"capture tail ({maximum} bytes)"
            | OutputTarget.Console -> "console"
            | OutputTarget.Inherit -> "inherited handle"
            | OutputTarget.Discard -> "discard"
            | OutputTarget.File path -> $"file: {path}"
            | OutputTarget.AppendFile path -> $"append file: {path}"
            | OutputTarget.Callback _ -> "callback"
            | OutputTarget.Sink _ -> "sink"
            | OutputTarget.Tee targets -> targets |> List.map target |> String.concat ", " |> sprintf "tee (%s)"
        { Commands = specification.Commands |> List.map renderCommand
          StdIn = input
          StdOut = target specification.StdOut
          StdErr = target specification.StdErr
          Connections = specification.Connections
          MergeStdErr = specification.MergeStdErr
          Framing = specification.Framing
          Timeout = specification.Timeout }

    let private validate result =
        match result.Stages |> List.tryFind (fun stage -> not stage.Succeeded) with
        | Some stage -> Error(ProcessError.StageFailed { Stage = stage; Result = result })
        | None -> Ok result

    /// Runs a process specification in the current Flow runtime.
    /// <example><code>specification |&gt; Process.run</code></example>
    let run<'env when 'env :> IHas<IProcess>> specification : Flow<'env, ProcessError, ProcessResult> =
        if specification.Leaves.Count <> 1 then invalidArg (nameof specification) "A process topology requires one final output stage. Connect merged producers to a consumer first."
        flow {
            let! service = Service<IProcess>.get()
            return! service.Run specification |> Flow.localEnv (fun _ -> ())
        }

    /// Runs a process specification with complete stdout and stderr capture.
    /// <example><code>Process.command "dotnet" [ "--info" ] |&gt; Process.capture</code></example>
    let capture<'env when 'env :> IHas<IProcess>> specification : Flow<'env, ProcessError, ProcessResult> =
        specification
        |> stdout OutputTarget.Capture
        |> stderr OutputTarget.Capture
        |> run

    /// Streams process events in the current Flow runtime. The last event is <c>Completed</c>.
    let stream<'env when 'env :> IHas<IProcess>> specification : FlowStream<'env, ProcessError, ProcessEvent> =
        FlowStream(fun environment cancellationToken ->
            let service = (environment :> IHas<IProcess>).Service
            let (FlowStream stream) = service.Stream specification
            stream () cancellationToken)

    let internal withRedactedArguments redacted specification =
        specification |> mapSingle (fun command -> { command with RedactedArguments = redacted })

#if !FABLE_COMPILER
    let private isInherit = function OutputTarget.Inherit -> true | _ -> false

    let private startInfo redirectOutput redirectError command =
        let info = ProcessStartInfo(command.FileName)
        command.Arguments |> List.iter info.ArgumentList.Add
        command.WorkingDirectory |> Option.iter (fun path -> info.WorkingDirectory <- path)
        command.Environment |> Map.iter (fun name value -> match value with Some v -> info.Environment[name] <- v | None -> info.Environment.Remove name |> ignore)
        info.RedirectStandardInput <- true; info.RedirectStandardOutput <- redirectOutput; info.RedirectStandardError <- redirectError
        info.UseShellExecute <- false; info.CreateNoWindow <- true
        info

    let private captureFor destination =
        let rec find = function
            | OutputTarget.Capture -> Some(CaptureBuffer(None, false))
            | OutputTarget.CaptureTail maximum -> Some(CaptureBuffer(Some maximum, true))
            | OutputTarget.Tee destinations -> destinations |> List.tryPick find
            | _ -> None
        find destination |> Option.defaultWith (fun () -> CaptureBuffer(Some -1, false))

    let private openFiles (fileSystem: IFileSystem) destination =
        let files = Dictionary<string * bool, FileStream>()
        let rec visit = function
            | OutputTarget.File path -> files.TryAdd((path, false), fileSystem.OpenFile(path, FileMode.Create, FileAccess.Write, FileShare.Read)) |> ignore
            | OutputTarget.AppendFile path -> files.TryAdd((path, true), fileSystem.OpenFile(path, FileMode.Append, FileAccess.Write, FileShare.Read)) |> ignore
            | OutputTarget.Tee items -> List.iter visit items
            | _ -> ()
        visit destination
        files

    let private writeDestination (console: IConsole) (gate: SemaphoreSlim) (files: Dictionary<string * bool, FileStream>) (channel: OutputChannel) destination (bytes: byte array) =
        let rec write = function
            | OutputTarget.Console
            | OutputTarget.Inherit ->
                let target = if channel = OutputChannel.StdOut then console.OpenStandardOutput() else console.OpenStandardError()
                target.WriteAsync(bytes, 0, bytes.Length)
            | OutputTarget.File path -> files[(path, false)].WriteAsync(bytes, 0, bytes.Length)
            | OutputTarget.AppendFile path -> files[(path, true)].WriteAsync(bytes, 0, bytes.Length)
            | OutputTarget.Callback callback -> Async.StartAsTask(callback bytes) :> Task
            | OutputTarget.Sink(callback, _) -> Async.StartAsTask(callback bytes) :> Task
            | OutputTarget.Tee items -> task { for item in items do do! write item }
            | _ -> Task.CompletedTask
        task {
            do! gate.WaitAsync()
            try do! write destination
            finally gate.Release() |> ignore
        }

    let private completeDestination destination =
        let rec complete = function
            | OutputTarget.Sink(_, finish) -> Async.StartAsTask(finish()) :> Task
            | OutputTarget.Tee items -> task { for item in items do do! complete item }
            | _ -> Task.CompletedTask
        complete destination

    let private waitForExit (proc: Diagnostics.Process) cancellationToken =
#if NETSTANDARD2_1
        Task.Run((fun () -> proc.WaitForExit()), cancellationToken)
#else
        proc.WaitForExitAsync(cancellationToken)
#endif

    /// Creates a live process service using an explicit clock for transcript timestamps and durations.
    let live (clock: IClock) (fileSystem: IFileSystem) (console: IConsole) : IProcess =
        let execute observer (specification: ProcessSpec) =
            let execution =
                    flow {
                      let! cancellationToken = Flow.Runtime.cancellationToken
                      let! outcome = async {
                        return! task {
                            let processes = ResizeArray<Diagnostics.Process>()
                            let started = ResizeArray<DateTimeOffset>()
                            let stdoutCapture = captureFor specification.StdOut
                            let stderrCapture = captureFor specification.StdErr
                            let stderrTails = specification.Commands |> List.map (fun _ -> CaptureBuffer(Some(64 * 1024), true)) |> List.toArray
                            let stdoutFiles = openFiles fileSystem specification.StdOut
                            let stderrFiles = openFiles fileSystem specification.StdErr
                            let startedAt = clock.UtcNow()
                            let terminateProcesses () =
                                for proc in processes do
                                    try
                                        if not proc.HasExited then
        #if NETSTANDARD2_1
                                            proc.Kill()
        #else
                                            proc.Kill(entireProcessTree = true)
        #endif
                                    with _ -> ()
                            try
                                try
                                    use registration = cancellationToken.Register terminateProcesses
                                    for index, command in specification.Commands |> List.indexed do
                                        let isFinal = index = specification.Commands.Length - 1
                                        let hasOutputConnection = specification.Connections |> List.exists (fun (source, _, _) -> source = index)
                                        let hasErrorConnection = specification.Connections |> List.exists (fun (source, _, both) -> source = index && both)
                                        let redirectOutput = hasOutputConnection || not (isFinal && isInherit specification.StdOut)
                                        let inheritError = isInherit specification.StdErr && not (isFinal && specification.MergeStdErr)
                                        let redirectError = hasErrorConnection || not inheritError
                                        let proc = new Diagnostics.Process(StartInfo = startInfo redirectOutput redirectError command)
                                        if not (proc.Start()) then raise (Exception $"Could not start {command.FileName}.")
                                        processes.Add proc; started.Add(clock.UtcNow())
                                    let copies =
                                        specification.Connections
                                        |> List.groupBy (fun (_, target, _) -> target)
                                        |> List.map (fun (targetIndex, connections) ->
                                            task {
                                                let target = processes[targetIndex].StandardInput.BaseStream
                                                use gate = new SemaphoreSlim(1, 1)
                                                let writeChunk (chunk: byte array) =
                                                    task {
                                                        do! gate.WaitAsync cancellationToken
                                                        try do! target.WriteAsync(chunk, 0, chunk.Length, cancellationToken)
                                                        finally gate.Release() |> ignore
                                                    }
                                                let pump (tail: CaptureBuffer option) (source: Stream) =
                                                    task {
                                                        let buffer = Array.zeroCreate<byte> 4096
                                                        use pendingLine = new MemoryStream()
                                                        let frameLines = connections.Length > 1 && specification.Framing = OutputFraming.Lines
                                                        let mutable reading = true
                                                        while reading do
                                                            let! count = source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                                                            if count = 0 then
                                                                reading <- false
                                                                if frameLines && pendingLine.Length > 0L then do! writeChunk (pendingLine.ToArray())
                                                            else
                                                                let chunk = buffer[.. count - 1]
                                                                tail |> Option.iter (fun capture -> capture.Append chunk)
                                                                if frameLines then
                                                                    for value in chunk do
                                                                        pendingLine.WriteByte value
                                                                        if value = byte '\n' then
                                                                            do! writeChunk (pendingLine.ToArray())
                                                                            pendingLine.SetLength 0L
                                                                else do! writeChunk chunk
                                                    }
                                                let pumps =
                                                    [| for sourceIndex, _, both in connections do
                                                        yield pump None processes[sourceIndex].StandardOutput.BaseStream
                                                        if both then yield pump (Some stderrTails[sourceIndex]) processes[sourceIndex].StandardError.BaseStream |]
                                                let! _ = Task.WhenAll pumps
                                                processes[targetIndex].StandardInput.Close()
                                            } :> Task)
                                        |> List.toArray
                                    let targets = specification.Connections |> Seq.map (fun (_, target, _) -> target) |> Set.ofSeq
                                    let roots = [ for index in 0 .. processes.Count - 1 do if not (targets.Contains index) then yield index ]
                                    let inputWrites : Task array =
                                        match specification.StdIn, roots with
                                        | InputSource.Empty, roots ->
                                            roots |> List.iter (fun index -> processes[index].StandardInput.Close())
                                            Array.empty
                                        | _, _ :: _ :: _ -> invalidArg "specification" "A primary input source requires exactly one root command."
                                        | source, [ root ] ->
                                            [| task {
                                                let target = processes[root].StandardInput.BaseStream
                                                let write bytes = target.WriteAsync(bytes, 0, bytes.Length, cancellationToken) |> Async.AwaitTask
                                                match source with
                                                | InputSource.Empty -> ()
                                                | InputSource.Text text -> do! write (specification.Commands[root].Encoding.GetBytes text) |> Async.StartAsTask
                                                | InputSource.Bytes bytes -> do! write bytes |> Async.StartAsTask
                                                | InputSource.File path ->
                                                    use source = fileSystem.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.Read)
                                                    do! source.CopyToAsync(target, 81920, cancellationToken)
                                                | InputSource.Read read ->
                                                    let! bytes = Async.StartAsTask(read(), cancellationToken = cancellationToken)
                                                    do! target.WriteAsync(bytes, 0, bytes.Length, cancellationToken)
                                                | InputSource.Produce produce ->
                                                    do! Async.StartAsTask(produce write, cancellationToken = cancellationToken)
                                                processes[root].StandardInput.Close()
                                            } :> Task |]
                                        | _, [] -> invalidArg "specification" "A process topology has no root command."

                                    use observerGate = new SemaphoreSlim(1, 1)
                                    use stdoutGate = new SemaphoreSlim(1, 1)
                                    use stderrGate = new SemaphoreSlim(1, 1)
                                    let read (stage: int) (channel: OutputChannel) destination (capture: CaptureBuffer) (stream: Stream) (encoding: Encoding) =
                                        task {
                                            let buffer = Array.zeroCreate<byte> 4096
                                            let decoder = encoding.GetDecoder()
                                            let characters = Array.zeroCreate<char> (encoding.GetMaxCharCount buffer.Length)
                                            let pendingLine = StringBuilder()
                                            let files = if channel = OutputChannel.StdOut then stdoutFiles else stderrFiles
                                            let destinationGate = if channel = OutputChannel.StdOut then stdoutGate else stderrGate
                                            let publishText (text: string) =
                                                match observer with
                                                | Some publish when text <> "" ->
                                                    task {
                                                        do! observerGate.WaitAsync cancellationToken
                                                        try do! Async.StartAsTask(publish { Stage = stage; Channel = channel; Text = text; Timestamp = clock.UtcNow() }, cancellationToken = cancellationToken)
                                                        finally observerGate.Release() |> ignore
                                                    }
                                                | _ -> Task.FromResult(())
                                            let decodeAndPublish (bytes: byte array) (count: int) (flush: bool) =
                                                task {
                                                    let characterCount = decoder.GetChars(bytes, 0, count, characters, 0, flush)
                                                    let text = String(characters, 0, characterCount)
                                                    match specification.Framing with
                                                    | OutputFraming.Chunks -> do! publishText text
                                                    | OutputFraming.Lines ->
                                                        pendingLine.Append text |> ignore
                                                        let mutable newline = pendingLine.ToString().IndexOf '\n'
                                                        while newline >= 0 do
                                                            let line = pendingLine.ToString(0, newline + 1)
                                                            pendingLine.Remove(0, newline + 1) |> ignore
                                                            do! publishText line
                                                            newline <- pendingLine.ToString().IndexOf '\n'
                                                        if flush && pendingLine.Length > 0 then
                                                            do! publishText (pendingLine.ToString())
                                                            pendingLine.Clear() |> ignore
                                                }
                                            let mutable reading = true
                                            while reading do
                                                let! count = stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                                                if count = 0 then
                                                    reading <- false
                                                    do! decodeAndPublish Array.empty 0 true
                                                else
                                                    let chunk = buffer[.. count - 1]
                                                    capture.Append chunk
                                                    if channel = OutputChannel.StdErr then stderrTails[stage].Append chunk
                                                    do! writeDestination console destinationGate files channel destination chunk
                                                    do! decodeAndPublish chunk chunk.Length false
                                        }
                                    let reads = ResizeArray<Task>()
                                    let finalEncoding = (List.last specification.Commands).Encoding
                                    let completionTasks =
                                        processes
                                        |> Seq.map (fun proc ->
                                            task {
                                                do! waitForExit proc cancellationToken
                                                return clock.UtcNow()
                                            })
                                        |> Seq.toArray
                                    if not (isInherit specification.StdOut) then
                                        reads.Add(read (processes.Count - 1) OutputChannel.StdOut specification.StdOut stdoutCapture processes[processes.Count - 1].StandardOutput.BaseStream finalEncoding)
                                    for index in 0 .. processes.Count - 1 do
                                        let stderrPiped = specification.Connections |> List.exists (fun (source, _, both) -> source = index && both)
                                        if not stderrPiped then
                                            let merged = specification.MergeStdErr && index = processes.Count - 1
                                            let destination = if merged then specification.StdOut else specification.StdErr
                                            let capture = if merged then stdoutCapture else stderrCapture
                                            if not (isInherit destination) then
                                                reads.Add(read index OutputChannel.StdErr destination capture processes[index].StandardError.BaseStream specification.Commands[index].Encoding)
                                    do! Task.WhenAll(Array.concat [ copies; inputWrites; reads.ToArray() ])
                                    do! completeDestination specification.StdOut
                                    do! completeDestination specification.StdErr
                                    let! completedStages = Task.WhenAll completionTasks
                                    cancellationToken.ThrowIfCancellationRequested()
                                    let completed = completedStages |> Array.max
                                    let outCapture = stdoutCapture.Finish finalEncoding
                                    let errCapture = stderrCapture.Finish finalEncoding
                                    let stages =
                                        specification.Commands
                                        |> List.mapi (fun index command ->
                                            { Stage = index; Command = renderCommand command; ExitCode = processes[index].ExitCode
                                              Succeeded = command.SuccessCodes.Contains processes[index].ExitCode
                                              StartedAt = started[index]; Duration = completedStages[index] - started[index]
                                              StdErrTail = stderrTails[index].Finish command.Encoding })
                                    return Ok { ExitCode = processes[processes.Count - 1].ExitCode; ExitCodes = stages |> List.map _.ExitCode
                                                StdOut = outCapture.Text; StdErr = errCapture.Text; StdOutCapture = outCapture; StdErrCapture = errCapture
                                                Stages = stages; StartedAt = startedAt; Duration = completed - startedAt }
                                with
                                | :? OperationCanceledException as error -> return Error(ProcessError.Canceled { Message = error.Message })
                                | error when processes.Count = 0 || processes.Count < specification.Commands.Length ->
                                    let command = specification.Commands[processes.Count] |> renderCommand
                                    return Error(ProcessError.StartFailed { Command = command; Message = error.Message })
                                | error -> return Error(ProcessError.IoFailed { Message = error.Message })
                            finally
                                terminateProcesses ()
                                processes |> Seq.iter (fun proc -> proc.Dispose())
                                stdoutFiles.Values |> Seq.iter (fun stream -> stream.Dispose())
                                stderrFiles.Values |> Seq.iter (fun stream -> stream.Dispose())
                        } |> Async.AwaitTask
                      }
                      return! outcome |> Result.bind validate
                    }
            match specification.Timeout with
            | Some timeout -> execution |> Flow.Runtime.timeout timeout (ProcessError.TimedOut { Specification = render specification; Timeout = timeout })
            | None -> execution

        let stream specification =
            let step state =
                match state with
                | Finished -> Flow.ok None
                | Running session ->
                    flow {
                        let! item = session.Events.Take()
                        match item with
                        | Ok(ProcessEvent.Completed result) -> return Some(ProcessEvent.Completed result, Finished)
                        | Ok event -> return Some(event, Running session)
                        | Error error -> return! Flow.fail error
                    }
                | NotStarted ->
                    flow {
                        let events = AsyncRendezvous<Result<ProcessEvent, ProcessError>>()
                        let observer output = events.Put(Ok(ProcessEvent.Output output))
                        let producer =
                            flow {
                                let! result = execute (Some observer) specification
                                do! events.Put(Ok(ProcessEvent.Completed result))
                                return result
                            }
                        let! fiber = Flow.fork producer
                        do! Flow.addFinalizerAsync (fun _ -> async {
                            fiber.InterruptSource.Cancel()
                            let! _ = fiber.ExitTask |> Async.AwaitTask
                            return ()
                        })
                        let session = { Events = events }
                        let! item = events.Take()
                        match item with
                        | Ok(ProcessEvent.Completed result) -> return Some(ProcessEvent.Completed result, Finished)
                        | Ok event -> return Some(event, Running session)
                        | Error error -> return! Flow.fail error
                    }
            FlowStream.unfoldFlow step NotStarted

        { new IProcess with
            member _.Run specification = execute None specification
            member _.Stream specification = stream specification }

    /// Builds a live process service from an explicit clock as a layer.
    let layer (clock: IClock) (fileSystem: IFileSystem) (console: IConsole) : Layer<unit, Never, IProcess> =
        Layer.succeed (live clock fileSystem console)
#endif

module DSL =
    /// Marks an interpolated command value for redaction in plans and transcripts.
    [<Sealed>]
    type SecretArgument internal (value: obj) =
        member internal _.Value = value

    type private ParsedToken = { Value: string; Display: string }

    let private formatValue format (value: obj) =
        if isNull value then ""
        else
            match value with
            | :? IFormattable as formattable -> formattable.ToString(format, CultureInfo.InvariantCulture)
            | _ -> string value

    let private parseCommandLine (format: string) (values: obj array) =
        let tokens = ResizeArray<ParsedToken>()
        let actual = StringBuilder()
        let display = StringBuilder()
        let mutable quote: char option = None
        let mutable escaped = false
        let mutable started = false

        let finish () =
            if started then
                tokens.Add { Value = actual.ToString(); Display = display.ToString() }
                actual.Clear() |> ignore
                display.Clear() |> ignore
                started <- false

        let literal (character: char) =
            if escaped then
                actual.Append character |> ignore
                display.Append character |> ignore
                started <- true
                escaped <- false
            else
                match quote, character with
                | _, '\\' -> escaped <- true
                | None, ('\'' | '"') -> quote <- Some character; started <- true
                | Some current, character when current = character -> quote <- None
                | None, character when Char.IsWhiteSpace character -> finish ()
                | _ -> actual.Append character |> ignore; display.Append character |> ignore; started <- true

        let hole (index: int) (valueFormat: string) =
            if index < 0 || index >= values.Length then invalidArg (nameof format) "Interpolation index is out of range."
            match values[index] with
            | :? SecretArgument as secret ->
                actual.Append(formatValue valueFormat secret.Value) |> ignore
                display.Append("***") |> ignore
            | value ->
                let text = formatValue valueFormat value
                actual.Append text |> ignore
                display.Append text |> ignore
            started <- true

        let mutable index = 0
        while index < format.Length do
            if format[index] = '{' && index + 1 < format.Length && format[index + 1] = '{' then
                literal '{'; index <- index + 2
            elif format[index] = '}' && index + 1 < format.Length && format[index + 1] = '}' then
                literal '}'; index <- index + 2
            elif format[index] = '{' then
                let closing = format.IndexOf('}', index + 1)
                if closing < 0 then invalidArg (nameof format) "Unclosed interpolation hole."
                let descriptor = format.Substring(index + 1, closing - index - 1)
                let separator = descriptor.IndexOfAny [| ','; ':' |]
                let indexText = if separator < 0 then descriptor else descriptor.Substring(0, separator)
                let valueIndex = Int32.Parse(indexText, CultureInfo.InvariantCulture)
                if valueIndex < 0 || valueIndex >= values.Length then invalidArg (nameof format) "Interpolation index is out of range."
                let valueFormat =
                    let colon = descriptor.IndexOf ':'
                    if colon < 0 then null else descriptor.Substring(colon + 1)
                hole valueIndex valueFormat
                index <- closing + 1
            else
                literal format[index]
                index <- index + 1

        if escaped then invalidArg (nameof format) "A command line cannot end with an escape character."
        if quote.IsSome then invalidArg (nameof format) "A command line contains an unclosed quote."
        finish ()
        if tokens.Count = 0 then invalidArg (nameof format) "A command line cannot be empty."

        let executable = tokens[0]
        if executable.Value <> executable.Display then invalidArg (nameof format) "The executable cannot be secret."
        let arguments = tokens |> Seq.skip 1 |> Seq.map _.Value |> Seq.toList
        let redacted =
            tokens
            |> Seq.skip 1
            |> Seq.mapi (fun index token -> index, token)
            |> Seq.choose (fun (index, token) -> if token.Value = token.Display then None else Some(index, token.Display))
            |> Map.ofSeq
        Process.command executable.Value arguments |> Process.withRedactedArguments redacted

    type EndpointConnector =
        static member Connect(source: InputSource, next: ProcessSpec) = Process.stdin source next
        static member Connect(source: ProcessSpec, next: ProcessSpec) = Process.pipe next source
        static member Connect(source: ProcessSpec, target: OutputTarget) = source |> Process.stdout target |> Process.run
        static member ToSpecification(source: ProcessSpec) = source

    /// Connects a typed input, command, specification, or terminal output endpoint.
    let inline (=>) (source: ^source) (destination: ^destination) : ^result =
        ((^source or ^destination or EndpointConnector) : (static member Connect : ^source * ^destination -> ^result) (source, destination))

    /// Builds a command-line-shaped command while preserving every interpolation hole as one argument.
    let cmd (commandLine: FormattableString) = parseCommandLine commandLine.Format (commandLine.GetArguments())

    /// Parses a fixed command line. Prefer <c>cmd $"...{value}"</c> whenever values are inserted.
    let cmdText (commandLine: string) = parseCommandLine commandLine Array.empty

    /// Builds a vertical specification from safely parsed command templates.
    let pipe (commandLines: seq<FormattableString>) =
        let commands = commandLines |> Seq.map cmd |> Seq.toList
        match commands with
        | [] -> invalidArg (nameof commandLines) "A specification requires at least one command."
        | head :: tail -> tail |> List.fold (fun specification next -> Process.pipe next specification) head

    /// Creates line-framed fan-in producers ready to connect to one consumer.
    let merge commands = Process.merge commands
    /// Creates raw-byte fan-in producers with explicitly nondeterministic chunk interleaving.
    let mergeBytes commands = Process.merge commands |> Process.framing OutputFraming.Chunks

    let private shellCommand (executable: string) (prefix: string -> string list) (placeholder: int -> string) (program: FormattableString) =
        let values = program.GetArguments()
        let valueFormats = Array.create values.Length null
        let format = program.Format
        let script = StringBuilder()
        let mutable index = 0
        while index < format.Length do
            if format[index] = '{' && index + 1 < format.Length && format[index + 1] = '{' then
                script.Append '{' |> ignore
                index <- index + 2
            elif format[index] = '}' && index + 1 < format.Length && format[index + 1] = '}' then
                script.Append '}' |> ignore
                index <- index + 2
            elif format[index] = '{' then
                let closing = format.IndexOf('}', index + 1)
                if closing < 0 then invalidArg (nameof program) "Unclosed shell interpolation hole."
                let descriptor = format.Substring(index + 1, closing - index - 1)
                let separator = descriptor.IndexOfAny [| ','; ':' |]
                let indexText = if separator < 0 then descriptor else descriptor.Substring(0, separator)
                let valueIndex = Int32.Parse(indexText, CultureInfo.InvariantCulture)
                if valueIndex < 0 || valueIndex >= values.Length then invalidArg (nameof program) "Shell interpolation index is out of range."
                let colon = descriptor.IndexOf ':'
                if colon >= 0 then valueFormats[valueIndex] <- descriptor.Substring(colon + 1)
                script.Append(placeholder valueIndex) |> ignore
                index <- closing + 1
            else
                script.Append format[index] |> ignore
                index <- index + 1
        let actualValues =
            values
            |> Array.mapi (fun index -> function
                | :? SecretArgument as secret -> formatValue valueFormats[index] secret.Value
                | value -> formatValue valueFormats[index] value)
            |> Array.toList
        let arguments = prefix (script.ToString()) @ actualValues
        let firstValue = arguments.Length - actualValues.Length
        let redacted =
            values
            |> Array.mapi (fun index value -> index, value)
            |> Array.choose (fun (index, value) -> match value with :? SecretArgument -> Some(firstValue + index, "***") | _ -> None)
            |> Map.ofArray
        Process.command executable arguments |> Process.withRedactedArguments redacted

    /// Builds a Bash program with interpolation values passed as positional arguments.
    let bash program = shellCommand "bash" (fun script -> [ "-o"; "pipefail"; "-c"; script; "axial" ]) (fun index -> $"\"${{{index + 1}}}\"") program
    /// Builds a POSIX shell program with interpolation values passed as positional arguments.
    let sh program = shellCommand "sh" (fun script -> [ "-c"; script; "axial" ]) (fun index -> $"\"${{{index + 1}}}\"") program
    /// Builds a PowerShell 7 program with interpolation values passed through <c>$args</c>.
    let pwsh program = shellCommand "pwsh" (fun script -> [ "-NoProfile"; "-NonInteractive"; "-Command"; script ]) (fun index -> $"$args[{index}]") program

    /// Builds an explicitly assembled Bash program. Values in the text are not escaped by Axial.
    let bashText program = Process.command "bash" [ "-o"; "pipefail"; "-c"; program ]
    /// Builds an explicitly assembled POSIX shell program. Values in the text are not escaped by Axial.
    let shText program = Process.command "sh" [ "-c"; program ]
    /// Builds an explicitly assembled PowerShell 7 program. Values in the text are not escaped by Axial.
    let pwshText program = Process.command "pwsh" [ "-NoProfile"; "-NonInteractive"; "-Command"; program ]

    /// Marks an interpolated value for diagnostic redaction.
    let secret value = SecretArgument(box value)
    let cwd path command = Process.workingDirectory path command
    let env name value command = Process.environment name value command
    let private toSpecification (source: ProcessSpec) = source
    /// Supplies a primary input source to a command or specification.
    let stdin source topology = toSpecification topology |> Process.stdin source
    /// Connects stdout from a command or specification to the next command's stdin.
    let pipeTo next source = toSpecification source |> Process.pipe next
    /// Configures final stdout on the specification.
    let stdout target source = toSpecification source |> Process.stdout target
    /// Configures combined stderr on the specification.
    let stderr target source = toSpecification source |> Process.stderr target
    /// Sets the maximum execution time for a command or specification.
    /// <example><code>cmd $"service-device" |&gt; timeout (TimeSpan.FromSeconds 30.0) |&gt; capture</code></example>
    let timeout after source = toSpecification source |> Process.timeout after
    /// Runs a command or specification in the current Flow runtime.
    let run source = toSpecification source |> Process.run
    /// Runs a command or specification and captures stdout and stderr.
    let capture source = toSpecification source |> Process.capture
    /// Forwards stdout and stderr to the host console while retaining structured completion data.
    let console source = toSpecification source |> Process.stdout OutputTarget.Console |> Process.stderr OutputTarget.Console |> Process.run
    /// Produces a bounded stream of structured output and completion events.
    let stream source = toSpecification source |> Process.stream
    /// Connects both stdout and stderr from the current final stage to the next command.
    let pipeBothTo next source = toSpecification source |> Process.pipeBoth next
    /// Routes final stderr through final stdout targets.
    let mergeStderr source = toSpecification source |> Process.mergeStderr
    /// Writes final stdout to a truncating file and runs the specification.
    let writeTo path source = toSpecification source |> Process.stdout (OutputTarget.File path) |> Process.run
    /// Writes final stdout to an appending file and runs the specification.
    let appendTo path source = toSpecification source |> Process.stdout (OutputTarget.AppendFile path) |> Process.run
    /// Captures commands concurrently with a fixed upper bound while preserving input order.
    let captureParallel maximumConcurrency commands =
        if maximumConcurrency <= 0 then invalidArg (nameof maximumConcurrency) "Maximum concurrency must be positive."
        let runBatch batch =
            batch
            |> List.map (fun specification -> specification |> Process.run |> Flow.map List.singleton)
            |> function
                | [] -> Flow.ok []
                | head :: tail ->
                    tail
                    |> List.fold (fun combined next -> Flow.zipPar combined next |> Flow.map (fun (left, right) -> left @ right)) head
        commands
        |> Seq.toList
        |> List.chunkBySize maximumConcurrency
        |> List.map runBatch
        |> Flow.sequence
        |> Flow.map List.concat

    [<RequireQualifiedAccess>]
    module Input =
        /// Supplies no bytes and closes stdin.
        let empty = InputSource.Empty
        /// Supplies encoded text incrementally when execution begins.
        let text value = InputSource.Text value
        /// Supplies exact bytes when execution begins.
        let bytes value = InputSource.Bytes value
        /// Streams bytes from a file when execution begins.
        let file path = InputSource.File path
        /// Reads one asynchronous byte block when execution begins.
        let read producer = InputSource.Read producer
        /// Produces asynchronous byte blocks with backpressure.
        let produce producer = InputSource.Produce producer
#if !FABLE_COMPILER
        /// Adapts a .NET stream into a backpressured input source.
        let stream (source: Stream) =
            InputSource.Produce(fun write -> async {
                let buffer = Array.zeroCreate<byte> 81920
                let mutable reading = true
                while reading do
                    let! count = source.ReadAsync(buffer, 0, buffer.Length) |> Async.AwaitTask
                    if count = 0 then reading <- false
                    else do! write buffer[.. count - 1]
            })
#endif

    [<RequireQualifiedAccess>]
    module Output =
        /// Retains all output bytes and their decoded text view.
        let capture = OutputTarget.Capture
        /// Retains only the final maximum number of bytes.
        let captureTail maximum = OutputTarget.CaptureTail maximum
        /// Forwards redirected bytes through the host console streams.
        let console = OutputTarget.Console
        /// Gives the child the host handle directly; this channel cannot also be observed or teed.
        let inheritHandles = OutputTarget.Inherit
        /// Drains and discards output.
        let discard = OutputTarget.Discard
        /// Writes output to a truncating file.
        let file path = OutputTarget.File path
        /// Writes output to an appending file.
        let appendFile path = OutputTarget.AppendFile path
        /// Sends exact byte chunks to an asynchronous backpressured callback.
        let callback write = OutputTarget.Callback write
        /// Sends every byte chunk to each target in order.
        let tee targets = OutputTarget.Tee(List.ofSeq targets)
#if !FABLE_COMPILER
        /// Adapts a writable .NET stream into an output target.
        let stream (target: Stream) =
            OutputTarget.Sink(
                (fun bytes -> target.WriteAsync(bytes, 0, bytes.Length) |> Async.AwaitTask),
                (fun () -> target.FlushAsync() |> Async.AwaitTask))
        /// Decodes output incrementally into a .NET text writer.
        let textWriter (encoding: Encoding) (target: TextWriter) =
            let decoder = encoding.GetDecoder()
            OutputTarget.Sink(
                (fun bytes -> async {
                    let chars = Array.zeroCreate<char> (encoding.GetMaxCharCount bytes.Length)
                    let count = decoder.GetChars(bytes, 0, bytes.Length, chars, 0, false)
                    do! target.WriteAsync(chars, 0, count) |> Async.AwaitTask
                }),
                (fun () -> async {
                    let chars = Array.zeroCreate<char> (encoding.GetMaxCharCount 0)
                    let count = decoder.GetChars(Array.empty, 0, 0, chars, 0, true)
                    if count > 0 then do! target.WriteAsync(chars, 0, count) |> Async.AwaitTask
                    do! target.FlushAsync() |> Async.AwaitTask
                }))
#endif

#if !FABLE_COMPILER
type ScriptEnvironment =
    { Process: IProcess }
    interface IHas<IProcess> with member this.Service = this.Process

[<RequireQualifiedAccess>]
module Script =
    let private describeCause = function
        | Cause.Fail error -> ProcessError.describe error
        | Cause.Die error -> error.ToString()
        | Cause.Interrupt -> "Interrupted."
        | cause -> $"{cause}"

    /// Runs a process workflow with live services, writes failures through the supplied console, and returns a host exit code.
    let run (console: IConsole) (workflow: Flow<ScriptEnvironment, ProcessError, 'value>) : int =
        match workflow.RunSynchronously({ Process = Process.live Clock.live FileSystem.live console }) with
        | Exit.Success _ -> 0
        | Exit.Failure(Cause.Fail error) ->
            console.WriteErrorLine(ProcessError.describe error)
            ProcessError.exitCode error
        | Exit.Failure cause ->
            console.WriteErrorLine(describeCause cause)
            1
#endif

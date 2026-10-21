namespace Axial.Process

open System
open System.Collections.Generic
open System.ComponentModel
open System.Diagnostics
open System.IO
open System.Globalization
open System.Text
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.PlatformService
open Axial.Console
open Axial.FileSystem

/// Identifies one standard output channel.
[<RequireQualifiedAccess>]
type OutputChannel =
    | StdOut
    | StdErr

/// Selects arbitrary decoded chunks or complete decoded lines for streamed events.
[<RequireQualifiedAccess>]
type OutputFraming =
    | Chunks
    | Lines

/// Supplies bytes to the first process stage.
[<RequireQualifiedAccess>]
type InputSource =
    | Empty
    | Text of string
    | Bytes of byte array
    | File of path: string
    | Inherit
    | Stream of FlowStream<unit, string, byte array>

    /// The source kind, rendered without reflection so it stays safe under NativeAOT.
    override this.ToString() =
        match this with
        | Empty -> "Empty"
        | Text text -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Text ({0} chars)", text.Length))
        | Bytes bytes -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Bytes ({0} bytes)", bytes.Length))
        | File path -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "File {0}", path))
        | Inherit -> "Inherit"
        | Stream _ -> "Stream"

/// Receives bytes independently of the transcript retention policy.
[<RequireQualifiedAccess>]
type OutputTarget =
    | Console
    | Inherit
    | Discard
    | File of path: string
    | AppendFile of path: string
    | Callback of write: (byte array -> Flow<unit, string, unit>)
    | Sink of write: (byte array -> Flow<unit, string, unit>) * complete: (unit -> Flow<unit, string, unit>)
    | Tee of OutputTarget list
    | SinkFactory of create: (unit -> (byte array -> Flow<unit, string, unit>) * (unit -> Flow<unit, string, unit>))

    /// The target kind, rendered without reflection so it stays safe under NativeAOT.
    override this.ToString() =
        match this with
        | Console -> "Console"
        | Inherit -> "Inherit"
        | Discard -> "Discard"
        | File path -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "File {0}", path))
        | AppendFile path -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "AppendFile {0}", path))
        | Callback _ -> "Callback"
        | Sink _ -> "Sink"
        | SinkFactory _ -> "SinkFactory"
        | Tee targets ->
            "Tee("
            + (targets |> List.map (fun target -> target.ToString()) |> String.concat ", ")
            + ")"


/// Controls captured bytes independently of external output routing.
[<RequireQualifiedAccess>]
type OutputRetention =
    | Nothing
    | Tail of maxBytes: int
    | All

/// Fallback settings; explicit command settings always take precedence.
type StageDefaults = internal {
    WorkingDirectory: string option
    Environment: Map<string, string option>
    Encoding: Encoding option
    SuccessCodes: Set<int> option
    Timeout: TimeSpan option option
    StdOutCapture: OutputRetention option
    StdErrCapture: OutputRetention option
}

/// Configuration for one safely tokenized command.
type internal CommandDefinition = {
    FileName: string
    Arguments: string list
    RedactedArguments: Map<int, string>
    WorkingDirectory: string option
    Environment: Map<string, string option>
    Encoding: Encoding option
    SuccessCodes: Set<int> option
    Timeout: TimeSpan option option
    StdIn: InputSource option
    StdOut: OutputTarget option
    StdErr: OutputTarget option
    StdOutCapture: OutputRetention option
    StdErrCapture: OutputRetention option
}

/// One immutable command; configure it before connecting stages.
type ProcessCommand = internal ProcessCommand of CommandDefinition

/// Requests a cooperative action from a running process stage.
[<RequireQualifiedAccess>]
type StopRequest =
    /// Asks a Windows GUI process to close its main window.
    | CloseMainWindow
    /// Sends SIGINT on Unix or CTRL_C_EVENT on Windows.
    | Interrupt
    /// Sends SIGTERM on Unix or CTRL_BREAK_EVENT on Windows.
    | Terminate
    /// Sends SIGHUP on Unix.
    | Hangup
    /// Closes the stage's redirected standard-input pipe.
    | CloseStdIn
    /// Sends a raw numeric signal on Unix.
    | Signal of int

    override this.ToString() =
        match this with
        | CloseMainWindow -> "CloseMainWindow"
        | Interrupt -> "Interrupt"
        | Terminate -> "Terminate"
        | Hangup -> "Hangup"
        | CloseStdIn -> "CloseStdIn"
        | Signal number -> "Signal " + number.ToString(CultureInfo.InvariantCulture)

[<EditorBrowsable(EditorBrowsableState.Never)>]
type IProcessStartedObserver =
    abstract Invoke:
        stage: int *
        processId: int *
        command: string *
        startedAt: DateTimeOffset *
        close: (StopRequest -> Async<Result<unit, string>>) -> Async<unit>

[<RequireQualifiedAccess>]
type internal ExecutionMode =
    | Batch
    | Streaming
    | Host

/// An immutable process topology, configured without starting any processes.
type ProcessSpec = internal {
    Commands: CommandDefinition list
    Connections: (int * int * bool) list
    Leaves: Set<int>
    Defaults: StageDefaults
    MergeStdErr: bool
    Framing: OutputFraming
    MaxLineLength: int
    MaxOutputBytes: int64 option * int64 option
    OverallTimeout: TimeSpan option
    DrainTimeout: TimeSpan option
    OnStarted: IProcessStartedObserver option
    StopGracefully: (StopRequest * TimeSpan) list
    Mode: ExecutionMode
}

/// Captured byte-exact output and its decoded view. Available is false for inherited handles.
type CapturedOutput = {
    Text: string
    Bytes: byte array
    Truncated: bool
    Available: bool
}

/// Decoded output with original and effective channel attribution.
type ProcessOutput = {
    Stage: int
    ObservedChannel: OutputChannel
    OriginalChannel: OutputChannel
    Text: string
    Timestamp: DateTimeOffset
}

/// Completion information and captured output for one stage.
type StageResult = {
    Stage: int
    ProcessId: int
    Command: string
    ExitCode: int
    Succeeded: bool
    StartedAt: DateTimeOffset
    Duration: TimeSpan
    StdOutCapture: CapturedOutput
    StdErrCapture: CapturedOutput
}

/// Completion of the complete topology; ExitCode belongs to the final stage.
type ProcessResult = {
    ExitCode: int
    ExitCodes: int list
    StdOut: string
    StdErr: string
    StdOutCapture: CapturedOutput
    StdErrCapture: CapturedOutput
    Stages: StageResult list
    StartedAt: DateTimeOffset
    Duration: TimeSpan
} with

    member this.StdOutTruncated = this.StdOutCapture.Truncated
    member this.StdErrTruncated = this.StdErrCapture.Truncated
    member this.StdOutCaptureAvailable = this.StdOutCapture.Available
    member this.StdErrCaptureAvailable = this.StdErrCapture.Available

    override this.ToString() =
        let codes = this.ExitCodes |> List.map string |> String.concat ","
        let duration = this.Duration.TotalMilliseconds.ToString("F0", Globalization.CultureInfo.InvariantCulture)
        (System.String.Format(Globalization.CultureInfo.InvariantCulture, "ProcessResult(exit {0} [{1}], {2} ms, stdout {3} chars, stderr {4} chars)", this.ExitCode, codes, duration, this.StdOut.Length, this.StdErr.Length))

/// A resolved per-command plan, including fallback provenance.
type ProcessStagePlan = {
    Stage: int
    Command: string
    WorkingDirectory: string option
    Environment: Map<string, string option>
    Encoding: string
    SuccessCodes: Set<int>
    Timeout: TimeSpan option
    StdIn: string
    StdOut: string
    StdErr: string
    StdOutCapture: OutputRetention
    StdErrCapture: OutputRetention
    Provenance: Map<string, string>
}

/// A redacted execution plan; inspection performs no operational effects.
type ProcessPlan = {
    Commands: string list
    Stages: ProcessStagePlan list
    Connections: (int * int * bool) list
    MergeStdErr: bool
    Framing: OutputFraming
    MaxLineLength: int
    MaxOutputBytes: int64 option * int64 option
    OverallTimeout: TimeSpan option
    DrainTimeout: TimeSpan option
}

type ProcessStartFailure = { Command: string; Message: string }

type ProcessTimeout = {
    Specification: string
    Timeout: TimeSpan
}

type ProcessStageTimeout = {
    Stage: int
    Command: string
    Timeout: TimeSpan
}

type StageFailure = {
    Stage: StageResult
    Result: ProcessResult
}

type ProcessIoFailure = { Message: string }

type ProcessLineFailure = {
    Stage: int
    OriginalChannel: OutputChannel
    Limit: int
}

type ProcessOutputFailure = { Stage: int; Channel: OutputChannel; Limit: int64; Command: string }

type ProcessObservationFailure = { Stage: int; Channel: OutputChannel }

type ProcessStopFailure = { Stage: int; Request: StopRequest; Message: string }

/// Expected process errors. Runtime cancellation remains Cause.Interrupt.
[<RequireQualifiedAccess>]
type ProcessError =
    | StartFailed of ProcessStartFailure
    | StageTimedOut of ProcessStageTimeout
    | OverallTimedOut of ProcessTimeout
    | StageFailed of StageFailure
    | IoFailed of ProcessIoFailure
    | InputFailed of string
    | OutputFailed of string
    | LineTooLong of ProcessLineFailure
    | OutputTooLarge of ProcessOutputFailure
    | ObservationUnavailable of ProcessObservationFailure
    | StopFailed of ProcessStopFailure

    override this.ToString() =
        match this with
        | StartFailed failure -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Could not start '{0}': {1}", failure.Command, failure.Message))
        | StageTimedOut failure -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Stage {0} ({1}) exceeded {2}.", failure.Stage, failure.Command, failure.Timeout))
        | OverallTimedOut failure ->
            (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Process specification '{0}' timed out after {1}.", failure.Specification, failure.Timeout))
        | StageFailed failure ->
            let stage = failure.Stage

            let diagnostic =
                if stage.StdErrCapture.Text = "" then
                    ""
                else
                    Environment.NewLine + stage.StdErrCapture.Text

            (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Stage {0} ({1}) exited with code {2}.{3}", stage.Stage, stage.Command, stage.ExitCode, diagnostic))
        | IoFailed failure -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Process I/O failed: {0}", failure.Message))
        | InputFailed cause -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Process input failed: {0}", cause))
        | OutputFailed cause -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Process output failed: {0}", cause))
        | LineTooLong failure ->
            let channel =
                match failure.OriginalChannel with
                | OutputChannel.StdOut -> "stdout"
                | OutputChannel.StdErr -> "stderr"

            (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Stage {0} {1} exceeded {2} UTF-16 code units per line.", failure.Stage, channel, failure.Limit))
        | OutputTooLarge failure ->
            let channel = if failure.Channel = OutputChannel.StdOut then "stdout" else "stderr"
            (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Stage {0} ({1}) {2} exceeded {3} bytes.", failure.Stage, failure.Command, channel, failure.Limit))
        | ObservationUnavailable failure ->
            let channel =
                match failure.Channel with
                | OutputChannel.StdOut -> "stdout"
                | OutputChannel.StdErr -> "stderr"

            (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Stage {0} {1} uses inherited handles and cannot be observed.", failure.Stage, channel))
        | StopFailed failure ->
            (System.String.Format(Globalization.CultureInfo.InvariantCulture, "Could not send {0} to stage {1}: {2}", failure.Request, failure.Stage, failure.Message))

/// A stage that has started and can receive cooperative stop requests.
type ProcessStarted = {
    Stage: int
    ProcessId: int
    Command: string
    StartedAt: DateTimeOffset
    Close: StopRequest -> Flow<unit, ProcessError, unit>
}

/// Executes lazily, awaiting each observation with backpressure.
type IProcess =
    abstract Execute:
        specification: ProcessSpec * observe: (ProcessOutput -> Async<unit>) -> Flow<unit, ProcessError, ProcessResult>

#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type IHasProcess =
    abstract Process: IProcess

[<Sealed>]
type SecretArgument internal (value: obj) =
    member internal _.Value = value

[<RequireQualifiedAccess>]
type ProcessCompletion =
    | Completed of ProcessResult
    | StoppedEarly

[<RequireQualifiedAccess>]
type ProcessStreamError<'error> =
    | ProcessFailed of ProcessError
    | ConsumerFailed of 'error

/// Cold output plus completion. Each terminal executes a fresh run.
type ProcessStream<'env, 'value> =
    internal | ProcessStream of
        (unit
                -> FlowStream<'env, ProcessError, 'value> *
                (unit -> ProcessCompletion) *
                Flow<'env, ProcessError, unit> *
                (unit -> Cause<ProcessError> option))

[<RequireQualifiedAccess>]
module ProcessError =
    let describe (error: ProcessError) = error.ToString()

    let exitCode =
        function
        | ProcessError.StageFailed failure -> failure.Stage.ExitCode
        | ProcessError.StageTimedOut _
        | ProcessError.OverallTimedOut _ -> 124
        | _ -> 1

[<RequireQualifiedAccess>]
module StageDefaults =
    let empty: StageDefaults = {
        WorkingDirectory = None
        Environment = Map.empty
        Encoding = None
        SuccessCodes = None
        Timeout = None
        StdOutCapture = None
        StdErrCapture = None
    }

    let workingDirectory path (settings: StageDefaults) = {
        settings with
            WorkingDirectory = Some path
    }

    let environment name value (settings: StageDefaults) = {
        settings with
            Environment = settings.Environment.Add(name, Some value)
    }

    let removeEnvironment name (settings: StageDefaults) = {
        settings with
            Environment = settings.Environment.Add(name, None)
    }

    let encoding value (settings: StageDefaults) = { settings with Encoding = Some value }

    let successCodes values (settings: StageDefaults) =
        if Set.isEmpty values then
            invalidArg (nameof values) "At least one success code is required."

        {
            settings with
                SuccessCodes = Some values
        }

    let timeout after (settings: StageDefaults) =
        if after <= TimeSpan.Zero then
            invalidArg (nameof after) "Timeout must be positive."

        {
            settings with
                Timeout = Some(Some after)
        }

    let internal checkRetention =
        function
        | OutputRetention.Tail size when size < 0 -> invalidArg "maxBytes" "Capture size cannot be negative."
        | _ -> ()

    let stdoutCapture policy (settings: StageDefaults) =
        checkRetention policy

        {
            settings with
                StdOutCapture = Some policy
        }

    let stderrCapture policy (settings: StageDefaults) =
        checkRetention policy

        {
            settings with
                StdErrCapture = Some policy
        }

/// Static conversion used by public inline APIs, without reflection.
[<EditorBrowsable(EditorBrowsableState.Never)>]
type ProcessInput =
    static member ToSpecification(specification: ProcessSpec) = specification

    static member ToSpecification(ProcessCommand command) = {
        Commands = [ command ]
        Connections = []
        Leaves = Set.singleton 0
        Defaults = StageDefaults.empty
        MergeStdErr = false
        Framing = OutputFraming.Chunks
        MaxLineLength = 1048576
        MaxOutputBytes = None, None
        OverallTimeout = None
        DrainTimeout = None
        OnStarted = None
        StopGracefully = []
        Mode = ExecutionMode.Batch
    }

    static member Map(transform: ProcessSpec -> ProcessSpec, specification: ProcessSpec) = transform specification

    static member Map(transform: ProcessSpec -> ProcessSpec, command: ProcessCommand) =
        let mapped = transform (ProcessInput.ToSpecification command)
        ProcessCommand(List.exactlyOne mapped.Commands)

module internal Configuration =
    let fallback explicit defaults builtIn =
        explicit |> Option.orElse defaults |> Option.defaultValue builtIn

    let combine (prior: StageDefaults) (later: StageDefaults) : StageDefaults = {
        WorkingDirectory = later.WorkingDirectory |> Option.orElse prior.WorkingDirectory
        Environment = Map.fold (fun state key value -> Map.add key value state) prior.Environment later.Environment
        Encoding = later.Encoding |> Option.orElse prior.Encoding
        SuccessCodes = later.SuccessCodes |> Option.orElse prior.SuccessCodes
        Timeout = later.Timeout |> Option.orElse prior.Timeout
        StdOutCapture = later.StdOutCapture |> Option.orElse prior.StdOutCapture
        StdErrCapture = later.StdErrCapture |> Option.orElse prior.StdErrCapture
    }

    let resolve (specification: ProcessSpec) (command: CommandDefinition) : CommandDefinition =
        let defaults = specification.Defaults
        let host = specification.Mode = ExecutionMode.Host
        let route = if host then OutputTarget.Console else OutputTarget.Discard
        let stdout = command.StdOut |> Option.defaultValue route
        let stderr = command.StdErr |> Option.defaultValue route

        let capture target explicit defaults fallback =
            let inherited =
                match target with
                | OutputTarget.Inherit -> true
                | _ -> false

            match explicit |> Option.orElse defaults with
            | Some value when inherited && value <> OutputRetention.Nothing ->
                invalidArg "capture" "Inherited handles cannot be captured."
            | Some value -> value
            | None when inherited -> OutputRetention.Nothing
            | None -> fallback

        let stdoutDefault =
            match specification.Mode with
            | ExecutionMode.Batch -> OutputRetention.All
            | ExecutionMode.Streaming -> OutputRetention.Tail 65536
            | ExecutionMode.Host -> OutputRetention.Nothing

        {
            command with
                WorkingDirectory = command.WorkingDirectory |> Option.orElse defaults.WorkingDirectory
                Environment =
                    Map.fold (fun state key value -> Map.add key value state) defaults.Environment command.Environment
                Encoding = Some(fallback command.Encoding defaults.Encoding Encoding.UTF8)
                SuccessCodes = Some(fallback command.SuccessCodes defaults.SuccessCodes (Set.singleton 0))
                Timeout = Some(fallback command.Timeout defaults.Timeout None)
                StdIn = Some(defaultArg command.StdIn InputSource.Empty)
                StdOut = Some stdout
                StdErr = Some stderr
                StdOutCapture = Some(capture stdout command.StdOutCapture defaults.StdOutCapture stdoutDefault)
                StdErrCapture =
                    Some(capture stderr command.StdErrCapture defaults.StdErrCapture (OutputRetention.Tail 65536))
        }

    let validate (specification: ProcessSpec) =
        let stdoutCap, stderrCap = specification.MaxOutputBytes
        for command in specification.Commands do
            if stdoutCap.IsSome && (match command.StdOut with Some OutputTarget.Inherit -> true | _ -> false) then
                invalidArg "specification" "A stdout byte cap cannot be enforced on inherited stdout."
            if stderrCap.IsSome && (match command.StdErr with Some OutputTarget.Inherit -> true | _ -> false) then
                invalidArg "specification" "A stderr byte cap cannot be enforced on inherited stderr."
        for source, target, both in specification.Connections do
            if specification.Commands[source].StdOut.IsSome then
                invalidArg "source" "Piped stdout already has an explicit destination."

            if both && specification.Commands[source].StdErr.IsSome then
                invalidArg "source" "Piped stderr already has an explicit destination."

            if specification.Commands[target].StdIn.IsSome then
                invalidArg "target" "Pipe-fed stdin already has an explicit source."

        if specification.MergeStdErr then
            let final = Set.minElement specification.Leaves

            if specification.Commands[final].StdErr.IsSome then
                invalidArg "specification" "Merged stderr already has an explicit destination."

        specification

    let renderCommand (command: CommandDefinition) =
        let quote (value: string) =
            if
                value.Length > 0
                && value |> Seq.forall (fun c -> Char.IsLetterOrDigit c || "-._/:=@+".Contains c)
            then
                value
            else
                "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""

        command.Arguments
        |> List.mapi (fun i value -> command.RedactedArguments |> Map.tryFind i |> Option.defaultValue (quote value))
        |> fun arguments -> String.concat " " (quote command.FileName :: arguments)



/// Static facade helpers keep immutable internals out of public inline expansion.
type ProcessInput with
    static member MergeStderr(specification: ProcessSpec) =
        if specification.Leaves.Count <> 1 then
            invalidArg "source" "Merging stderr requires one final command."

        {
            specification with
                MergeStdErr = true
        }
        |> Configuration.validate

    static member Framing(value, specification: ProcessSpec) = { specification with Framing = value }

    static member MaxLineLength(value, specification: ProcessSpec) =
        if value <= 0 then
            invalidArg "value" "Line length must be positive."

        {
            specification with
                MaxLineLength = value
        }

    static member MaxOutputBytes(stdout, stderr, specification: ProcessSpec) =
        for value in [ stdout; stderr ] do
            match value with
            | Some limit when limit < 0L -> invalidArg "value" "Output byte limits must be non-negative."
            | _ -> ()
        { specification with MaxOutputBytes = stdout, stderr } |> Configuration.validate

    static member Defaults(defaults, specification: ProcessSpec) = {
        specification with
            Defaults = Configuration.combine specification.Defaults defaults
    }

    static member OverallTimeout(after, specification: ProcessSpec) =
        if after <= TimeSpan.Zero then
            invalidArg "after" "Timeout must be positive."

        {
            specification with
                OverallTimeout = Some after
        }

    static member OnStarted(handler, specification: ProcessSpec) = { specification with OnStarted = Some handler }

    static member StopGracefully(steps, specification: ProcessSpec) = { specification with StopGracefully = steps }

    static member DrainTimeout(after, specification: ProcessSpec) =
        if after <= TimeSpan.Zero then
            invalidArg "after" "Drain timeout must be positive."

        {
            specification with
                DrainTimeout = Some after
        }

    static member Render(specification: ProcessSpec) =
        specification.Commands
        |> List.map Configuration.renderCommand
        |> String.concat " | "

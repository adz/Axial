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
    | Inherit
    | ReadOnce of read: (unit -> Async<byte array>)
    | Produce of produce: ((byte array -> Async<unit>) -> Async<unit>)

    /// The source kind, rendered without reflection so it stays safe under NativeAOT.
    override this.ToString() =
        match this with
        | Empty -> "Empty"
        | Text text -> $"Text ({text.Length} chars)"
        | Bytes bytes -> $"Bytes ({bytes.Length} bytes)"
        | File path -> $"File {path}"
        | Inherit -> "Inherit"
        | ReadOnce _ -> "ReadOnce"
        | Produce _ -> "Produce"

/// Receives bytes independently of the transcript retention policy.
[<RequireQualifiedAccess>]
type OutputTarget =
    | Console
    | Inherit
    | Discard
    | File of path: string
    | AppendFile of path: string
    | Callback of write: (byte array -> Async<unit>)
    | Sink of write: (byte array -> Async<unit>) * complete: (unit -> Async<unit>)
    | Tee of OutputTarget list
    | SinkFactory of create: (unit -> (byte array -> Async<unit>) * (unit -> Async<unit>))

    /// The target kind, rendered without reflection so it stays safe under NativeAOT.
    override this.ToString() =
        match this with
        | Console -> "Console"
        | Inherit -> "Inherit"
        | Discard -> "Discard"
        | File path -> $"File {path}"
        | AppendFile path -> $"AppendFile {path}"
        | Callback _ -> "Callback"
        | Sink _ -> "Sink"
        | SinkFactory _ -> "SinkFactory"
        | Tee targets -> "Tee(" + (targets |> List.map (fun target -> target.ToString()) |> String.concat ", ") + ")"


/// Controls captured bytes independently of external output routing.
[<RequireQualifiedAccess>]
type OutputRetention = Nothing | Tail of maxBytes: int | All

/// Fallback settings; explicit command settings always take precedence.
type StageDefaults = internal {
    WorkingDirectory: string option
    Environment: Map<string, string option>
    Encoding: Encoding option
    SuccessCodes: Set<int> option
    Timeout: TimeSpan option option
    StdOutCapture: OutputRetention option
    StdErrCapture: OutputRetention option }

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
    StdErrCapture: OutputRetention option }

/// One immutable command; configure it before connecting stages.
type ProcessCommand = internal ProcessCommand of CommandDefinition

[<RequireQualifiedAccess>]
type internal ExecutionMode = Batch | Streaming | Host

/// An immutable process topology, configured without starting any processes.
type ProcessSpec = internal {
    Commands: CommandDefinition list
    Connections: (int * int * bool) list
    Leaves: Set<int>
    Defaults: StageDefaults
    MergeStdErr: bool
    Framing: OutputFraming
    MaxLineLength: int
    OverallTimeout: TimeSpan option
    DrainTimeout: TimeSpan option
    Mode: ExecutionMode }

/// Captured byte-exact output and its decoded view. Available is false for inherited handles.
type CapturedOutput = {
    Text: string
    Bytes: byte array
    Truncated: bool
    Available: bool }

/// Decoded output with original and effective channel attribution.
type ProcessOutput = {
    Stage: int
    ObservedChannel: OutputChannel
    OriginalChannel: OutputChannel
    Text: string
    Timestamp: DateTimeOffset }

/// Completion information and captured output for one stage.
type StageResult = {
    Stage: int
    Command: string
    ExitCode: int
    Succeeded: bool
    StartedAt: DateTimeOffset
    Duration: TimeSpan
    StdOutCapture: CapturedOutput
    StdErrCapture: CapturedOutput }

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
    Duration: TimeSpan } with
    member this.StdOutTruncated = this.StdOutCapture.Truncated
    member this.StdErrTruncated = this.StdErrCapture.Truncated
    member this.StdOutCaptureAvailable = this.StdOutCapture.Available
    member this.StdErrCaptureAvailable = this.StdErrCapture.Available
    override this.ToString() =
        let codes = this.ExitCodes |> List.map string |> String.concat ","
        $"ProcessResult(exit {this.ExitCode} [{codes}], {this.Duration.TotalMilliseconds:F0} ms, stdout {this.StdOut.Length} chars, stderr {this.StdErr.Length} chars)"

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
    Provenance: Map<string,string> }

/// A redacted execution plan; inspection performs no operational effects.
type ProcessPlan = {
    Commands: string list
    Stages: ProcessStagePlan list
    Connections: (int * int * bool) list
    MergeStdErr: bool
    Framing: OutputFraming
    MaxLineLength: int
    OverallTimeout: TimeSpan option
    DrainTimeout: TimeSpan option }

type ProcessStartFailure = { Command: string; Message: string }
type ProcessTimeout = { Specification: string; Timeout: TimeSpan }
type ProcessStageTimeout = { Stage: int; Command: string; Timeout: TimeSpan }
type StageFailure = { Stage: StageResult; Result: ProcessResult }
type ProcessIoFailure = { Message: string }
type ProcessLineFailure = { Stage: int; OriginalChannel: OutputChannel; Limit: int }
type ProcessObservationFailure = { Stage: int; Channel: OutputChannel }

/// Expected process errors. Runtime cancellation remains Cause.Interrupt.
[<RequireQualifiedAccess>]
type ProcessError =
    | StartFailed of ProcessStartFailure
    | StageTimedOut of ProcessStageTimeout
    | OverallTimedOut of ProcessTimeout
    | StageFailed of StageFailure
    | IoFailed of ProcessIoFailure
    | LineTooLong of ProcessLineFailure
    | ObservationUnavailable of ProcessObservationFailure
    override this.ToString() =
        match this with
        | StartFailed failure -> $"Could not start '{failure.Command}': {failure.Message}"
        | StageTimedOut failure -> $"Stage {failure.Stage} ({failure.Command}) exceeded {failure.Timeout}."
        | OverallTimedOut failure -> $"Process specification '{failure.Specification}' timed out after {failure.Timeout}."
        | StageFailed failure ->
            let stage = failure.Stage
            let diagnostic = if stage.StdErrCapture.Text = "" then "" else Environment.NewLine + stage.StdErrCapture.Text
            $"Stage {stage.Stage} ({stage.Command}) exited with code {stage.ExitCode}.{diagnostic}"
        | IoFailed failure -> $"Process I/O failed: {failure.Message}"
        | LineTooLong failure ->
            let channel = match failure.OriginalChannel with OutputChannel.StdOut -> "stdout" | OutputChannel.StdErr -> "stderr"
            $"Stage {failure.Stage} {channel} exceeded {failure.Limit} UTF-16 code units per line."
        | ObservationUnavailable failure ->
            let channel = match failure.Channel with OutputChannel.StdOut -> "stdout" | OutputChannel.StdErr -> "stderr"
            $"Stage {failure.Stage} {channel} uses inherited handles and cannot be observed."

/// Executes lazily, awaiting each observation with backpressure.
type IProcess =
    abstract Execute: specification: ProcessSpec * observe: (ProcessOutput -> Async<unit>) -> Flow<unit, ProcessError, ProcessResult>

#if FABLE_COMPILER
[<Fable.Core.Mangle>]
#endif
type IHasProcess =
    abstract Process: IProcess

[<Sealed>]
type SecretArgument internal (value: obj) =
    member internal _.Value = value

#if !FABLE_COMPILER
type ProcessHostEnvironment = {
    Process: IProcess
    Console: IConsole
    Clock: IClock } with
    interface IHasProcess with member this.Process = this.Process
    interface IHasConsole with member this.Console = this.Console
    interface IHasClock with member this.Clock = this.Clock
#endif

[<RequireQualifiedAccess>]
type ProcessCompletion = Completed of ProcessResult | StoppedEarly

[<RequireQualifiedAccess>]
type ProcessStreamError<'error> = ProcessFailed of ProcessError | ConsumerFailed of 'error

/// Cold output plus completion. Each terminal executes a fresh run.
type ProcessStream<'env,'value> =
    internal ProcessStream of (unit -> FlowStream<'env,ProcessError,'value> * (unit -> ProcessCompletion) * Flow<'env,ProcessError,unit> * (unit -> Cause<ProcessError> option))

[<RequireQualifiedAccess>]
module ProcessError =
    let describe (error: ProcessError) = error.ToString()
    let exitCode = function
        | ProcessError.StageFailed failure -> failure.Stage.ExitCode
        | ProcessError.StageTimedOut _ | ProcessError.OverallTimedOut _ -> 124
        | _ -> 1

[<RequireQualifiedAccess>]
module StageDefaults =
    let empty: StageDefaults = {
        WorkingDirectory = None; Environment = Map.empty; Encoding = None; SuccessCodes = None
        Timeout = None; StdOutCapture = None; StdErrCapture = None }
    let workingDirectory path (settings: StageDefaults) = { settings with WorkingDirectory = Some path }
    let environment name value (settings: StageDefaults) = { settings with Environment = settings.Environment.Add(name,Some value) }
    let removeEnvironment name (settings: StageDefaults) = { settings with Environment = settings.Environment.Add(name,None) }
    let encoding value (settings: StageDefaults) = { settings with Encoding = Some value }
    let successCodes values (settings: StageDefaults) =
        if Set.isEmpty values then invalidArg (nameof values) "At least one success code is required."
        { settings with SuccessCodes = Some values }
    let timeout after (settings: StageDefaults) =
        if after <= TimeSpan.Zero then invalidArg (nameof after) "Timeout must be positive."
        { settings with Timeout = Some(Some after) }
    let internal checkRetention = function
        | OutputRetention.Tail size when size < 0 -> invalidArg "maxBytes" "Capture size cannot be negative."
        | _ -> ()
    let stdoutCapture policy (settings: StageDefaults) =
        checkRetention policy
        { settings with StdOutCapture = Some policy }
    let stderrCapture policy (settings: StageDefaults) =
        checkRetention policy
        { settings with StdErrCapture = Some policy }

/// Static conversion used by public inline APIs, without reflection.
[<EditorBrowsable(EditorBrowsableState.Never)>]
type ProcessInput =
    static member ToSpecification(specification: ProcessSpec) = specification
    static member ToSpecification(ProcessCommand command) = {
        Commands = [command]; Connections = []; Leaves = Set.singleton 0
        Defaults = StageDefaults.empty; MergeStdErr = false; Framing = OutputFraming.Chunks
        MaxLineLength = 1048576; OverallTimeout = None; DrainTimeout = None; Mode = ExecutionMode.Batch }
    static member Map(transform: ProcessSpec -> ProcessSpec, specification: ProcessSpec) = transform specification
    static member Map(transform: ProcessSpec -> ProcessSpec, command: ProcessCommand) =
        let mapped = transform (ProcessInput.ToSpecification command)
        ProcessCommand (List.exactlyOne mapped.Commands)

module internal Configuration =
    let fallback explicit defaults builtIn = explicit |> Option.orElse defaults |> Option.defaultValue builtIn
    let combine (prior: StageDefaults) (later: StageDefaults): StageDefaults = {
        WorkingDirectory = later.WorkingDirectory |> Option.orElse prior.WorkingDirectory
        Environment = Map.fold (fun state key value -> Map.add key value state) prior.Environment later.Environment
        Encoding = later.Encoding |> Option.orElse prior.Encoding
        SuccessCodes = later.SuccessCodes |> Option.orElse prior.SuccessCodes
        Timeout = later.Timeout |> Option.orElse prior.Timeout
        StdOutCapture = later.StdOutCapture |> Option.orElse prior.StdOutCapture
        StdErrCapture = later.StdErrCapture |> Option.orElse prior.StdErrCapture }
    let resolve (specification: ProcessSpec) (command: CommandDefinition): CommandDefinition =
        let defaults = specification.Defaults
        let host = specification.Mode = ExecutionMode.Host
        let route = if host then OutputTarget.Console else OutputTarget.Discard
        let stdout = command.StdOut |> Option.defaultValue route
        let stderr = command.StdErr |> Option.defaultValue route
        let capture target explicit defaults fallback =
            let inherited = match target with OutputTarget.Inherit -> true | _ -> false
            match explicit |> Option.orElse defaults with
            | Some value when inherited && value <> OutputRetention.Nothing -> invalidArg "capture" "Inherited handles cannot be captured."
            | Some value -> value
            | None when inherited -> OutputRetention.Nothing
            | None -> fallback
        let stdoutDefault = match specification.Mode with ExecutionMode.Batch -> OutputRetention.All | ExecutionMode.Streaming -> OutputRetention.Tail 65536 | ExecutionMode.Host -> OutputRetention.Nothing
        { command with
            WorkingDirectory = command.WorkingDirectory |> Option.orElse defaults.WorkingDirectory
            Environment = Map.fold (fun state key value -> Map.add key value state) defaults.Environment command.Environment
            Encoding = Some(fallback command.Encoding defaults.Encoding Encoding.UTF8)
            SuccessCodes = Some(fallback command.SuccessCodes defaults.SuccessCodes (Set.singleton 0))
            Timeout = Some(fallback command.Timeout defaults.Timeout None)
            StdIn = Some(defaultArg command.StdIn InputSource.Empty)
            StdOut = Some stdout; StdErr = Some stderr
            StdOutCapture = Some(capture stdout command.StdOutCapture defaults.StdOutCapture stdoutDefault)
            StdErrCapture = Some(capture stderr command.StdErrCapture defaults.StdErrCapture (OutputRetention.Tail 65536)) }
    let validate (specification: ProcessSpec) =
        for source,target,both in specification.Connections do
            if specification.Commands[source].StdOut.IsSome then invalidArg "source" "Piped stdout already has an explicit destination."
            if both && specification.Commands[source].StdErr.IsSome then invalidArg "source" "Piped stderr already has an explicit destination."
            if specification.Commands[target].StdIn.IsSome then invalidArg "target" "Pipe-fed stdin already has an explicit source."
        if specification.MergeStdErr then
            let final = Set.minElement specification.Leaves
            if specification.Commands[final].StdErr.IsSome then invalidArg "specification" "Merged stderr already has an explicit destination."
        specification
    let renderCommand (command: CommandDefinition) =
        let quote (value: string) =
            if value.Length > 0 && value |> Seq.forall (fun c -> Char.IsLetterOrDigit c || "-._/:=@+".Contains c) then value
            else "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
        command.Arguments
        |> List.mapi (fun i value -> command.RedactedArguments |> Map.tryFind i |> Option.defaultValue (quote value))
        |> fun arguments -> String.concat " " (quote command.FileName :: arguments)



/// Static facade helpers keep immutable internals out of public inline expansion.
type ProcessInput with
    static member MergeStderr(specification: ProcessSpec) =
        if specification.Leaves.Count<>1 then invalidArg "source" "Merging stderr requires one final command."
        {specification with MergeStdErr=true} |> Configuration.validate
    static member Framing(value,specification: ProcessSpec) = {specification with Framing=value}
    static member MaxLineLength(value,specification: ProcessSpec) =
        if value<=0 then invalidArg "value" "Line length must be positive."
        {specification with MaxLineLength=value}
    static member Defaults(defaults,specification: ProcessSpec) = {specification with Defaults=Configuration.combine specification.Defaults defaults}
    static member OverallTimeout(after,specification: ProcessSpec) =
        if after<=TimeSpan.Zero then invalidArg "after" "Timeout must be positive."
        {specification with OverallTimeout=Some after}
    static member DrainTimeout(after,specification: ProcessSpec) =
        if after<=TimeSpan.Zero then invalidArg "after" "Drain timeout must be positive."
        {specification with DrainTimeout=Some after}
    static member Render(specification: ProcessSpec) = specification.Commands |> List.map Configuration.renderCommand |> String.concat " | "

/// One value shared by async waiters; cancellation removes the waiter rather than abandoning a continuation.
type internal AsyncCell<'value>() =
    let gate = obj()
    let mutable value: 'value option = None
    let waiters = Dictionary<int, 'value -> unit>()
    let mutable next = 0
    member _.Set(item) =
        let callbacks = lock gate (fun () ->
            if value.IsSome then [||] else
            value <- Some item
            let callbacks = waiters.Values |> Seq.toArray
            waiters.Clear()
            callbacks)
        for callback in callbacks do callback item
    member _.Wait(token: CancellationToken): Async<'value> = async {
        return! Async.FromContinuations(fun (success,error,cancel) ->
            let completionGate = obj()
            let mutable registration: IDisposable option = None
            let mutable settled = false
            let finish callback =
                let invoke,disposable = lock completionGate (fun () ->
                    if settled then false,None
                    else
                        settled <- true
                        true,registration)
                disposable |> Option.iter _.Dispose()
                if invoke then callback()
            let id,existing = lock gate (fun () ->
                next <- next + 1
                next,value)
            let deliver item = finish(fun () -> success item)
            match existing with
            | Some item -> deliver item
            | None ->
                lock gate (fun () -> waiters[id] <- deliver)
                let canceled () =
                    let removed = lock gate (fun () -> waiters.Remove id)
                    if removed then
                        finish(fun () ->
#if FABLE_COMPILER
                            error (OperationCanceledException())
#else
                            cancel (OperationCanceledException token)
#endif
                        )
                let created = token.Register canceled |> box |> unbox<IDisposable>
                let disposeNow = lock completionGate (fun () ->
                    registration <- Some created
                    settled)
                if disposeNow then created.Dispose()
                let completed = lock gate (fun () -> value)
                completed |> Option.iter (fun item ->
                    let removed = lock gate (fun () -> waiters.Remove id)
                    if removed then deliver item))
    }

type private AsyncRendezvous<'value>() =
    let gate = obj()
    let mutable pending: ('value * AsyncCell<unit>) option = None
    let mutable waiter: AsyncCell<'value * AsyncCell<unit>> option = None
    member _.Put(value,token: CancellationToken): Async<unit> = async {
        let acknowledged = AsyncCell<unit>()
        let waiting = lock gate (fun () ->
            match waiter,pending with
            | Some consumer,None -> waiter <- None; Some consumer
            | None,None -> pending <- Some(value,acknowledged); None
            | _ -> invalidOp "Only one serialized producer may publish output.")
        waiting |> Option.iter (fun consumer -> consumer.Set(value,acknowledged))
        return! acknowledged.Wait token
    } with
    member _.Take(token: CancellationToken): Async<'value * AsyncCell<unit>> = async {
        let waiting = AsyncCell<'value * AsyncCell<unit>>()
        let buffered = lock gate (fun () ->
            match pending,waiter with
            | Some item,None -> pending <- None; Some item
            | None,None -> waiter <- Some waiting; None
            | _ -> invalidOp "Only one process consumer may pull output.")
        match buffered with
        | Some item -> return item
        | None -> return! waiting.Wait token
    }

#if !FABLE_COMPILER
/// Scripted service; the full handler can coordinate cancellation and failures after output.
type TestProcess() =
    let specs = ResizeArray<ProcessSpec>()
    let mutable outputs: ProcessOutput list = []
    let mutable resultHandler: ProcessSpec -> ProcessResult = fun _ -> invalidOp "Configure TestProcess.RunHandler."
    let mutable executeHandler: (ProcessSpec -> (ProcessOutput -> Async<unit>) -> Flow<unit,ProcessError,ProcessResult>) option = None
    member _.RunSpecs: IReadOnlyList<ProcessSpec> = specs
    member _.Outputs with get() = outputs and set value = outputs <- value
    member _.RunHandler with get() = resultHandler and set value = resultHandler <- value
    member _.ExecuteHandler with get() = executeHandler and set value = executeHandler <- value
    member _.Clear() = specs.Clear()
    static member Result(exitCode: int, stdOut: string, ?stdErr: string): ProcessResult =
        let stdErr = defaultArg stdErr ""
        let captured text = {Text=text; Bytes=Encoding.UTF8.GetBytes(text:string); Truncated=false; Available=true}
        { ExitCode=exitCode; ExitCodes=[exitCode]; StdOut=stdOut; StdErr=stdErr
          StdOutCapture=captured stdOut; StdErrCapture=captured stdErr; Stages=[]
          StartedAt=DateTimeOffset.UnixEpoch; Duration=TimeSpan.Zero }
    interface IProcess with
        member _.Execute(specification,observe) = Flow.delay (fun () ->
            specs.Add specification
            match executeHandler with
            | Some handler -> handler specification observe
            | None -> flow {
                for output in outputs do do! observe output
                return resultHandler specification
              })

type TestProcessHostEnvironment = {
    Environment: ProcessHostEnvironment
    Process: TestProcess
    Console: TestConsole
    Clock: ManualClock }

#if !FABLE_COMPILER
type private ProcessFailureException(error: ProcessError) =
    inherit Exception(error.ToString())
    member _.Error = error

type private ObserverDefectException(error: exn) =
    inherit Exception(error.Message,error)

type private StageExitException(stage: int) =
    inherit Exception("Stage exited unsuccessfully.")
    member _.Stage = stage

type private CaptureBuffer(policy: OutputRetention, available: bool) =
    let gate = obj()
    let mutable truncated = false
    let mutable tailBytes: byte array = [||]
    let stream = new MemoryStream()
    member _.Append(chunk: byte array) = lock gate (fun () ->
        match policy with
        | OutputRetention.All -> stream.Write(chunk,0,chunk.Length)
        | OutputRetention.Nothing -> truncated <- truncated || chunk.Length > 0
        | OutputRetention.Tail limit ->
            if chunk.Length >= limit then
                truncated <- truncated || tailBytes.Length + chunk.Length > limit
                tailBytes <- if limit=0 then [||] else chunk[chunk.Length-limit..]
            else
                let prior = min tailBytes.Length (limit-chunk.Length)
                truncated <- truncated || prior < tailBytes.Length
                tailBytes <- Array.append (if prior=0 then [||] else tailBytes[tailBytes.Length-prior..]) chunk)
    member _.Finish(encoding: Encoding): CapturedOutput = lock gate (fun () ->
        let bytes = match policy with OutputRetention.All -> stream.ToArray() | _ -> tailBytes
        {Text=encoding.GetString bytes; Bytes=bytes; Truncated=truncated; Available=available})
    interface IDisposable with member _.Dispose() = stream.Dispose()
#endif

#endif

[<RequireQualifiedAccess>]
module Process =
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let inline toSpecification (source: ^source): ProcessSpec =
        ((^source or ProcessInput): (static member ToSpecification: ^source -> ProcessSpec) source)
    let ofCommand (command: ProcessCommand) = ProcessInput.ToSpecification command
    let private single (ProcessCommand command) = command
    let private mapSingle transform (ProcessCommand command) = ProcessCommand (transform command)
    let internal withRedactedArguments redacted command = mapSingle (fun c -> { c with RedactedArguments = redacted }) command
    let private formatValue format (value: obj) =
        if isNull value then ""
        else
            match value with
            | :? IFormattable as formattable -> formattable.ToString(format, CultureInfo.InvariantCulture)
            | _ -> string value

    /// Creates a one-command process specification from an executable and already-separated arguments.
    /// Example: Process.commandArgs "git" [ "status"; "--short" ] |&gt; Process.toFlow
    let commandArgs fileName arguments =
        if String.IsNullOrWhiteSpace fileName then invalidArg (nameof fileName) "A command file name cannot be empty."
        { FileName = fileName; Arguments = arguments; RedactedArguments = Map.empty
          WorkingDirectory = None; Environment = Map.empty
          Encoding = None; SuccessCodes = None; Timeout = None
          StdIn = None; StdOut = None; StdErr = None; StdOutCapture = None; StdErrCapture = None }
        |> ProcessCommand

    let private commandFromTemplate (format: string) (values: obj array) =
        let tokens = ResizeArray<string * string>()
        let actual = StringBuilder()
        let display = StringBuilder()
        let mutable quote: char option = None
        let mutable escaped = false
        let mutable started = false

        let finish () =
            if started then
                tokens.Add(actual.ToString(), display.ToString())
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

        let hole index valueFormat =
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
                let colon = descriptor.IndexOf ':'
                hole valueIndex (if colon < 0 then null else descriptor.Substring(colon + 1))
                index <- closing + 1
            else
                literal format[index]
                index <- index + 1

        if escaped then invalidArg (nameof format) "A command line cannot end with an escape character."
        if quote.IsSome then invalidArg (nameof format) "A command line contains an unclosed quote."
        finish ()
        if tokens.Count = 0 then invalidArg (nameof format) "A command line cannot be empty."

        let executable, executableDisplay = tokens[0]
        if executable <> executableDisplay then invalidArg (nameof format) "The executable cannot be secret."
        let arguments = tokens |> Seq.skip 1 |> Seq.map fst |> Seq.toList
        let redacted =
            tokens
            |> Seq.skip 1
            |> Seq.mapi (fun argumentIndex (value, display) -> if value = display then None else Some(argumentIndex, display))
            |> Seq.choose id
            |> Map.ofSeq
        commandArgs executable arguments |> withRedactedArguments redacted

    /// Creates a safely tokenized one-command process specification from an interpolated command template.
    /// Each interpolation hole is one native argument.
    /// Example: Process.command $"git show {revision}" |&gt; Process.toFlow
    let command (commandLine: FormattableString) = commandFromTemplate commandLine.Format (commandLine.GetArguments())

    /// Creates a safely tokenized one-command process specification from fixed command text.
    let commandText (commandLine: string) = commandFromTemplate commandLine Array.empty


    let fileName command = (single command).FileName
    let arguments command = (single command).Arguments
    let tryWorkingDirectory command = (single command).WorkingDirectory
    let environmentVariables command = (single command).Environment
    let acceptedExitCodes command = (single command).SuccessCodes |> Option.defaultValue (Set.singleton 0)
    let arg value command = mapSingle (fun c -> { c with Arguments = c.Arguments @ [value] }) command
    let secretArg value command = mapSingle (fun c -> { c with Arguments = c.Arguments @ [value]; RedactedArguments = c.RedactedArguments.Add(c.Arguments.Length,"***") }) command
    let workingDirectory path command = mapSingle (fun c -> { c with WorkingDirectory = Some path }) command
    let environment name value command = mapSingle (fun c -> { c with Environment = c.Environment.Add(name,Some value) }) command
    let removeEnvironment name command = mapSingle (fun c -> { c with Environment = c.Environment.Add(name,None) }) command
    let encoding value command = mapSingle (fun c -> { c with Encoding = Some value }) command
    let successCodes values command =
        let codes = Set.ofSeq values
        if Set.isEmpty codes then invalidArg (nameof values) "At least one success code is required."
        mapSingle (fun c -> { c with SuccessCodes = Some codes }) command
    /// Bounds one stage from successful native launch to direct exit; use overallTimeout to include drainage.
    let timeout after command =
        if after <= TimeSpan.Zero then invalidArg (nameof after) "Timeout must be positive."
        mapSingle (fun c -> { c with Timeout = Some(Some after) }) command
    let withoutTimeout command = mapSingle (fun c -> { c with Timeout = Some None }) command
    /// Configures root input; an explicit source conflicts with a pipe feeding this command.
    let stdinFrom source command = mapSingle (fun c -> { c with StdIn = Some source }) command
    let rec private checkTarget = function
        | OutputTarget.Tee targets ->
            if targets |> List.exists (function OutputTarget.Inherit -> true | _ -> false) then invalidArg "target" "Tee cannot contain inherited handles."
            List.iter checkTarget targets
        | _ -> ()
    /// Configures a destination independently of capture; explicit destinations conflict with piping this channel.
    let stdoutTo target command =
        checkTarget target
        mapSingle (fun c -> { c with StdOut = Some target }) command
    /// Configures separate stderr routing; merged stderr uses the final stdout destination.
    let stderrTo target command =
        checkTarget target
        mapSingle (fun c -> { c with StdErr = Some target }) command
    let stdoutToConsole command = stdoutTo OutputTarget.Console command
    let stdoutToFile path command = stdoutTo (OutputTarget.File path) command
    let stdoutToAppendFile path command = stdoutTo (OutputTarget.AppendFile path) command
    let stdoutToDiscard command = stdoutTo OutputTarget.Discard command
    let stdoutToInheritHandles command = stdoutTo OutputTarget.Inherit command
    let stderrToConsole command = stderrTo OutputTarget.Console command
    let stderrToFile path command = stderrTo (OutputTarget.File path) command
    let stderrToAppendFile path command = stderrTo (OutputTarget.AppendFile path) command
    let stderrToDiscard command = stderrTo OutputTarget.Discard command
    let stderrToInheritHandles command = stderrTo OutputTarget.Inherit command
    let stdinFromText text command = stdinFrom (InputSource.Text text) command
    let stdinFromBytes bytes command = stdinFrom (InputSource.Bytes bytes) command
    let stdinFromFile path command = stdinFrom (InputSource.File path) command
    let stdinFromInheritHandles command = stdinFrom InputSource.Inherit command
    /// Changes settings on the unique final command; executable and arguments must remain unchanged.
    let mapFinalCommand transform (specification: ProcessSpec) =
        if specification.Leaves.Count <> 1 then invalidArg "specification" "Exactly one final command is required."
        let final = Set.minElement specification.Leaves
        let prior = specification.Commands[final]
        let (ProcessCommand updated) = transform (ProcessCommand prior)
        if updated.FileName <> prior.FileName || updated.Arguments <> prior.Arguments then invalidArg "transform" "The final command's executable and arguments cannot be replaced."
        { specification with Commands = specification.Commands |> List.mapi (fun i c -> if i = final then {updated with RedactedArguments=Map.fold (fun state key value -> Map.add key value state) updated.RedactedArguments prior.RedactedArguments} else c) }
        |> Configuration.validate
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let connect both (next: ProcessSpec) (source: ProcessSpec) =
        let roots = [for i in 0..next.Commands.Length-1 do if next.Connections |> List.forall (fun (_,target,_) -> target <> i) then yield i]
        if roots.Length <> 1 then invalidArg "next" "The destination topology must have one root."
        if both && source.Leaves.Count <> 1 then invalidArg "source" "Piping stdout and stderr requires one producer."
        let offset = source.Commands.Length
        { source with
            Commands = source.Commands @ next.Commands
            Connections = source.Connections @ (source.Leaves |> Seq.map (fun i -> i,offset+roots[0],both) |> Seq.toList) @ (next.Connections |> List.map (fun (a,b,c) -> a+offset,b+offset,c))
            Leaves = next.Leaves |> Set.map ((+) offset)
            Defaults = Configuration.combine source.Defaults next.Defaults
            OverallTimeout = next.OverallTimeout |> Option.orElse source.OverallTimeout
            DrainTimeout = next.DrainTimeout |> Option.orElse source.DrainTimeout
            MergeStdErr = next.MergeStdErr }
        |> Configuration.validate
    let inline pipe next source = connect false (toSpecification next) (toSpecification source)
    let inline pipeStdoutAndStderr next source = connect true (toSpecification next) (toSpecification source)
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let mergeSpecifications (specifications: ProcessSpec list) =
        match specifications with
        | [] -> invalidArg "sources" "Fan-in requires at least one producer."
        | first::rest ->
            rest |> List.fold (fun (prior: ProcessSpec) (next: ProcessSpec) ->
                let offset = prior.Commands.Length
                { prior with
                    Commands = prior.Commands @ next.Commands
                    Connections = prior.Connections @ (next.Connections |> List.map (fun (a,b,c) -> a+offset,b+offset,c))
                    Leaves = Set.union prior.Leaves (next.Leaves |> Set.map ((+)offset))
                    Defaults = Configuration.combine prior.Defaults next.Defaults
                    Framing = OutputFraming.Lines }) {first with Framing = OutputFraming.Lines}
    let inline mergeStdout sources = sources |> Seq.map toSpecification |> Seq.toList |> mergeSpecifications
    let inline mergeStderr source = ProcessInput.MergeStderr(toSpecification source)
    let inline framing value source = ProcessInput.Framing(value,toSpecification source)
    let inline maxLineLength value source = ProcessInput.MaxLineLength(value,toSpecification source)
    /// Supplies topology-wide fallbacks, including future stages. Explicit command settings always win.
    let inline stageDefaults defaults source = ProcessInput.Defaults(defaults,toSpecification source)
    /// Bounds the topology from launch through drain and completion-aware consumer callbacks.
    let inline overallTimeout after source = ProcessInput.OverallTimeout(after,toSpecification source)
    /// Bounds remaining I/O and sink completion after every managed stage exits.
    let inline drainTimeout after source = ProcessInput.DrainTimeout(after,toSpecification source)
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let setCapture stderr policy (specification: ProcessSpec) =
        StageDefaults.checkRetention policy
        if not stderr && specification.Leaves.Count <> 1 then invalidArg "specification" "Capturing stdout requires one final command."
        let commands =
            specification.Commands |> List.mapi (fun i c ->
                if stderr && not (specification.Connections |> List.exists (fun (source,_,both) -> source=i && both)) && not (specification.MergeStdErr && specification.Leaves.Contains i) then { c with StdErrCapture = Some policy }
                elif not stderr && specification.Leaves.Contains i then { c with StdOutCapture = Some policy }
                else c)
        { specification with Commands = commands }
    /// Retains all final stdout bytes; stderr and destinations are unchanged. Streaming can then use unbounded memory.
    let inline captureAll source = ((^source or ProcessInput): (static member Map: (ProcessSpec -> ProcessSpec) * ^source -> ^result) (setCapture false OutputRetention.All,source))
    /// Retains the last size bytes of final stdout. Byte boundaries may split encoded characters.
    let inline captureTail size source = ((^source or ProcessInput): (static member Map: (ProcessSpec -> ProcessSpec) * ^source -> ^result) (setCapture false (OutputRetention.Tail size),source))
    /// Omits the stdout transcript while retaining live routing, observation and stderr diagnostics.
    let inline captureNothing source = ((^source or ProcessInput): (static member Map: (ProcessSpec -> ProcessSpec) * ^source -> ^result) (setCapture false OutputRetention.Nothing,source))
    /// Retains complete readable unmerged stderr for every stage.
    let inline captureStderrAll source = ((^source or ProcessInput): (static member Map: (ProcessSpec -> ProcessSpec) * ^source -> ^result) (setCapture true OutputRetention.All,source))
    /// Bounds each readable unmerged stderr transcript in bytes.
    let inline captureStderrTail size source = ((^source or ProcessInput): (static member Map: (ProcessSpec -> ProcessSpec) * ^source -> ^result) (setCapture true (OutputRetention.Tail size),source))
    /// Omits readable unmerged stderr transcripts, including diagnostic tails.
    let inline captureStderrNothing source = ((^source or ProcessInput): (static member Map: (ProcessSpec -> ProcessSpec) * ^source -> ^result) (setCapture true OutputRetention.Nothing,source))
    let inline render source = ProcessInput.Render(toSpecification source)
    let private describeInput = function
        | InputSource.Empty -> "empty" | InputSource.Inherit -> "inherited handle"
        | InputSource.Text value -> $"text ({value.Length} characters)"
        | InputSource.Bytes value -> $"bytes ({value.Length} bytes)"
        | InputSource.File path -> $"file: {path}"
        | InputSource.ReadOnce _ -> "asynchronous read once" | InputSource.Produce _ -> "asynchronous producer"
    let rec private describeTarget = function
        | OutputTarget.Console -> "console" | OutputTarget.Inherit -> "inherited handle" | OutputTarget.Discard -> "discard"
        | OutputTarget.File path -> $"file: {path}" | OutputTarget.AppendFile path -> $"append file: {path}"
        | OutputTarget.Callback _ -> "callback" | OutputTarget.Sink _ | OutputTarget.SinkFactory _ -> "sink"
        | OutputTarget.Tee targets -> "tee (" + (targets |> List.map describeTarget |> String.concat ", ") + ")"
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let planSpecification (specification: ProcessSpec): ProcessPlan =
        let stages = specification.Commands |> List.mapi (fun i original ->
            let resolved = Configuration.resolve specification original
            let outgoing = specification.Connections |> List.filter(fun (source,_,_) -> source=i)
            let incoming = specification.Connections |> List.filter(fun (_,target,_) -> target=i)
            let pipedOutput = not outgoing.IsEmpty
            let pipedError = outgoing |> List.exists(fun (_,_,both)->both)
            let mergedError = specification.MergeStdErr && specification.Leaves.Contains i
            let destinations = outgoing |> List.map(fun (_,target,_)->string target) |> String.concat ","
            let sources = incoming |> List.map(fun (source,_,_)->string source) |> String.concat ","
            let provenance name explicit defaults = name, if explicit then "command" elif defaults then "stageDefaults" else "execution default"
            { Stage = i; Command = Configuration.renderCommand original
              WorkingDirectory = resolved.WorkingDirectory; Environment = resolved.Environment
              Encoding =
#if FABLE_COMPILER
                  let sample = resolved.Encoding.Value.GetBytes "A"
                  if sample.Length=2 then (if sample[0]=65uy then "utf-16" else "utf-16BE") else "utf-8"
#else
                  resolved.Encoding.Value.WebName
#endif
              ; SuccessCodes = resolved.SuccessCodes.Value; Timeout = resolved.Timeout.Value
              StdIn = if incoming.IsEmpty then describeInput resolved.StdIn.Value else "pipe from stages " + sources
              StdOut = if pipedOutput then "pipe to stages " + destinations else describeTarget resolved.StdOut.Value
              StdErr = if pipedError then "pipe to stages " + destinations elif mergedError then "merged into stdout" else describeTarget resolved.StdErr.Value
              StdOutCapture = if pipedOutput then OutputRetention.Nothing else resolved.StdOutCapture.Value
              StdErrCapture = if pipedError || mergedError then OutputRetention.Nothing else resolved.StdErrCapture.Value
              Provenance = Map.ofList ([provenance "stdin" original.StdIn.IsSome false;
                                        provenance "stdout" original.StdOut.IsSome false;
                                        provenance "stderr" original.StdErr.IsSome false;
                                        provenance "successCodes" original.SuccessCodes.IsSome specification.Defaults.SuccessCodes.IsSome;
                                        provenance "workingDirectory" original.WorkingDirectory.IsSome specification.Defaults.WorkingDirectory.IsSome;
                                       provenance "encoding" original.Encoding.IsSome specification.Defaults.Encoding.IsSome;
                                       provenance "timeout" original.Timeout.IsSome specification.Defaults.Timeout.IsSome;
                                       provenance "stdoutCapture" original.StdOutCapture.IsSome specification.Defaults.StdOutCapture.IsSome;
                                       provenance "stderrCapture" original.StdErrCapture.IsSome specification.Defaults.StdErrCapture.IsSome]
                                       @ [for KeyValue(key,_) in resolved.Environment do
                                            yield provenance ("environment."+key) (original.Environment.ContainsKey key) (specification.Defaults.Environment.ContainsKey key)]
                                       @ [if not incoming.IsEmpty then yield "stdin","pipe"
                                          if pipedOutput then
                                              yield "stdout","pipe"
                                              yield "stdoutCapture","unavailable: pipe"
                                          if pipedError || mergedError then
                                              yield "stderr",(if pipedError then "pipe" else "mergeStderr")
                                              yield "stderrCapture","unavailable: connected channel"]) })
        { Commands = stages |> List.map _.Command; Stages = stages; Connections = specification.Connections
          MergeStdErr = specification.MergeStdErr; Framing = specification.Framing; MaxLineLength = specification.MaxLineLength
          OverallTimeout = specification.OverallTimeout; DrainTimeout = specification.DrainTimeout }
    let inline plan source = planSpecification (toSpecification source)
    let service<'env,'error when 'env :> IHasProcess>: Flow<'env,'error,IProcess> = Flow.envWith _.Process
    let private prepare mode (specification: ProcessSpec) =
        if specification.Leaves.Count <> 1 then invalidArg "specification" "Connect fan-in producers to a final command before execution."
        let configured = { specification with Mode = mode } |> Configuration.validate
        { configured with Commands = configured.Commands |> List.map (Configuration.resolve configured); Defaults = StageDefaults.empty }
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let toFlowSpecification<'env when 'env :> IHasProcess> (specification: ProcessSpec): Flow<'env,ProcessError,ProcessResult> =
        Flow.delay (fun () ->
            let configured = prepare specification.Mode specification
            flow {
                let! processes = service
                return! processes.Execute(configured,fun _ -> async.Return ()) |> Flow.localEnv (fun _ -> ())
            })
    let inline toFlow source = toFlowSpecification (toSpecification source)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let createOutputStream<'env when 'env :> IHasProcess and 'env :> IHasClock> requestedChannel (specification: ProcessSpec): ProcessStream<'env,ProcessOutput> =
        ProcessStream(fun () ->
            let configured = prepare ExecutionMode.Streaming specification
            let unavailable = configured.Commands |> List.indexed |> List.tryPick (fun (i,c) ->
                let final = configured.Leaves.Contains i
                match requestedChannel with
                | Some OutputChannel.StdOut when final && (match c.StdOut.Value with OutputTarget.Inherit -> true | _ -> false) -> Some {Stage=i;Channel=OutputChannel.StdOut}
                | Some OutputChannel.StdErr when not (configured.Connections |> List.exists(fun (source,_,both)->source=i && both)) && not (final && configured.MergeStdErr) && (match c.StdErr.Value with OutputTarget.Inherit -> true | _ -> false) -> Some {Stage=i;Channel=OutputChannel.StdErr}
                | _ -> None)
            let events = AsyncRendezvous<Choice<ProcessOutput,Exit<ProcessResult,ProcessError>>>()
            let deadline = AsyncCell<Cause<ProcessError>>()
            let mutable completion = ProcessCompletion.StoppedEarly
            let mutable pendingFailure: Cause<ProcessError> option = None
            let mutable previous: AsyncCell<unit> option = None
            let mutable started = false
            let step finished = flow {
                if finished then return None
                else
                    previous |> Option.iter (fun acknowledgment -> acknowledgment.Set())
                    previous <- None
                    if not started then
                        started <- true
                        match unavailable with
                        | Some failure -> return! Flow.fail (ProcessError.ObservationUnavailable failure)
                        | None -> ()
                        let! processes = service
                        let publish outcome = flow {
                            let! token = Flow.cancellationToken
                            do! events.Put(Choice2Of2 outcome,token)
                        }
                        let producer =
                            flow {
                                let! token = Flow.cancellationToken
                                return! processes.Execute(configured,fun output -> async {
#if FABLE_COMPILER
                                    return! events.Put(Choice1Of2 output,token)
#else
                                    let! observationToken = Async.CancellationToken
                                    return! events.Put(Choice1Of2 output,observationToken)
#endif
                                })
                                        |> Flow.localEnv(fun (_:'env) -> ())
                            }
                            |> Flow.fold
                                (Exit.Success >> publish)
                                (fun cause ->
                                    match Cause.untraced cause with
                                    | Cause.Interrupt -> ()
                                    | _ -> pendingFailure <- Some cause
                                    match Cause.untraced cause with
                                    | Cause.Fail(ProcessError.OverallTimedOut _) -> deadline.Set cause
                                    | _ -> ()
                                    match Cause.untraced cause with
                                    | Cause.Interrupt -> Flow.succeed()
                                    | _ -> publish(Exit.Failure cause))
                        let! fiber = Flow.fork producer
                        do! Flow.scopeAsyncFinalizer(fun _ -> async {
                            fiber.InterruptSource.Cancel()
#if FABLE_COMPILER
                            let! _ = fiber.ExitTask
#else
                            let! _ = fiber.ExitTask |> Async.AwaitTask
#endif
                            return () })
                    let! token = Flow.cancellationToken
                    let! item,acknowledgment = events.Take token
                    match item with
                    | Choice1Of2 output ->
                        previous <- Some acknowledgment
                        return Some(output,false)
                    | Choice2Of2 outcome ->
                        acknowledgment.Set()
                        pendingFailure <- None
                        let! result = Flow.ofExit outcome
                        completion <- ProcessCompletion.Completed result
                        return None
            }
            let observed = FlowStream.unfoldFlow step false
            let selected = match requestedChannel with None -> observed | Some channel -> observed |> FlowStream.filter(fun output -> output.ObservedChannel=channel)
            let guard =
                flow {
                    let! token = Flow.cancellationToken
                    let! cause = deadline.Wait token
                    pendingFailure <- None
                    return! Flow.ofExit(Exit.Failure cause)
                }
            selected,(fun () -> completion),guard,(fun () -> pendingFailure))
    /// Observes all readable unconnected output channels, preserving originating channel metadata.
    let inline streamOutputs source = createOutputStream None (toSpecification source)
    /// Observes final effective stdout chunks; explicitly merged stderr is included.
    let inline streamStdout source = createOutputStream (Some OutputChannel.StdOut) (toSpecification source)
    /// Observes readable unmerged stderr chunks from every stage.
    let inline streamStderr source = createOutputStream (Some OutputChannel.StdErr) (toSpecification source)
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let lineStream requested source =
        let (ProcessStream factory) = createOutputStream requested {source with Framing=OutputFraming.Lines}
        ProcessStream(fun () ->
            let stream,completion,guard,failure = factory()
            stream |> FlowStream.map (fun output -> output.Text),completion,guard,failure)
    /// Observes final stdout lines without LF or CRLF terminators; stderr stays separate by default.
    let inline streamStdoutLines source = lineStream (Some OutputChannel.StdOut) (toSpecification source)
    /// Observes readable unmerged stderr lines from every stage.
    let inline streamStderrLines source = lineStream (Some OutputChannel.StdErr) (toSpecification source)
#if !FABLE_COMPILER
    let private inherited = function OutputTarget.Inherit -> true | _ -> false
    let private startInfo redirectInput redirectOutput redirectError (command: CommandDefinition) =
        let info = ProcessStartInfo(command.FileName)
        command.Arguments |> List.iter info.ArgumentList.Add
        command.WorkingDirectory |> Option.iter(fun path -> info.WorkingDirectory <- path)
        command.Environment |> Map.iter(fun name value -> match value with Some text -> info.Environment[name] <- text | None -> info.Environment.Remove name |> ignore)
        info.RedirectStandardInput <- redirectInput
        info.RedirectStandardOutput <- redirectOutput
        info.RedirectStandardError <- redirectError
        info.UseShellExecute <- false
        info.CreateNoWindow <- true
        info
    let private waitForExit (proc: Diagnostics.Process) token =
#if NETSTANDARD2_1
        Task.Run((fun () -> proc.WaitForExit()),token)
#else
        proc.WaitForExitAsync(token)
#endif
    let rec private instantiate = function
        | OutputTarget.SinkFactory create ->
            let write,finish = create()
            OutputTarget.Sink(write,finish)
        | OutputTarget.Tee targets -> OutputTarget.Tee(List.map instantiate targets)
        | target -> target

    let rec private openTargets (fileSystem: IFileSystem) (files: Dictionary<string * bool,FileStream>) = function
        | OutputTarget.File path | OutputTarget.AppendFile path as target ->
            let append = match target with OutputTarget.AppendFile _ -> true | _ -> false
            if not(files.ContainsKey(path,append)) then
                files.Add((path,append),fileSystem.OpenFile(path,(if append then FileMode.Append else FileMode.Create),FileAccess.Write,FileShare.Read))
        | OutputTarget.Tee targets -> List.iter (openTargets fileSystem files) targets
        | _ -> ()
    let private invokeCallback (token: CancellationToken) (work: Async<unit>) =
        let result = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let fail (error: exn) =
            match error with
            | :? OperationCanceledException as canceled when ForeignCancellation.isOurs token canceled ->
                result.TrySetCanceled(token) |> ignore
            | _ -> result.TrySetException(ObserverDefectException error) |> ignore
        Async.StartWithContinuations(work, (fun () -> result.TrySetResult() |> ignore), fail, (fun canceled -> fail canceled), cancellationToken=token)
        result.Task

    let rec private writeTarget (console: IConsole) (files: Dictionary<string * bool,FileStream>) (token: CancellationToken) channel target (bytes: byte array) =
        (task {
            match target with
            | OutputTarget.Console ->
                let stream = if channel=OutputChannel.StdOut then console.OpenStandardOutput() else console.OpenStandardError()
                do! stream.WriteAsync(bytes,0,bytes.Length,token)
            | OutputTarget.File path -> do! files[path,false].WriteAsync(bytes,0,bytes.Length,token)
            | OutputTarget.AppendFile path -> do! files[path,true].WriteAsync(bytes,0,bytes.Length,token)
            | OutputTarget.Callback callback | OutputTarget.Sink(callback,_) ->
                do! invokeCallback token (callback bytes)
            | OutputTarget.Tee targets -> for item in targets do do! writeTarget console files token channel item bytes
            | _ -> ()
        })
    let rec private finishTarget (token: CancellationToken) target =
        (task {
            match target with
            | OutputTarget.Sink(_,finish) ->
                do! invokeCallback token (finish())
            | OutputTarget.Tee targets -> for item in targets do do! finishTarget token item
            | _ -> ()
        })

    /// Executes native processes; extra effects are explicit clock, filesystem and console services.
    let live (clock: IClock) (fileSystem: IFileSystem) (console: IConsole): IProcess =
        let execute (specification: ProcessSpec) observe =
            let execution = flow {
                let! cancellationToken = Flow.cancellationToken
                let! outcome = async {
                    return! (task {
                        use ioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                        let token = ioCancellation.Token
                        let commands = specification.Commands |> List.toArray
                        let processes = ResizeArray<Diagnostics.Process>()
                        let starts = ResizeArray<DateTimeOffset>()
                        let elapsedStarts = ResizeArray<TimeSpan>()
                        let exitTasks = ResizeArray<Task<unit>>()
                        let launchComplete = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
                        use launchRegistration = token.Register(fun () -> launchComplete.TrySetCanceled(token) |> ignore)
                        let elapsedEnds = Array.create commands.Length None
                        let errorGate = obj()
                        let mutable firstFailure: exn option = None
                        let files = Dictionary<string * bool,FileStream>()
                        let outputGates = Array.init commands.Length (fun _ -> new SemaphoreSlim(1,1))
                        let errorGates = Array.init commands.Length (fun _ -> new SemaphoreSlim(1,1))
                        use observerGate = new SemaphoreSlim(1,1)
                        let stdoutTargets = commands |> Array.map(fun c -> instantiate c.StdOut.Value)
                        let stderrTargets = commands |> Array.map(fun c -> instantiate c.StdErr.Value)
                        let stdout = commands |> Array.mapi(fun i c ->
                            let available = not(inherited c.StdOut.Value) && not(specification.Connections |> List.exists(fun (source,_,_)->source=i))
                            new CaptureBuffer((if available then c.StdOutCapture.Value else OutputRetention.Nothing),available))
                        let stderr = commands |> Array.mapi(fun i c ->
                            let available = not(inherited c.StdErr.Value) && not(specification.Connections |> List.exists(fun (source,_,both)->source=i && both)) && not(specification.MergeStdErr && specification.Leaves.Contains i)
                            new CaptureBuffer((if available then c.StdErrCapture.Value else OutputRetention.Nothing),available))
                        let startedAt = clock.UtcNow()
                        let startedElapsed = clock.Elapsed()
                        let terminate () =
                            for proc in lock processes (fun () -> processes.ToArray()) do
                                try
                                    if not proc.HasExited then
#if NETSTANDARD2_1
                                        proc.Kill()
#else
                                        proc.Kill(entireProcessTree=true)
#endif
                                with _ -> ()
                        let recordFailure error =
                            let first = lock errorGate (fun () ->
                                if firstFailure.IsSome then false else firstFailure <- Some error; true)
                            if first then
                                ioCancellation.Cancel()
                                terminate()
                        let supervise (work: Task<'value>) =
                            (task {
                                try return! work
                                with error ->
                                    if not token.IsCancellationRequested then recordFailure error
                                    return raise error // axial-allow-raise
                            })
                        let monitor i (command: CommandDefinition) =
                            (task {
                                let wait = waitForExit processes[i] token
                                match command.Timeout.Value with
                                | Some after ->
                                    use timerCancellation = CancellationTokenSource.CreateLinkedTokenSource(token)
                                    let remaining = max TimeSpan.Zero (after-(clock.Elapsed()-elapsedStarts[i]))
                                    let timer = clock.Sleep(remaining,timerCancellation.Token)
                                    try
                                        let! winner = Task.WhenAny(wait,timer)
                                        if obj.ReferenceEquals(winner,timer) && not wait.IsCompleted then
                                            do! timer
                                            raise(ProcessFailureException(ProcessError.StageTimedOut {Stage=i;Command=Configuration.renderCommand command;Timeout=after})) // axial-allow-raise
                                        do! wait
                                    finally timerCancellation.Cancel()
                                | None -> do! wait
                                elapsedEnds[i] <- Some(clock.Elapsed())
                                if not(command.SuccessCodes.Value.Contains processes[i].ExitCode) then
                                    do! launchComplete.Task
                                    recordFailure(StageExitException i)
                                return ()
                            }) |> supervise
                        let buildResult () =
                            let final = Set.minElement specification.Leaves
                            let stages = commands |> Array.mapi(fun i command ->
                                let exitCode = if i < processes.Count && processes[i].HasExited then processes[i].ExitCode else -1
                                {Stage=i; Command=Configuration.renderCommand command; ExitCode=exitCode
                                 Succeeded=command.SuccessCodes.Value.Contains exitCode
                                 StartedAt=(if i<starts.Count then starts[i] else startedAt)
                                 Duration=(if i<elapsedStarts.Count then (defaultArg elapsedEnds[i] (clock.Elapsed()))-elapsedStarts[i] else TimeSpan.Zero)
                                 StdOutCapture=stdout[i].Finish command.Encoding.Value
                                 StdErrCapture=stderr[i].Finish command.Encoding.Value}) |> Array.toList
                            let contributors = stages |> List.filter(fun stage ->
                                not(specification.Connections |> List.exists(fun (source,_,both)->source=stage.Stage && both)) && not(specification.MergeStdErr && stage.Stage=final))
                            let full = contributors |> List.forall(fun stage -> commands[stage.Stage].StdErrCapture.Value=OutputRetention.All)
                            let parts = contributors |> List.map(fun stage -> commands[stage.Stage].Encoding.Value,stage.StdErrCapture)
                            let allBytes = parts |> List.sumBy(fun (_,capture)->capture.Bytes.Length)
                            let mutable skip = if full then 0 else max 0 (allBytes-65536)
                            let kept = parts |> List.map(fun (encoding,capture)->
                                let removed = min skip capture.Bytes.Length
                                skip <- skip-removed
                                let bytes = if removed=capture.Bytes.Length then [||] else capture.Bytes[removed..]
                                encoding.GetString bytes,bytes)
                            let errorCapture = {
                                Text=kept |> List.map fst |> String.concat ""
                                Bytes=kept |> List.map snd |> Array.concat
                                Truncated=(not full && allBytes>65536) || (contributors |> List.exists(fun stage->stage.StdErrCapture.Truncated))
                                Available=contributors |> List.forall(fun stage->stage.StdErrCapture.Available) }
                            let outputCapture = stages[final].StdOutCapture
                            {ExitCode=stages[final].ExitCode; ExitCodes=stages |> List.map _.ExitCode
                             StdOut=outputCapture.Text; StdErr=errorCapture.Text
                             StdOutCapture=outputCapture; StdErrCapture=errorCapture; Stages=stages
                             StartedAt=startedAt; Duration=clock.Elapsed()-startedElapsed}
                        try
                            try
                                Array.iter (openTargets fileSystem files) stdoutTargets
                                Array.iter (openTargets fileSystem files) stderrTargets
                                use registration = token.Register terminate
                                for i,command in commands |> Array.indexed do
                                    token.ThrowIfCancellationRequested()
                                    let pipedOut = specification.Connections |> List.exists(fun (source,_,_)->source=i)
                                    let pipedError = specification.Connections |> List.exists(fun (source,_,both)->source=i && both)
                                    let isRoot = specification.Connections |> List.forall(fun (_,target,_)->target<>i)
                                    let merged = specification.MergeStdErr && specification.Leaves.Contains i
                                    let redirectInput = not(isRoot && (match command.StdIn.Value with InputSource.Inherit -> true | _ -> false))
                                    let redirectOutput = pipedOut || not(inherited stdoutTargets[i])
                                    let redirectError = pipedError || not(inherited(if merged then stdoutTargets[i] else stderrTargets[i]))
                                    let proc =
                                        try new Diagnostics.Process(StartInfo=startInfo redirectInput redirectOutput redirectError command)
                                        with error ->
                                            raise(ProcessFailureException(ProcessError.StartFailed {Command=Configuration.renderCommand command;Message=error.Message})) // axial-allow-raise
                                    try
                                        if not(proc.Start()) then raise(Exception $"Could not start {command.FileName}.") // axial-allow-effect: process // axial-allow-raise
                                    with error ->
                                        proc.Dispose()
                                        raise(ProcessFailureException(ProcessError.StartFailed {Command=Configuration.renderCommand command;Message=error.Message})) // axial-allow-raise
                                    lock processes (fun () -> processes.Add proc)
                                    starts.Add(clock.UtcNow())
                                    elapsedStarts.Add(clock.Elapsed())
                                    exitTasks.Add(monitor i command)
                                let copies = specification.Connections |> List.groupBy(fun (_,target,_)->target) |> List.map(fun (target,connections)->
                                    (task {
                                        use gate = new SemaphoreSlim(1,1)
                                        let write bytes =
                                            (task {
                                                do! gate.WaitAsync token
                                                try do! processes[target].StandardInput.BaseStream.WriteAsync(bytes,0,bytes.Length,token)
                                                finally gate.Release() |> ignore
                                            })
                                        let pump source channel (stream: Stream) =
                                            (task {
                                                let buffer = Array.zeroCreate<byte> 4096
                                                use pending = new MemoryStream()
                                                let lineMode = connections.Length>1 && specification.Framing=OutputFraming.Lines
                                                let encoding = commands[source].Encoding.Value
                                                let decoder = encoding.GetDecoder()
                                                let decoded = Array.zeroCreate<char>(encoding.GetMaxCharCount 1)
                                                let singleByte = Array.zeroCreate<byte> 1
                                                let mutable lineLength = 0
                                                let mutable lastWasCR = false
                                                let checkLength () =
                                                    if lineLength>specification.MaxLineLength && not(lineLength=specification.MaxLineLength+1 && lastWasCR) then
                                                        raise(ProcessFailureException(ProcessError.LineTooLong {Stage=source;OriginalChannel=channel;Limit=specification.MaxLineLength})) // axial-allow-raise
                                                let mutable reading = true
                                                while reading do
                                                    let! count = stream.ReadAsync(buffer,0,buffer.Length,token)
                                                    if count=0 then
                                                        reading <- false
                                                        if lineMode then
                                                            lineLength <- lineLength+decoder.GetChars([||],0,0,decoded,0,true)
                                                            if lineLength>specification.MaxLineLength then
                                                                raise(ProcessFailureException(ProcessError.LineTooLong {Stage=source;OriginalChannel=channel;Limit=specification.MaxLineLength})) // axial-allow-raise
                                                        if pending.Length>0 then do! write(pending.ToArray())
                                                    elif lineMode then
                                                        for b in buffer[..count-1] do
                                                            pending.WriteByte b
                                                            singleByte[0] <- b
                                                            let length = decoder.GetChars(singleByte,0,1,decoded,0,false)
                                                            for index in 0..length-1 do
                                                                if decoded[index]='\n' then
                                                                    do! write(pending.ToArray())
                                                                    pending.SetLength 0L
                                                                    lineLength <- 0
                                                                    lastWasCR <- false
                                                                else
                                                                    lineLength <- lineLength+1
                                                                    lastWasCR <- decoded[index]='\r'
                                                                    checkLength()
                                                    else do! write buffer[..count-1]
                                            }) |> supervise
                                        let pumps = [|for source,_,both in connections do
                                                        yield pump source OutputChannel.StdOut processes[source].StandardOutput.BaseStream :> Task
                                                        if both then yield pump source OutputChannel.StdErr processes[source].StandardError.BaseStream :> Task|]
                                        do! Task.WhenAll pumps
                                        processes[target].StandardInput.Close()
                                    }) |> supervise :> Task) |> List.toArray
                                let inputs = [|for i,command in commands |> Array.indexed do
                                                if specification.Connections |> List.forall(fun (_,target,_)->target<>i) then
                                                    match command.StdIn.Value with
                                                    | InputSource.Inherit -> ()
                                                    | source -> yield ((task {
                                                        let target = processes[i].StandardInput.BaseStream
                                                        let write (bytes: byte array) = target.WriteAsync(bytes,0,bytes.Length,token) |> Async.AwaitTask
                                                        match source with
                                                        | InputSource.Empty | InputSource.Inherit -> ()
                                                        | InputSource.Text text -> do! target.WriteAsync(command.Encoding.Value.GetBytes text,0,command.Encoding.Value.GetByteCount text,token)
                                                        | InputSource.Bytes bytes -> do! target.WriteAsync(bytes,0,bytes.Length,token)
                                                        | InputSource.File path ->
                                                            use input = fileSystem.OpenFile(path,FileMode.Open,FileAccess.Read,FileShare.Read)
                                                            do! input.CopyToAsync(target,81920,token)
                                                        | InputSource.ReadOnce read ->
                                                            let! bytes = Async.StartAsTask(read(),cancellationToken=token)
                                                            do! target.WriteAsync(bytes,0,bytes.Length,token)
                                                        | InputSource.Produce producer -> do! Async.StartAsTask(producer write,cancellationToken=token)
                                                        processes[i].StandardInput.Close()
                                                      }) |> supervise :> Task)|]
                                let read stage original observed target (capture: CaptureBuffer) (stream: Stream) =
                                    (task {
                                        let buffer = Array.zeroCreate<byte> 4096
                                        let encoding = commands[stage].Encoding.Value
                                        let decoder = encoding.GetDecoder()
                                        let chars = Array.zeroCreate<char>(encoding.GetMaxCharCount buffer.Length)
                                        let pending = StringBuilder()
                                        let targetGate = if observed=OutputChannel.StdOut then outputGates[stage] else errorGates[stage]
                                        let publish text =
                                            (task {
                                                do! observerGate.WaitAsync token
                                                try
                                                    do! invokeCallback token (observe {Stage=stage;OriginalChannel=original;ObservedChannel=observed;Text=text;Timestamp=clock.UtcNow()})
                                                finally observerGate.Release() |> ignore
                                            })
                                        let decode bytes count flush =
                                            (task {
                                                let length = decoder.GetChars(bytes,0,count,chars,0,flush)
                                                match specification.Framing with
                                                | OutputFraming.Chunks -> if length>0 then do! publish(String(chars,0,length))
                                                | OutputFraming.Lines ->
                                                    for index in 0..length-1 do
                                                        let c = chars[index]
                                                        if c='\n' then
                                                            if pending.Length>0 && pending[pending.Length-1]='\r' then pending.Length <- pending.Length-1
                                                            do! publish(pending.ToString())
                                                            pending.Clear() |> ignore
                                                        else
                                                            pending.Append c |> ignore
                                                            // One extra CR is permitted only while it may be a CRLF terminator.
                                                            if pending.Length>specification.MaxLineLength && not(pending.Length=specification.MaxLineLength+1 && c='\r') then
                                                                raise(ProcessFailureException(ProcessError.LineTooLong {Stage=stage;OriginalChannel=original;Limit=specification.MaxLineLength})) // axial-allow-raise
                                                    if flush && pending.Length>0 then
                                                        if pending.Length>specification.MaxLineLength then raise(ProcessFailureException(ProcessError.LineTooLong {Stage=stage;OriginalChannel=original;Limit=specification.MaxLineLength})) // axial-allow-raise
                                                        do! publish(pending.ToString())
                                                        pending.Clear() |> ignore
                                            })
                                        let mutable reading = true
                                        while reading do
                                            let! count = stream.ReadAsync(buffer,0,buffer.Length,token)
                                            if count=0 then reading <- false; do! decode [||] 0 true
                                            else
                                                let bytes = buffer[..count-1]
                                                do! targetGate.WaitAsync token
                                                try
                                                    capture.Append bytes
                                                    do! writeTarget console files token observed target bytes
                                                finally targetGate.Release() |> ignore
                                                do! decode bytes count false
                                    }) |> supervise
                                let reads = ResizeArray<Task>()
                                let final = Set.minElement specification.Leaves
                                if not(inherited stdoutTargets[final]) then reads.Add(read final OutputChannel.StdOut OutputChannel.StdOut stdoutTargets[final] stdout[final] processes[final].StandardOutput.BaseStream)
                                for i in 0..commands.Length-1 do
                                    if not(specification.Connections |> List.exists(fun (source,_,both)->source=i && both)) then
                                        let merged = specification.MergeStdErr && i=final
                                        let target = if merged then stdoutTargets[i] else stderrTargets[i]
                                        if not(inherited target) then reads.Add(read i OutputChannel.StdErr (if merged then OutputChannel.StdOut else OutputChannel.StdErr) target (if merged then stdout[i] else stderr[i]) processes[i].StandardError.BaseStream)
                                launchComplete.TrySetResult() |> ignore
                                let exits = exitTasks.ToArray()
                                let io =
                                    (task {
                                        do! Task.WhenAll(Array.concat [copies;inputs;reads.ToArray()])
                                        for target in stdoutTargets do do! finishTarget token target
                                        for i in 0..stderrTargets.Length-1 do
                                            if not(specification.MergeStdErr && i=final) then do! finishTarget token stderrTargets[i]
                                    }) |> supervise
                                let allExited = Task.WhenAll exits
                                let drain =
                                    (task {
                                        let! _ = allExited
                                        match specification.DrainTimeout with
                                        | Some after when not io.IsCompleted ->
                                            use timerCancellation = CancellationTokenSource.CreateLinkedTokenSource(token)
                                            let allEnded = elapsedEnds |> Array.choose id |> Array.max
                                            let remaining = max TimeSpan.Zero (after-(clock.Elapsed()-allEnded))
                                            let timer = clock.Sleep(remaining,timerCancellation.Token)
                                            try
                                                let! winner = Task.WhenAny(io :> Task,timer)
                                                if obj.ReferenceEquals(winner,timer) && not io.IsCompleted then
                                                    do! timer
                                                    raise(IOException $"Process I/O did not finish within {after} after all process stages exited.") // axial-allow-raise
                                            finally timerCancellation.Cancel()
                                        | _ -> ()
                                    }) |> supervise
                                do! Task.WhenAll [|io :> Task;drain :> Task|]
                                match firstFailure with
                                | Some error -> return raise error // axial-allow-raise
                                | None -> return Choice1Of3(buildResult())
                            with
                            | error ->
                                let original = lock errorGate (fun () -> defaultArg firstFailure error)
                                ioCancellation.Cancel()
                                terminate()
                                for proc in processes do
                                    try proc.WaitForExit() with _ -> ()
                                try
                                    let! _ = Task.WhenAll(exitTasks.ToArray())
                                    ()
                                with _ -> ()
                                match original with
                                | :? OperationCanceledException when cancellationToken.IsCancellationRequested -> return! Task.FromCanceled<_>(cancellationToken)
                                | :? ObserverDefectException as defect -> return Choice3Of3 defect.InnerException
                                | :? ProcessFailureException as failure -> return Choice2Of3 failure.Error
                                | :? StageExitException as failure ->
                                    let result = buildResult()
                                    return Choice2Of3(ProcessError.StageFailed {Stage=result.Stages[failure.Stage];Result=result})
                                | :? OperationCanceledException -> return Choice3Of3 original
                                | _ -> return Choice2Of3(ProcessError.IoFailed {Message=original.Message})
                        finally
                            ioCancellation.Cancel()
                            terminate()
                            for proc in processes do
                                try proc.WaitForExit() with _ -> ()
                                proc.Dispose()
                            for file in files.Values do file.Dispose()
                            for buffer in stdout do (buffer:>IDisposable).Dispose()
                            for buffer in stderr do (buffer:>IDisposable).Dispose()
                            for gate in outputGates do gate.Dispose()
                            for gate in errorGates do gate.Dispose()
                        }) |> Async.AwaitTask
                }
                return!
                    match outcome with
                    | Choice1Of3 result -> Flow.succeed result
                    | Choice2Of3 error -> Flow.fail error
                    | Choice3Of3 defect -> Flow.die defect
            }
            let timed =
                match specification.OverallTimeout with
                | Some after -> execution |> Flow.timeout after (ProcessError.OverallTimedOut {Specification=render specification;Timeout=after})
                | None -> execution
            timed |> Flow.localEnv(fun () -> ClockEnvironment(clock))
        {new IProcess with member _.Execute(specification,observe) = execute specification observe}
#endif
#if !FABLE_COMPILER
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let createHost (console: IConsole): ProcessHostEnvironment =
        {Process=live Clock.live FileSystem.live console;Console=console;Clock=Clock.live}
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let liveHost = lazy(createHost Console.live)
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let hostCode (environment: #IHasConsole) outcome =
        match outcome with
        | Exit.Success _ -> 0
        | Exit.Failure(Cause.Fail error) ->
            // The summary does not replay output already forwarded through console routes.
            let summary = match error with
                          | ProcessError.StageFailed failure -> $"Stage {failure.Stage.Stage} ({failure.Stage.Command}) exited with code {failure.Stage.ExitCode}."
                          | other -> ProcessError.describe other
            environment.Console.WriteErrorLine summary
            ProcessError.exitCode error
        | Exit.Failure cause ->
            environment.Console.WriteErrorLine(string cause)
            1
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    type Runner =
        static member Run(specification: ProcessSpec) = toFlowSpecification specification |> Flow.run liveHost.Value
        static member Run(command: ProcessCommand) = Runner.Run(ProcessInput.ToSpecification command)
        static member Run(workflow: Flow<ProcessHostEnvironment,'error,'value>) = Flow.run liveHost.Value workflow
        static member RunWith(environment: 'env, specification: ProcessSpec) = toFlowSpecification specification |> Flow.run environment
        static member RunWith(environment: 'env, command: ProcessCommand) = Runner.RunWith(environment,ProcessInput.ToSpecification command)
        static member RunWith(environment: 'env, workflow: Flow<'env,'error,'value>) = Flow.run environment workflow
        static member RunExitCode(specification: ProcessSpec) =
            toFlowSpecification {specification with Mode=ExecutionMode.Host} |> Flow.run liveHost.Value |> hostCode liveHost.Value
        static member RunExitCode(command: ProcessCommand) = Runner.RunExitCode(ProcessInput.ToSpecification command)
        static member RunExitCode(workflow: Flow<ProcessHostEnvironment,ProcessError,'value>) = Flow.run liveHost.Value workflow |> hostCode liveHost.Value
        static member RunExitCodeWith(environment: 'env, specification: ProcessSpec) =
            toFlowSpecification {specification with Mode=ExecutionMode.Host} |> Flow.run environment |> hostCode environment
        static member RunExitCodeWith(environment: 'env, command: ProcessCommand) = Runner.RunExitCodeWith(environment,ProcessInput.ToSpecification command)
        static member RunExitCodeWith(environment: 'env, workflow: Flow<'env,ProcessError,'value>) = Flow.run environment workflow |> hostCode environment
    /// Executes immediately and returns Exit, retaining successful output and failure causes.
    let inline run (source:^source): ^result =
        ((^source or Runner): (static member Run: ^source -> ^result) source)
    /// Executes with supplied services and returns the complete Exit.
    let inline runWith (environment:'env) (source:^source): ^result =
        ((^source or Runner): (static member RunWith: 'env * ^source -> ^result) (environment,source))
    /// Executes at a program boundary. Direct commands default to console routes and no stdout capture.
    let inline runExitCode (source:^source): int =
        ((^source or Runner): (static member RunExitCode: ^source -> int) source)
    /// Executes at a program boundary with supplied services.
    let inline runExitCodeWith (environment:'env) (source:^source): int =
        ((^source or Runner): (static member RunExitCodeWith: 'env * ^source -> int) (environment,source))
    let test () = TestProcess()
#endif

[<RequireQualifiedAccess>]
module ProcessStream =
    let private transform operation (ProcessStream factory) = ProcessStream(fun () ->
        let stream,completion,guard,failure = factory()
        operation stream,completion,guard,failure)
    /// Transforms values while preserving process completion.
    let map mapping source = transform (FlowStream.map mapping) source
    /// Selects values without losing process completion or skipping drainage of other channels.
    let filter predicate source = transform (FlowStream.filter predicate) source
    /// Stops the owned process after a prefix. Zero does not start a process.
    let take count source =
        if count<0 then invalidArg "count" "Count cannot be negative."
        transform (FlowStream.take count) source
    /// Stops before the first nonmatching value, awaits owned cleanup, and reports StoppedEarly.
    let takeWhile predicate source = transform (FlowStream.takeWhile predicate) source
    /// Converts to ordinary FlowStream, discarding successful process completion.
    let toFlowStream (ProcessStream factory) = FlowStream(fun env token ->
        let (FlowStream stream),_,_,_ = factory()
        stream env token)
    let private terminal consume (ProcessStream factory) =
        Flow.delay(fun () ->
            let stream,completion,guard,failure = factory()
            let consumer = consume stream |> Flow.map(fun value -> value,completion())
            let watcher = guard |> Flow.bind(fun () -> Flow.never)
            Flow.race consumer watcher
            |> Flow.scoped
            |> Flow.fold Flow.succeed (fun cause ->
                let combined = failure() |> Option.map(fun producer -> Cause.Both(cause,producer)) |> Option.defaultValue cause
                Flow.ofExit(Exit.Failure combined)))
    /// Folds selected values and returns the state together with completion; capture policy is independent.
    let runFold folder initial source = terminal (FlowStream.runFold folder initial) source
    /// Collects every selected value plus completion. The collected list can grow without bound.
    let runCollect source = terminal FlowStream.runCollect source
    /// Consumes selected output without accumulating values and returns completion.
    let runDrain source = terminal FlowStream.runDrain source |> Flow.map snd
    /// Synchronous callback; use runForEachFlow for explicit service operations.
    let runForEach action source = terminal (FlowStream.runForEach action) source |> Flow.map snd
    let private effectfulTerminal consume (ProcessStream factory) =
        Flow.delay(fun () ->
            let stream,completion,guard,failure = factory()
            let consumer = consume (FlowStream.mapError ProcessStreamError.ProcessFailed stream) |> Flow.map(fun value -> value,completion())
            let watcher = guard |> Flow.mapError ProcessStreamError.ProcessFailed |> Flow.bind(fun () -> Flow.never)
            Flow.race consumer watcher
            |> Flow.scoped
            |> Flow.fold Flow.succeed (fun cause ->
                let combined = failure() |> Option.map(fun producer -> Cause.Both(cause,Cause.map ProcessStreamError.ProcessFailed producer)) |> Option.defaultValue cause
                Flow.ofExit(Exit.Failure combined)))
    /// Runs each callback in the application environment; preserves consumer errors separately.
    let runForEachFlow action source =
        effectfulTerminal (FlowStream.runForEachFlow(fun value -> action value |> Flow.mapError ProcessStreamError.ConsumerFailed)) source |> Flow.map snd
    /// Folds with application workflows; typed consumer failures use ConsumerFailed.
    let runFoldFlow folder initial source =
        Flow.delay(fun () ->
            let state = ref initial
            runForEachFlow (fun value -> folder state.Value value |> Flow.map(fun next -> state.Value <- next)) source
            |> Flow.map(fun completion -> state.Value,completion))

#if !FABLE_COMPILER
[<RequireQualifiedAccess>]
module ProcessHostEnvironment =
    let live = Process.liveHost.Value
    let liveWithConsole console = Process.createHost console
    let ofParts processes console clock: ProcessHostEnvironment = {Process=processes;Console=console;Clock=clock}
    let test () =
        let processes = Process.test()
        let console = Console.test()
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        {Environment=ofParts processes console clock;Process=processes;Console=console;Clock=clock}
#endif

module DSL =
    let private formatValue format (value: obj) =
        if isNull value then ""
        else
            match value with
            | :? IFormattable as formattable -> formattable.ToString(format, CultureInfo.InvariantCulture)
            | _ -> string value


    type EndpointConnector =
        static member Connect(source: InputSource, next: ProcessCommand) = Process.stdinFrom source next
        static member Connect(source: ProcessCommand, next: ProcessCommand) = Process.pipe next source
        static member Connect(source: ProcessSpec, next: ProcessCommand) = Process.pipe next source
        static member Connect(source: ProcessCommand, next: ProcessSpec) = Process.pipe next source
        static member Connect(source: ProcessSpec, next: ProcessSpec) = Process.pipe next source
        static member Connect(source: ProcessCommand, target: OutputTarget) = Process.stdoutTo target source
    let inline (=>) (source:^source) (destination:^destination): ^result =
        ((^source or ^destination or EndpointConnector): (static member Connect: ^source * ^destination -> ^result) (source,destination))
    let cmd template = Process.command template
    let cmdText text = Process.commandText text
    let pipe commandLines =
        match commandLines |> Seq.map cmd |> Seq.toList with
        | [] -> invalidArg "commandLines" "A pipeline needs a command."
        | head::tail -> tail |> List.fold(fun prior next -> Process.pipe next prior) (Process.ofCommand head)
    let inline mergeStdout sources = Process.mergeStdout sources
    let inline mergeStdoutBytes sources = Process.mergeStdout sources |> Process.framing OutputFraming.Chunks
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
        Process.commandArgs executable arguments |> Process.withRedactedArguments redacted

    /// Builds a Bash program with interpolation values passed as positional arguments.
    let bash program = shellCommand "bash" (fun script -> [ "-o"; "pipefail"; "-c"; script; "axial" ]) (fun index -> "\"${" + string(index+1) + "}\"") program
    /// Builds a POSIX shell program with interpolation values passed as positional arguments.
    let sh program = shellCommand "sh" (fun script -> [ "-c"; script; "axial" ]) (fun index -> "\"${" + string(index+1) + "}\"") program
    /// Builds a PowerShell 7 program with interpolation values passed through <c>$args</c>.
    let pwsh program = shellCommand "pwsh" (fun script -> [ "-NoProfile"; "-NonInteractive"; "-Command"; script ]) (fun index -> $"$args[{index}]") program

    /// Builds an explicitly assembled Bash program. Values in the text are not escaped by Axial.
    let bashText program = Process.commandArgs "bash" [ "-o"; "pipefail"; "-c"; program ]
    /// Builds an explicitly assembled POSIX shell program. Values in the text are not escaped by Axial.
    let shText program = Process.commandArgs "sh" [ "-c"; program ]
    /// Builds an explicitly assembled PowerShell 7 program. Values in the text are not escaped by Axial.
    let pwshText program = Process.commandArgs "pwsh" [ "-NoProfile"; "-NonInteractive"; "-Command"; program ]


    let secret value = SecretArgument(box value)
    let cwd path command = Process.workingDirectory path command
    let env name value command = Process.environment name value command
    let stdinFrom value command = Process.stdinFrom value command
    let stdoutTo value command = Process.stdoutTo value command
    let stderrTo value command = Process.stderrTo value command
    let stdoutToFile value command = Process.stdoutToFile value command
    let stdoutToAppendFile value command = Process.stdoutToAppendFile value command
    let stderrToFile value command = Process.stderrToFile value command
    let stderrToAppendFile value command = Process.stderrToAppendFile value command
    let stdinFromText value command = Process.stdinFromText value command
    let stdinFromBytes value command = Process.stdinFromBytes value command
    let stdinFromFile value command = Process.stdinFromFile value command
    let timeout value command = Process.timeout value command
    let stdoutToConsole command = Process.stdoutToConsole command
    let stdoutToDiscard command = Process.stdoutToDiscard command
    let stdoutToInheritHandles command = Process.stdoutToInheritHandles command
    let stderrToConsole command = Process.stderrToConsole command
    let stderrToDiscard command = Process.stderrToDiscard command
    let stderrToInheritHandles command = Process.stderrToInheritHandles command
    let stdinFromInheritHandles command = Process.stdinFromInheritHandles command
    let withoutTimeout command = Process.withoutTimeout command
    let inline framing value source = Process.framing value source
    let inline maxLineLength value source = Process.maxLineLength value source
    let inline stageDefaults value source = Process.stageDefaults value source
    let inline overallTimeout value source = Process.overallTimeout value source
    let inline drainTimeout value source = Process.drainTimeout value source
    /// Same as Process.captureAll. Retains final stdout completely.
    let inline captureAll source = Process.captureAll source
    /// Same as Process.captureNothing. Omits stdout retention; stderr diagnostics remain.
    let inline captureNothing source = Process.captureNothing source
    /// Same as Process.captureStderrAll. Retains readable unmerged stderr completely.
    let inline captureStderrAll source = Process.captureStderrAll source
    /// Same as Process.captureStderrNothing. Omits stderr diagnostic retention.
    let inline captureStderrNothing source = Process.captureStderrNothing source
    let inline toFlow source = Process.toFlow source
    let inline streamOutputs source = Process.streamOutputs source
    let inline streamStdout source = Process.streamStdout source
    let inline streamStderr source = Process.streamStderr source
    let inline streamStdoutLines source = Process.streamStdoutLines source
    let inline streamStderrLines source = Process.streamStderrLines source
    let inline mergeStderr source = Process.mergeStderr source
    /// Same as Process.captureTail. Bounds final stdout retention in bytes.
    let inline captureTail value source = Process.captureTail value source
    /// Same as Process.captureStderrTail. Bounds each readable unmerged stderr channel.
    let inline captureStderrTail value source = Process.captureStderrTail value source
    let mapFinalCommand transform source = Process.mapFinalCommand transform source
    let inline pipeTo next source = Process.pipe next source
    let inline pipeStdoutAndStderrTo next source = Process.pipeStdoutAndStderr next source
#if !FABLE_COMPILER
    let inline run source = Process.run source
    let inline runWith environment source = Process.runWith environment source
    let inline runExitCode source = Process.runExitCode source
    let inline runExitCodeWith environment source = Process.runExitCodeWith environment source
#endif
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let captureParallelSpecifications maximumConcurrency commands =
        if maximumConcurrency<=0 then invalidArg "maximumConcurrency" "Concurrency must be positive."
        let runBatch batch =
            batch |> List.map(fun specification -> Process.toFlowSpecification specification |> Flow.map List.singleton)
            |> function
                | [] -> Flow.ok []
                | first::rest -> rest |> List.fold(fun prior next -> Flow.zipPar prior next |> Flow.map(fun (left,right)->left@right)) first
        commands |> Seq.toList |> List.chunkBySize maximumConcurrency |> List.map runBatch |> Flow.sequence |> Flow.map List.concat
    /// Captures independent commands or specs with bounded workflow concurrency, preserving input order.
    let inline captureParallel maximumConcurrency commands =
        commands |> Seq.map Process.toSpecification |> captureParallelSpecifications maximumConcurrency
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
        /// Gives the root process the host's stdin handle directly, so programs that prompt on the console
        /// (such as `ssh` asking for a password) can read it. Stdin is not redirected, so the child, not Axial,
        /// decides when it ends. Pipe-connected stages still receive redirected stdin.
        let inheritHandles = InputSource.Inherit
        /// Reads one asynchronous byte block when execution begins.
        let readOnce producer = InputSource.ReadOnce producer
        /// Produces asynchronous byte blocks with backpressure.
        let produce producer = InputSource.Produce producer
#if !FABLE_COMPILER
        /// Adapts a .NET stream into a backpressured input source.
        let stream (source: Stream) =
            InputSource.Produce(fun write -> async {
                let! cancellationToken = Async.CancellationToken
                let buffer = Array.zeroCreate<byte> 81920
                let mutable reading = true
                while reading do
                    let! count = source.ReadAsync(buffer, 0, buffer.Length, cancellationToken) |> Async.AwaitTask
                    if count = 0 then reading <- false
                    else do! write buffer[.. count - 1]
            })
#endif

    [<RequireQualifiedAccess>]
    module Output =
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
        /// Sends exact byte chunks to an asynchronous backpressured callback using the process cancellation token.
        let callback write = OutputTarget.Callback write
        /// Sends every byte chunk to each target in order.
        let tee targets = OutputTarget.Tee(List.ofSeq targets)
#if !FABLE_COMPILER
        /// Adapts a writable .NET stream into an output target.
        let stream (target: Stream) =
            OutputTarget.Sink(
                (fun bytes -> async {
                    let! cancellationToken = Async.CancellationToken
                    do! target.WriteAsync(bytes, 0, bytes.Length, cancellationToken) |> Async.AwaitTask
                }),
                (fun () -> async {
                    let! cancellationToken = Async.CancellationToken
                    do! target.FlushAsync(cancellationToken) |> Async.AwaitTask
                }))
        /// Decodes output incrementally into a .NET text writer.
        let textWriter (encoding: Encoding) (target: TextWriter) =
            OutputTarget.SinkFactory(fun () ->
              let decoder = encoding.GetDecoder()
              (
                (fun bytes -> async {
                    let chars = Array.zeroCreate<char> (encoding.GetMaxCharCount bytes.Length)
                    let count = decoder.GetChars(bytes, 0, bytes.Length, chars, 0, false)
#if NETSTANDARD2_1
                    do! target.WriteAsync(chars, 0, count) |> Async.AwaitTask
#else
                    let! cancellationToken = Async.CancellationToken
                    do! target.WriteAsync(chars.AsMemory(0, count), cancellationToken) |> Async.AwaitTask
#endif
                }),
                (fun () -> async {
                    let chars = Array.zeroCreate<char> (encoding.GetMaxCharCount 0)
                    let count = decoder.GetChars(Array.empty, 0, 0, chars, 0, true)
#if NETSTANDARD2_1
                    if count > 0 then do! target.WriteAsync(chars, 0, count) |> Async.AwaitTask
                    do! target.FlushAsync() |> Async.AwaitTask
#else
                    let! cancellationToken = Async.CancellationToken
                    if count > 0 then do! target.WriteAsync(chars.AsMemory(0, count), cancellationToken) |> Async.AwaitTask
                    do! target.FlushAsync(cancellationToken) |> Async.AwaitTask
#endif
                })))
#endif

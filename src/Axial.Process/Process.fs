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

#if !FABLE_COMPILER
type ProcessHostEnvironment = {
    Process: IProcess
    Console: IConsole
    Clock: IClock
} with

    interface IHasProcess with
        member this.Process = this.Process

    interface IHasConsole with
        member this.Console = this.Console

    interface IHasClock with
        member this.Clock = this.Clock

type TestProcessHostEnvironment = {
    Environment: ProcessHostEnvironment
    Process: TestProcess
    Console: TestConsole
    Clock: ManualClock
}
#endif

/// Raised by Process.run when execution does not succeed. Use Process.tryRun to retain the Exit instead.
#if !FABLE_COMPILER
type ProcessRunException<'error>(cause: Cause<'error>) =
    inherit Exception(string cause)
    member _.Cause = cause
#endif

[<RequireQualifiedAccess>]
module Process =
    let internal classifyDeadlineWaitFailure (token: CancellationToken) (error: exn) : Cause<ProcessError> =
        if ForeignCancellation.isOurs token error then Cause.Interrupt else Cause.Die error

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let inline toSpecification (source: ^source) : ProcessSpec =
        ((^source or ProcessInput): (static member ToSpecification: ^source -> ProcessSpec) source)

    let ofCommand (command: ProcessCommand) = ProcessInput.ToSpecification command
    let private single (ProcessCommand command) = command
    let private mapSingle transform (ProcessCommand command) = ProcessCommand(transform command)

    let internal withRedactedArguments redacted command =
        mapSingle (fun c -> { c with RedactedArguments = redacted }) command

    let private formatValue format (value: obj) =
        if isNull value then
            ""
        else
#if FABLE_COMPILER
            string value
#else
            match value with
            | :? IFormattable as formattable -> formattable.ToString(format, CultureInfo.InvariantCulture)
            | _ -> string value
#endif

    /// Creates a one-command process specification from an executable and already-separated arguments.
    /// Example: Process.commandArgs "git" [ "status"; "--short" ] |&gt; Process.toFlow
    let commandArgs fileName arguments =
        if String.IsNullOrWhiteSpace fileName then
            invalidArg (nameof fileName) "A command file name cannot be empty."

        {
            FileName = fileName
            Arguments = arguments
            RedactedArguments = Map.empty
            WorkingDirectory = None
            Environment = Map.empty
            Encoding = None
            SuccessCodes = None
            Timeout = None
            StdIn = None
            StdOut = None
            StdErr = None
            StdOutCapture = None
            StdErrCapture = None
        }
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
                | None, ('\'' | '"') ->
                    quote <- Some character
                    started <- true
                | Some current, character when current = character -> quote <- None
                | None, character when Char.IsWhiteSpace character -> finish ()
                | _ ->
                    actual.Append character |> ignore
                    display.Append character |> ignore
                    started <- true

        let hole index valueFormat =
            if index < 0 || index >= values.Length then
                invalidArg (nameof format) "Interpolation index is out of range."

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
                literal '{'
                index <- index + 2
            elif format[index] = '}' && index + 1 < format.Length && format[index + 1] = '}' then
                literal '}'
                index <- index + 2
            elif format[index] = '{' then
                let closing = format.IndexOf('}', index + 1)

                if closing < 0 then
                    invalidArg (nameof format) "Unclosed interpolation hole."

                let descriptor = format.Substring(index + 1, closing - index - 1)
                let separator = descriptor.IndexOfAny [| ','; ':' |]

                let indexText =
                    if separator < 0 then
                        descriptor
                    else
                        descriptor.Substring(0, separator)

#if FABLE_COMPILER
                let valueIndex = Int32.Parse(indexText)
#else
                let valueIndex = Int32.Parse(indexText, CultureInfo.InvariantCulture)
#endif
                let colon = descriptor.IndexOf ':'
                hole valueIndex (if colon < 0 then null else descriptor.Substring(colon + 1))
                index <- closing + 1
            else
                literal format[index]
                index <- index + 1

        if escaped then
            invalidArg (nameof format) "A command line cannot end with an escape character."

        if quote.IsSome then
            invalidArg (nameof format) "A command line contains an unclosed quote."

        finish ()

        if tokens.Count = 0 then
            invalidArg (nameof format) "A command line cannot be empty."

        let executable, executableDisplay = tokens[0]

        if executable <> executableDisplay then
            invalidArg (nameof format) "The executable cannot be secret."

        let arguments = tokens |> Seq.skip 1 |> Seq.map fst |> Seq.toList

        let redacted =
            tokens
            |> Seq.skip 1
            |> Seq.mapi (fun argumentIndex (value, display) ->
                if value = display then
                    None
                else
                    Some(argumentIndex, display))
            |> Seq.choose id
            |> Map.ofSeq

        commandArgs executable arguments |> withRedactedArguments redacted

    /// Creates a safely tokenized one-command process specification from an interpolated command template.
    /// Each interpolation hole is one native argument.
    /// Example: Process.command $"git show {revision}" |&gt; Process.toFlow
    let command (commandLine: FormattableString) =
        commandFromTemplate commandLine.Format (commandLine.GetArguments())

    /// Creates a safely tokenized one-command process specification from fixed command text.
    let commandText (commandLine: string) =
        commandFromTemplate commandLine Array.empty


    let fileName command = (single command).FileName
    let arguments command = (single command).Arguments
    let tryWorkingDirectory command = (single command).WorkingDirectory
    let environmentVariables command = (single command).Environment

    let acceptedExitCodes command =
        (single command).SuccessCodes |> Option.defaultValue (Set.singleton 0)

    let arg value command =
        mapSingle
            (fun c -> {
                c with
                    Arguments = c.Arguments @ [ value ]
            })
            command

    let secretArg value command =
        mapSingle
            (fun c -> {
                c with
                    Arguments = c.Arguments @ [ value ]
                    RedactedArguments = c.RedactedArguments.Add(c.Arguments.Length, "***")
            })
            command

    let workingDirectory path command =
        mapSingle (fun c -> { c with WorkingDirectory = Some path }) command

    let environment name value command =
        mapSingle
            (fun c -> {
                c with
                    Environment = c.Environment.Add(name, Some value)
            })
            command

    let removeEnvironment name command =
        mapSingle
            (fun c -> {
                c with
                    Environment = c.Environment.Add(name, None)
            })
            command

    let encoding value command =
        mapSingle (fun c -> { c with Encoding = Some value }) command

    let successCodes values command =
        let codes = Set.ofSeq values

        if Set.isEmpty codes then
            invalidArg (nameof values) "At least one success code is required."

        mapSingle (fun c -> { c with SuccessCodes = Some codes }) command

    /// Bounds one stage from successful native launch to direct exit; use overallTimeout to include drainage.
    let timeout after command =
        if after <= TimeSpan.Zero then
            invalidArg (nameof after) "Timeout must be positive."

        mapSingle (fun c -> { c with Timeout = Some(Some after) }) command

    let withoutTimeout command =
        mapSingle (fun c -> { c with Timeout = Some None }) command

    /// Configures root input; an explicit source conflicts with a pipe feeding this command.
    let stdinFrom source command =
        mapSingle (fun c -> { c with StdIn = Some source }) command

    let rec private checkTarget =
        function
        | OutputTarget.Tee targets ->
            if
                targets
                |> List.exists (function
                    | OutputTarget.Inherit -> true
                    | _ -> false)
            then
                invalidArg "target" "Tee cannot contain inherited handles."

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

    let stdoutToFile path command =
        stdoutTo (OutputTarget.File path) command

    let stdoutToAppendFile path command =
        stdoutTo (OutputTarget.AppendFile path) command

    let stdoutToDiscard command = stdoutTo OutputTarget.Discard command
    let stdoutToInheritHandles command = stdoutTo OutputTarget.Inherit command
    let stderrToConsole command = stderrTo OutputTarget.Console command

    let stderrToFile path command =
        stderrTo (OutputTarget.File path) command

    let stderrToAppendFile path command =
        stderrTo (OutputTarget.AppendFile path) command

    let stderrToDiscard command = stderrTo OutputTarget.Discard command
    let stderrToInheritHandles command = stderrTo OutputTarget.Inherit command

    let stdinFromText text command =
        stdinFrom (InputSource.Text text) command

    let stdinFromBytes bytes command =
        stdinFrom (InputSource.Bytes bytes) command

    let stdinFromFile path command =
        stdinFrom (InputSource.File path) command

    let stdinFromInheritHandles command = stdinFrom InputSource.Inherit command

    /// Changes settings on the unique final command; executable and arguments must remain unchanged.
    let mapFinalCommand transform (specification: ProcessSpec) =
        if specification.Leaves.Count <> 1 then
            invalidArg "specification" "Exactly one final command is required."

        let final = Set.minElement specification.Leaves
        let prior = specification.Commands[final]
        let (ProcessCommand updated) = transform (ProcessCommand prior)

        if updated.FileName <> prior.FileName || updated.Arguments <> prior.Arguments then
            invalidArg "transform" "The final command's executable and arguments cannot be replaced."

        {
            specification with
                Commands =
                    specification.Commands
                    |> List.mapi (fun i c ->
                        if i = final then
                            {
                                updated with
                                    RedactedArguments =
                                        Map.fold
                                            (fun state key value -> Map.add key value state)
                                            updated.RedactedArguments
                                            prior.RedactedArguments
                            }
                        else
                            c)
        }
        |> Configuration.validate

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let connect both (next: ProcessSpec) (source: ProcessSpec) =
        let roots = [
            for i in 0 .. next.Commands.Length - 1 do
                if next.Connections |> List.forall (fun (_, target, _) -> target <> i) then
                    yield i
        ]

        if roots.Length <> 1 then
            invalidArg "next" "The destination topology must have one root."

        if both && source.Leaves.Count <> 1 then
            invalidArg "source" "Piping stdout and stderr requires one producer."

        let offset = source.Commands.Length

        {
            source with
                Commands = source.Commands @ next.Commands
                Connections =
                    source.Connections
                    @ (source.Leaves |> Seq.map (fun i -> i, offset + roots[0], both) |> Seq.toList)
                    @ (next.Connections |> List.map (fun (a, b, c) -> a + offset, b + offset, c))
                Leaves = next.Leaves |> Set.map ((+) offset)
                Defaults = Configuration.combine source.Defaults next.Defaults
                OverallTimeout = next.OverallTimeout |> Option.orElse source.OverallTimeout
                DrainTimeout = next.DrainTimeout |> Option.orElse source.DrainTimeout
                OnStarted = next.OnStarted |> Option.orElse source.OnStarted
                StopGracefully = if next.StopGracefully.IsEmpty then source.StopGracefully else next.StopGracefully
                MergeStdErr = next.MergeStdErr
        }
        |> Configuration.validate

    let inline pipe next source =
        connect false (toSpecification next) (toSpecification source)

    let inline pipeStdoutAndStderr next source =
        connect true (toSpecification next) (toSpecification source)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let mergeSpecifications (specifications: ProcessSpec list) =
        match specifications with
        | [] -> invalidArg "sources" "Fan-in requires at least one producer."
        | first :: rest ->
            rest
            |> List.fold
                (fun (prior: ProcessSpec) (next: ProcessSpec) ->
                    let offset = prior.Commands.Length

                    {
                        prior with
                            Commands = prior.Commands @ next.Commands
                            Connections =
                                prior.Connections
                                @ (next.Connections |> List.map (fun (a, b, c) -> a + offset, b + offset, c))
                            Leaves = Set.union prior.Leaves (next.Leaves |> Set.map ((+) offset))
                            Defaults = Configuration.combine prior.Defaults next.Defaults
                            Framing = OutputFraming.Lines
                    })
                {
                    first with
                        Framing = OutputFraming.Lines
                }

    let inline mergeStdout sources =
        sources |> Seq.map toSpecification |> Seq.toList |> mergeSpecifications

    let inline mergeStderr source =
        ProcessInput.MergeStderr(toSpecification source)

    let inline framing value source =
        ProcessInput.Framing(value, toSpecification source)

    let inline maxLineLength value source =
        ProcessInput.MaxLineLength(value, toSpecification source)

    /// Fails and terminates the topology when either original output channel exceeds its byte limit.
    let inline maxOutputBytes stdout stderr source =
        ProcessInput.MaxOutputBytes(stdout, stderr, toSpecification source)

    /// Supplies topology-wide fallbacks, including future stages. Explicit command settings always win.
    let inline stageDefaults defaults source =
        ProcessInput.Defaults(defaults, toSpecification source)

    /// Bounds the topology from launch through drain and completion-aware consumer callbacks.
    let inline overallTimeout after source =
        ProcessInput.OverallTimeout(after, toSpecification source)

    /// Invokes the handler once for every stage, immediately after it starts and before output is observed.
    let inline onStarted (handler: ProcessStarted -> Flow<unit, Never, unit>) source =
        let callback stage processId command startedAt close =
            async {
                let closeFlow request =
                    Flow.fromAsync (close request)
                    |> Flow.bind (function
                        | Ok () -> Flow.succeed ()
                        | Error StopRequestError.Unsupported -> Flow.fail (ProcessError.StopUnsupported request)
                        | Error (StopRequestError.Failed message) ->
                            Flow.fail (ProcessError.StopFailed { Stage = stage; Request = request; Message = message }))

                let started = {
                    Stage = stage
                    ProcessId = processId
                    Command = command
                    StartedAt = startedAt
                    Close = closeFlow
                }

                return! Flow.toAsync () (handler started)
            }

        let observer =
            { new IProcessStartedObserver with
                member _.Invoke(stage, processId, command, startedAt, close) =
                    callback stage processId command startedAt close }

        ProcessInput.OnStarted(observer, toSpecification source)

    /// Sends each request to all live stages on interruption or timeout, waiting its grace period before escalating.
    let inline stopGracefully (steps: (StopRequest * TimeSpan) list) source =
        for _, grace in steps do
            if grace < TimeSpan.Zero then invalidArg (nameof steps) "Grace periods cannot be negative."

        ProcessInput.StopGracefully(steps, toSpecification source)

    /// Bounds remaining I/O and sink completion after every managed stage exits.
    let inline drainTimeout after source =
        ProcessInput.DrainTimeout(after, toSpecification source)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let setCapture stderr policy (specification: ProcessSpec) =
        StageDefaults.checkRetention policy

        if not stderr && specification.Leaves.Count <> 1 then
            invalidArg "specification" "Capturing stdout requires one final command."

        let commands =
            specification.Commands
            |> List.mapi (fun i c ->
                if
                    stderr
                    && not (
                        specification.Connections
                        |> List.exists (fun (source, _, both) -> source = i && both)
                    )
                    && not (specification.MergeStdErr && specification.Leaves.Contains i)
                then
                    { c with StdErrCapture = Some policy }
                elif not stderr && specification.Leaves.Contains i then
                    { c with StdOutCapture = Some policy }
                else
                    c)

        {
            specification with
                Commands = commands
        }

    /// Retains all final stdout bytes; stderr and destinations are unchanged. Streaming can then use unbounded memory.
    let inline captureAll source =
        ((^source or ProcessInput): (static member Map: (ProcessSpec -> ProcessSpec) * ^source -> ^result) (setCapture
                                                                                                                false
                                                                                                                OutputRetention
                                                                                                                    .All,
                                                                                                            source))

    /// Retains the last size bytes of final stdout. Byte boundaries may split encoded characters.
    let inline captureTail size source =
        ((^source or ProcessInput): (static member Map: (ProcessSpec -> ProcessSpec) * ^source -> ^result) (setCapture
                                                                                                                false
                                                                                                                (OutputRetention
                                                                                                                    .Tail
                                                                                                                    size),
                                                                                                            source))

    /// Omits the stdout transcript while retaining live routing, observation and stderr diagnostics.
    let inline captureNothing source =
        ((^source or ProcessInput): (static member Map: (ProcessSpec -> ProcessSpec) * ^source -> ^result) (setCapture
                                                                                                                false
                                                                                                                OutputRetention
                                                                                                                    .Nothing,
                                                                                                            source))

    /// Retains complete readable unmerged stderr for every stage.
    let inline captureStderrAll source =
        ((^source or ProcessInput): (static member Map: (ProcessSpec -> ProcessSpec) * ^source -> ^result) (setCapture
                                                                                                                true
                                                                                                                OutputRetention
                                                                                                                    .All,
                                                                                                            source))

    /// Bounds each readable unmerged stderr transcript in bytes.
    let inline captureStderrTail size source =
        ((^source or ProcessInput): (static member Map: (ProcessSpec -> ProcessSpec) * ^source -> ^result) (setCapture
                                                                                                                true
                                                                                                                (OutputRetention
                                                                                                                    .Tail
                                                                                                                    size),
                                                                                                            source))

    /// Omits readable unmerged stderr transcripts, including diagnostic tails.
    let inline captureStderrNothing source =
        ((^source or ProcessInput): (static member Map: (ProcessSpec -> ProcessSpec) * ^source -> ^result) (setCapture
                                                                                                                true
                                                                                                                OutputRetention
                                                                                                                    .Nothing,
                                                                                                            source))

    let inline render source =
        ProcessInput.Render(toSpecification source)

    let private describeInput =
        function
        | InputSource.Empty -> "empty"
        | InputSource.Inherit -> "inherited handle"
        | InputSource.Text value -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "text ({0} characters)", value.Length))
        | InputSource.Bytes value -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "bytes ({0} bytes)", value.Length))
        | InputSource.File path -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "file: {0}", path))
        | InputSource.Stream _ -> "flow stream"

    let rec private describeTarget =
        function
        | OutputTarget.Console -> "console"
        | OutputTarget.Inherit -> "inherited handle"
        | OutputTarget.Discard -> "discard"
        | OutputTarget.File path -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "file: {0}", path))
        | OutputTarget.AppendFile path -> (System.String.Format(Globalization.CultureInfo.InvariantCulture, "append file: {0}", path))
        | OutputTarget.Callback _ -> "callback"
        | OutputTarget.Sink _
        | OutputTarget.SinkFactory _ -> "sink"
        | OutputTarget.Tee targets -> "tee (" + (targets |> List.map describeTarget |> String.concat ", ") + ")"

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let planSpecification (specification: ProcessSpec) : ProcessPlan =
        let stages =
            specification.Commands
            |> List.mapi (fun i original ->
                let resolved = Configuration.resolve specification original

                let outgoing =
                    specification.Connections |> List.filter (fun (source, _, _) -> source = i)

                let incoming =
                    specification.Connections |> List.filter (fun (_, target, _) -> target = i)

                let pipedOutput = not outgoing.IsEmpty
                let pipedError = outgoing |> List.exists (fun (_, _, both) -> both)
                let mergedError = specification.MergeStdErr && specification.Leaves.Contains i

                let destinations =
                    outgoing |> List.map (fun (_, target, _) -> string target) |> String.concat ","

                let sources =
                    incoming |> List.map (fun (source, _, _) -> string source) |> String.concat ","

                let provenance name explicit defaults =
                    name,
                    if explicit then "command"
                    elif defaults then "stageDefaults"
                    else "execution default"

                {
                    Stage = i
                    Command = Configuration.renderCommand original
                    WorkingDirectory = resolved.WorkingDirectory
                    Environment = resolved.Environment
                    Encoding =
#if FABLE_COMPILER
                        let sample = resolved.Encoding.Value.GetBytes "A"

                        if sample.Length = 2 then
                            (if sample[0] = 65uy then "utf-16" else "utf-16BE")
                        else
                            "utf-8"
#else
                        resolved.Encoding.Value.WebName
#endif
                    SuccessCodes = resolved.SuccessCodes.Value
                    Timeout = resolved.Timeout.Value
                    StdIn =
                        if incoming.IsEmpty then
                            describeInput resolved.StdIn.Value
                        else
                            "pipe from stages " + sources
                    StdOut =
                        if pipedOutput then
                            "pipe to stages " + destinations
                        else
                            describeTarget resolved.StdOut.Value
                    StdErr =
                        if pipedError then "pipe to stages " + destinations
                        elif mergedError then "merged into stdout"
                        else describeTarget resolved.StdErr.Value
                    StdOutCapture =
                        if pipedOutput then
                            OutputRetention.Nothing
                        else
                            resolved.StdOutCapture.Value
                    StdErrCapture =
                        if pipedError || mergedError then
                            OutputRetention.Nothing
                        else
                            resolved.StdErrCapture.Value
                    Provenance =
                        Map.ofList (
                            [
                                provenance "stdin" original.StdIn.IsSome false
                                provenance "stdout" original.StdOut.IsSome false
                                provenance "stderr" original.StdErr.IsSome false
                                provenance
                                    "successCodes"
                                    original.SuccessCodes.IsSome
                                    specification.Defaults.SuccessCodes.IsSome
                                provenance
                                    "workingDirectory"
                                    original.WorkingDirectory.IsSome
                                    specification.Defaults.WorkingDirectory.IsSome
                                provenance "encoding" original.Encoding.IsSome specification.Defaults.Encoding.IsSome
                                provenance "timeout" original.Timeout.IsSome specification.Defaults.Timeout.IsSome
                                provenance
                                    "stdoutCapture"
                                    original.StdOutCapture.IsSome
                                    specification.Defaults.StdOutCapture.IsSome
                                provenance
                                    "stderrCapture"
                                    original.StdErrCapture.IsSome
                                    specification.Defaults.StdErrCapture.IsSome
                            ]
                            @ [
                                for KeyValue(key, _) in resolved.Environment do
                                    yield
                                        provenance
                                            ("environment." + key)
                                            (original.Environment.ContainsKey key)
                                            (specification.Defaults.Environment.ContainsKey key)
                            ]
                            @ [
                                if not incoming.IsEmpty then
                                    yield "stdin", "pipe"
                                if pipedOutput then
                                    yield "stdout", "pipe"
                                    yield "stdoutCapture", "unavailable: pipe"
                                if pipedError || mergedError then
                                    yield "stderr", (if pipedError then "pipe" else "mergeStderr")
                                    yield "stderrCapture", "unavailable: connected channel"
                            ]
                        )
                })

        {
            Commands = stages |> List.map _.Command
            Stages = stages
            Connections = specification.Connections
            MergeStdErr = specification.MergeStdErr
            Framing = specification.Framing
            MaxLineLength = specification.MaxLineLength
            MaxOutputBytes = specification.MaxOutputBytes
            OverallTimeout = specification.OverallTimeout
            DrainTimeout = specification.DrainTimeout
        }

    let inline plan source =
        planSpecification (toSpecification source)

    /// <summary>Renders a plan as short lines: the command pipeline, then one line per stage.</summary>
    /// <param name="plan">A plan produced by <c>plan</c>.</param>
    /// <returns>The command pipeline followed by each stage's input, output, and error routes.</returns>
    let renderPlan (plan: ProcessPlan) : string list =
        let commands = String.concat " | " plan.Commands

        [
            yield (System.String.Format(Globalization.CultureInfo.InvariantCulture, "commands: {0}", commands))
            for stage in plan.Stages do
                yield (System.String.Format(Globalization.CultureInfo.InvariantCulture, "stage {0}: in {1}, out {2}, err {3}", stage.Stage, stage.StdIn, stage.StdOut, stage.StdErr))
        ]

    let service<'env, 'error when 'env :> IHasProcess> : Flow<'env, 'error, IProcess> =
        Flow.envWith _.Process

    let private prepare mode (specification: ProcessSpec) =
        if specification.Leaves.Count <> 1 then
            invalidArg "specification" "Connect fan-in producers to a final command before execution."

        let configured = { specification with Mode = mode } |> Configuration.validate

        {
            configured with
                Commands = configured.Commands |> List.map (Configuration.resolve configured)
                Defaults = StageDefaults.empty
        }

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let toFlowSpecification<'env when 'env :> IHasProcess>
        (specification: ProcessSpec)
        : Flow<'env, ProcessError, ProcessResult> =
        Flow.delay (fun () ->
            let configured = prepare specification.Mode specification

            flow {
                let! processes = service

                return!
                    processes.Execute(
                        configured,
#if FABLE_COMPILER
                        (fun _ -> async.Return())
#else
                        ProcessObserver.ignore
#endif
                    )
                    |> Flow.localEnv (fun _ -> ())
            })

    let inline toFlow source =
        toFlowSpecification (toSpecification source)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let createOutputStream<'env when 'env :> IHasProcess and 'env :> IHasClock>
        requestedChannel
        (specification: ProcessSpec)
        : ProcessStream<'env, ProcessOutput> =
        ProcessStream(fun () ->
            let configured = prepare ExecutionMode.Streaming specification

            let unavailable =
                configured.Commands
                |> List.indexed
                |> List.tryPick (fun (i, c) ->
                    let final = configured.Leaves.Contains i

                    match requestedChannel with
                    | Some OutputChannel.StdOut when
                        final
                        && (match c.StdOut.Value with
                            | OutputTarget.Inherit -> true
                            | _ -> false)
                        ->
                        Some {
                            Stage = i
                            Channel = OutputChannel.StdOut
                        }
                    | Some OutputChannel.StdErr when
                        not (
                            configured.Connections
                            |> List.exists (fun (source, _, both) -> source = i && both)
                        )
                        && not (final && configured.MergeStdErr)
                        && (match c.StdErr.Value with
                            | OutputTarget.Inherit -> true
                            | _ -> false)
                        ->
                        Some {
                            Stage = i
                            Channel = OutputChannel.StdErr
                        }
                    | _ -> None)

            let events =
                AsyncRendezvous<Choice<ProcessOutput, Exit<ProcessResult, ProcessError>>>()

            let deadline = AsyncCell<Cause<ProcessError>>()
            let mutable completion = ProcessCompletion.StoppedEarly
            let mutable pendingFailure: Cause<ProcessError> option = None
            let mutable previous: AsyncCell<unit> option = None
            let mutable started = false

            let step finished =
                flow {
                    if finished then
                        return None
                    else
                        previous |> Option.iter (fun acknowledgment -> acknowledgment.Set())
                        previous <- None

                        if not started then
                            started <- true

                            match unavailable with
                            | Some failure -> return! Flow.fail (ProcessError.ObservationUnavailable failure)
                            | None -> ()

                            let! processes = service

                            let publish outcome =
                                flow {
                                    let! token = Flow.cancellationToken
                                    do! events.Put(Choice2Of2 outcome, token)
                                }

                            let producer =
                                flow {
                                    let! token = Flow.cancellationToken

                                    return!
                                        processes.Execute(
                                            configured,
                                            fun output ->
                                                async {
#if FABLE_COMPILER
                                                    return! events.Put(Choice1Of2 output, token)
#else
                                                    let! observationToken = Async.CancellationToken
                                                    return! events.Put(Choice1Of2 output, observationToken)
#endif
                                                }
                                        )
                                        |> Flow.localEnv (fun (_: 'env) -> ())
                                }
                                |> Flow.foldCause (Exit.Success >> publish) (fun cause ->
                                    match Cause.untraced cause with
                                    | Cause.Interrupt -> ()
                                    | _ -> pendingFailure <- Some cause

                                    match Cause.untraced cause with
                                    | Cause.Fail(ProcessError.OverallTimedOut _) -> deadline.Set cause
                                    | _ -> ()

                                    match Cause.untraced cause with
                                    | Cause.Interrupt -> Flow.succeed ()
                                    | _ -> publish (Exit.Failure cause))

                            let! fiber = Flow.fork producer

                            do!
                                Flow.scopeFinalizer (
                                    Flow.fromAsync (async {
                                        fiber.InterruptSource.Cancel()
#if FABLE_COMPILER
                                        let! _ = fiber.ExitTask
#else
                                        let! _ = fiber.ExitTask |> Async.AwaitTask
#endif
                                        return ()
                                    }))

                        let! token = Flow.cancellationToken
                        let! item, acknowledgment = events.Take token

                        match item with
                        | Choice1Of2 output ->
                            previous <- Some acknowledgment
                            return Some(output, false)
                        | Choice2Of2 outcome ->
                            acknowledgment.Set()
                            pendingFailure <- None
                            let! result = Flow.ofExit outcome
                            completion <- ProcessCompletion.Completed result
                            return None
                }

            let observed = FlowStream.unfoldFlow step false

            let selected =
                match requestedChannel with
                | None -> observed
                | Some channel -> observed |> FlowStream.filter (fun output -> output.ObservedChannel = channel)

            let guard =
                flow {
                    let! token = Flow.cancellationToken
                    let! outcome = deadline.Wait token |> Async.Catch |> Flow.fromAsync

                    match outcome with
                    | Choice1Of2 cause ->
                        pendingFailure <- None
                        return! Flow.ofExit (Exit.Failure cause)
                    | Choice2Of2 error ->
                        return! Flow.ofExit (Exit.Failure(classifyDeadlineWaitFailure token error))
                }

            selected, (fun () -> completion), guard, (fun () -> pendingFailure))

    /// Observes all readable unconnected output channels, preserving originating channel metadata.
    let inline streamOutputs source =
        createOutputStream None (toSpecification source)

    /// Observes final effective stdout chunks; explicitly merged stderr is included.
    let inline streamStdout source =
        createOutputStream (Some OutputChannel.StdOut) (toSpecification source)

    /// Observes readable unmerged stderr chunks from every stage.
    let inline streamStderr source =
        createOutputStream (Some OutputChannel.StdErr) (toSpecification source)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let lineStream requested source =
        let (ProcessStream factory) =
            createOutputStream requested {
                source with
                    Framing = OutputFraming.Lines
            }

        ProcessStream(fun () ->
            let stream, completion, guard, failure = factory ()
            stream |> FlowStream.map (fun output -> output.Text), completion, guard, failure)

    /// Observes final stdout lines without LF or CRLF terminators; stderr stays separate by default.
    let inline streamStdoutLines source =
        lineStream (Some OutputChannel.StdOut) (toSpecification source)

    /// Observes readable unmerged stderr lines from every stage.
    let inline streamStderrLines source =
        lineStream (Some OutputChannel.StdErr) (toSpecification source)
#if !FABLE_COMPILER
    let live (clock: IClock) (fileSystem: IFileSystem) (console: IConsole) =
        { new IProcess with
            member _.Execute(specification, observe) =
                ProcessRun.execute clock fileSystem console specification observe }
#endif
#if !FABLE_COMPILER
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let createHost (console: IConsole) : ProcessHostEnvironment = {
        Process = live Clock.live FileSystem.live console
        Console = console
        Clock = Clock.live
    }

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let liveHost = lazy (createHost Console.live)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let unwrapRun =
        function
        | Exit.Success value -> value
        | Exit.Failure cause -> raise (ProcessRunException cause)

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    type Runner =
        static member TryRun(specification: ProcessSpec) =
            toFlowSpecification specification |> Flow.run liveHost.Value

        static member TryRun(command: ProcessCommand) =
            Runner.TryRun(ProcessInput.ToSpecification command)

        static member TryRun(workflow: Flow<ProcessHostEnvironment, 'error, 'value>) = Flow.run liveHost.Value workflow

        static member TryRunWith(environment: 'env, specification: ProcessSpec) =
            toFlowSpecification specification |> Flow.run environment

        static member TryRunWith(environment: 'env, command: ProcessCommand) =
            Runner.TryRunWith(environment, ProcessInput.ToSpecification command)

        static member TryRunWith(environment: 'env, workflow: Flow<'env, 'error, 'value>) =
            Flow.run environment workflow

        static member Run(specification: ProcessSpec) =
            Runner.TryRun specification |> unwrapRun

        static member Run(command: ProcessCommand) = Runner.TryRun command |> unwrapRun
        static member Run(workflow: Flow<ProcessHostEnvironment, 'error, 'value>) = Runner.TryRun workflow |> unwrapRun

        static member RunWith(environment: 'env, specification: ProcessSpec) =
            Runner.TryRunWith(environment, specification) |> unwrapRun

        static member RunWith(environment: 'env, command: ProcessCommand) =
            Runner.TryRunWith(environment, command) |> unwrapRun

        static member RunWith(environment: 'env, workflow: Flow<'env, 'error, 'value>) =
            Runner.TryRunWith(environment, workflow) |> unwrapRun

    /// Runs with live services and returns the value; raises ProcessRunException on failure.
    let inline run (source: ^source) : ^result =
        ((^source or Runner): (static member Run: ^source -> ^result) source)

    /// Runs with supplied services and returns the value; raises ProcessRunException on failure.
    let inline runWith (environment: 'env) (source: ^source) : ^result =
        ((^source or Runner): (static member RunWith: 'env * ^source -> ^result) (environment, source))

    /// Runs with live services and returns Exit, including typed failures and interruptions.
    let inline tryRun (source: ^source) : ^result =
        ((^source or Runner): (static member TryRun: ^source -> ^result) source)

    /// Runs with supplied services and returns Exit without raising on workflow failure.
    let inline tryRunWith (environment: 'env) (source: ^source) : ^result =
        ((^source or Runner): (static member TryRunWith: 'env * ^source -> ^result) (environment, source))

    let test () = TestProcess()
#endif

#if !FABLE_COMPILER
[<RequireQualifiedAccess>]
module ProcessHostEnvironment =
    let live = Process.liveHost.Value
    let liveWithConsole console = Process.createHost console

    let ofParts processes console clock : ProcessHostEnvironment = {
        Process = processes
        Console = console
        Clock = clock
    }

    let test () =
        let processes = Process.test ()
        let console = Console.test ()
        let clock = ManualClock(DateTimeOffset.UnixEpoch)

        {
            Environment = ofParts processes console clock
            Process = processes
            Console = console
            Clock = clock
        }
#endif

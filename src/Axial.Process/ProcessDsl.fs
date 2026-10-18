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

module DSL =
    let private formatValue format (value: obj) =
        if isNull value then
            ""
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

    let inline (=>) (source: ^source) (destination: ^destination) : ^result =
        ((^source or ^destination or EndpointConnector): (static member Connect: ^source * ^destination -> ^result) (source,
                                                                                                                     destination))

    let cmd template = Process.command template
    let cmdText text = Process.commandText text

    let pipe commandLines =
        match commandLines |> Seq.map cmd |> Seq.toList with
        | [] -> invalidArg "commandLines" "A pipeline needs a command."
        | head :: tail ->
            tail
            |> List.fold (fun prior next -> Process.pipe next prior) (Process.ofCommand head)

    let inline mergeStdout sources = Process.mergeStdout sources

    let inline mergeStdoutBytes sources =
        Process.mergeStdout sources |> Process.framing OutputFraming.Chunks

    let private shellCommand
        (executable: string)
        (prefix: string -> string list)
        (placeholder: int -> string)
        (program: FormattableString)
        =
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

                if closing < 0 then
                    invalidArg (nameof program) "Unclosed shell interpolation hole."

                let descriptor = format.Substring(index + 1, closing - index - 1)
                let separator = descriptor.IndexOfAny [| ','; ':' |]

                let indexText =
                    if separator < 0 then
                        descriptor
                    else
                        descriptor.Substring(0, separator)

                let valueIndex = Int32.Parse(indexText, CultureInfo.InvariantCulture)

                if valueIndex < 0 || valueIndex >= values.Length then
                    invalidArg (nameof program) "Shell interpolation index is out of range."

                let colon = descriptor.IndexOf ':'

                if colon >= 0 then
                    valueFormats[valueIndex] <- descriptor.Substring(colon + 1)

                script.Append(placeholder valueIndex) |> ignore
                index <- closing + 1
            else
                script.Append format[index] |> ignore
                index <- index + 1

        let actualValues =
            values
            |> Array.mapi (fun index ->
                function
                | :? SecretArgument as secret -> formatValue valueFormats[index] secret.Value
                | value -> formatValue valueFormats[index] value)
            |> Array.toList

        let arguments = prefix (script.ToString()) @ actualValues
        let firstValue = arguments.Length - actualValues.Length

        let redacted =
            values
            |> Array.mapi (fun index value -> index, value)
            |> Array.choose (fun (index, value) ->
                match value with
                | :? SecretArgument -> Some(firstValue + index, "***")
                | _ -> None)
            |> Map.ofArray

        Process.commandArgs executable arguments
        |> Process.withRedactedArguments redacted

    /// Builds a Bash program with interpolation values passed as positional arguments.
    let bash program =
        shellCommand
            "bash"
            (fun script -> [ "-o"; "pipefail"; "-c"; script; "axial" ])
            (fun index -> "\"${" + string (index + 1) + "}\"")
            program

    /// Builds a POSIX shell program with interpolation values passed as positional arguments.
    let sh program =
        shellCommand
            "sh"
            (fun script -> [ "-c"; script; "axial" ])
            (fun index -> "\"${" + string (index + 1) + "}\"")
            program

    /// Builds a PowerShell 7 program with interpolation values passed through <c>$args</c>.
    let pwsh program =
        shellCommand
            "pwsh"
            (fun script -> [ "-NoProfile"; "-NonInteractive"; "-Command"; script ])
            (fun index -> $"$args[{index}]")
            program

    /// Builds an explicitly assembled Bash program. Values in the text are not escaped by Axial.
    let bashText program =
        Process.commandArgs "bash" [ "-o"; "pipefail"; "-c"; program ]

    /// Builds an explicitly assembled POSIX shell program. Values in the text are not escaped by Axial.
    let shText program =
        Process.commandArgs "sh" [ "-c"; program ]

    /// Builds an explicitly assembled PowerShell 7 program. Values in the text are not escaped by Axial.
    let pwshText program =
        Process.commandArgs "pwsh" [ "-NoProfile"; "-NonInteractive"; "-Command"; program ]


    let secret value = SecretArgument(box value)
    let cwd path command = Process.workingDirectory path command
    let env name value command = Process.environment name value command
    let stdinFrom value command = Process.stdinFrom value command
    let stdoutTo value command = Process.stdoutTo value command
    let stderrTo value command = Process.stderrTo value command
    let stdoutToFile value command = Process.stdoutToFile value command

    let stdoutToAppendFile value command =
        Process.stdoutToAppendFile value command

    let stderrToFile value command = Process.stderrToFile value command

    let stderrToAppendFile value command =
        Process.stderrToAppendFile value command

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

    let mapFinalCommand transform source =
        Process.mapFinalCommand transform source

    let inline pipeTo next source = Process.pipe next source
    let inline pipeStdoutAndStderrTo next source = Process.pipeStdoutAndStderr next source

    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let captureParallelSpecifications maximumConcurrency commands =
        if maximumConcurrency <= 0 then
            invalidArg "maximumConcurrency" "Concurrency must be positive."

        let runBatch batch =
            batch
            |> List.map (fun specification -> Process.toFlowSpecification specification |> Flow.map List.singleton)
            |> function
                | [] -> Flow.ok []
                | first :: rest ->
                    rest
                    |> List.fold
                        (fun prior next -> Flow.zipPar prior next |> Flow.map (fun (left, right) -> left @ right))
                        first

        commands
        |> Seq.toList
        |> List.chunkBySize maximumConcurrency
        |> List.map runBatch
        |> Flow.sequence
        |> Flow.map List.concat

    /// Captures independent commands or specs with bounded workflow concurrency, preserving input order.
    let inline captureParallel maximumConcurrency commands =
        commands
        |> Seq.map Process.toSpecification
        |> captureParallelSpecifications maximumConcurrency

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
        /// Consumes a cold byte stream with backpressure. Close over dependencies with Flow.localEnv (or Layer.provide) before building the spec.
        let flowStream (source: FlowStream<unit, 'error, byte array>) =
            source |> FlowStream.mapError (fun error -> (Cause.Fail error).ToString()) |> InputSource.Stream
#if !FABLE_COMPILER
        /// Adapts a .NET stream into a backpressured input source.
        let stream (source: Stream) =
            // The stream remains caller-owned; each execution creates a fresh enumerator.
            let blocks =
                { new Collections.Generic.IAsyncEnumerable<byte array> with
                    member _.GetAsyncEnumerator(token) =
                        let buffer = Array.zeroCreate<byte> 81920
                        let mutable current = [||]
                        { new Collections.Generic.IAsyncEnumerator<byte array> with
                            member _.Current = current
                            member _.MoveNextAsync() =
                                ValueTask<bool>(task {
                                    let! count = source.ReadAsync(buffer, 0, buffer.Length, token)
                                    current <- buffer[.. count - 1]
                                    return count > 0
                                })
                            member _.DisposeAsync() = ValueTask() } }
            blocks |> FlowStream.ofAsyncEnumerable |> FlowStream.mapError (fun error -> string error) |> InputSource.Stream
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
        /// Sends exact byte chunks to a backpressured Flow callback using the process cancellation token.
        let callback (write: byte array -> Flow<unit, 'error, unit>) =
            OutputTarget.Callback(fun bytes -> write bytes |> Flow.mapError (fun error -> (Cause.Fail error).ToString()))
        /// Sends every byte chunk to each target in order.
        let tee targets = OutputTarget.Tee(List.ofSeq targets)
#if !FABLE_COMPILER
        /// Adapts a writable .NET stream into an output target.
        let stream (target: Stream) =
            OutputTarget.Sink(
                (fun bytes -> Flow.fromAsync (async { let! token = Async.CancellationToken
                                                  do! target.WriteAsync(bytes, 0, bytes.Length, token) |> Async.AwaitTask })),
                (fun () -> Flow.fromAsync (async { let! token = Async.CancellationToken
                                                  do! target.FlushAsync(token) |> Async.AwaitTask }))
            )

        /// Decodes output incrementally into a .NET text writer.
        let textWriter (encoding: Encoding) (target: TextWriter) =
            OutputTarget.SinkFactory(fun () ->
                let decoder = encoding.GetDecoder()
                let write (bytes: byte array) =
                    Flow.fromAsync (async {
                            let! token = Async.CancellationToken
                            let chars = Array.zeroCreate<char>(encoding.GetMaxCharCount bytes.Length)
                            let count = decoder.GetChars(bytes, 0, bytes.Length, chars, 0, false)
#if NETSTANDARD2_1
                            do! target.WriteAsync(chars, 0, count) |> Async.AwaitTask
#else
                            do! target.WriteAsync(chars.AsMemory(0, count), token) |> Async.AwaitTask
#endif
                        })
                let finish () =
                    Flow.fromAsync (async {
                            let! token = Async.CancellationToken
                            let chars = Array.zeroCreate<char>(encoding.GetMaxCharCount 0)
                            let count = decoder.GetChars([||], 0, 0, chars, 0, true)
                            if count > 0 then
#if NETSTANDARD2_1
                                do! target.WriteAsync(chars, 0, count) |> Async.AwaitTask
#else
                                do! target.WriteAsync(chars.AsMemory(0, count), token) |> Async.AwaitTask
#endif
#if NETSTANDARD2_1
                            do! target.FlushAsync() |> Async.AwaitTask
#else
                            do! target.FlushAsync(token) |> Async.AwaitTask
#endif
                        })
                write, finish)
#endif

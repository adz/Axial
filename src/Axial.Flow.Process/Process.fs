namespace Axial.Flow.Process

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open Axial.Flow

/// <summary>A safely tokenized external command.</summary>
type Command =
    internal
        { FileName: string
          Arguments: string list
          WorkingDirectory: string option
          Environment: Map<string, string option>
          StandardInput: string option }

/// <summary>One or more commands whose standard streams are connected left to right.</summary>
type Pipeline =
    internal { Commands: Command list }

/// <summary>Represents the captured outcome of an external process pipeline.</summary>
type ProcessResult =
    {
        /// <summary>The exit code returned by the last process.</summary>
        ExitCode: int
        /// <summary>The standard output produced by the last process.</summary>
        StdOut: string
        /// <summary>The combined standard error produced by every process.</summary>
        StdErr: string
        /// <summary>The exit code of each process, from left to right.</summary>
        ExitCodes: int list
    }

/// <summary>An incremental chunk read from a running pipeline.</summary>
[<RequireQualifiedAccess>]
type ProcessOutput =
    /// <summary>Text written by the final command to standard output.</summary>
    | StdOut of text: string
    /// <summary>Text written to standard error, with the command's zero-based pipeline index.</summary>
    | StdErr of commandIndex: int * text: string

/// <summary>Describes failure to start or successfully complete a process pipeline.</summary>
[<RequireQualifiedAccess>]
type ProcessError =
    /// <summary>A command could not be started.</summary>
    | StartFailed of command: string * message: string
    /// <summary>Pipeline execution was canceled.</summary>
    | Canceled of message: string
    /// <summary>At least one command exited unsuccessfully.</summary>
    | ExitedNonZero of result: ProcessResult
    /// <summary>An unexpected process I/O failure occurred.</summary>
    | Io of message: string

/// <summary>Provides asynchronous execution for typed external-process pipelines.</summary>
type IProcess =
    /// <summary>Executes a pipeline and captures its output without interpreting exit codes.</summary>
    abstract Execute : pipeline: Pipeline * onOutput: (ProcessOutput -> Task<unit>) option * cancellationToken: CancellationToken -> Task<Result<ProcessResult, ProcessError>>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module ProcessError =
    /// <summary>Formats a process failure for logs or terminal diagnostics.</summary>
    /// <example><code>error |&gt; ProcessError.describe</code></example>
    let describe = function
        | ProcessError.StartFailed(command, message) -> $"Could not start '{command}': {message}"
        | ProcessError.Canceled message -> $"Process execution was canceled: {message}"
        | ProcessError.ExitedNonZero result ->
            let exitCodes = String.Join(", ", result.ExitCodes)
            $"Process pipeline exited with codes {exitCodes}."
        | ProcessError.Io message -> $"Process I/O failed: {message}"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Process =
    /// <summary>Creates a safely tokenized command. Each argument is passed to the executable exactly as supplied.</summary>
    /// <example><code>Process.command "git" [ "status"; "--short" ]</code></example>
    let command (fileName: string) (arguments: string list) : Command =
        if String.IsNullOrWhiteSpace fileName then
            invalidArg (nameof fileName) "A command file name cannot be empty."

        { FileName = fileName
          Arguments = arguments
          WorkingDirectory = None
          Environment = Map.empty
          StandardInput = None }

    /// <summary>Returns the executable name from a command value.</summary>
    /// <example><code>command |&gt; Process.fileName</code></example>
    let fileName (command: Command) = command.FileName

    /// <summary>Returns the safely tokenized arguments from a command value.</summary>
    /// <example><code>command |&gt; Process.arguments</code></example>
    let arguments (command: Command) = command.Arguments

    /// <summary>Returns the configured working directory.</summary>
    /// <example><code>command |&gt; Process.tryWorkingDirectory</code></example>
    let tryWorkingDirectory (command: Command) = command.WorkingDirectory

    /// <summary>Returns environment overrides, where <c>None</c> removes an inherited variable.</summary>
    /// <example><code>command |&gt; Process.environmentVariables</code></example>
    let environmentVariables (command: Command) = command.Environment

    /// <summary>Returns configured standard-input text.</summary>
    /// <example><code>command |&gt; Process.tryInput</code></example>
    let tryInput (command: Command) = command.StandardInput

    /// <summary>Adds one argument without invoking a shell or performing string splitting.</summary>
    /// <example><code>Process.command "printf" [ "%s" ] |&gt; Process.arg "hello world"</code></example>
    let arg (value: string) (command: Command) : Command =
        { command with Arguments = command.Arguments @ [ value ] }

    /// <summary>Runs a command in the supplied working directory.</summary>
    /// <example><code>Process.command "git" [ "status" ] |&gt; Process.workingDirectory repo</code></example>
    let workingDirectory (path: string) (command: Command) : Command =
        { command with WorkingDirectory = Some path }

    /// <summary>Sets an environment variable for one command.</summary>
    /// <example><code>Process.command "dotnet" [ "test" ] |&gt; Process.environment "CI" "true"</code></example>
    let environment (name: string) (value: string) (command: Command) : Command =
        { command with Environment = command.Environment.Add(name, Some value) }

    /// <summary>Removes an inherited environment variable for one command.</summary>
    /// <example><code>Process.command "env" [] |&gt; Process.removeEnvironment "TOKEN"</code></example>
    let removeEnvironment (name: string) (command: Command) : Command =
        { command with Environment = command.Environment.Add(name, None) }

    /// <summary>Supplies text to the standard input of the first command.</summary>
    /// <example><code>Process.command "sort" [] |&gt; Process.input "b\na\n"</code></example>
    let input (text: string) (command: Command) : Command =
        { command with StandardInput = Some text }

    /// <summary>Converts a command into a one-command pipeline.</summary>
    /// <example><code>Process.command "git" [ "status" ] |&gt; Process.pipeline</code></example>
    let pipeline (command: Command) : Pipeline = { Commands = [ command ] }

    /// <summary>Returns the commands in a pipeline from left to right.</summary>
    /// <example><code>pipeline |&gt; Process.commands</code></example>
    let commands (pipeline: Pipeline) = pipeline.Commands

    /// <summary>Connects the current pipeline's standard output to the next command's standard input.</summary>
    /// <example><code>Process.command "git" [ "log" ] |&gt; Process.pipe (Process.command "head" [ "-n"; "5" ])</code></example>
    let pipe (next: Command) (source: Pipeline) : Pipeline =
        { Commands = source.Commands @ [ next ] }

    /// <summary>Executes a pipeline and returns all exit codes and captured output, including non-zero exits.</summary>
    /// <example><code>Process.command "git" [ "status" ] |&gt; Process.pipeline |&gt; Process.runResult</code></example>
    let runResult<'env when 'env :> IHas<IProcess>> (pipeline: Pipeline) : Flow<'env, ProcessError, ProcessResult> =
        flow {
            let! service = Service<IProcess>.get()
            let! cancellationToken = Flow.Runtime.cancellationToken
            let! outcome: Result<ProcessResult, ProcessError> = service.Execute(pipeline, None, cancellationToken)
            return! outcome
        }

    /// <summary>Executes a pipeline while asynchronously observing stdout and stderr chunks as they arrive.</summary>
    /// <example><code>pipeline |&gt; Process.runResultStreaming (fun output -&gt; task { printf "%A" output })</code></example>
    let runResultStreaming<'env when 'env :> IHas<IProcess>>
        (onOutput: ProcessOutput -> Task<unit>)
        (pipeline: Pipeline)
        : Flow<'env, ProcessError, ProcessResult> =
        flow {
            let! service = Service<IProcess>.get()
            let! cancellationToken = Flow.Runtime.cancellationToken
            let! outcome: Result<ProcessResult, ProcessError> = service.Execute(pipeline, Some onOutput, cancellationToken)
            return! outcome
        }

    /// <summary>Executes a pipeline and fails with <c>ExitedNonZero</c> when any command returns a non-zero exit code.</summary>
    /// <example><code>Process.command "dotnet" [ "test" ] |&gt; Process.pipeline |&gt; Process.run</code></example>
    let run<'env when 'env :> IHas<IProcess>> (pipeline: Pipeline) : Flow<'env, ProcessError, ProcessResult> =
        flow {
            let! result = runResult pipeline
            if result.ExitCodes |> List.exists ((<>) 0) then
                return! Flow.fail (ProcessError.ExitedNonZero result)
            else
                return result
        }

    /// <summary>Streams output while executing and fails when any command returns a non-zero exit code.</summary>
    /// <example><code>pipeline |&gt; Process.runStreaming (fun output -&gt; task { printf "%A" output })</code></example>
    let runStreaming<'env when 'env :> IHas<IProcess>>
        (onOutput: ProcessOutput -> Task<unit>)
        (pipeline: Pipeline)
        : Flow<'env, ProcessError, ProcessResult> =
        flow {
            let! result = runResultStreaming onOutput pipeline
            if result.ExitCodes |> List.exists ((<>) 0) then
                return! Flow.fail (ProcessError.ExitedNonZero result)
            else
                return result
        }

    /// <summary>Executes one command. Prefer <c>command</c>, <c>|&gt;&gt;</c>, and <c>run</c> for new code.</summary>
    /// <example><code>Process.execute "dotnet" [ "--version" ]</code></example>
    let execute<'env when 'env :> IHas<IProcess>> (fileName: string) (arguments: string list) : Flow<'env, ProcessError, ProcessResult> =
        command fileName arguments |> pipeline |> run

#if !FABLE_COMPILER
    let private startInfo (command: Command) =
        let info = ProcessStartInfo(command.FileName)
        command.Arguments |> List.iter info.ArgumentList.Add
        command.WorkingDirectory |> Option.iter (fun path -> info.WorkingDirectory <- path)
        command.Environment
        |> Map.iter (fun name value ->
            match value with
            | Some setting -> info.Environment[name] <- setting
            | None -> info.Environment.Remove name |> ignore)
        info.RedirectStandardInput <- true
        info.RedirectStandardOutput <- true
        info.RedirectStandardError <- true
        info.UseShellExecute <- false
        info.CreateNoWindow <- true
        info

    let private waitForExit (proc: Diagnostics.Process) (cancellationToken: CancellationToken) =
#if NETSTANDARD2_1
        task {
            do! Task.Run((fun () -> proc.WaitForExit()), cancellationToken)
        }
#else
        proc.WaitForExitAsync(cancellationToken)
#endif

    /// <summary>Creates a live process service backed by <see cref="T:System.Diagnostics.Process" />.</summary>
    let live : IProcess =
        { new IProcess with
            member _.Execute(pipeline, onOutput, cancellationToken) =
                task {
                    let commands = pipeline.Commands
                    if List.isEmpty commands then
                        return Error(ProcessError.StartFailed("<empty>", "A pipeline must contain at least one command."))
                    else
                        let processes = ResizeArray<Diagnostics.Process>()
                        let! outcome =
                            task {
                              try
                                for command in commands do
                                    let proc = new Diagnostics.Process(StartInfo = startInfo command)
                                    try
                                        if not (proc.Start()) then
                                            raise (InvalidOperationException $"Could not start {command.FileName}.")
                                        processes.Add proc
                                    with error ->
                                        proc.Dispose()
                                        raise (Exception($"{command.FileName}\u0000{error.Message}", error))

                                use registration =
                                    cancellationToken.Register(fun () ->
                                        for proc in processes do
                                            try
                                                if not proc.HasExited then proc.Kill()
                                            with _ -> ())

                                let copyTasks = ResizeArray<Task>()
                                for index = 0 to processes.Count - 2 do
                                    let copy = processes[index].StandardOutput.BaseStream.CopyToAsync(processes[index + 1].StandardInput.BaseStream)
                                    copyTasks.Add(
                                        task {
                                            do! copy
                                            processes[index + 1].StandardInput.Close()
                                        })

                                match commands.Head.StandardInput with
                                | Some text ->
                                    do! processes[0].StandardInput.WriteAsync text
                                    processes[0].StandardInput.Close()
                                | None -> processes[0].StandardInput.Close()

                                let readStream output (stream: StreamReader) =
                                    task {
                                        let captured = StringBuilder()
                                        let buffer = Array.zeroCreate<char> 1024
                                        let mutable reading = true
                                        while reading do
                                            let! count = stream.ReadAsync(buffer, 0, buffer.Length)
                                            if count = 0 then
                                                reading <- false
                                            else
                                                let text = String(buffer, 0, count)
                                                captured.Append text |> ignore
                                                match onOutput with
                                                | Some observe -> do! observe (output text)
                                                | None -> ()
                                        return captured.ToString()
                                    }

                                let stdoutTask = readStream ProcessOutput.StdOut processes[processes.Count - 1].StandardOutput
                                let stderrTasks =
                                    processes
                                    |> Seq.mapi (fun index proc -> readStream (fun text -> ProcessOutput.StdErr(index, text)) proc.StandardError)
                                    |> Seq.toArray
                                do! Task.WhenAll copyTasks
                                let! _ = processes |> Seq.map (fun proc -> waitForExit proc cancellationToken) |> Task.WhenAll
                                cancellationToken.ThrowIfCancellationRequested()
                                let! stdout = stdoutTask
                                let! stderr = Task.WhenAll stderrTasks
                                let exitCodes = processes |> Seq.map _.ExitCode |> Seq.toList
                                return Ok { ExitCode = List.last exitCodes; StdOut = stdout; StdErr = String.concat "" stderr; ExitCodes = exitCodes }
                              with
                              | :? OperationCanceledException as error -> return Error(ProcessError.Canceled error.Message)
                              | error when error.Message.Contains "\u0000" ->
                                  let parts = error.Message.Split('\u0000', 2)
                                  return Error(ProcessError.StartFailed(parts[0], parts[1]))
                              | error -> return Error(ProcessError.Io error.Message)
                            }
                        processes |> Seq.iter _.Dispose()
                        return outcome
                } }

    /// <summary>Builds the live process service as a layer.</summary>
    let layer : Layer<unit, Never, IProcess> = Layer.succeed live
#endif

/// <summary>Opt-in, shell-like names for concise process pipelines.</summary>
module DSL =
    type ProcessPipe =
        static member Pipe(source: Command, next: Command) = Process.pipeline source |> Process.pipe next
        static member Pipe(source: Pipeline, next: Command) = Process.pipe next source
        static member ToPipeline(source: Command) = Process.pipeline source
        static member ToPipeline(source: Pipeline) = source

    /// <summary>Connects the command or pipeline on the left to the command on the right.</summary>
    /// <example><code>cmd "printf" [ "hello" ] |&gt;&gt; cmd "cat" []</code></example>
    let inline (|>>) (source: ^source) (next: Command) : Pipeline =
        ((^source or ProcessPipe) : (static member Pipe : ^source * Command -> Pipeline) (source, next))

    /// <summary>Short form of <c>Process.command</c>.</summary>
    /// <example><code>cmd "git" [ "status"; "--short" ]</code></example>
    let cmd fileName arguments = Process.command fileName arguments

    /// <summary>Short form of <c>Process.workingDirectory</c>.</summary>
    /// <example><code>cmd "git" [ "status" ] |&gt; cwd repo</code></example>
    let cwd path command = Process.workingDirectory path command

    /// <summary>Short form of <c>Process.environment</c>.</summary>
    /// <example><code>cmd "dotnet" [ "test" ] |&gt; env "CI" "true"</code></example>
    let env name value command = Process.environment name value command

    /// <summary>Short form of <c>Process.input</c>.</summary>
    /// <example><code>cmd "sort" [] |&gt; stdin "b\na\n"</code></example>
    let stdin text command = Process.input text command

    /// <summary>Executes a command or pipeline and requires successful exit codes.</summary>
    /// <example><code>cmd "dotnet" [ "test" ] |&gt; run</code></example>
    let inline run (source: ^source) =
        ((^source or ProcessPipe) : (static member ToPipeline : ^source -> Pipeline) source)
        |> Process.run

    /// <summary>Executes a command or pipeline without interpreting its exit codes.</summary>
    /// <example><code>cmd "tool" [] |&gt; runResult</code></example>
    let inline runResult (source: ^source) =
        ((^source or ProcessPipe) : (static member ToPipeline : ^source -> Pipeline) source)
        |> Process.runResult

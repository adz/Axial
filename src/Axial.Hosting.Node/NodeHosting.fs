namespace Axial.Hosting.Node

open System
open System.Collections.Generic
open Axial
open Axial.PlatformService
open Fable.Core
open Fable.Core.JsInterop

/// <summary>Node.js process environment adaptation for Axial's explicit environment-variable service.</summary>
[<RequireQualifiedAccess>]
module NodeEnvironment =
    [<Emit("typeof process !== 'undefined' && process?.versions?.node != null")>]
    let private isNode () : bool = jsNative

    let internal ensureNode () =
        if not (isNode ()) then
            raise (PlatformNotSupportedException("Axial.Hosting.Node requires Fable JavaScript running on Node.js."))

    [<Emit("process.env[$0] ?? null")>]
    let private tryGetRaw (_name: string) : string = jsNative

    [<Emit("$1 == null ? delete process.env[$0] : (process.env[$0] = $1)")>]
    let private setRaw (_name: string) (_value: string) : unit = jsNative

    [<Emit("Object.entries(process.env).filter(([,v]) => v != null)")>]
    let private entries () : (string * string) array = jsNative

    [<Emit("$0.replace(/\\$\\{?([A-Za-z_][A-Za-z0-9_]*)\\}?/g, (_, n) => process.env[n] ?? '')")>]
    let private expandRaw (_text: string) : string = jsNative
    /// <summary>Creates an environment-variable service backed by <c>process.env</c>.</summary>
    let live : IEnvironmentVariables =
        { new IEnvironmentVariables with
            member _.TryGet name =
                ensureNode ()
                Option.ofObj (tryGetRaw name)
            member _.Set(name, value) =
                ensureNode ()
                setRaw name (Option.toObj value)
            member _.Expand text =
                ensureNode ()
                expandRaw text
            member _.GetAll() =
                ensureNode ()
                let values = Dictionary<string, string>()
                for name, value in entries () do values[name] <- value
                values :> IReadOnlyDictionary<string, string> }

/// <summary>Console-error logging for Node applications.</summary>
[<RequireQualifiedAccess>]
module NodeLogging =
    // Keep percent directives in user messages literal; trailing absent arguments are omitted so the console never prints null or [].
    let private arguments (message: string) (entry: LogEntry) : obj array =
        let error: obj = match entry.Error with Some error -> box error | None -> null
        let annotations = Map.toArray entry.Annotations
        let locals = List.toArray entry.Locals
        let tail: obj array =
            if locals.Length > 0 then [| error; box annotations; box locals |]
            elif annotations.Length > 0 then [| error; box annotations |]
            elif not (isNull error) then [| error |]
            else [||]
        Array.append [| box "%s"; box message |] tail

    [<Emit("console.error(...$0)")>]
    let private write (_arguments: obj array) : unit = jsNative

    /// <summary>Writes enabled entries to stderr with level, exception and metadata snapshots.</summary>
    let sink (minimum: LogLevel) : ILogSink =
        { new ILogSink with
            member _.IsEnabled level = level >= minimum
            member _.Write entry =
                if entry.Level >= minimum then
                    write (arguments (string entry.Level + ": " + entry.Message) entry) }

/// <summary>Root logging policy. None leaves the runtime default; LogSink.none silences the host.</summary>
type NodeAppOptions =
    { LogSink: ILogSink option }
    static member Default = { LogSink = Some(NodeLogging.sink LogLevel.Information) }

/// <summary>Node.js signal, argument, and process-exit integration for root Flow applications.</summary>
[<RequireQualifiedAccess>]
module NodeApp =
    [<Emit("process.argv.slice(2)")>]
    let private argvRaw () : string array = jsNative

    [<Emit("process.on($0, $1)")>]
    let private onSignal (_name: string) (_handler: unit -> unit) : unit = jsNative

    [<Emit("process.off($0, $1)")>]
    let private offSignal (_name: string) (_handler: unit -> unit) : unit = jsNative

    [<Emit("process.exitCode = $0")>]
    let private setExitCode (_code: int) : unit = jsNative

    [<Emit("console.error($0)")>]
    let private writeError (_message: string) : unit = jsNative
    /// <summary>Gets command-line arguments after the Node executable and script path.</summary>
    let arguments () : string list =
        NodeEnvironment.ensureNode ()
        argvRaw () |> Array.toList

    /// <summary>Maps a final application exit to conventional Node process exit codes.</summary>
    let exitCode (exit: Exit<'value, 'error>) : int =
        match exit with
        | Exit.Success _ -> 0
        | Exit.Failure cause when Cause.defects cause |> List.isEmpty |> not -> 2
        | Exit.Failure cause when Cause.isInterrupted cause -> 130
        | Exit.Failure _ -> 1

    /// <summary>
    /// Starts a Node application, translating SIGINT and SIGTERM into coordinated stop and publishing its exit code.
    /// </summary>
    let startWithOptions
        (options: NodeAppOptions)
        (describeError: 'error -> string)
        (environment: 'env)
        (application: Flow<'env, 'error, 'value>)
        : AppHandle<'error, 'value> =
        NodeEnvironment.ensureNode ()
        let hosted = match options.LogSink with Some sink -> Flow.withLogSink sink application | None -> application
        let running = App.start environment hosted
        let mutable signalExitCode: int option = None

        let requestStop () =
            async {
                let! _ = running.Stop()
                return ()
            }
            |> Async.StartImmediate

        let interrupt () =
            signalExitCode <- Some 130
            requestStop ()

        let terminate () =
            signalExitCode <- Some 143
            requestStop ()

        onSignal "SIGINT" interrupt
        onSignal "SIGTERM" terminate

        async {
            let! exit = running.Completion
            offSignal "SIGINT" interrupt
            offSignal "SIGTERM" terminate

            match exit with
            | Exit.Success _ -> ()
            | Exit.Failure cause when Cause.isInterrupted cause -> ()
            | Exit.Failure cause -> writeError (Cause.prettyPrint describeError cause)

            let code =
                match exit with
                | Exit.Failure cause when Cause.defects cause |> List.isEmpty |> not -> 2
                | Exit.Failure cause when Cause.isInterrupted cause -> defaultArg signalExitCode 130
                | _ -> exitCode exit

            setExitCode code
        }
        |> Async.StartImmediate

        running

    /// <summary>Starts a Node application with the default console-error log sink.</summary>
    let start describeError environment application =
        startWithOptions NodeAppOptions.Default describeError environment application

    /// <summary>Starts a Node application with explicit logging policy and waits for its final exit.</summary>
    let runWithOptions
        (options: NodeAppOptions)
        (describeError: 'error -> string)
        (environment: 'env)
        (application: Flow<'env, 'error, 'value>)
        : Async<Exit<'value, 'error>> =
        (startWithOptions options describeError environment application).Completion

    /// <summary>Starts a Node application with the default console-error sink and waits for its final exit.</summary>
    let run describeError environment application =
        runWithOptions NodeAppOptions.Default describeError environment application

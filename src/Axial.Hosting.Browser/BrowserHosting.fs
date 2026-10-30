namespace Axial.Hosting.Browser

open System
open Axial
open Fable.Core
open Fable.Core.JsInterop

/// <summary>A structural binding for the browser and JavaScript <c>AbortSignal</c> contract.</summary>
[<AllowNullLiteral>]
type AbortSignal = interface end

/// <summary>Browser console logging with a minimum level.</summary>
[<RequireQualifiedAccess>]
module BrowserLogging =
    let sink (_minimum: LogLevel) : ILogSink = LogSink.none

/// <summary>Root logging policy. None leaves the runtime default; LogSink.none silences the host.</summary>
type BrowserAppOptions =
    { LogSink: ILogSink option }
    static member Default = { LogSink = Some(BrowserLogging.sink LogLevel.Information) }

/// <summary>Browser ownership and AbortSignal integration for root Flow applications.</summary>
[<RequireQualifiedAccess>]
module BrowserApp =
    [<Emit("typeof window !== 'undefined' && typeof document !== 'undefined'")>]
    let private isBrowser () : bool = jsNative

    let private ensureBrowser () =
        if not (isBrowser ()) then
            raise (PlatformNotSupportedException("Axial.Hosting.Browser requires Fable JavaScript running in a browser."))

    [<Emit("$0.aborted")>]
    let private isAborted (_signal: AbortSignal) : bool = jsNative

    [<Emit("$0.addEventListener('abort', $1, { once: true })")>]
    let private addAbort (_signal: AbortSignal) (_handler: unit -> unit) : unit = jsNative

    [<Emit("$0.removeEventListener('abort', $1)")>]
    let private removeAbort (_signal: AbortSignal) (_handler: unit -> unit) : unit = jsNative
    /// <summary>Starts an application owned explicitly by the calling UI or browser module.</summary>
    let mountWithOptions
        (_options: BrowserAppOptions)
        (environment: 'env)
        (application: Flow<'env, 'error, 'value>)
        : AppHandle<'error, 'value> =
        ensureBrowser ()
        App.start environment application

    /// <summary>Starts an application and translates an AbortSignal into coordinated application stop.</summary>
    let startWithSignalOptions
        (_options: BrowserAppOptions)
        (signal: AbortSignal)
        (environment: 'env)
        (application: Flow<'env, 'error, 'value>)
        : AppHandle<'error, 'value> =
        ensureBrowser ()
        if isNull signal then nullArg (nameof signal)

        let running = App.start environment application

        let requestStop () =
            async {
                let! _ = running.Stop()
                return ()
            }
            |> Async.StartImmediate

        if isAborted signal then requestStop () else addAbort signal requestStop

        async {
            let! _ = running.Completion
            removeAbort signal requestStop
        }
        |> Async.StartImmediate

        running

    /// <summary>Mounts a browser application with the default browser-console log sink.</summary>
    let mount environment application =
        mountWithOptions BrowserAppOptions.Default environment application

    /// <summary>Starts a browser application with the default sink and coordinated AbortSignal stop.</summary>
    let startWithSignal signal environment application =
        startWithSignalOptions BrowserAppOptions.Default signal environment application

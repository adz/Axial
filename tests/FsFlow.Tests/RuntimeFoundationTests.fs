namespace FsFlow.Tests

open System
open System.Collections.Generic
open System.Threading
open FsFlow
open FsFlow.Capabilities.Core
open Swensen.Unquote
open Xunit

module RuntimeFoundationTests =
    type private Service =
        { Name: string }

    type private RuntimeOnly =
        { Clock: IClock
          Log: ILog }
        interface IRuntimeCaps with
            member this.Clock = this.Clock
            member this.Log = this.Log

    type private AppOnly =
        { EnvironmentVariables: IEnvironmentVariables }
        interface IAppCaps with
            member this.EnvironmentVariables = this.EnvironmentVariables

    type private RuntimeServices =
        { Clock: IClock
          Log: ILog }
        interface IRuntimeCaps with
            member this.Clock = this.Clock
            member this.Log = this.Log

    type private AppServices =
        { EnvironmentVariables: IEnvironmentVariables }
        interface IAppCaps with
            member this.EnvironmentVariables = this.EnvironmentVariables

    type private AppEnv =
        { Clock: IClock
          Log: ILog
          EnvironmentVariables: IEnvironmentVariables }
        interface IRuntimeCaps with
            member this.Clock = this.Clock
            member this.Log = this.Log
        interface IAppCaps with
            member this.EnvironmentVariables = this.EnvironmentVariables

    [<Fact>]
    let ``runtime registry stores tagged services independently`` () =
        use scope = new Scope()

        let registry =
            Registry.empty scope
            |> Registry.add<Service> None { Name = "default" }
            |> Registry.add<Service> (Some "Main") { Name = "main" }
            |> Registry.add<Service> (Some "Audit") { Name = "audit" }

        test <@ Registry.get<Service> None registry = { Name = "default" } @>
        test <@ Registry.get<Service> (Some "Main") registry = { Name = "main" } @>
        test <@ Registry.get<Service> (Some "Audit") registry = { Name = "audit" } @>
        test <@ Registry.tryGet<Service> (Some "Missing") registry = None @>

    [<Fact>]
    let ``runtime registry replace only changes the addressed slot`` () =
        use scope = new Scope()

        let registry =
            Registry.empty scope
            |> Registry.add<Service> None { Name = "default" }
            |> Registry.add<Service> (Some "Main") { Name = "main" }
            |> Registry.replace<Service> (Some "Main") { Name = "main-2" }

        test <@ Registry.get<Service> None registry = { Name = "default" } @>
        test <@ Registry.get<Service> (Some "Main") registry = { Name = "main-2" } @>

    [<Fact>]
    let ``runtime registry missing lookup fails intentionally`` () =
        use scope = new Scope()

        let registry = Registry.empty scope

        let failed =
            try
                Registry.get<Service> (Some "Missing") registry |> ignore
                false
            with :? KeyNotFoundException ->
                true

        test <@ failed @>

    [<Fact>]
    let ``scope finalizers run in reverse order and only once`` () =
        let calls = ResizeArray<string>()

        let scope = new Scope()
        scope.AddFinalizer(fun () -> calls.Add "first")
        scope.AddFinalizer(fun () -> calls.Add "second")
        scope.AddFinalizer(fun () -> calls.Add "third")

        (scope :> IDisposable).Dispose()
        (scope :> IDisposable).Dispose()

        test <@ List.ofSeq calls = [ "third"; "second"; "first" ] @>

    [<Fact>]
    let ``runtime adapter projects tagged registry services into a nominal contract`` () =
        let scope = new Scope()
        let clock = Clock.fromValue (DateTimeOffset(2026, 5, 15, 9, 30, 0, TimeSpan.Zero))
        let logMessages = ResizeArray<string>()
        let envVars =
            { new IEnvironmentVariables with
                member _.TryGet name = if name = "FSFLOW_APP_TEST" then Some "1" else None }

        let logger =
            { new ILog with
                member _.Info message = logMessages.Add message }

        let registry =
            Registry.empty scope
            |> Registry.add<IClock> None clock
            |> Registry.add<ILog> (Some "Main") logger
            |> Registry.add<IEnvironmentVariables> None envVars

        let adapter (reg: Registry) =
            {
                Clock = Registry.get<IClock> None reg
                Log = Registry.get<ILog> (Some "Main") reg
                EnvironmentVariables = Registry.get<IEnvironmentVariables> None reg
            }

        let env = RuntimeAdapter.provide adapter registry

        test <@ (env :> IRuntimeCaps).Clock.UtcNow() = DateTimeOffset(2026, 5, 15, 9, 30, 0, TimeSpan.Zero) @>
        (env :> IRuntimeCaps).Log.Info "hello"
        test <@ List.ofSeq logMessages = [ "hello" ] @>
        test <@ (env :> IAppCaps).EnvironmentVariables.TryGet "FSFLOW_APP_TEST" = Some "1" @>

    [<Fact>]
    let ``runtime adapter can project a runtime-only contract`` () =
        let scope = new Scope()
        let clock = Clock.fromValue (DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero))
        let logMessages = ResizeArray<string>()

        let logger =
            { new ILog with
                member _.Info message = logMessages.Add message }

        let registry =
            Registry.empty scope
            |> Registry.add<IClock> None clock
            |> Registry.add<ILog> (Some "Main") logger

        let adapter (reg: Registry) =
            {
                Clock = Registry.get<IClock> None reg
                Log = Registry.get<ILog> (Some "Main") reg
            }

        let env = RuntimeAdapter.provide adapter registry

        test <@ (env :> IRuntimeCaps).Clock.UtcNow() = DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero) @>
        (env :> IRuntimeCaps).Log.Info "runtime-only"
        test <@ List.ofSeq logMessages = [ "runtime-only" ] @>

    [<Fact>]
    let ``runtime adapter can project an app-only contract`` () =
        let scope = new Scope()
        let registry =
            Registry.empty scope
            |> Registry.add<IEnvironmentVariables> None
                { new IEnvironmentVariables with
                    member _.TryGet name = if name = "FSFLOW_APP_TEST" then Some "1" else None }

        let adapter (reg: Registry) =
            {
                EnvironmentVariables = Registry.get<IEnvironmentVariables> None reg
            }

        let env = RuntimeAdapter.provide adapter registry

        test <@ (env :> IAppCaps).EnvironmentVariables.TryGet "FSFLOW_APP_TEST" = Some "1" @>

    [<Fact>]
    let ``runtime context keeps runtime and app halves separate in the same workflow`` () =
        let runtime =
            { Clock = Clock.fromValue (DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero))
              Log =
                  { new ILog with
                      member _.Info _ = () } }

        let app =
            { EnvironmentVariables =
                  { new IEnvironmentVariables with
                      member _.TryGet name = if name = "FSFLOW_CTX_TEST" then Some "ctx" else None } }

        let context = HostContext.create runtime app CancellationToken.None

        let workflow : Flow<HostContext<RuntimeServices, AppServices>, string, string> =
            flow {
                let! now = Flow.readHost (fun (runtime: RuntimeServices) -> runtime.Clock.UtcNow())
                let! value =
                    Flow.readAppEnv (fun (app: AppServices) -> app.EnvironmentVariables.TryGet "FSFLOW_CTX_TEST")
                match value with
                | Some value ->
                    let formattedNow = now.ToString("HH:mm")
                    return $"{formattedNow}:{value}"
                | None -> return! Flow.fail "missing"
            }

        test <@ Flow.runSync context workflow = Exit.Success "11:00:ctx" @>

    [<Fact>]
    let ``missing tagged services fail at the adapter boundary`` () =
        let scope = new Scope()
        let registry = Registry.empty scope

        let failed =
            try
                let _ =
                    Registry.get<ILog> (Some "Main") registry
                false
            with :? KeyNotFoundException ->
                true

        test <@ failed @>

    [<Fact>]
    let ``runtime layer composes and projects values without exposing registry internals`` () =
        let scope = new Scope()
        let registry = Registry.empty scope

        let baseLayer : Layer<string, int> =
            fun _ _ -> EffectFlow.ofValue 21

        let mapped = RuntimeLayer.map ((*) 2) baseLayer
        let bound =
            RuntimeLayer.bind
                (fun value ->
                    fun _ _ -> EffectFlow.ofValue (value + 1))
                mapped

        let provided =
            RuntimeLayer.provide bound (fun value -> $"value={value}")

        let result =
            provided registry CancellationToken.None
            |> fun effect -> effect.AsTask().GetAwaiter().GetResult()

        test <@ result = Exit.Success "value=43" @>

    [<Fact>]
    let ``runtime layer local override affects only the nested subtree`` () =
        let scope = new Scope()
        let originalLogMessages = ResizeArray<string>()
        let overrideLogMessages = ResizeArray<string>()

        let originalLogger =
            { new ILog with
                member _.Info message = originalLogMessages.Add message }

        let overrideLogger =
            { new ILog with
                member _.Info message = overrideLogMessages.Add message }

        let registry =
            Registry.empty scope
            |> Registry.add<ILog> (Some "Main") originalLogger

        let readLog : Layer<string, unit> =
            fun reg _ ->
                let logger = Registry.get<ILog> (Some "Main") reg
                logger.Info "hit"
                EffectFlow.ofValue ()

        let overridden =
            RuntimeLayer.local
                (Registry.replace<ILog> (Some "Main") overrideLogger)
                readLog

        let outerResult =
            RuntimeLayer.run readLog registry CancellationToken.None
            |> fun effect -> effect.AsTask().GetAwaiter().GetResult()

        let innerResult =
            RuntimeLayer.run overridden registry CancellationToken.None
            |> fun effect -> effect.AsTask().GetAwaiter().GetResult()

        test <@ outerResult = Exit.Success () @>
        test <@ innerResult = Exit.Success () @>
        test <@ List.ofSeq originalLogMessages = [ "hit" ] @>
        test <@ List.ofSeq overrideLogMessages = [ "hit" ] @>

    [<Theory>]
    [<InlineData("success")>]
    [<InlineData("failure")>]
    [<InlineData("interrupt")>]
    [<InlineData("defect")>]
    let ``runtime layer disposes scope finalizers for every exit path`` (mode: string) =
        let calls = ResizeArray<string>()
        let scope = new Scope()
        let registry = Registry.empty scope

        let layer : Layer<string, int> =
            fun reg _ ->
                reg.Scope.AddFinalizer(fun () -> calls.Add mode)
                match mode with
                | "success" -> EffectFlow.ofValue 1
                | "failure" -> EffectFlow.ofError "boom"
                | "interrupt" -> EffectFlow.ofInterrupt ()
                | "defect" -> EffectFlow.ofDie (InvalidOperationException("defect"))
                | other -> failwithf "Unexpected mode: %s" other

        let exit =
            RuntimeLayer.run layer registry CancellationToken.None
            |> fun effect -> effect.AsTask().GetAwaiter().GetResult()

        test <@ List.ofSeq calls = [ mode ] @>
        match mode, exit with
        | "success", Exit.Success 1 -> ()
        | "failure", Exit.Failure (Cause.Fail "boom") -> ()
        | "interrupt", Exit.Failure Cause.Interrupt -> ()
        | "defect", Exit.Failure (Cause.Die ex) ->
            test <@ ex.Message.Contains("defect") @>
        | _ ->
            failwithf "Unexpected exit for mode %s: %A" mode exit

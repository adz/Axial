namespace FsFlow.Tests

open System
open System.Threading
open System.Threading.Tasks
open FsFlow
open FsFlow.Services.Core
open Swensen.Unquote
open Xunit

module RuntimeFoundationTests =
    [<Fact>]
    let ``scope finalizers run in reverse order and only once`` () =
        let calls = ResizeArray<string>()

        let scope = new Scope()
        scope.AddFinalizer(fun _ ->
            calls.Add "first"
            Task.CompletedTask)
        scope.AddFinalizer(fun _ ->
            calls.Add "second"
            Task.CompletedTask)
        scope.AddFinalizer(fun _ ->
            calls.Add "third"
            Task.CompletedTask)

        (scope :> IDisposable).Dispose()
        (scope :> IDisposable).Dispose()

        test <@ List.ofSeq calls = [ "third"; "second"; "first" ] @>

    [<Fact>]
    let ``scope aggregates cleanup failures`` () =
        let scope = new Scope()
        scope.AddFinalizer(fun _ -> Task.FromException(InvalidOperationException("first")))
        scope.AddFinalizer(fun _ -> Task.FromException(InvalidOperationException("second")))

        let aggregate =
            try
                scope.Close(CancellationToken.None).GetAwaiter().GetResult()
                failwith "Expected scope cleanup failure."
            with :? AggregateException as error ->
                error

        test <@ aggregate.InnerExceptions.Count = 2 @>
        test <@ aggregate.InnerExceptions[0].Message = "second" @>
        test <@ aggregate.InnerExceptions[1].Message = "first" @>

    [<Fact>]
    let ``base runtime layer provisions explicit services from IServiceProvider`` () =
        let clock = Clock.fromValue (DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero))
        let logMessages = ResizeArray<string>()
        let logger =
            { new ILog with
                member _.Info message = logMessages.Add message }
        let random = Random.fromValue 42
        let guid = Guid.fromValue (Guid.Parse "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
        let envVars = EnvironmentVariables.fromPairs [ "FSFLOW_TEST", "value" ]

        let provider =
            { new IServiceProvider with
                member _.GetService(serviceType) =
                    if serviceType = typeof<IClock> then clock :> obj
                    elif serviceType = typeof<ILog> then logger :> obj
                    elif serviceType = typeof<IRandom> then random :> obj
                    elif serviceType = typeof<IGuid> then guid :> obj
                    elif serviceType = typeof<IEnvironmentVariables> then envVars :> obj
                    else null }

        let workflow : Flow<BaseRuntime, BaseRuntimeError, string> =
            flow {
                let! now = Clock.now<BaseRuntime, BaseRuntimeError>
                let formattedNow = now.ToString("HH:mm")
                do! Log.info<BaseRuntime, BaseRuntimeError> $"now={formattedNow}"
                let! next = Random.nextInt<BaseRuntime, BaseRuntimeError> 1 10
                let! id = Guid.newGuid<BaseRuntime, BaseRuntimeError>
                let! value = EnvironmentVariables.tryGet<BaseRuntime, BaseRuntimeError> "FSFLOW_TEST"
                let envValue = defaultArg value "<missing>"
                return $"{next}:{id}:{envValue}"
            }

        let result =
            workflow
            |> Flow.provide BaseRuntime.fromServiceProvider
            |> Flow.runSync provider

        test <@ result = Exit.Success "42:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:value" @>
        test <@ List.ofSeq logMessages = [ "now=10:00" ] @>

    [<Fact>]
    let ``base runtime layer reports missing provider services as typed failures`` () =
        let provider =
            { new IServiceProvider with
                member _.GetService(serviceType) =
                    if serviceType = typeof<IClock> then Clock.live :> obj else null }

        let result =
            Flow.env<BaseRuntime, BaseRuntimeError>
            |> Flow.provide BaseRuntime.fromServiceProvider
            |> Flow.runSync provider

        test <@ result = Exit.Failure (Cause.Fail (BaseRuntimeError.MissingService "ILog")) @>

    [<Fact>]
    let ``base runtime remains a plain record of explicit services`` () =
        let runtime =
            {
                Clock = Clock.fromValue (DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero))
                Log = Log.live
                Random = Random.fromValue 7
                Guid = Guid.fromValue (Guid.Parse "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
                EnvironmentVariables = EnvironmentVariables.fromPairs [ "FSFLOW_RUNTIME", "ok" ]
            }

        test <@ runtime.Clock.UtcNow() = DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero) @>
        test <@ runtime.Random.NextInt 0 10 = 7 @>
        test <@ runtime.Guid.NewGuid() = Guid.Parse "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" @>
        test <@ runtime.EnvironmentVariables.TryGet "FSFLOW_RUNTIME" = Some "ok" @>

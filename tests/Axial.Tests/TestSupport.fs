namespace Axial.Tests

open System
open System.Diagnostics
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks.Sources
open Axial
open Axial.PlatformService

[<AutoOpen>]
module TestExtensions =
    module Flow =
        let runSync (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
            flow.RunSynchronously(environment)
        
        let runSyncWithToken (environment: 'env) (cancellationToken: CancellationToken) (flow: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
            flow.RunSynchronously(environment, cancellationToken = cancellationToken)

module TestSupport =
    /// Polls a fact for at most the supplied duration; false means the failure deadline expired.
    let waitUntil (condition: unit -> bool) (deadline: TimeSpan) =
        if deadline < TimeSpan.Zero then
            invalidArg (nameof deadline) "A polling deadline must be finite and non-negative."

        let elapsed = Stopwatch.StartNew()
        let mutable ready = condition ()

        while not ready && elapsed.Elapsed < deadline do
            Thread.Sleep 1
            ready <- condition ()

        ready

    /// Blocks the calling thread for the duration to simulate work (a slow sample, a loaded pool).
    /// The one allowed Thread.Sleep for simulated work; never use it to wait for a fact.
    let occupyThread (duration: TimeSpan) = Thread.Sleep duration

    type Address =
        { City: string }

    type Customer =
        { Name: string
          Address: Address
          Lines: string list }

    type LoginError =
        | InvalidUser
        | InvalidPwd
        | Unauthorized of string
        | TokenErr of string

    type ReaderEnv =
        { Prefix: string
          Count: int }

    type IDeviceClient =
        abstract Name: string

    type RuntimeServices =
        { RuntimePrefix: string
          Seen: ResizeArray<string> }

    type IHasDeviceClient =
        abstract DeviceClient : IDeviceClient

    type AppDependencies =
        { DeviceClient: IDeviceClient
          Value: int }
        interface IHasDeviceClient with
            member this.DeviceClient = this.DeviceClient

    type RecordingServiceProvider(serviceType: Type, service: obj) =
        interface IServiceProvider with
            member _.GetService(requestedType: Type) =
                if requestedType = serviceType then service else null

    let publicInstanceMethodNames (targetType: Type) =
        targetType.GetMethods()
        |> Array.filter (fun methodInfo -> methodInfo.IsPublic && not methodInfo.IsSpecialName)
        |> Array.map _.Name
        |> Array.distinct
        |> Array.sort

    let flowBuilderBindAndReturnFromArgumentNames () =
        typeof<FlowBuilder>.GetMethods()
        |> Array.filter (fun methodInfo ->
            methodInfo.IsPublic
            && not methodInfo.IsSpecialName
            && (methodInfo.Name = "Bind" || methodInfo.Name = "ReturnFrom"))
        |> Array.collect (fun methodInfo -> methodInfo.GetParameters())
        |> Array.map (fun parameterInfo -> parameterInfo.ParameterType.Name)
        |> Array.distinct
        |> Array.sort

    let hasAsyncResultReturnFromOverload (builderType: Type) =
        builderType.GetMethods()
        |> Array.exists (fun methodInfo ->
            if not methodInfo.IsPublic || methodInfo.IsSpecialName || methodInfo.Name <> "ReturnFrom" then
                false
            else
                let parameters = methodInfo.GetParameters()

                if parameters.Length <> 1 || not parameters[0].ParameterType.IsGenericType then
                    false
                else
                    let asyncType = parameters[0].ParameterType

                    if asyncType.GetGenericTypeDefinition() <> typedefof<Async<_>> then
                        false
                    else
                        let asyncValueType = asyncType.GetGenericArguments()[0]

                        asyncValueType.IsGenericType
                        && asyncValueType.GetGenericTypeDefinition() = typedefof<Result<_, _>>)

    let runFsiScript (scriptContents: string) =
        let scriptPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.fsx")
        File.WriteAllText(scriptPath, scriptContents)

        try
            use childProcess =
                new Process(
                    StartInfo =
                        ProcessStartInfo(
                            FileName = "dotnet",
                            Arguments = $"fsi \"{scriptPath}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false
                        )
                )

            childProcess.Start() |> ignore

            let standardOutput = childProcess.StandardOutput.ReadToEndAsync()
            let standardError = childProcess.StandardError.ReadToEndAsync()
            childProcess.WaitForExit()
            Task.WhenAll(standardOutput, standardError).Wait()

            childProcess.ExitCode, standardOutput.Result + standardError.Result
        finally
            File.Delete scriptPath

    let runBashScriptWithArguments (scriptPath: string) (arguments: string list) (environment: (string * string) list) =
        let quotedArguments =
            arguments |> List.map (fun argument -> $"\"{argument}\"") |> String.concat " "

        use childProcess =
            new Process(
                StartInfo =
                    ProcessStartInfo(
                        FileName = "bash",
                        Arguments = $"\"{scriptPath}\" {quotedArguments}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    )
            )

        for key, value in environment do
            childProcess.StartInfo.EnvironmentVariables[key] <- value

        childProcess.Start() |> ignore

        let standardOutput = childProcess.StandardOutput.ReadToEndAsync()
        let standardError = childProcess.StandardError.ReadToEndAsync()
        let completed = childProcess.WaitForExit(TimeSpan.FromMinutes(2.0))

        if not completed then
            try
                childProcess.Kill(entireProcessTree = true)
            with _ ->
                ()

        Task.WhenAll(standardOutput, standardError).Wait(TimeSpan.FromSeconds(5.0)) |> ignore

        let readCompletedOutput (streamName: string) (readTask: Task<string>) =
            if readTask.IsCompletedSuccessfully then
                readTask.Result
            elif readTask.IsFaulted then
                $"{Environment.NewLine}{streamName} read failed: {readTask.Exception.GetBaseException().Message}"
            elif readTask.IsCanceled then
                $"{Environment.NewLine}{streamName} read was canceled."
            else
                $"{Environment.NewLine}{streamName} read did not complete after the process timeout."

        let output =
            readCompletedOutput "stdout" standardOutput
            + readCompletedOutput "stderr" standardError

        if completed then
            childProcess.ExitCode, output
        else
            124, output + $"{Environment.NewLine}Timed out waiting for {scriptPath}."

    /// Every step names a cumulative sleep-registration fact and an explicit clock advance.
    /// Registration is signalled by a latch, never inferred from idle time or thread-pool counters.
    let runWithCheckpoints (steps: (int * TimeSpan * Flow<unit, 'error, unit>) list) (workflow: Flow<unit, 'error, 'value>) : Exit<'value, 'error> =
        let clock = ManualClock(DateTimeOffset.UnixEpoch)
        use registered = new SemaphoreSlim(0)
        let mutable registrations = 0
        let notifying =
            { new IClock with
                member _.UtcNow() = (clock :> IClock).UtcNow()
                member _.Elapsed() = (clock :> IClock).Elapsed()
                member _.Sleep(delay, token) =
                    let pending = (clock :> IClock).Sleep(delay, token)
                    if delay > TimeSpan.Zero then
                        Interlocked.Increment(&registrations) |> ignore
                        registered.Release() |> ignore
                    pending }
        let driven = flow {
            let! fiber = workflow |> Flow.withClock notifying |> Flow.fork
            for required, duration, checkpoint in steps do
                do! Flow.fromTask (fun token -> task {
                    while Volatile.Read(&registrations) < required do
                        let! signalled = registered.WaitAsync(TimeSpan.FromSeconds 10., token)
                        if not signalled then failwithf "Expected sleep registration %d, observed %d" required registrations
                    return ()
                })
                do! TestClock.advance duration
                do! checkpoint
            return! Fiber.join fiber
        }
        driven |> Flow.withClock clock |> Flow.runSync ()

    let runWithSteps steps workflow =
        workflow |> runWithCheckpoints (steps |> List.map (fun (required, duration) -> required, duration, Flow.ok ()))

    let awaitSignal (signal: SemaphoreSlim) : Flow<unit, 'error, unit> =
        Flow.fromTask (fun token -> task {
            let! signalled = signal.WaitAsync(TimeSpan.FromSeconds 10., token)
            if not signalled then failwith "Expected stream observation was not signalled."
            return ()
        })

    /// A serial timed flow registers exactly one positive delay before each explicit advance.
    let serialSteps (durations: TimeSpan list) =
        durations |> List.mapi (fun index duration -> index + 1, duration)

    let runtimeNow () : Flow<unit, 'error, TimeSpan> = Clock.elapsed

    let runBashScript (scriptPath: string) (environment: (string * string) list) =
        runBashScriptWithArguments scriptPath [] environment

    type SingleConsumptionValueTaskSource<'value>(value: 'value) as this =
        let consumptionCount = ref 0

        member _.AsValueTask() =
            ValueTask<'value>(this :> IValueTaskSource<'value>, 0s)

        member _.ConsumptionCount = consumptionCount.Value

        interface IValueTaskSource<'value> with
            member _.GetStatus(_token: int16) = ValueTaskSourceStatus.Succeeded

            member _.OnCompleted
                (
                    _continuation: Action<obj>,
                    _state: obj,
                    _token: int16,
                    _flags: ValueTaskSourceOnCompletedFlags
                ) =
                ()

            member _.GetResult(_token: int16) =
                let consumptions = Interlocked.Increment consumptionCount

                if consumptions > 1 then
                    invalidOp "ValueTask source consumed more than once."

                value

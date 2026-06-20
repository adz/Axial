namespace FsFlow.Tests

open System
open System.Diagnostics
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks.Sources
open Axial.Flow
open Axial.Result
open Axial.Validation

[<AutoOpen>]
module TestExtensions =
    module Flow =
        let runSync (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
            flow.RunSynchronously(environment)
        
        let runSyncWithToken (environment: 'env) (cancellationToken: CancellationToken) (flow: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
            flow.RunSynchronously(environment, cancellationToken = cancellationToken)

module TestSupport =
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

    type AppDependencies =
        { DeviceClient: IDeviceClient
          Value: int }
        interface IHas<IDeviceClient> with
            member this.Service = this.DeviceClient

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

    let runBashScript (scriptPath: string) (environment: (string * string) list) =
        use childProcess =
            new Process(
                StartInfo =
                    ProcessStartInfo(
                        FileName = "bash",
                        Arguments = $"\"{scriptPath}\"",
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

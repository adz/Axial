namespace FsFlow.Tests

open System
open System.Diagnostics
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Threading.Tasks.Sources
open FsFlow

[<AutoOpen>]
module TestExtensions =
    module Flow =
        let runSync (environment: 'env) (flow: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
            Flow.run environment flow |> fun t -> t.AsTask().GetAwaiter().GetResult()
        
        let runFullSync (environment: 'env) (cancellationToken: CancellationToken) (flow: Flow<'env, 'error, 'value>) : Exit<'value, 'error> =
            Flow.runFull environment cancellationToken flow |> fun t -> t.AsTask().GetAwaiter().GetResult()

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
                        UseShellExecute = false
                    )
            )

        for key, value in environment do
            childProcess.StartInfo.EnvironmentVariables[key] <- value

        childProcess.Start() |> ignore

        let standardOutput = childProcess.StandardOutput.ReadToEnd()
        childProcess.WaitForExit()

        childProcess.ExitCode, standardOutput

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

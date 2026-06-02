namespace FsFlow.Services.Core

open System
open System.Collections.Generic
open System.Globalization
open System.Threading.Tasks
open FsFlow

/// <summary>Provides synchronous access to the current UTC clock.</summary>
type IClock = FsFlow.IClock

/// <summary>Provides synchronous access to runtime logging.</summary>
type ILog = FsFlow.ILog

/// <summary>Provides synchronous random-number generation.</summary>
type IRandom = FsFlow.IRandom

/// <summary>Provides synchronous GUID generation.</summary>
type IGuid = FsFlow.IGuid

/// <summary>Provides synchronous environment-variable lookup.</summary>
type IEnvironmentVariables = FsFlow.IEnvironmentVariables

/// <summary>Describes a meaningful environment-variable failure.</summary>
[<RequireQualifiedAccess>]
type EnvironmentVariableError =
    /// <summary>The requested variable was not present.</summary>
    | MissingVariable of name: string

    /// <summary>The requested variable existed but could not be parsed.</summary>
    | InvalidVariable of name: string * value: string * expected: string

/// <summary>Describes a service-provider bootstrap failure while building the base runtime.</summary>
[<RequireQualifiedAccess>]
type BaseRuntimeError =
    /// <summary>A required service was missing from the provider.</summary>
    | MissingService of serviceName: string

/// <summary>Groups the standard explicit services commonly used by workflow hosts.</summary>
type BaseRuntime =
    {
        Clock: IClock
        Log: ILog
        Random: IRandom
        Guid: IGuid
        EnvironmentVariables: IEnvironmentVariables
    }

    interface IHas<IClock> with
        member this.Service = this.Clock

    interface IHas<ILog> with
        member this.Service = this.Log

    interface IHas<IRandom> with
        member this.Service = this.Random

    interface IHas<IGuid> with
        member this.Service = this.Guid

    interface IHas<IEnvironmentVariables> with
        member this.Service = this.EnvironmentVariables

/// <summary>Helpers for the clock service.</summary>
[<RequireQualifiedAccess>]
module Clock =
    /// <summary>Reads the current UTC timestamp from an explicit clock service.</summary>
    let now<'env, 'error when 'env :> IHas<IClock>> : Flow<'env, 'error, DateTimeOffset> =
        Service<IClock>.get()
        |> Flow.map (fun clock -> clock.UtcNow())

    /// <summary>Creates a live clock backed by <see cref="P:System.DateTimeOffset.UtcNow" />.</summary>
    let live : IClock =
        { new IClock with
            member _.UtcNow() = DateTimeOffset.UtcNow }

    /// <summary>Creates a deterministic clock that always returns the supplied instant.</summary>
    let fromValue (utcNow: DateTimeOffset) : IClock =
        { new IClock with
            member _.UtcNow() = utcNow }

    /// <summary>Builds the live clock as a layer.</summary>
    let layer : Layer<unit, Never, IClock> =
        Layer.succeed live

/// <summary>Helpers for the logging service.</summary>
[<RequireQualifiedAccess>]
module Log =
    /// <summary>Writes an informational log message through an explicit logging service.</summary>
    let info<'env, 'error when 'env :> IHas<ILog>> (message: string) : Flow<'env, 'error, unit> =
        Service<ILog>.get()
        |> Flow.map (fun log -> log.Info message)

    /// <summary>Creates a no-op logger for tests and local service bundles.</summary>
    let live : ILog =
        { new ILog with
            member _.Info _ = () }

    /// <summary>Builds the live logger as a layer.</summary>
    let layer : Layer<unit, Never, ILog> =
        Layer.succeed live

/// <summary>Helpers for the random-number service.</summary>
[<RequireQualifiedAccess>]
module Random =
    /// <summary>Reads a random integer from an explicit random-number service.</summary>
    let nextInt<'env, 'error when 'env :> IHas<IRandom>>
        (minInclusive: int)
        (maxExclusive: int)
        : Flow<'env, 'error, int> =
        Service<IRandom>.get()
        |> Flow.map (fun random -> random.NextInt minInclusive maxExclusive)

    /// <summary>Creates a live random-number generator backed by <see cref="T:System.Random" />.</summary>
    let live : IRandom =
        let rng = System.Random()
        let gate = obj()

        { new IRandom with
            member _.NextInt minInclusive maxExclusive =
                #if FABLE_COMPILER
                rng.Next(minInclusive, maxExclusive)
                #else
                lock gate (fun () -> rng.Next(minInclusive, maxExclusive))
                #endif
        }

    /// <summary>Creates a deterministic random generator that always returns the supplied value.</summary>
    let fromValue (value: int) : IRandom =
        { new IRandom with
            member _.NextInt _ _ = value }

    /// <summary>Builds the live random-number generator as a layer.</summary>
    let layer : Layer<unit, Never, IRandom> =
        Layer.succeed live

/// <summary>Helpers for the GUID service.</summary>
[<RequireQualifiedAccess>]
module Guid =
    /// <summary>Reads a GUID from an explicit GUID service.</summary>
    let newGuid<'env, 'error when 'env :> IHas<IGuid>> : Flow<'env, 'error, global.System.Guid> =
        Service<IGuid>.get()
        |> Flow.map (fun guid -> guid.NewGuid())

    /// <summary>Creates a live GUID service backed by <see cref="M:System.Guid.NewGuid" />.</summary>
    let live : IGuid =
        { new IGuid with
            member _.NewGuid() = global.System.Guid.NewGuid() }

    /// <summary>Creates a deterministic GUID service that always returns the supplied value.</summary>
    let fromValue (value: global.System.Guid) : IGuid =
        { new IGuid with
            member _.NewGuid() = value }

    /// <summary>Builds the live GUID service as a layer.</summary>
    let layer : Layer<unit, Never, IGuid> =
        Layer.succeed live

/// <summary>Helpers for the environment-variable service.</summary>
[<RequireQualifiedAccess>]
module EnvironmentVariables =
    /// <summary>Reads a raw environment-variable value from an explicit environment-variable service.</summary>
    let tryGet<'env, 'error when 'env :> IHas<IEnvironmentVariables>>
        (name: string)
        : Flow<'env, 'error, string option> =
        Service<IEnvironmentVariables>.get()
        |> Flow.map (fun environmentVariables -> environmentVariables.TryGet name)

    /// <summary>Creates a live provider backed by the current process environment.</summary>
    let live : IEnvironmentVariables =
        { new IEnvironmentVariables with
            member _.TryGet name =
                #if FABLE_COMPILER
                None
                #else
                match Environment.GetEnvironmentVariable name with
                | null -> None
                | value -> Some value
                #endif
        }

    /// <summary>Creates a deterministic provider from a fixed set of name/value pairs.</summary>
    let fromPairs (values: seq<string * string>) : IEnvironmentVariables =
        #if FABLE_COMPILER
        let lookup = Dictionary<string, string>()

        for (name, value) in values do
            lookup[name.ToLowerInvariant()] <- value

        { new IEnvironmentVariables with
            member _.TryGet name =
                match lookup.TryGetValue(name.ToLowerInvariant()) with
                | true, value -> Some value
                | false, _ -> None }
        #else
        let lookup = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        for (name, value) in values do
            lookup[name] <- value

        { new IEnvironmentVariables with
            member _.TryGet name =
                match lookup.TryGetValue name with
                | true, value -> Some value
                | false, _ -> None }
        #endif

    /// <summary>Builds the live environment-variable service as a layer.</summary>
    let layer : Layer<unit, Never, IEnvironmentVariables> =
        Layer.succeed live

/// <summary>Helpers for reading and parsing environment variables through an explicit service.</summary>
[<RequireQualifiedAccess>]
module EnvironmentVariable =
    let private readParsed
        (expected: string)
        (parser: string -> 'value option)
        (name: string)
        : Flow<'env, EnvironmentVariableError, 'value>
        when 'env :> IHas<IEnvironmentVariables> =
        flow {
            let! value = EnvironmentVariables.tryGet name

            match value with
            | None -> return! Flow.fail (EnvironmentVariableError.MissingVariable name)
            | Some value ->
                match parser value with
                | Some parsed -> return parsed
                | None -> return! Flow.fail (EnvironmentVariableError.InvalidVariable(name, value, expected))
        }

    /// <summary>Reads a raw string environment variable through an explicit service.</summary>
    let get<'env when 'env :> IHas<IEnvironmentVariables>>
        (name: string)
        : Flow<'env, EnvironmentVariableError, string> =
        flow {
            let! value = EnvironmentVariables.tryGet name

            match value with
            | Some value -> return value
            | None -> return! Flow.fail (EnvironmentVariableError.MissingVariable name)
        }

    /// <summary>Reads a raw string environment variable without wrapping it in a result.</summary>
    let tryGet<'env, 'error when 'env :> IHas<IEnvironmentVariables>>
        (name: string)
        : Flow<'env, 'error, string option> =
        EnvironmentVariables.tryGet name

    /// <summary>Reads an integer environment variable through an explicit service.</summary>
    let getInt<'env when 'env :> IHas<IEnvironmentVariables>>
        (name: string)
        : Flow<'env, EnvironmentVariableError, int> =
        readParsed "an integer" (fun value ->
            match Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a GUID environment variable through an explicit service.</summary>
    let getGuid<'env when 'env :> IHas<IEnvironmentVariables>>
        (name: string)
        : Flow<'env, EnvironmentVariableError, global.System.Guid> =
        readParsed "a GUID" (fun value ->
            match Guid.TryParse value with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a boolean environment variable through an explicit service.</summary>
    let getBool<'env when 'env :> IHas<IEnvironmentVariables>>
        (name: string)
        : Flow<'env, EnvironmentVariableError, bool> =
        readParsed "a boolean" (fun value ->
            match Boolean.TryParse value with
            | true, parsed -> Some parsed
            | false, _ -> None) name

/// <summary>Helpers for formatting environment-variable errors.</summary>
[<RequireQualifiedAccess>]
module EnvironmentVariableErrors =
    /// <summary>Formats a human-readable description for an error.</summary>
    let describe =
        function
        | EnvironmentVariableError.MissingVariable name ->
            $"Missing required environment variable '{name}'."
        | EnvironmentVariableError.InvalidVariable(name, value, expected) ->
            $"Environment variable '{name}' had value '{value}' but expected {expected}."

/// <summary>Helpers for constructing the standard explicit service bundle used by workflow hosts.</summary>
[<RequireQualifiedAccess>]
module BaseRuntime =
    let private getService<'service> (serviceProvider: IServiceProvider) =
        match serviceProvider.GetService(typeof<'service>) with
        | null -> Error (BaseRuntimeError.MissingService typeof<'service>.Name)
        | service -> Ok (unbox<'service> service)

    /// <summary>Creates the standard live base runtime as an explicit service bundle.</summary>
    let liveValue : BaseRuntime =
        {
            Clock = Clock.live
            Log = Log.live
            Random = Random.live
            Guid = Guid.live
            EnvironmentVariables = EnvironmentVariables.live
        }

    /// <summary>Builds the standard live base runtime as an explicit service bundle.</summary>
    let live : Layer<unit, Never, BaseRuntime> =
        Layer.succeed liveValue

    /// <summary>Builds the base runtime from an <see cref="T:System.IServiceProvider" />.</summary>
    #if FABLE_COMPILER
    let fromServiceProvider : Layer<IServiceProvider, BaseRuntimeError, BaseRuntime> =
        Layer.effect (fun _ _ ->
            async.Return(
                Exit.Failure (
                    Cause.Die (PlatformNotSupportedException("IServiceProvider layers are not supported on Fable."))
                )
            ))
    #else
    let fromServiceProvider : Layer<IServiceProvider, BaseRuntimeError, BaseRuntime> =
        Layer.effect (fun (serviceProvider, _) _ ->
            ValueTask<Exit<BaseRuntime, BaseRuntimeError>>(
                task {
                    match
                        getService<IClock> serviceProvider,
                        getService<ILog> serviceProvider,
                        getService<IRandom> serviceProvider,
                        getService<IGuid> serviceProvider,
                        getService<IEnvironmentVariables> serviceProvider
                    with
                    | Ok clock, Ok log, Ok random, Ok guid, Ok environmentVariables ->
                        return
                            Exit.Success
                                {
                                    Clock = clock
                                    Log = log
                                    Random = random
                                    Guid = guid
                                    EnvironmentVariables = environmentVariables
                                }
                    | Error error, _, _, _, _
                    | _, Error error, _, _, _
                    | _, _, Error error, _, _
                    | _, _, _, Error error, _
                    | _, _, _, _, Error error ->
                        return Exit.Failure (Cause.Fail error)
                }))
    #endif

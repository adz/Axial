namespace FsFlow.Services.Core

open System
open System.Collections
open System.Collections.Generic
open System.Globalization
open System.Threading.Tasks
open FsFlow

/// <summary>Provides synchronous access to the current UTC clock.</summary>
type IClock = FsFlow.IClock

/// <summary>Provides synchronous access to workflow logging as an explicit service.</summary>
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

    /// <summary>Reads the current UTC date/time from an explicit clock service.</summary>
    let utcDateTime<'env, 'error when 'env :> IHas<IClock>> : Flow<'env, 'error, DateTime> =
        now |> Flow.map _.UtcDateTime

    /// <summary>Reads the current Unix timestamp in seconds from an explicit clock service.</summary>
    let unixTimeSeconds<'env, 'error when 'env :> IHas<IClock>> : Flow<'env, 'error, int64> =
        now |> Flow.map _.ToUnixTimeSeconds()

    /// <summary>Reads the current Unix timestamp in milliseconds from an explicit clock service.</summary>
    let unixTimeMilliseconds<'env, 'error when 'env :> IHas<IClock>> : Flow<'env, 'error, int64> =
        now |> Flow.map _.ToUnixTimeMilliseconds()

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
    /// <summary>Writes a log message at the requested level through an explicit logging service.</summary>
    let log<'env, 'error when 'env :> IHas<ILog>>
        (level: LogLevel)
        (message: string)
        : Flow<'env, 'error, unit> =
        Service<ILog>.get()
        |> Flow.map (fun log -> log.Log level message)

    /// <summary>Writes a trace log message through an explicit logging service.</summary>
    let trace<'env, 'error when 'env :> IHas<ILog>> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Trace message

    /// <summary>Writes a debug log message through an explicit logging service.</summary>
    let debug<'env, 'error when 'env :> IHas<ILog>> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Debug message

    /// <summary>Writes an informational log message through an explicit logging service.</summary>
    let info<'env, 'error when 'env :> IHas<ILog>> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Information message

    /// <summary>Writes a warning log message through an explicit logging service.</summary>
    let warning<'env, 'error when 'env :> IHas<ILog>> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Warning message

    /// <summary>Writes an error log message through an explicit logging service.</summary>
    let error<'env, 'error when 'env :> IHas<ILog>> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Error message

    /// <summary>Writes a critical log message through an explicit logging service.</summary>
    let critical<'env, 'error when 'env :> IHas<ILog>> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Critical message

    /// <summary>Creates a no-op logger for tests and local service bundles.</summary>
    let live : ILog =
        { new ILog with
            member _.Log _ _ = () }

    /// <summary>Creates a logger from a synchronous sink function.</summary>
    let fromSink (sink: LogLevel -> string -> unit) : ILog =
        { new ILog with
            member _.Log level message = sink level message }

    /// <summary>Builds the live logger as a layer.</summary>
    let layer : Layer<unit, Never, ILog> =
        Layer.succeed live

/// <summary>Helpers for the random-number service.</summary>
[<RequireQualifiedAccess>]
module Random =
    /// <summary>Reads a non-negative random integer from an explicit random-number service.</summary>
    let next<'env, 'error when 'env :> IHas<IRandom>> : Flow<'env, 'error, int> =
        Service<IRandom>.get()
        |> Flow.map (fun random -> random.Next())

    /// <summary>Reads a random integer less than the supplied maximum from an explicit random-number service.</summary>
    let nextMax<'env, 'error when 'env :> IHas<IRandom>>
        (maxExclusive: int)
        : Flow<'env, 'error, int> =
        Service<IRandom>.get()
        |> Flow.map (fun random -> random.NextMax maxExclusive)

    /// <summary>Reads a random integer from an explicit random-number service.</summary>
    let nextInt<'env, 'error when 'env :> IHas<IRandom>>
        (minInclusive: int)
        (maxExclusive: int)
        : Flow<'env, 'error, int> =
        Service<IRandom>.get()
        |> Flow.map (fun random -> random.NextInt minInclusive maxExclusive)

    /// <summary>Reads a random floating-point value from an explicit random-number service.</summary>
    let nextDouble<'env, 'error when 'env :> IHas<IRandom>> : Flow<'env, 'error, float> =
        Service<IRandom>.get()
        |> Flow.map (fun random -> random.NextDouble())

    /// <summary>Fills a byte buffer through an explicit random-number service.</summary>
    let nextBytes<'env, 'error when 'env :> IHas<IRandom>>
        (buffer: byte array)
        : Flow<'env, 'error, unit> =
        Service<IRandom>.get()
        |> Flow.map (fun random -> random.NextBytes buffer)

    /// <summary>Creates a byte array filled through an explicit random-number service.</summary>
    let bytes<'env, 'error when 'env :> IHas<IRandom>>
        (count: int)
        : Flow<'env, 'error, byte array> =
        flow {
            let buffer = Array.zeroCreate<byte> count
            do! nextBytes buffer
            return buffer
        }

    /// <summary>Creates a live random-number generator backed by <see cref="T:System.Random" />.</summary>
    let live : IRandom =
        let rng = System.Random()
        let gate = obj()

        { new IRandom with
            member _.Next() =
                #if FABLE_COMPILER
                rng.Next()
                #else
                lock gate rng.Next
                #endif

            member _.NextMax maxExclusive =
                #if FABLE_COMPILER
                rng.Next(maxExclusive)
                #else
                lock gate (fun () -> rng.Next maxExclusive)
                #endif

            member _.NextInt minInclusive maxExclusive =
                #if FABLE_COMPILER
                rng.Next(minInclusive, maxExclusive)
                #else
                lock gate (fun () -> rng.Next(minInclusive, maxExclusive))
                #endif

            member _.NextDouble() =
                #if FABLE_COMPILER
                rng.NextDouble()
                #else
                lock gate rng.NextDouble
                #endif

            member _.NextBytes buffer =
                #if FABLE_COMPILER
                for index in 0 .. buffer.Length - 1 do
                    buffer[index] <- byte (rng.Next(0, 256))
                #else
                lock gate (fun () -> rng.NextBytes buffer)
                #endif
        }

    /// <summary>Creates a deterministic random generator that always returns the supplied value.</summary>
    let fromValue (value: int) : IRandom =
        { new IRandom with
            member _.Next() = value
            member _.NextMax _ = value
            member _.NextInt _ _ = value
            member _.NextDouble() = float value
            member _.NextBytes buffer =
                for index in 0 .. buffer.Length - 1 do
                    buffer[index] <- byte value }

    /// <summary>Creates a deterministic random generator from fixed integer, double, and byte values.</summary>
    let fromFixed
        (integer: int)
        (doubleValue: float)
        (byteValue: byte)
        : IRandom =
        { new IRandom with
            member _.Next() = integer
            member _.NextMax _ = integer
            member _.NextInt _ _ = integer
            member _.NextDouble() = doubleValue
            member _.NextBytes buffer =
                for index in 0 .. buffer.Length - 1 do
                    buffer[index] <- byteValue }

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

    /// <summary>Returns all visible environment variables from an explicit environment-variable service.</summary>
    let getAll<'env, 'error when 'env :> IHas<IEnvironmentVariables>>
        : Flow<'env, 'error, IReadOnlyDictionary<string, string>> =
        Service<IEnvironmentVariables>.get()
        |> Flow.map (fun environmentVariables -> environmentVariables.GetAll())

    /// <summary>Sets or updates an environment variable through an explicit environment-variable service.</summary>
    let set<'env, 'error when 'env :> IHas<IEnvironmentVariables>>
        (name: string)
        (value: string)
        : Flow<'env, 'error, unit> =
        Service<IEnvironmentVariables>.get()
        |> Flow.map (fun environmentVariables -> environmentVariables.Set(name, Some value))

    /// <summary>Clears an environment variable through an explicit environment-variable service.</summary>
    let clear<'env, 'error when 'env :> IHas<IEnvironmentVariables>>
        (name: string)
        : Flow<'env, 'error, unit> =
        Service<IEnvironmentVariables>.get()
        |> Flow.map (fun environmentVariables -> environmentVariables.Set(name, None))

    /// <summary>Expands environment-variable references in text through an explicit environment-variable service.</summary>
    let expand<'env, 'error when 'env :> IHas<IEnvironmentVariables>>
        (text: string)
        : Flow<'env, 'error, string> =
        Service<IEnvironmentVariables>.get()
        |> Flow.map (fun environmentVariables -> environmentVariables.Expand text)

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

            member _.Set(name, value) =
                #if FABLE_COMPILER
                ignore name
                ignore value
                #else
                Environment.SetEnvironmentVariable(name, Option.toObj value)
                #endif

            member _.Expand text =
                #if FABLE_COMPILER
                text
                #else
                Environment.ExpandEnvironmentVariables text
                #endif

            member _.GetAll() =
                #if FABLE_COMPILER
                Dictionary<string, string>() :> IReadOnlyDictionary<string, string>
                #else
                let values = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                let environmentVariables = Environment.GetEnvironmentVariables()

                for entry in environmentVariables do
                    let entry = entry :?> DictionaryEntry
                    values[string entry.Key] <- string entry.Value

                values :> IReadOnlyDictionary<string, string>
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
                | false, _ -> None

            member _.Set(name, value) =
                match value with
                | Some value -> lookup[name.ToLowerInvariant()] <- value
                | None -> lookup.Remove(name.ToLowerInvariant()) |> ignore

            member _.Expand text =
                lookup
                |> Seq.fold (fun (expanded: string) pair -> expanded.Replace("%" + pair.Key + "%", pair.Value)) text

            member _.GetAll() =
                lookup :> IReadOnlyDictionary<string, string> }
        #else
        let lookup = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)

        for (name, value) in values do
            lookup[name] <- value

        { new IEnvironmentVariables with
            member _.TryGet name =
                match lookup.TryGetValue name with
                | true, value -> Some value
                | false, _ -> None

            member _.Set(name, value) =
                match value with
                | Some value -> lookup[name] <- value
                | None -> lookup.Remove name |> ignore

            member _.Expand text =
                lookup
                |> Seq.fold (fun (expanded: string) pair -> expanded.Replace("%" + pair.Key + "%", pair.Value)) text

            member _.GetAll() =
                lookup :> IReadOnlyDictionary<string, string> }
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

    /// <summary>Reads a 64-bit integer environment variable through an explicit service.</summary>
    let getInt64<'env when 'env :> IHas<IEnvironmentVariables>>
        (name: string)
        : Flow<'env, EnvironmentVariableError, int64> =
        readParsed "a 64-bit integer" (fun value ->
            match Int64.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a floating-point environment variable through an explicit service.</summary>
    let getDouble<'env when 'env :> IHas<IEnvironmentVariables>>
        (name: string)
        : Flow<'env, EnvironmentVariableError, float> =
        readParsed "a floating-point number" (fun value ->
            match Double.TryParse(value, NumberStyles.Float ||| NumberStyles.AllowThousands, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a decimal environment variable through an explicit service.</summary>
    let getDecimal<'env when 'env :> IHas<IEnvironmentVariables>>
        (name: string)
        : Flow<'env, EnvironmentVariableError, decimal> =
        readParsed "a decimal number" (fun value ->
            match Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture) with
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

    /// <summary>Reads a URI environment variable through an explicit service.</summary>
    let getUri<'env when 'env :> IHas<IEnvironmentVariables>>
        (name: string)
        : Flow<'env, EnvironmentVariableError, Uri> =
        readParsed "an absolute URI" (fun value ->
            match Uri.TryCreate(value, UriKind.Absolute) with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a time span environment variable through an explicit service.</summary>
    let getTimeSpan<'env when 'env :> IHas<IEnvironmentVariables>>
        (name: string)
        : Flow<'env, EnvironmentVariableError, TimeSpan> =
        readParsed "a time span" (fun value ->
            match TimeSpan.TryParse(value, CultureInfo.InvariantCulture) with
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
        Layer.fromAsync (fun _ _ ->
            async.Return(
                Exit.Failure (
                    Cause.Die (PlatformNotSupportedException("IServiceProvider layers are not supported on Fable."))
                )
            ))
    #else
    let fromServiceProvider : Layer<IServiceProvider, BaseRuntimeError, BaseRuntime> =
        Layer.fromValueTask (fun (serviceProvider, _) _ ->
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

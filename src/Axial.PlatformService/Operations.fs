// This file defines each service's Flow-facing operations alongside its `live` implementation (Clock.live,
// Guid.live, ...): the ambient effects here are each service's own explicit, mockable boundary, not a leak.
// axial-allow-effect-file: clock, guid
namespace Axial.PlatformService

open System
open System.Collections
open System.Collections.Generic
open System.Globalization
open System.Threading.Tasks
open Axial
open Axial.Layers
#if FABLE_COMPILER
open Fable.Core
#endif

/// <summary>Groups the standard operational services commonly used by workflow hosts.</summary>
type BaseRuntime =
    { Clock: IClock
      Log: ILog
      Random: IRandom
      Guid: IGuid
      EnvironmentVariables: IEnvironmentVariables }
    interface IHasClock with
        member this.Clock = this.Clock
    interface IHasLog with
        member this.Log = this.Log
    interface IHasRandom with
        member this.Random = this.Random
    interface IHasGuid with
        member this.Guid = this.Guid
    interface IHasEnvironmentVariables with
        member this.EnvironmentVariables = this.EnvironmentVariables

/// <summary>Helpers for the clock service.</summary>
[<RequireQualifiedAccess>]
module Clock =
    /// Reads the service from the environment.
    let service<'env, 'error when 'env :> IHasClock> : Flow<'env, 'error, IClock> =
        Flow.envWith _.Clock

    /// <summary>Reads the current UTC timestamp from an explicit clock service.</summary>
    let now<'env, 'error when 'env :> IHasClock> : Flow<'env, 'error, DateTimeOffset> =
        service
        |> Flow.map (fun clock -> clock.UtcNow())

    /// <summary>Reads the current UTC date/time from an explicit clock service.</summary>
    let utcDateTime<'env, 'error when 'env :> IHasClock> : Flow<'env, 'error, DateTime> =
        now |> Flow.map _.UtcDateTime

    /// <summary>Reads the current Unix timestamp in seconds from an explicit clock service.</summary>
    let unixTimeSeconds<'env, 'error when 'env :> IHasClock> : Flow<'env, 'error, int64> =
        now |> Flow.map _.ToUnixTimeSeconds()

    /// <summary>Reads the current Unix timestamp in milliseconds from an explicit clock service.</summary>
    let unixTimeMilliseconds<'env, 'error when 'env :> IHasClock> : Flow<'env, 'error, int64> =
        now |> Flow.map _.ToUnixTimeMilliseconds()

    /// <summary>Reads a monotonic timestamp from an explicit clock service.</summary>
    /// <remarks>Only differences between readings are meaningful. Prefer <c>Clock.timed</c> to measure a flow.</remarks>
    let elapsed<'env, 'error when 'env :> IHasClock> : Flow<'env, 'error, TimeSpan> =
        service
        |> Flow.map (fun clock -> clock.Elapsed())

    /// <summary>Runs a flow and returns its value together with how long it took.</summary>
    /// <remarks>
    /// Measured with the clock's monotonic timer, so the duration is unaffected by system clock adjustments and is
    /// deterministic under a test clock. Failures propagate unchanged and are not timed.
    /// </remarks>
    /// <param name="flow">The flow to measure.</param>
    /// <example>
    /// <code>
    /// let! report, took = buildReport |&gt; Clock.timed
    /// </code>
    /// </example>
    let timed<'env, 'error, 'value when 'env :> IHasClock>
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value * TimeSpan> =
        service
        |> Flow.bind (fun (clock: IClock) ->
            let started = clock.Elapsed()
            flow |> Flow.map (fun value -> value, clock.Elapsed() - started))

    /// <summary>Creates a live clock backed by <see cref="P:System.DateTimeOffset.UtcNow" /> and a monotonic timer.</summary>
    let live : IClock =
        { new IClock with
            member _.UtcNow() = DateTimeOffset.UtcNow
            member _.Elapsed() = Platform.monotonicNow ()
            member _.Sleep(delay, cancellationToken) =
#if FABLE_COMPILER
                let pending =
                    JS.Promise.Create<unit>(fun resolve reject ->
                        let deadline = Platform.monotonicNow () + delay
                        let settled = ref false
                        let timer: obj ref = ref null
                        let registration: IDisposable option ref = ref None

                        let finish (outcome: Result<unit, exn>) =
                            if not settled.Value then
                                settled.Value <- true
                                Platform.cancelTimer timer.Value
                                registration.Value |> Option.iter _.Dispose()
                                match outcome with
                                | Ok () -> resolve (box ())
                                | Error error -> reject (box error)

                        let rec wait () =
                            let remaining = deadline - Platform.monotonicNow ()
                            if remaining <= TimeSpan.Zero then
                                finish (Ok ())
                            else
                                timer.Value <- Platform.scheduleTimer wait (int (min 2147483647.0 (Math.Ceiling remaining.TotalMilliseconds)))

                        if cancellationToken.IsCancellationRequested then
                            finish (Error(OperationCanceledException()))
                        else
                            if not (isNull (box cancellationToken)) then
                                let registered = unbox<IDisposable> (box (cancellationToken.Register(fun () -> finish (Error(OperationCanceledException())))))
                                registration.Value <- Some registered
                                if settled.Value then registered.Dispose()
                            if not settled.Value then wait ())
                unbox<Task> pending
#else
                task {
                    let! exit = (Platform.sleepExecution delay cancellationToken : Platform.Execution<unit, Never>).AsTask()
                    match exit with
                    | Exit.Success() -> ()
                    | Exit.Failure Cause.Interrupt -> raise (OperationCanceledException(cancellationToken))
                    | Exit.Failure cause -> failwith $"Clock sleep failed: {cause}"
                } :> Task
#endif
            }

    /// <summary>Creates a deterministic clock that always returns the supplied instant; measured durations are zero.</summary>
    let fromValue (utcNow: DateTimeOffset) : IClock =
        { new IClock with
            member _.UtcNow() = utcNow
            member _.Elapsed() = TimeSpan.Zero
            member _.Sleep(_, _) = raise (InvalidOperationException("A fixed clock cannot sleep; use a manual clock for timed flows."))
        }

    /// <summary>Builds the live clock as a layer.</summary>
    let layer : Layer<unit, Never, IClock> =
        Layer.succeed live

/// <summary>A single entry recorded by a <see cref="T:Axial.PlatformService.TestLog" />.</summary>
type LogEntry =
    { Level: LogLevel
      Message: string
      Error: exn option }

/// <summary>A test double for <see cref="T:Axial.PlatformService.ILog" /> that records entries for assertions.</summary>
type TestLog() =
    let entries = ResizeArray<LogEntry>()

    /// <summary>Records a message at the supplied level.</summary>
    member _.Log(level: LogLevel, message: string) =
        entries.Add { Level = level; Message = message; Error = None }

    /// <summary>Records a message carrying an exception at the supplied level.</summary>
    member _.LogException(level: LogLevel, error: exn, message: string) =
        entries.Add { Level = level; Message = message; Error = Some error }

    /// <summary>The recorded entries, in the order they were written.</summary>
    member _.Entries : IReadOnlyList<LogEntry> = entries

    /// <summary>Removes all recorded entries.</summary>
    member _.Clear() = entries.Clear()

    interface ILog with
        member this.Log level message = this.Log(level, message)
        member this.LogException level error message = this.LogException(level, error, message)

/// <summary>Helpers for the logging service.</summary>
[<RequireQualifiedAccess>]
module Log =
    /// Reads the service from the environment.
    let service<'env, 'error when 'env :> IHasLog> : Flow<'env, 'error, ILog> =
        Flow.envWith _.Log

    /// <summary>Writes a log message at the requested level through an explicit logging service.</summary>
    let log<'env, 'error when 'env :> IHasLog>
        (level: LogLevel)
        (message: string)
        : Flow<'env, 'error, unit> =
        service
        |> Flow.map (fun log -> log.Log level message)

    /// <summary>Writes a trace log message through an explicit logging service.</summary>
    let trace<'env, 'error when 'env :> IHasLog> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Trace message

    /// <summary>Writes a debug log message through an explicit logging service.</summary>
    let debug<'env, 'error when 'env :> IHasLog> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Debug message

    /// <summary>Writes an informational log message through an explicit logging service.</summary>
    let info<'env, 'error when 'env :> IHasLog> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Information message

    /// <summary>Writes a warning log message through an explicit logging service.</summary>
    let warning<'env, 'error when 'env :> IHasLog> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Warning message

    /// <summary>Writes an error log message through an explicit logging service.</summary>
    let error<'env, 'error when 'env :> IHasLog> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Error message

    /// <summary>Writes a critical log message through an explicit logging service.</summary>
    let critical<'env, 'error when 'env :> IHasLog> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Critical message

    /// <summary>Writes a log message carrying an exception through an explicit logging service.</summary>
    let logException<'env, 'error when 'env :> IHasLog>
        (level: LogLevel)
        (error: exn)
        (message: string)
        : Flow<'env, 'error, unit> =
        service
        |> Flow.map (fun log -> log.LogException level error message)

    /// <summary>Writes an error log message carrying an exception through an explicit logging service.</summary>
    let errorExn<'env, 'error when 'env :> IHasLog> (error: exn) (message: string) : Flow<'env, 'error, unit> =
        logException LogLevel.Error error message

    /// <summary>Writes a critical log message carrying an exception through an explicit logging service.</summary>
    let criticalExn<'env, 'error when 'env :> IHasLog> (error: exn) (message: string) : Flow<'env, 'error, unit> =
        logException LogLevel.Critical error message

    /// <summary>Creates a no-op logger for tests and local service bundles.</summary>
    let live : ILog =
        { new ILog with
            member _.Log _ _ = ()
            member _.LogException _ _ _ = () }

    /// <summary>Creates a logger from a synchronous sink function. Exceptions are appended to the message text.</summary>
    let fromSink (sink: LogLevel -> string -> unit) : ILog =
        { new ILog with
            member _.Log level message = sink level message
            member _.LogException level error message = sink level $"{message}{Environment.NewLine}{error}" }

    /// <summary>Creates a recording logger whose entries are exposed for assertions.</summary>
    let capturing () : TestLog = TestLog()

    /// <summary>Builds the live logger as a layer.</summary>
    let layer : Layer<unit, Never, ILog> =
        Layer.succeed live

/// <summary>Helpers for the random-number service.</summary>
[<RequireQualifiedAccess>]
module Random =
    /// Reads the service from the environment.
    let service<'env, 'error when 'env :> IHasRandom> : Flow<'env, 'error, IRandom> =
        Flow.envWith _.Random

    /// <summary>Reads a non-negative random integer from an explicit random-number service.</summary>
    let next<'env, 'error when 'env :> IHasRandom> : Flow<'env, 'error, int> =
        service
        |> Flow.map (fun random -> random.Next())

    /// <summary>Reads a random integer less than the supplied maximum from an explicit random-number service.</summary>
    let nextMax<'env, 'error when 'env :> IHasRandom>
        (maxExclusive: int)
        : Flow<'env, 'error, int> =
        service
        |> Flow.map (fun random -> random.NextMax maxExclusive)

    /// <summary>Reads a random integer from an explicit random-number service.</summary>
    let nextInt<'env, 'error when 'env :> IHasRandom>
        (minInclusive: int)
        (maxExclusive: int)
        : Flow<'env, 'error, int> =
        service
        |> Flow.map (fun random -> random.NextInt minInclusive maxExclusive)

    /// <summary>Reads a random floating-point value from an explicit random-number service.</summary>
    let nextDouble<'env, 'error when 'env :> IHasRandom> : Flow<'env, 'error, float> =
        service
        |> Flow.map (fun random -> random.NextDouble())

    /// <summary>Fills a byte buffer through an explicit random-number service.</summary>
    let nextBytes<'env, 'error when 'env :> IHasRandom>
        (buffer: byte array)
        : Flow<'env, 'error, unit> =
        service
        |> Flow.map (fun random -> random.NextBytes buffer)

    /// <summary>Creates a byte array filled through an explicit random-number service.</summary>
    let bytes<'env, 'error when 'env :> IHasRandom>
        (count: int)
        : Flow<'env, 'error, byte array> =
        flow {
            let buffer = Array.zeroCreate<byte> count
            do! nextBytes buffer
            return buffer
        }

    /// <summary>Creates a live random-number generator backed by <see cref="T:System.Random" />.</summary>
    let live : IRandom =
        Platform.random ()

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

    /// <summary>Creates a deterministic random generator that returns the supplied values in order, then throws.</summary>
    let fromSequence (values: seq<int>) : IRandom =
        let values = Seq.toArray values
        let mutable index = 0

        let next () =
            if index >= values.Length then
                invalidOp "Scripted random exhausted; supply more values with Random.fromSequence."

            let value = values[index]
            index <- index + 1
            value

        { new IRandom with
            member _.Next() = next ()
            member _.NextMax _ = next ()
            member _.NextInt _ _ = next ()
            member _.NextDouble() = float (next ())
            member _.NextBytes buffer =
                for index in 0 .. buffer.Length - 1 do
                    buffer[index] <- byte (next ()) }

    /// <summary>Builds the live random-number generator as a layer.</summary>
    let layer : Layer<unit, Never, IRandom> =
        Layer.succeed live

/// <summary>Helpers for the GUID service.</summary>
[<RequireQualifiedAccess>]
module Guid =
    /// Reads the service from the environment.
    let service<'env, 'error when 'env :> IHasGuid> : Flow<'env, 'error, IGuid> =
        Flow.envWith _.Guid

    /// <summary>Reads a GUID from an explicit GUID service.</summary>
    let newGuid<'env, 'error when 'env :> IHasGuid> : Flow<'env, 'error, global.System.Guid> =
        service
        |> Flow.map (fun guid -> guid.NewGuid())

    /// <summary>Creates a live GUID service backed by <c>Guid.NewGuid()</c>.</summary>
    let live : IGuid =
        { new IGuid with
            member _.NewGuid() = global.System.Guid.NewGuid() }

    /// <summary>Creates a deterministic GUID service that always returns the supplied value.</summary>
    let fromValue (value: global.System.Guid) : IGuid =
        { new IGuid with
            member _.NewGuid() = value }

    /// <summary>Creates a deterministic GUID service that returns the supplied values in order, then throws.</summary>
    let fromSequence (values: seq<global.System.Guid>) : IGuid =
        let values = Seq.toArray values
        let mutable index = 0

        { new IGuid with
            member _.NewGuid() =
                if index >= values.Length then
                    invalidOp "Scripted GUID exhausted; supply more values with Guid.fromSequence."

                let value = values[index]
                index <- index + 1
                value }

    /// <summary>Builds the live GUID service as a layer.</summary>
    let layer : Layer<unit, Never, IGuid> =
        Layer.succeed live

/// <summary>Helpers for the environment-variable service.</summary>
[<RequireQualifiedAccess>]
module EnvironmentVariables =
    /// Reads the service from the environment.
    let service<'env, 'error when 'env :> IHasEnvironmentVariables> : Flow<'env, 'error, IEnvironmentVariables> =
        Flow.envWith _.EnvironmentVariables

    /// <summary>Reads a raw environment-variable value from an explicit environment-variable service.</summary>
    let tryGet<'env, 'error when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, 'error, string option> =
        service
        |> Flow.map (fun environmentVariables -> environmentVariables.TryGet name)

    /// <summary>Returns all visible environment variables from an explicit environment-variable service.</summary>
    let getAll<'env, 'error when 'env :> IHasEnvironmentVariables>
        : Flow<'env, 'error, IReadOnlyDictionary<string, string>> =
        service
        |> Flow.map (fun environmentVariables -> environmentVariables.GetAll())

    /// <summary>Sets or updates an environment variable through an explicit environment-variable service.</summary>
    let set<'env, 'error when 'env :> IHasEnvironmentVariables>
        (name: string)
        (value: string)
        : Flow<'env, 'error, unit> =
        service
        |> Flow.map (fun environmentVariables -> environmentVariables.Set(name, Some value))

    /// <summary>Clears an environment variable through an explicit environment-variable service.</summary>
    let clear<'env, 'error when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, 'error, unit> =
        service
        |> Flow.map (fun environmentVariables -> environmentVariables.Set(name, None))

    /// <summary>Expands environment-variable references in text through an explicit environment-variable service.</summary>
    let expand<'env, 'error when 'env :> IHasEnvironmentVariables>
        (text: string)
        : Flow<'env, 'error, string> =
        service
        |> Flow.map (fun environmentVariables -> environmentVariables.Expand text)

    /// <summary>Creates a live provider backed by the current process environment.</summary>
    let live : IEnvironmentVariables =
        Platform.environmentVariables ()

    /// <summary>Creates a deterministic provider from a fixed set of name/value pairs.</summary>
    let fromPairs (values: seq<string * string>) : IEnvironmentVariables =
        Platform.environmentVariablesFromPairs values

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
        when 'env :> IHasEnvironmentVariables =
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
    let get<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, string> =
        flow {
            let! value = EnvironmentVariables.tryGet name

            match value with
            | Some value -> return value
            | None -> return! Flow.fail (EnvironmentVariableError.MissingVariable name)
        }

    /// <summary>Reads a raw string environment variable without wrapping it in a result.</summary>
    let tryGet<'env, 'error when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, 'error, string option> =
        EnvironmentVariables.tryGet name

    /// <summary>Reads an integer environment variable through an explicit service.</summary>
    let getInt<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, int> =
        readParsed "an integer" (fun value ->
            match Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a 64-bit integer environment variable through an explicit service.</summary>
    let getInt64<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, int64> =
        readParsed "a 64-bit integer" (fun value ->
            match Int64.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a floating-point environment variable through an explicit service.</summary>
    let getDouble<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, float> =
        readParsed "a floating-point number" (fun value ->
            match Double.TryParse(value, NumberStyles.Float ||| NumberStyles.AllowThousands, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a decimal environment variable through an explicit service.</summary>
    let getDecimal<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, decimal> =
        readParsed "a decimal number" (fun value ->
            match Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a GUID environment variable through an explicit service.</summary>
    let getGuid<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, global.System.Guid> =
        readParsed "a GUID" (fun value ->
            match Guid.TryParse value with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a URI environment variable through an explicit service.</summary>
    let getUri<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, Uri> =
        readParsed "an absolute URI" (fun value ->
            match Uri.TryCreate(value, UriKind.Absolute) with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a time span environment variable through an explicit service.</summary>
    let getTimeSpan<'env when 'env :> IHasEnvironmentVariables>
        (name: string)
        : Flow<'env, EnvironmentVariableError, TimeSpan> =
        readParsed "a time span" (fun value ->
            match TimeSpan.TryParse(value, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | false, _ -> None) name

    /// <summary>Reads a boolean environment variable through an explicit service.</summary>
    let getBool<'env when 'env :> IHasEnvironmentVariables>
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
    /// <summary>Creates the standard live base runtime as an explicit service bundle.</summary>
    let liveValue : BaseRuntime =
        { Clock = Clock.live; Log = Log.live; Random = Random.live; Guid = Guid.live; EnvironmentVariables = EnvironmentVariables.live }

    /// <summary>Builds the standard live base runtime as an explicit service bundle.</summary>
    let live : Layer<unit, Never, BaseRuntime> =
        Layer.succeed liveValue

    /// <summary>Builds the base runtime from an <see cref="T:System.IServiceProvider" />.</summary>
    let fromServiceProvider : Layer<IServiceProvider, BaseRuntimeError, BaseRuntime> =
        Platform.servicesFromServiceProvider
        |> Layer.map (fun (clock, log, random, guid, environmentVariables) ->
            { Clock = clock; Log = log; Random = random; Guid = guid; EnvironmentVariables = environmentVariables })

#if !FABLE_COMPILER
/// <summary>A deterministic test environment: a manual clock, a recording log, and scripted random, GUID, and
/// environment-variable services. Replace the random, GUID, and environment-variable services with scripted values
/// before running a test that depends on them.</summary>
type TestRuntime(startUtc: DateTimeOffset) =
    /// <summary>The manual clock; advance it with <c>AdvanceBy</c> or <c>AdvanceToNextDeadline</c>.</summary>
    member val Clock = ManualClock(startUtc)

    /// <summary>The recording logger; inspect <c>Entries</c> after a run.</summary>
    member val Log = TestLog()

    /// <summary>The random service; assign a scripted generator such as <c>Random.fromSequence</c>.</summary>
    member val Random : IRandom = Random.fromSequence Seq.empty with get, set

    /// <summary>The GUID service; assign a scripted generator such as <c>Guid.fromSequence</c>.</summary>
    member val Guid : IGuid = Guid.fromSequence Seq.empty with get, set

    /// <summary>The environment-variable service; assign an in-memory provider such as <c>EnvironmentVariables.fromPairs</c>.</summary>
    member val EnvironmentVariables : IEnvironmentVariables = EnvironmentVariables.fromPairs Seq.empty with get, set

    interface IHasClock with
        member this.Clock = this.Clock
    interface IHasLog with
        member this.Log = this.Log
    interface IHasRandom with
        member this.Random = this.Random
    interface IHasGuid with
        member this.Guid = this.Guid
    interface IHasEnvironmentVariables with
        member this.EnvironmentVariables = this.EnvironmentVariables

/// <summary>Helpers for constructing deterministic test environments.</summary>
[<RequireQualifiedAccess>]
module TestRuntime =
    /// <summary>Creates a deterministic test environment whose clock starts at the supplied instant.</summary>
    let create (startUtc: DateTimeOffset) : TestRuntime = TestRuntime(startUtc)

    /// <summary>Builds a deterministic test environment as a layer.</summary>
    let layer (startUtc: DateTimeOffset) : Layer<unit, Never, TestRuntime> =
        Layer.succeed (create startUtc)
#endif

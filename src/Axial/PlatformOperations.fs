namespace Axial.PlatformService

open System
open System.Collections.Generic
open System.Threading.Tasks
open Axial

/// <summary>Helpers for the clock service.</summary>
[<RequireQualifiedAccess>]
module Clock =
    /// Reads the clock installed in the current runtime scope.
    let service<'env, 'error> : Flow<'env, 'error, IClock> =
        Flow(fun _ _ -> Execution.ofValue (RuntimePlatform.current().Platform.Clock))

    /// <summary>Reads the current UTC timestamp from an explicit clock service.</summary>
    let now<'env, 'error> : Flow<'env, 'error, DateTimeOffset> =
        service
        |> Flow.map (fun clock -> clock.UtcNow())

    /// <summary>Reads the current UTC date/time from an explicit clock service.</summary>
    let utcDateTime<'env, 'error> : Flow<'env, 'error, DateTime> =
        now |> Flow.map _.UtcDateTime

    /// <summary>Reads the current Unix timestamp in seconds from an explicit clock service.</summary>
    let unixTimeSeconds<'env, 'error> : Flow<'env, 'error, int64> =
        now |> Flow.map _.ToUnixTimeSeconds()

    /// <summary>Reads the current Unix timestamp in milliseconds from an explicit clock service.</summary>
    let unixTimeMilliseconds<'env, 'error> : Flow<'env, 'error, int64> =
        now |> Flow.map _.ToUnixTimeMilliseconds()

    /// <summary>Reads a monotonic timestamp from an explicit clock service.</summary>
    /// <remarks>Only differences between readings are meaningful. Prefer <c>Clock.timed</c> to measure a flow.</remarks>
    let elapsed<'env, 'error> : Flow<'env, 'error, TimeSpan> =
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
    let timed<'env, 'error, 'value>
        (flow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value * TimeSpan> =
        service
        |> Flow.bind (fun (clock: IClock) ->
            let started = clock.Elapsed()
            flow |> Flow.map (fun value -> value, clock.Elapsed() - started))

    /// <summary>Creates a live clock backed by <see cref="P:System.DateTimeOffset.UtcNow" /> and a monotonic timer.</summary>
    let live : IClock = PlatformDefaults.clock

    /// <summary>Creates a manual clock starting at the supplied instant. Sleeps require explicit advancement.</summary>
    let fromValue (utcNow: DateTimeOffset) : ManualClock = ManualClock utcNow

#if !FABLE_COMPILER && !NETSTANDARD2_1
    /// <summary>Creates an Axial clock backed by a .NET <see cref="T:System.TimeProvider" />.</summary>
    let fromTimeProvider (provider: TimeProvider) : IClock =
        if isNull provider then nullArg (nameof provider)

        let startedAt = provider.GetTimestamp()

        { new IClock with
            member _.UtcNow() = provider.GetUtcNow()
            member _.Elapsed() = provider.GetElapsedTime(startedAt)
            member _.Sleep(delay, cancellationToken) = Axial.Platform.Bridge.timeProviderDelay provider delay cancellationToken }

    /// <summary>Wraps an Axial clock as a .NET <see cref="T:System.TimeProvider" />.</summary>
    let toTimeProvider (clock: IClock) : TimeProvider =
        { new TimeProvider() with
            member _.GetUtcNow() = clock.UtcNow()
            member _.GetTimestamp() = clock.Elapsed().Ticks
            override _.TimestampFrequency = TimeSpan.TicksPerSecond

            member _.CreateTimer(callback, state, dueTime, period) =
                let mutable disposed = 0
                let mutable current = Axial.Platform.Bridge.newCancellationSource()
                let mutable currentDue = dueTime
                let mutable currentPeriod = period

                let run () =
                    let token = current.Token

                    task {
                        try
                            if currentDue <> System.Threading.Timeout.InfiniteTimeSpan then
                                if currentDue > TimeSpan.Zero then
                                    do! clock.Sleep(currentDue, token)

                                callback.Invoke state

                                if currentPeriod <> System.Threading.Timeout.InfiniteTimeSpan && currentPeriod > TimeSpan.Zero then
                                    while not token.IsCancellationRequested do
                                        do! clock.Sleep(currentPeriod, token)
                                        callback.Invoke state
                        with :? OperationCanceledException ->
                            ()
                    }

                let restart () =
                    let previous = current
                    current <- Axial.Platform.Bridge.newCancellationSource()
                    previous.Cancel()
                    previous.Dispose()
                    run () |> ignore

                run () |> ignore

                { new System.Threading.ITimer with
                    member _.Change(newDueTime, newPeriod) =
                        if disposed = 1 then
                            false
                        else
                            currentDue <- newDueTime
                            currentPeriod <- newPeriod
                            restart ()
                            true

                    member _.Dispose() =
                        if System.Threading.Interlocked.Exchange(&disposed, 1) = 0 then
                            current.Cancel()
                            current.Dispose()

                    member _.DisposeAsync() =
                        if System.Threading.Interlocked.Exchange(&disposed, 1) = 0 then
                            current.Cancel()
                            current.Dispose()

                        System.Threading.Tasks.ValueTask.CompletedTask } }
#endif

/// <summary>A thread-safe recording log sink. Entries are immutable snapshots.</summary>
type TestLog() =
    let entries = ResizeArray<LogEntry>()

    /// <summary>The recorded entries, in the order they were written.</summary>
    member _.Entries : IReadOnlyList<LogEntry> = lock entries (fun () -> entries.ToArray() :> IReadOnlyList<LogEntry>)

    /// <summary>Removes all recorded entries.</summary>
    member _.Clear() = lock entries (fun () -> entries.Clear())

    interface ILogSink with
        member _.IsEnabled _ = true
        member _.Write entry = lock entries (fun () -> entries.Add entry)

/// <summary>Helpers for the logging service.</summary>
[<RequireQualifiedAccess>]
module Log =
    let private emit (level: LogLevel) (error: exn option) (message: string) : Flow<'env, 'error, unit> =
        let completed = Execution.ofValue ()
        Flow(fun _ _ ->
            let runtime = RuntimePlatform.current ()
            let sink = runtime.LogSink
            if LogSink.enabled sink level then
                LogSink.write sink
                    { Level = level; Message = message; Error = error
                      Annotations = runtime.Context.Annotations
                      Locals = runtime.Context.Locals |> Map.toList |> List.choose (fun (_, struct (name, value)) -> name |> Option.map (fun key -> key, value)) }
            completed)

    /// Writes only if the current destination enables this level.
    let log<'env, 'error> (level: LogLevel) (message: string) : Flow<'env, 'error, unit> = emit level None message

    /// <summary>Writes a trace message to the installed log sink.</summary>
    let trace<'env, 'error> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Trace message

    /// <summary>Writes a debug message to the installed log sink.</summary>
    let debug<'env, 'error> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Debug message

    /// <summary>Writes an informational message to the installed log sink.</summary>
    let info<'env, 'error> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Information message

    /// <summary>Writes a warning message to the installed log sink.</summary>
    let warning<'env, 'error> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Warning message

    /// <summary>Writes an error message to the installed log sink.</summary>
    let error<'env, 'error> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Error message

    /// <summary>Writes a critical message to the installed log sink.</summary>
    let critical<'env, 'error> (message: string) : Flow<'env, 'error, unit> =
        log LogLevel.Critical message

    /// <summary>Writes a message carrying an exception to the installed log sink.</summary>
    let logException<'env, 'error>
        (level: LogLevel)
        (error: exn)
        (message: string)
        : Flow<'env, 'error, unit> =
        emit level (Some error) message

    /// <summary>Writes an error message carrying an exception to the installed log sink.</summary>
    let errorExn<'env, 'error> (error: exn) (message: string) : Flow<'env, 'error, unit> =
        logException LogLevel.Error error message

    /// <summary>Writes a critical message carrying an exception to the installed log sink.</summary>
    let criticalExn<'env, 'error> (error: exn) (message: string) : Flow<'env, 'error, unit> =
        logException LogLevel.Critical error message

    /// Console destination, Information and above, on stderr.
    let live : ILogSink = PlatformDefaults.logSink
    let memory () : TestLog = TestLog()

/// <summary>Helpers for the random-number service.</summary>
[<RequireQualifiedAccess>]
module Random =
    /// Reads the random generator installed in the current runtime scope.
    let service<'env, 'error> : Flow<'env, 'error, IRandom> =
        Flow(fun _ _ -> Execution.ofValue (RuntimePlatform.current().Platform.Random))

    /// <summary>Reads a non-negative random integer from an explicit random-number service.</summary>
    let next<'env, 'error> : Flow<'env, 'error, int> =
        service
        |> Flow.map (fun random -> random.Next())

    /// <summary>Reads a random integer less than the supplied maximum from an explicit random-number service.</summary>
    let nextMax<'env, 'error>
        (maxExclusive: int)
        : Flow<'env, 'error, int> =
        service
        |> Flow.map (fun random -> random.NextMax maxExclusive)

    /// <summary>Reads a random integer from an explicit random-number service.</summary>
    let nextInt<'env, 'error>
        (minInclusive: int)
        (maxExclusive: int)
        : Flow<'env, 'error, int> =
        service
        |> Flow.map (fun random -> random.NextInt minInclusive maxExclusive)

    /// <summary>Reads a random floating-point value from an explicit random-number service.</summary>
    let nextDouble<'env, 'error> : Flow<'env, 'error, float> =
        service
        |> Flow.map (fun random -> random.NextDouble())

    /// <summary>Fills a byte buffer through an explicit random-number service.</summary>
    let nextBytes<'env, 'error>
        (buffer: byte array)
        : Flow<'env, 'error, unit> =
        service
        |> Flow.map (fun random -> random.NextBytes buffer)

    /// <summary>Creates a byte array filled through an explicit random-number service.</summary>
    let bytes<'env, 'error>
        (count: int)
        : Flow<'env, 'error, byte array> =
        flow {
            let buffer = Array.zeroCreate<byte> count
            do! nextBytes buffer
            return buffer
        }

    /// <summary>Creates a live random-number generator backed by <see cref="T:System.Random" />.</summary>
    let live : IRandom =
        PlatformDefaults.random

    /// Creates a reproducible generator, independent for each test run.
    let seeded (seed: int) : IRandom =
        let generator = Axial.Platform.Bridge.seededRandom seed
        let gate = obj()
        { new IRandom with
            member _.Next() = lock gate generator.Next
            member _.NextMax maximum = lock gate (fun () -> generator.Next maximum)
            member _.NextInt minimum maximum = lock gate (fun () -> generator.Next(minimum, maximum))
            member _.NextDouble() = lock gate generator.NextDouble
            member _.NextBytes buffer = lock gate (fun () -> generator.NextBytes buffer) }

    /// <summary>Creates a deterministic random generator that always returns the supplied value.</summary>
    /// <remarks>
    /// <c>NextDouble</c> returns the value as a <c>float</c> and <c>NextBytes</c> truncates it to a <c>byte</c>;
    /// use <c>Random.fromFixed</c> when a test needs distinct integer, double, and byte values.
    /// </remarks>
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
    /// <remarks>
    /// Each scripted integer is returned directly by <c>Next</c>, <c>NextMax</c>, and <c>NextInt</c>.
    /// <c>NextDouble</c> returns it as a <c>float</c> and <c>NextBytes</c> truncates it to a <c>byte</c>;
    /// use <c>Random.fromFixed</c> when a test needs distinct integer, double, and byte values.
    /// </remarks>
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

/// <summary>Helpers for the GUID service.</summary>
[<RequireQualifiedAccess>]
module Guid =
    /// Reads the GUID generator installed in the current runtime scope.
    let service<'env, 'error> : Flow<'env, 'error, IGuid> =
        Flow(fun _ _ -> Execution.ofValue (RuntimePlatform.current().Platform.Guid))

    /// <summary>Reads a GUID from an explicit GUID service.</summary>
    let newGuid<'env, 'error> : Flow<'env, 'error, global.System.Guid> =
        service
        |> Flow.map (fun guid -> guid.NewGuid())

    /// <summary>Creates a live GUID service backed by <c>Guid.NewGuid()</c>.</summary>
    let live : IGuid = PlatformDefaults.guid

    /// <summary>Creates a deterministic GUID service that always returns the supplied value.</summary>
    let fromValue (value: global.System.Guid) : IGuid =
        { new IGuid with
            member _.NewGuid() = value }

    /// <summary>Creates a deterministic GUID service that returns the supplied values in order, then throws.</summary>
    let sequence (values: seq<global.System.Guid>) : IGuid =
        let values = Seq.toArray values
        let mutable index = 0

        { new IGuid with
            member _.NewGuid() =
                if index >= values.Length then
                    invalidOp "Scripted GUID exhausted; supply more values with Guid.sequence."

                let value = values[index]
                index <- index + 1
                value }



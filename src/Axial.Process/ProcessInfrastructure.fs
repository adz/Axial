namespace Axial.Process

open System
open System.Collections.Generic
open System.ComponentModel
open System.Diagnostics
open System.IO
open System.Globalization
open System.Text
open System.Threading
open System.Threading.Tasks
open Axial
open Axial.PlatformService
open Axial.Console
open Axial.FileSystem

/// One value shared by async waiters; cancellation removes the waiter rather than abandoning a continuation.
type internal AsyncCell<'value>() =
    let gate = obj ()
    let mutable value: 'value option = None
    let waiters = Dictionary<int, 'value -> unit>()
    let mutable next = 0

    member _.Set(item) =
        let callbacks =
            lock gate (fun () ->
                if value.IsSome then
                    [||]
                else
                    value <- Some item
                    let callbacks = waiters.Values |> Seq.toArray
                    waiters.Clear()
                    callbacks)

        for callback in callbacks do
            callback item

    member _.Wait(token: CancellationToken) : Async<'value> =
        async {
            return!
                Async.FromContinuations(fun (success, error, cancel) ->
                    let completionGate = obj ()
                    let mutable registration: IDisposable option = None
                    let mutable settled = false

                    let finish callback =
                        let invoke, disposable =
                            lock completionGate (fun () ->
                                if settled then
                                    false, None
                                else
                                    settled <- true
                                    true, registration)

                        disposable |> Option.iter _.Dispose()

                        if invoke then
                            callback ()

                    let id, existing =
                        lock gate (fun () ->
                            next <- next + 1
                            next, value)

                    let deliver item = finish (fun () -> success item)

                    match existing with
                    | Some item -> deliver item
                    | None ->
                        lock gate (fun () -> waiters[id] <- deliver)

                        let canceled () =
                            let removed = lock gate (fun () -> waiters.Remove id)

                            if removed then
                                finish (fun () ->
#if FABLE_COMPILER
                                    error (OperationCanceledException())
#else
                                    cancel (OperationCanceledException token)
#endif
                                )

                        let created = token.Register canceled |> box |> unbox<IDisposable>

                        let disposeNow =
                            lock completionGate (fun () ->
                                registration <- Some created
                                settled)

                        if disposeNow then
                            created.Dispose()

                        let completed = lock gate (fun () -> value)

                        completed
                        |> Option.iter (fun item ->
                            let removed = lock gate (fun () -> waiters.Remove id)

                            if removed then
                                deliver item))
        }

type internal AsyncRendezvous<'value>() =
    let gate = obj ()
    let mutable pending: ('value * AsyncCell<unit>) option = None
    let mutable waiter: AsyncCell<'value * AsyncCell<unit>> option = None

    member _.Put(value, token: CancellationToken) : Async<unit> =
        async {
            let acknowledged = AsyncCell<unit>()

            let waiting =
                lock gate (fun () ->
                    match waiter, pending with
                    | Some consumer, None ->
                        waiter <- None
                        Some consumer
                    | None, None ->
                        pending <- Some(value, acknowledged)
                        None
                    | _ -> invalidOp "Only one serialized producer may publish output.")

            waiting |> Option.iter (fun consumer -> consumer.Set(value, acknowledged))
            return! acknowledged.Wait token
        }

    member _.Take(token: CancellationToken) : Async<'value * AsyncCell<unit>> =
        async {
            let waiting = AsyncCell<'value * AsyncCell<unit>>()

            let buffered =
                lock gate (fun () ->
                    match pending, waiter with
                    | Some item, None ->
                        pending <- None
                        Some item
                    | None, None ->
                        waiter <- Some waiting
                        None
                    | _ -> invalidOp "Only one process consumer may pull output.")

            match buffered with
            | Some item -> return item
            | None -> return! waiting.Wait token
        }

#if !FABLE_COMPILER
/// Scripted service; the full handler can coordinate cancellation and failures after output.
type TestProcess() =
    let specs = ResizeArray<ProcessSpec>()
    let mutable outputs: ProcessOutput list = []

    let mutable resultHandler: ProcessSpec -> ProcessResult =
        fun _ -> invalidOp "Configure TestProcess.RunHandler."

    let mutable executeHandler
        : (ProcessSpec -> (ProcessOutput -> Async<unit>) -> Flow<unit, ProcessError, ProcessResult>) option =
        None

    member _.RunSpecs: IReadOnlyList<ProcessSpec> = specs

    member _.Outputs
        with get () = outputs
        and set value = outputs <- value

    member _.RunHandler
        with get () = resultHandler
        and set value = resultHandler <- value

    member _.ExecuteHandler
        with get () = executeHandler
        and set value = executeHandler <- value

    member _.Clear() = specs.Clear()

    static member Result(exitCode: int, stdOut: string, ?stdErr: string) : ProcessResult =
        let stdErr = defaultArg stdErr ""

        let captured text = {
            Text = text
            Bytes = Encoding.UTF8.GetBytes(text: string)
            Truncated = false
            Available = true
        }

        {
            ExitCode = exitCode
            ExitCodes = [ exitCode ]
            StdOut = stdOut
            StdErr = stdErr
            StdOutCapture = captured stdOut
            StdErrCapture = captured stdErr
            Stages = []
            StartedAt = DateTimeOffset.UnixEpoch
            Duration = TimeSpan.Zero
        }

    interface IProcess with
        member _.Execute(specification, observe) =
            Flow.delay (fun () ->
                specs.Add specification

                match executeHandler with
                | Some handler -> handler specification observe
                | None ->
                    flow {
                        for output in outputs do
                            do! observe output

                        return resultHandler specification
                    })

#if !FABLE_COMPILER
type private ProcessFailureException(error: ProcessError) =
    inherit Exception(error.ToString())
    member _.Error = error

type private ObserverDefectException(error: exn) =
    inherit Exception(error.Message, error)

type private StageExitException(stage: int) =
    inherit Exception("Stage exited unsuccessfully.")
    member _.Stage = stage

type private CaptureBuffer(policy: OutputRetention, available: bool) =
    let gate = obj ()
    let mutable truncated = false
    let mutable tailBytes: byte array = [||]
    let stream = new MemoryStream()

    member _.Append(chunk: byte array) =
        lock gate (fun () ->
            match policy with
            | OutputRetention.All -> stream.Write(chunk, 0, chunk.Length)
            | OutputRetention.Nothing -> truncated <- truncated || chunk.Length > 0
            | OutputRetention.Tail limit ->
                if chunk.Length >= limit then
                    truncated <- truncated || tailBytes.Length + chunk.Length > limit
                    tailBytes <- if limit = 0 then [||] else chunk[chunk.Length - limit ..]
                else
                    let prior = min tailBytes.Length (limit - chunk.Length)
                    truncated <- truncated || prior < tailBytes.Length

                    tailBytes <-
                        Array.append
                            (if prior = 0 then
                                 [||]
                             else
                                 tailBytes[tailBytes.Length - prior ..])
                            chunk)

    member _.Finish(encoding: Encoding) : CapturedOutput =
        lock gate (fun () ->
            let bytes =
                match policy with
                | OutputRetention.All -> stream.ToArray()
                | _ -> tailBytes

            {
                Text = encoding.GetString bytes
                Bytes = bytes
                Truncated = truncated
                Available = available
            })

    interface IDisposable with
        member _.Dispose() = stream.Dispose()
#endif

#endif

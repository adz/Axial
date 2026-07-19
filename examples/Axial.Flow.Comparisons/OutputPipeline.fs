/// Scenario 6 — producer/consumer pipeline with backpressure and interruption.
///
/// Stream imported records (or live process output), transform them, persist them, and stop the
/// producer promptly when the consumer fails or the caller cancels.
///
/// Made visible by the type: stream failure and environment requirements stay typed through the
/// pipeline, and a forked producer is a Fiber value someone must join or interrupt.
/// Enforced by the runtime: a cold FlowStream is pulled by its consumer, so a consumer failure
/// stops the producer as part of the same evaluation; fiber interruption reaches sleeps.
/// Still the application's responsibility: choosing the buffering policy and owning every fiber.
module Axial.Flow.Comparisons.OutputPipeline

open System
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Axial.Flow
open Axial.Flow.Process

// --- Shared domain -----------------------------------------------------------------

type RecordError =
    | BadRecord of string
    | SinkFull

/// Parses one raw line into a persistable record.
let parseLine (line: string) : Result<string, RecordError> =
    if line.StartsWith "#" then Error(BadRecord line) else Ok(line.ToUpperInvariant())

// --- Ordinary implementation -------------------------------------------------------

module Ordinary =

    /// Channels, a background producer task, and manual observation of both sides. The broken
    /// version below returns when the consumer fails while its producer keeps writing.
    let processLines
        (produce: ChannelWriter<string> -> CancellationToken -> Task)
        (persist: string -> Result<unit, RecordError>)
        (cancellationToken: CancellationToken)
        : Task<Result<int, RecordError>> =
        task {
            let channel = Channel.CreateBounded<string> 16
            use linked = CancellationTokenSource.CreateLinkedTokenSource cancellationToken

            let producer =
                task {
                    try
                        do! produce channel.Writer linked.Token
                        channel.Writer.Complete()
                    with error ->
                        channel.Writer.Complete error
                }

            let mutable count = 0
            let mutable failure = None

            try
                let mutable proceed = true

                while proceed do
                    let! hasMore = channel.Reader.WaitToReadAsync linked.Token

                    if not hasMore then
                        proceed <- false
                    else
                        let mutable line = Unchecked.defaultof<string>

                        while channel.Reader.TryRead(&line) do
                            if failure.IsNone then
                                match parseLine line |> Result.bind persist with
                                | Ok() -> count <- count + 1
                                | Error error ->
                                    failure <- Some error
                                    proceed <- false
                                    // Forgetting this line is the broken version: the producer
                                    // keeps running after the consumer has given up.
                                    linked.Cancel()
            with :? OperationCanceledException when not cancellationToken.IsCancellationRequested ->
                () // our own linked.Cancel() unwinding the reader

            // Must remember to observe the producer, or its exception is lost.
            try
                do! producer
            with _ ->
                ()

            match failure with
            | Some error -> return Error error
            | None -> return Ok count
        }

// --- Axial implementation ----------------------------------------------------------

module WithFlow =

    /// Flow<'env, RecordError, int> over a cold stream: the consumer pulls, so when persistence
    /// fails the stream is simply never pulled again — no channel, no linked tokens, no orphan.
    let processLines
        (lines: FlowStream<'env, RecordError, string>)
        (persist: string -> Result<unit, RecordError>)
        : Flow<'env, RecordError, int> =
        flow {
            let counter = ref 0

            do!
                lines
                |> FlowStream.mapFlow (fun line -> Flow.fromResult (parseLine line))
                |> FlowStream.runForEachFlow (fun record ->
                    flow {
                        do! Flow.fromResult (persist record)
                        counter.Value <- counter.Value + 1
                    })

            return counter.Value
        }

    /// The process-output variant: `Process.stream` emits typed events from a live process, and
    /// the same pipeline shape consumes them. Requires IHas<IProcess> in the environment.
    let streamProcessOutput<'env when 'env :> IHas<IProcess>>
        (specification: ProcessSpec)
        (persist: string -> Result<unit, RecordError>)
        : Flow<'env, RecordError, int> =
        let lines =
            Process.stream specification
            |> FlowStream.mapError (fun error -> BadRecord(ProcessError.describe error))
            |> FlowStream.choose (function
                | ProcessEvent.Output output when output.Channel = OutputChannel.StdOut ->
                    Some(output.Text.TrimEnd('\r', '\n'))
                | _ -> None)

        processLines lines persist

    /// Explicit coordination when the producer must run ahead: fork it, keep the Fiber, and
    /// always join or interrupt it. Detached fibers are how work leaks.
    let withOwnedProducer
        (producer: Flow<'env, RecordError, unit>)
        (consumer: Flow<'env, RecordError, int>)
        : Flow<'env, RecordError, int> =
        flow {
            let! fiber = Flow.fork producer
            let! processed = consumer

            match! Flow.interrupt fiber with
            | Exit.Success() -> ()
            | Exit.Failure _ -> () // already settled; interruption of a finished fiber is a no-op

            return processed
        }

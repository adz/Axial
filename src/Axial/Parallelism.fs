namespace Axial

open System

/// <summary>A validated concurrency bound and placement for parallel Flow operations.</summary>
type Parallelism =
    private
        { Count: int
          Placement: unit -> RunOn }

/// <summary>Creates concurrency and placement policies for parallel Flow and stream operators.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Parallelism =
    let private create count placement =
        if count <= 0 then invalidArg (nameof count) "Parallelism must be positive."
        { Count = count; Placement = placement }

    /// <summary>Creates a positive concurrency bound whose work starts inline.</summary>
    let bounded count = create count (fun () -> RunOn.Inline)

    /// <summary>Sizes an inline concurrency bound from the number of processors available to the process.</summary>
    let ofProcessors (size: int -> int) =
#if FABLE_COMPILER
        let processors = 1
#else
        // Sizing a worker pool is executor mechanics, like the thread pool reading the same value.
        let processors = Environment.ProcessorCount // axial-allow-AXG001: intentional environment host or live-service boundary
#endif
        create (max 1 (size processors)) (fun () -> RunOn.Inline)

    /// <summary>Runs every item using <paramref name="placement"/>, preserving the concurrency bound.</summary>
    let runOn placement parallelism = { parallelism with Placement = fun () -> placement }

    /// <summary>Uses one thread-pool worker per available processor for CPU-heavy work.</summary>
    let cpu = ofProcessors id |> runOn RunOn.ThreadPool

    /// <summary>Runs up to <paramref name="count"/> waiting operations inline.</summary>
    let io count = bounded count

    /// <summary>Runs up to <paramref name="count"/> blocking operations on dedicated threads.</summary>
    let blocking count = bounded count |> runOn RunOn.DedicatedThread

    /// <summary>Creates a fresh adaptive learner for each traversal run.</summary>
    let adaptive count = create count RunOn.adaptive

    let internal value parallelism = parallelism.Count
    let internal placement parallelism = parallelism.Placement()

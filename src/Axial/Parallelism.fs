namespace Axial

open System

/// <summary>A validated upper bound for concurrent Flow operations.</summary>
type Parallelism = private Parallelism of int

/// <summary>Creates <see cref="T:Axial.Parallelism" /> bounds for parallel Flow and stream operators.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Parallelism =
    /// <summary>Creates a positive concurrency bound.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is not positive.</exception>
    let bounded count =
        if count <= 0 then invalidArg (nameof count) "Parallelism must be positive."
        Parallelism count

    /// <summary>Sizes a concurrency bound from the number of processors available to the process.</summary>
    /// <remarks>
    /// Use this for CPU-bound work. The result is clamped to at least 1, so <c>fun n -> n / 2</c> is safe on a
    /// single-core machine. The processor count is read once, when this is called. On JavaScript the count is 1.
    /// </remarks>
    /// <param name="size">Computes the bound from the processor count.</param>
    /// <example>
    /// <code>
    /// files |&gt; Flow.traversePar (Parallelism.ofProcessors id) hashFile
    /// </code>
    /// </example>
    let ofProcessors (size: int -> int) =
#if FABLE_COMPILER
        let processors = 1
#else
        // Sizing a worker pool is executor mechanics, like the thread pool reading the same value.
        let processors = Environment.ProcessorCount // axial-allow-effect: environment
#endif
        Parallelism(max 1 (size processors))

    let internal value (Parallelism count) = count

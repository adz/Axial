/// What every torture scenario shares: the checks it reports, and the seeded chaos that drives its random choices.
module Axial.TortureTest.Scenario

let clockEnvironment = Axial.ClockEnvironment(Axial.PlatformService.Clock.live)

/// One invariant and whether it held on a run.
type Check = { Invariant: string; Held: bool }

let check (invariant: string) (held: bool) : Check = { Invariant = invariant; Held = held }

/// One round of a scenario: its seed, and the random choices and sizes derived from it.
/// <remarks>
/// The seed fixes the sequence of random choices (which fiber to interrupt, how long to wait), so a failing round
/// can be rerun with the same choices; the interleaving of fibers still differs from run to run. A scale below 100
/// shrinks every size, for slower hosts such as the Fable tests on Node.
/// </remarks>
type Round(seed: int, scale: int) =
    let gate = obj ()
    // xorshift on 32-bit integers, so JavaScript and .NET produce the same sequence for a seed.
    let mutable state = if seed = 0 then 0x2545F491 else seed

    member _.Seed = seed

    /// A number from 0 up to, but not including, <paramref name="bound" />.
    member _.Next(bound: int) : int =
        lock gate (fun () ->
            state <- state ^^^ (state <<< 13)
            state <- state ^^^ ((state >>> 17) &&& 0x7FFF)
            state <- state ^^^ (state <<< 5)
            ((state % bound) + bound) % bound)

    /// True with the given percentage chance.
    member this.Chance(percent: int) : bool = this.Next 100 < percent

    /// Scales a size for this round, never below 1.
    member _.Size(full: int) : int = max 1 (full * scale / 100)

/// A scope finalizer that runs a synchronous action: a Task-returning function on .NET, an Async one under Fable.
#if FABLE_COMPILER
let finalizer (action: unit -> unit) : System.Threading.CancellationToken -> Async<unit> = fun _ -> async { action () }
#else
let finalizer (action: unit -> unit) : System.Threading.CancellationToken -> System.Threading.Tasks.Task =
    fun _ ->
        action ()
        System.Threading.Tasks.Task.CompletedTask
#endif

/// Adds one to a counter shared between fibers and returns the new value. JavaScript runs one fiber at a time, so a
/// plain increment is enough there.
let increment (counter: int ref) : int =
#if FABLE_COMPILER
    counter.Value <- counter.Value + 1
    counter.Value
#else
    System.Threading.Interlocked.Increment &counter.contents
#endif

/// Subtracts one from a counter shared between fibers and returns the new value.
let decrement (counter: int ref) : int =
#if FABLE_COMPILER
    counter.Value <- counter.Value - 1
    counter.Value
#else
    System.Threading.Interlocked.Decrement &counter.contents
#endif

#if FABLE_COMPILER
[<Fable.Core.Emit("performance.now()")>]
let private milliseconds () : float = Fable.Core.Util.jsNative
#endif

/// Starts measuring real time and returns a function that reads the time since. Only the schedule timing checks use
/// it: they are about when runs actually start.
let stopwatch () : unit -> System.TimeSpan =
#if FABLE_COMPILER
    let started = milliseconds ()
    fun () -> System.TimeSpan.FromMilliseconds(milliseconds () - started)
#else
    let watch = System.Diagnostics.Stopwatch.StartNew() // axial-allow-effect: clock
    fun () -> watch.Elapsed // axial-allow-effect: clock
#endif

/// Runs a flow and returns its outcome as a value, so a scenario can inspect failures and interruptions.
let exitOf (flow: Axial.Flow<'env, 'error, 'value>) : Axial.Flow<'env, 'none, Axial.Exit<'value, 'error>> =
    flow |> Axial.Flow.fold (fun value -> Axial.Flow.ok (Axial.Exit.Success value)) (fun cause -> Axial.Flow.ok (Axial.Exit.Failure cause))

let isInterrupted (exit: Axial.Exit<'value, 'error>) =
    match exit with
    | Axial.Exit.Failure cause -> Axial.Cause.isInterrupted cause
    | Axial.Exit.Success _ -> false

/// Whether each producer's values appear in increasing order within <paramref name="values" />.
let inOrderPerProducer (values: (int * int) list) : bool =
    values
    |> List.groupBy fst
    |> List.forall (fun (_, own) -> own |> List.map snd |> List.pairwise |> List.forall (fun (a, b) -> a < b))

/// A named scenario that runs once per round and reports its invariants.
type Scenario =
    { Name: string
      Title: string
      Run: Round -> Axial.Flow<Axial.ClockEnvironment, Axial.Never, Check list> }

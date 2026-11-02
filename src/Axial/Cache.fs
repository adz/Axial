namespace Axial

open System
open System.Collections.Generic
open System.Threading

/// <summary>A keyed, single-flight cache of flow results.</summary>
/// <remarks>Successes are retained until invalidated, evicted, or expired; failures are not cached.
/// Lookups run in the cache owner's scope, independently of callers' interruptions.</remarks>
[<Sealed>]
type Cache<'key, 'error, 'value when 'key: equality>
    internal (start: 'key -> (Exit<'value, 'error> -> unit) -> Platform.Signal<Exit<'value, 'error>> * (unit -> Execution<unit, 'error>),
              capacity: int option, ttl: TimeSpan option, clock: IClock) =
    let gate = obj ()
    let entries = Dictionary<'key, Platform.Signal<Exit<'value, 'error>> * TimeSpan option>(HashIdentity.Structural)
    let order = ResizeArray<'key>()

    member internal _.Get(key: 'key, cancellationToken: CancellationToken) : Execution<'value, 'error> =
        let signal, launch =
            Platform.lock gate (fun () ->
                let found = entries.TryGetValue key
                let expired =
                    match found, ttl with
                    | (true, (_, Some stored)), Some duration -> clock.Elapsed() - stored >= duration
                    | _ -> false
                if expired then
                    entries.Remove key |> ignore
                    order.Remove key |> ignore
                match entries.TryGetValue key with
                | true, (signal, _) ->
                    order.Remove key |> ignore
                    order.Add key |> ignore
                    signal, None
                | _ ->
                    let mine = ref None
                    let forget exit =
                        Platform.lock gate (fun () ->
                            match entries.TryGetValue key, mine.Value with
                            | (true, (active, _)), Some own when obj.ReferenceEquals(active, own) ->
                                match exit with
                                | Exit.Failure _ ->
                                    entries.Remove key |> ignore
                                    order.Remove key |> ignore
                                | Exit.Success _ -> entries[key] <- (active, Some(clock.Elapsed()))
                            | _ -> ())
                    let signal, launch = start key forget
                    mine.Value <- Some signal
                    entries[key] <- (signal, None)
                    order.Add key |> ignore
                    match capacity with
                    | Some limit when entries.Count > limit ->
                        let oldest = order[0]
                        order.RemoveAt(0)
                        entries.Remove oldest |> ignore
                    | _ -> ()
                    signal, Some launch)
        match launch with
        | Some launch -> launch () |> Execution.bind (fun () -> Flow.awaitShared signal cancellationToken)
        | None -> Flow.awaitShared signal cancellationToken

    member internal _.Invalidate(key: 'key) =
        Platform.lock gate (fun () ->
            entries.Remove key |> ignore
            order.Remove key |> ignore)

    member internal _.InvalidateAll() =
        Platform.lock gate (fun () ->
            entries.Clear()
            order.Clear())

    member internal _.Count = Platform.lock gate (fun () -> entries.Count)

/// <summary>Functions for creating and reading a single-flight cache.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Cache =
    let private create capacity ttl (lookup: Cache<'key, 'error, 'value> -> 'key -> Flow<'env, 'error, 'value>) : Flow<'env, 'none, Cache<'key, 'error, 'value>> =
        Flow(fun environment _ ->
            let owner = RuntimeState.current()
            let clock = RuntimePlatform.current().Platform.Clock
            let self: Cache<'key, 'error, 'value> option ref = ref None
            let start key onSettled = Flow.startShared owner environment (lookup self.Value.Value key) onSettled
            let cache = Cache(start, capacity, ttl, clock)
            self.Value <- Some cache
            Execution.ofValue cache)

    /// <summary>Creates a cache that computes missing values with <paramref name="lookup" />.</summary>
    /// <remarks>Captures the runtime clock when the cache is created to schedule detached lookup fibers.
    /// Lookups capture the environment and scope where the cache is made. Closing that scope interrupts them.</remarks>
    let make (lookup: 'key -> Flow<'env, 'error, 'value>) : Flow<'env, 'none, Cache<'key, 'error, 'value>> =
        create None None (fun _ key -> lookup key)

    /// <summary>Creates a cache retaining at most <paramref name="capacity" /> keys, evicting the least recently read key.</summary>
    /// <remarks>Eviction does not interrupt an in-flight lookup or its existing waiters.</remarks>
    let makeBounded (capacity: int) (lookup: 'key -> Flow<'env, 'error, 'value>) : Flow<'env, 'none, Cache<'key, 'error, 'value>> =
        if capacity <= 0 then invalidArg (nameof capacity) "Capacity must be positive."
        create (Some capacity) None (fun _ key -> lookup key)

    /// <summary>Creates an LRU cache whose successful entries expire after <paramref name="timeToLive" /> of monotonic clock time.</summary>
    let makeBoundedWithExpiry (capacity: int) (timeToLive: TimeSpan) (lookup: 'key -> Flow<'env, 'error, 'value>) : Flow<'env, 'none, Cache<'key, 'error, 'value>> =
        if capacity <= 0 then invalidArg (nameof capacity) "Capacity must be positive."
        if timeToLive <= TimeSpan.Zero then invalidArg (nameof timeToLive) "Time to live must be positive."
        create (Some capacity) (Some timeToLive) (fun _ key -> lookup key)

    /// <summary>Creates a cache whose lookup may read other keys through the same cache.</summary>
    /// <remarks>Looking up the same key recursively, directly or through a cycle, deadlocks the single-flight lookup.</remarks>
    let makeRec (lookup: Cache<'key, 'error, 'value> -> 'key -> Flow<'env, 'error, 'value>) : Flow<'env, 'none, Cache<'key, 'error, 'value>> =
        create None None lookup

    /// <summary>Returns the cached value for a key, or starts its lookup.</summary>
    let get (key: 'key) (cache: Cache<'key, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken -> cache.Get(key, cancellationToken))

    /// <summary>Forgets a key without interrupting existing waiters.</summary>
    let invalidate (key: 'key) (cache: Cache<'key, 'error, 'value>) : Flow<'env, 'none, unit> =
        Flow(fun _ _ -> cache.Invalidate key; Execution.ofValue ())

    /// <summary>Forgets every cached key.</summary>
    let invalidateAll (cache: Cache<'key, 'error, 'value>) : Flow<'env, 'none, unit> =
        Flow(fun _ _ -> cache.InvalidateAll(); Execution.ofValue ())

    /// <summary>Returns the number of retained keys, including in-flight lookups.</summary>
    let count (cache: Cache<'key, 'error, 'value>) : Flow<'env, 'none, int> =
        Flow(fun _ _ -> Execution.ofValue cache.Count)

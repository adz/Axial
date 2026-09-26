namespace Axial

open System.Collections.Generic
open System.Threading

/// <summary>
/// A keyed, single-flight cache of flow results, created with <c>Cache.make</c>.
/// </summary>
/// <remarks>
/// Each key's lookup runs at most once at a time: callers that ask for a key while its lookup runs wait for the same
/// result. Successes are kept until invalidated; failures are not cached. Lookups run in the scope and environment
/// where the cache was made, so interrupting one caller never cancels a lookup other callers are waiting for.
/// </remarks>
/// <typeparam name="key">The key type; compared with structural equality.</typeparam>
/// <typeparam name="error">The lookup's typed error.</typeparam>
/// <typeparam name="value">The cached value.</typeparam>
[<Sealed>]
type Cache<'key, 'error, 'value when 'key: equality>
    internal (start: 'key -> (Exit<'value, 'error> -> unit) -> Platform.Signal<Exit<'value, 'error>> * (unit -> Execution<unit, 'error>)) =
    let gate = obj ()
    let entries = Dictionary<'key, Platform.Signal<Exit<'value, 'error>>>(HashIdentity.Structural)

    member internal _.Get(key: 'key, cancellationToken: CancellationToken) : Execution<'value, 'error> =
        let signal, launch =
            Platform.lock gate (fun () ->
                match entries.TryGetValue key with
                | true, signal -> signal, None
                | _ ->
                    // The lookup starts only after this lock is released, so `mine` is set before `forget` can run.
                    let mine = ref None

                    let forget exit =
                        match exit with
                        | Exit.Failure _ ->
                            Platform.lock gate (fun () ->
                                match entries.TryGetValue key, mine.Value with
                                | (true, active), Some own when obj.ReferenceEquals(active, own) -> entries.Remove key |> ignore
                                | _ -> ())
                        | Exit.Success _ -> ()

                    let signal, launch = start key forget
                    mine.Value <- Some signal
                    entries[key] <- signal
                    signal, Some launch)

        match launch with
        | Some launch -> launch () |> Execution.bind (fun () -> Flow.awaitShared signal cancellationToken)
        | None -> Flow.awaitShared signal cancellationToken

    member internal _.Invalidate(key: 'key) =
        Platform.lock gate (fun () -> entries.Remove key |> ignore)

    member internal _.InvalidateAll() =
        Platform.lock gate (fun () -> entries.Clear())

    member internal _.Count = Platform.lock gate (fun () -> entries.Count)

/// <summary>Functions for creating and reading a single-flight <see cref="T:Axial.Cache`3" />.</summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Cache =
    /// <summary>Creates a cache that computes missing values with <paramref name="lookup" />.</summary>
    /// <remarks>
    /// The cache captures the environment and scope of the flow that makes it. Lookups run there as detached fibers,
    /// and closing that scope interrupts any lookup still running. Make the cache where it should live, typically
    /// in a service's layer.
    /// </remarks>
    /// <param name="lookup">Computes the value for a key.</param>
    /// <example>
    /// <code>
    /// flow {
    ///     let! users = Cache.make loadUser
    ///     let! first = users |&gt; Cache.get 42
    ///     let! again = users |&gt; Cache.get 42 // no second load
    ///     return first = again
    /// }
    /// </code>
    /// </example>
    let make (lookup: 'key -> Flow<'env, 'error, 'value>) : Flow<'env, 'none, Cache<'key, 'error, 'value>> =
        Flow(fun environment _ ->
            let owner = RuntimeState.current()

            let start key onSettled =
                let signal, launch = Flow.startShared owner environment (lookup key) onSettled
                signal, launch

            Execution.ofValue (Cache(start)))

    /// <summary>Returns the value for a key, running the lookup if no success is cached and none is running.</summary>
    /// <remarks>Interrupting this flow ends this caller's wait; the lookup keeps running for other callers.</remarks>
    /// <param name="key">The key to read.</param>
    /// <param name="cache">The cache.</param>
    let get (key: 'key) (cache: Cache<'key, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow(fun _ cancellationToken -> cache.Get(key, cancellationToken))

    /// <summary>Forgets a key's cached value, so the next <c>get</c> runs the lookup again.</summary>
    /// <remarks>A lookup already running is not interrupted; callers already waiting still receive its result.</remarks>
    /// <param name="key">The key to forget.</param>
    /// <param name="cache">The cache.</param>
    let invalidate (key: 'key) (cache: Cache<'key, 'error, 'value>) : Flow<'env, 'none, unit> =
        Flow(fun _ _ ->
            cache.Invalidate key
            Execution.ofValue ())

    /// <summary>Forgets every cached value.</summary>
    /// <param name="cache">The cache.</param>
    let invalidateAll (cache: Cache<'key, 'error, 'value>) : Flow<'env, 'none, unit> =
        Flow(fun _ _ ->
            cache.InvalidateAll()
            Execution.ofValue ())

    /// <summary>Returns the number of keys that are cached or being looked up.</summary>
    /// <param name="cache">The cache.</param>
    let count (cache: Cache<'key, 'error, 'value>) : Flow<'env, 'none, int> =
        Flow(fun _ _ -> Execution.ofValue cache.Count)

namespace Axial.State

open Axial

/// <summary>
/// Represents a handle to a mutable reference that can be updated atomically.
/// </summary>
/// <typeparam name="T">The type of the value stored in the reference.</typeparam>
/// <example>
/// <code>
/// flow {
///     let! r = Ref.make 0
///     do! Ref.set 1 r
///     let! v = Ref.get r
///     return v
/// }
/// </code>
/// </example>
type Ref<'T> =
    private
    | Ref of ('T ref * obj)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Ref =
    /// <summary>Creates a new <see cref="T:Axial.State.Ref`1" /> with the initial value.</summary>
    /// <param name="value">The initial value of the reference.</param>
    /// <returns>A flow that creates and returns the reference.</returns>
    /// <example>
    /// <code>
    /// (Ref.make 10).RunSynchronously(())
    /// </code>
    /// </example>
    let make (value: 'T) : Flow<'env, 'none, Ref<'T>> =
        Flow.ok (Ref (ref value, obj()))

    /// <summary>Reads the current value of the reference.</summary>
    /// <param name="reference">The <see cref="T:Axial.State.Ref`1" /> to read from.</param>
    /// <returns>A flow that returns the current value.</returns>
    /// <example>
    /// <code>
    /// Ref.get myRef
    /// </code>
    /// </example>
    let get (Ref (cell, gate) as reference) : Flow<'env, 'none, 'T> =
        Flow.envWith (fun _ -> Platform.lock gate (fun () -> cell.Value))

    /// <summary>Sets the value of the reference to the specified value.</summary>
    /// <param name="value">The new value to set.</param>
    /// <param name="reference">The <see cref="T:Axial.State.Ref`1" /> to update.</param>
    /// <returns>A flow that sets the value and returns unit.</returns>
    /// <example>
    /// <code>
    /// Ref.set 20 myRef
    /// </code>
    /// </example>
    let set (value: 'T) (Ref (cell, gate) as reference) : Flow<'env, 'none, unit> =
        Flow.envWith (fun _ -> Platform.lock gate (fun () -> cell.Value <- value))

    /// <summary>Updates the value of the reference using the supplied function.</summary>
    /// <param name="f">The update function of type <c>'T -> 'T</c>.</param>
    /// <param name="reference">The <see cref="T:Axial.State.Ref`1" /> to update.</param>
    /// <returns>A flow that updates the value and returns unit.</returns>
    /// <example>
    /// <code>
    /// Ref.update (fun x -> x + 1) myRef
    /// </code>
    /// </example>
    let update (f: 'T -> 'T) (Ref (cell, gate) as reference) : Flow<'env, 'none, unit> =
        Flow.envWith (fun _ -> Platform.lock gate (fun () -> cell.Value <- f cell.Value))

    /// <summary>Updates the value of the reference using the supplied function and returns a derived value.</summary>
    /// <param name="f">The update function of type <c>'T -> 'v * 'T</c>, returning the result before the next state.</param>
    /// <param name="reference">The <see cref="T:Axial.State.Ref`1" /> to update.</param>
    /// <returns>A flow that updates the value and returns the first part of the tuple returned by <paramref name="f" />.</returns>
    /// <example>
    /// <code>
    /// Ref.modify (fun x -> "increased", x + 1) myRef
    /// </code>
    /// </example>
    let modify (f: 'T -> 'v * 'T) (Ref (cell, gate) as reference) : Flow<'env, 'none, 'v> =
        Flow.envWith (fun _ ->
            Platform.lock gate (fun () ->
                let result, next = f cell.Value
                cell.Value <- next
                result))

    /// <summary>Sets the value of the reference and returns the value it held before the update.</summary>
    /// <param name="value">The new value to set.</param>
    /// <param name="reference">The <see cref="T:Axial.State.Ref`1" /> to update.</param>
    /// <returns>A flow that returns the previous value.</returns>
    /// <example>
    /// <code>
    /// Ref.getAndSet 20 myRef
    /// </code>
    /// </example>
    let getAndSet (value: 'T) (Ref (cell, gate) as reference) : Flow<'env, 'none, 'T> =
        Flow.envWith (fun _ ->
            Platform.lock gate (fun () ->
                let previous = cell.Value
                cell.Value <- value
                previous))

    /// <summary>Updates the value using the supplied function and returns the value it held before the update.</summary>
    /// <param name="f">The update function of type <c>'T -> 'T</c>.</param>
    /// <param name="reference">The <see cref="T:Axial.State.Ref`1" /> to update.</param>
    /// <returns>A flow that returns the previous value.</returns>
    /// <example>
    /// <code>
    /// Ref.getAndUpdate (fun x -> x + 1) myRef
    /// </code>
    /// </example>
    let getAndUpdate (f: 'T -> 'T) (Ref (cell, gate) as reference) : Flow<'env, 'none, 'T> =
        Flow.envWith (fun _ ->
            Platform.lock gate (fun () ->
                let previous = cell.Value
                cell.Value <- f previous
                previous))

    /// <summary>Updates the value using the supplied function and returns the value after the update.</summary>
    /// <param name="f">The update function of type <c>'T -> 'T</c>.</param>
    /// <param name="reference">The <see cref="T:Axial.State.Ref`1" /> to update.</param>
    /// <returns>A flow that returns the updated value.</returns>
    /// <example>
    /// <code>
    /// Ref.updateAndGet (fun x -> x + 1) myRef
    /// </code>
    /// </example>
    let updateAndGet (f: 'T -> 'T) (Ref (cell, gate) as reference) : Flow<'env, 'none, 'T> =
        Flow.envWith (fun _ ->
            Platform.lock gate (fun () ->
                let next = f cell.Value
                cell.Value <- next
                next))

namespace Axial.Validation

open System

/// <summary>
/// Computation expression builder for a validation block scoped to a path segment or path prefix.
/// </summary>
/// <exclude/>
type ValidationScopeBuilder(scopePath: PathSegment list) =
    member _.Return(value: 'value) : Validation<'value, 'error> =
        Validation.ok value

    member _.ReturnFrom(validation: Validation<'value, 'error>) : Validation<'value, 'error> =
        validation

    member _.ReturnFrom(result: Result<'value, 'error>) : Validation<'value, 'error> =
        Validation.fromResult result

    member _.Zero() : Validation<unit, 'error> =
        Validation.ok ()

    member _.Bind
        (
            validation: Validation<'value, 'error>,
            binder: 'value -> Validation<'next, 'error>
        ) : Validation<'next, 'error> =
        Validation.bind binder validation

    member _.Bind
        (
            result: Result<'value, 'error>,
            binder: 'value -> Validation<'next, 'error>
        ) : Validation<'next, 'error> =
        result
        |> Validation.fromResult
        |> Validation.bind binder

    member _.Delay(factory: unit -> Validation<'value, 'error>) : Validation<'value, 'error> =
        factory ()

    member _.Run(validation: Validation<'value, 'error>) : Validation<'value, 'error> =
        Validation.at scopePath validation

    member _.Combine
        (
            first: Validation<unit, 'error>,
            second: Validation<'value, 'error>
        ) : Validation<'value, 'error> =
        Validation.bind (fun () -> second) first

    member _.MergeSources
        (
            left: Validation<'left, 'error>,
            right: Validation<'right, 'error>
        ) : Validation<'left * 'right, 'error> =
        Validation.map2 (fun leftValue rightValue -> leftValue, rightValue) left right

    /// <summary>Scopes a nested validation block under the supplied path prefix.</summary>
    /// <param name="path">The path prefix to append.</param>
    /// <returns>A scoped validation builder.</returns>
    member _.at(path: PathSegment list) = ValidationScopeBuilder(scopePath @ path)

    /// <summary>Scopes a nested validation block under a keyed branch.</summary>
    /// <param name="key">The branch key.</param>
    /// <returns>A scoped validation builder.</returns>
    member this.key(key: string) = this.at [ PathSegment.Key key ]

    /// <summary>Scopes a nested validation block under an indexed branch.</summary>
    /// <param name="index">The branch index.</param>
    /// <returns>A scoped validation builder.</returns>
    member this.index(index: int) = this.at [ PathSegment.Index index ]

    /// <summary>Scopes a nested validation block under a named branch.</summary>
    /// <param name="name">The branch name.</param>
    /// <returns>A scoped validation builder.</returns>
    member this.name(name: string) = this.at [ PathSegment.Name name ]

    member _.TryWith
        (
            validation: Validation<'value, 'error>,
            handler: exn -> Validation<'value, 'error>
        ) : Validation<'value, 'error> =
        try
            validation
        with error ->
            handler error

    member _.TryFinally(validation: Validation<'value, 'error>, compensation: unit -> unit) : Validation<'value, 'error> =
        try
            validation
        finally
            compensation ()

    member this.Using
        (
            resource: 'resource,
            binder: 'resource -> Validation<'value, 'error>
        ) : Validation<'value, 'error>
        when 'resource :> IDisposable =
        this.TryFinally(
            binder resource,
            fun () ->
                if not (isNull (box resource)) then
                    resource.Dispose()
        )

    member this.While
        (
            guard: unit -> bool,
            body: Validation<unit, 'error>
        ) : Validation<unit, 'error> =
        if guard () then
            this.Bind(body, fun () -> this.While(guard, body))
        else
            this.Zero()

    member this.For
        (
            sequence: seq<'value>,
            binder: 'value -> Validation<unit, 'error>
        ) : Validation<unit, 'error> =
        this.Using(
            sequence.GetEnumerator(),
            fun enumerator -> this.While(enumerator.MoveNext, this.Delay(fun () -> binder enumerator.Current))
        )

/// <summary>
/// Computation expression builder for accumulating <see cref="T:Axial.Validation`2" /> workflows.
/// </summary>
/// <exclude/>
type ValidateBuilder() =
    member _.Return(value: 'value) : Validation<'value, 'error> =
        Validation.ok value

    member _.ReturnFrom(validation: Validation<'value, 'error>) : Validation<'value, 'error> =
        validation

    member _.ReturnFrom(result: Result<'value, 'error>) : Validation<'value, 'error> =
        Validation.fromResult result

    member _.Zero() : Validation<unit, 'error> =
        Validation.ok ()

    member _.Bind
        (
            validation: Validation<'value, 'error>,
            binder: 'value -> Validation<'next, 'error>
        ) : Validation<'next, 'error> =
        Validation.bind binder validation

    member _.Bind
        (
            result: Result<'value, 'error>,
            binder: 'value -> Validation<'next, 'error>
        ) : Validation<'next, 'error> =
        result
        |> Validation.fromResult
        |> Validation.bind binder

    member _.Delay(factory: unit -> Validation<'value, 'error>) : Validation<'value, 'error> =
        factory ()

    member _.Run(validation: Validation<'value, 'error>) : Validation<'value, 'error> =
        validation

    member _.Combine
        (
            first: Validation<unit, 'error>,
            second: Validation<'value, 'error>
        ) : Validation<'value, 'error> =
        Validation.bind (fun () -> second) first

    member _.MergeSources
        (
            left: Validation<'left, 'error>,
            right: Validation<'right, 'error>
        ) : Validation<'left * 'right, 'error> =
        Validation.map2 (fun leftValue rightValue -> leftValue, rightValue) left right

    /// <summary>Scopes a validation block under the supplied path prefix.</summary>
    /// <param name="path">The path prefix to apply to the block.</param>
    /// <returns>A scoped validation builder.</returns>
    member _.at(path: PathSegment list) = ValidationScopeBuilder(path)

    /// <summary>Scopes a validation block under a keyed branch.</summary>
    /// <param name="key">The branch key.</param>
    /// <returns>A scoped validation builder.</returns>
    member this.key(key: string) = this.at [ PathSegment.Key key ]

    /// <summary>Scopes a validation block under an indexed branch.</summary>
    /// <param name="index">The branch index.</param>
    /// <returns>A scoped validation builder.</returns>
    member this.index(index: int) = this.at [ PathSegment.Index index ]

    /// <summary>Scopes a validation block under a named branch.</summary>
    /// <param name="name">The branch name.</param>
    /// <returns>A scoped validation builder.</returns>
    member this.name(name: string) = this.at [ PathSegment.Name name ]

    member _.TryWith
        (
            validation: Validation<'value, 'error>,
            handler: exn -> Validation<'value, 'error>
        ) : Validation<'value, 'error> =
        try
            validation
        with error ->
            handler error

    member _.TryFinally(validation: Validation<'value, 'error>, compensation: unit -> unit) : Validation<'value, 'error> =
        try
            validation
        finally
            compensation ()

    member this.Using
        (
            resource: 'resource,
            binder: 'resource -> Validation<'value, 'error>
        ) : Validation<'value, 'error>
        when 'resource :> IDisposable =
        this.TryFinally(
            binder resource,
            fun () ->
                if not (isNull (box resource)) then
                    resource.Dispose()
        )

    member this.While
        (
            guard: unit -> bool,
            body: Validation<unit, 'error>
        ) : Validation<unit, 'error> =
        if guard () then
            this.Bind(body, fun () -> this.While(guard, body))
        else
            this.Zero()

    member this.For
        (
            sequence: seq<'value>,
            binder: 'value -> Validation<unit, 'error>
        ) : Validation<unit, 'error> =
        this.Using(
            sequence.GetEnumerator(),
            fun enumerator -> this.While(enumerator.MoveNext, this.Delay(fun () -> binder enumerator.Current))
        )

namespace Axial.ErrorHandling

open System

/// <summary>
/// Computation expression builder for fail-fast <see cref="T:System.Result`2" /> workflows.
/// </summary>
/// <exclude/>
type ResultBuilder() =
    member _.Return(value: 'value) : Result<'value, 'error> =
        Ok value

    member _.ReturnFrom(result: Result<'value, 'error>) : Result<'value, 'error> =
        result

    member _.Zero() : Result<unit, 'error> =
        Ok ()

    member _.Bind
        (
            result: Result<'value, 'error>,
            binder: 'value -> Result<'next, 'error>
        ) : Result<'next, 'error> =
        Result.bind binder result

    member _.Delay(factory: unit -> Result<'value, 'error>) : Result<'value, 'error> =
        factory ()

    member _.Run(result: Result<'value, 'error>) : Result<'value, 'error> =
        result

    member _.Combine
        (
            first: Result<unit, 'error>,
            second: Result<'value, 'error>
        ) : Result<'value, 'error> =
        Result.bind (fun () -> second) first

    member _.TryWith
        (
            result: Result<'value, 'error>,
            handler: exn -> Result<'value, 'error>
        ) : Result<'value, 'error> =
        try
            result
        with error ->
            handler error

    member _.TryFinally(result: Result<'value, 'error>, compensation: unit -> unit) : Result<'value, 'error> =
        try
            result
        finally
            compensation ()

    member this.Using
        (
            resource: 'resource,
            binder: 'resource -> Result<'value, 'error>
        ) : Result<'value, 'error>
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
            body: Result<unit, 'error>
        ) : Result<unit, 'error> =
        if guard () then
            this.Bind(body, fun () -> this.While(guard, body))
        else
            this.Zero()

    member this.For
        (
            sequence: seq<'value>,
            binder: 'value -> Result<unit, 'error>
        ) : Result<unit, 'error> =
        this.Using(
            sequence.GetEnumerator(),
            fun enumerator -> this.While(enumerator.MoveNext, this.Delay(fun () -> binder enumerator.Current))
        )

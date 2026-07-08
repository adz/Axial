namespace Axial.Flow

open System.Threading
open System.Threading.Tasks

/// <summary>
/// Represents a provisioning step that builds an explicit environment inside a scope.
/// </summary>
/// <typeparam name="input">The input environment required to build the layer.</typeparam>
/// <typeparam name="error">The typed failure produced during provisioning.</typeparam>
/// <typeparam name="output">The environment or service bundle produced by the layer.</typeparam>
type Layer<'input, 'error, 'output> =
    internal
    | Layer of (('input * Scope) -> CancellationToken -> Execution<'output, 'error>)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Layer =
    let inline internal invoke
        (layer: Layer<'input, 'error, 'output>)
        (input: 'input)
        (scope: Scope)
        (cancellationToken: CancellationToken)
        : Execution<'output, 'error> =
        let (Layer operation) = layer
        operation (input, scope) cancellationToken

    let internal fromExecution
        (operation: ('input * Scope) -> CancellationToken -> Execution<'output, 'error>)
        : Layer<'input, 'error, 'output> =
        Layer operation

    /// <summary>Creates a layer from a raw async provisioning function.</summary>
    /// <platforms>Fable compatible</platforms>
    let fromAsync
        (operation: ('input * Scope) -> CancellationToken -> Async<Exit<'output, 'error>>)
        : Layer<'input, 'error, 'output> =
        Layer(fun input cancellationToken ->
            Platform.executionOfAsyncExit cancellationToken (operation input cancellationToken))

#if !FABLE_COMPILER
    /// <summary>Creates a layer from a raw value task provisioning function.</summary>
    /// <platforms>.NET only</platforms>
    let fromValueTask
        (operation: ('input * Scope) -> CancellationToken -> ValueTask<Exit<'output, 'error>>)
        : Layer<'input, 'error, 'output> =
        Layer operation

    /// <summary>Creates a layer from a raw task provisioning function.</summary>
    /// <platforms>.NET only</platforms>
    let fromTask
        (operation: ('input * Scope) -> CancellationToken -> Task<Exit<'output, 'error>>)
        : Layer<'input, 'error, 'output> =
        Layer(fun input cancellationToken ->
            ValueTask<Exit<'output, 'error>>(operation input cancellationToken))
#endif

    /// <summary>Creates a layer that succeeds with a fixed output value.</summary>
    let succeed (value: 'output) : Layer<'input, 'error, 'output> =
        Layer(fun _ _ -> Execution.ofValue value)

    /// <summary>Projects part of the input environment into the layer output.</summary>
    let read (projection: 'input -> 'output) : Layer<'input, 'error, 'output> =
        Layer(fun (input, _) _ -> Execution.ofValue (projection input))

    /// <summary>Registers an asynchronous finalizer with the layer scope.</summary>
    /// <param name="finalizer">The finalizer to run when the layer scope closes.</param>
    /// <returns>A layer that registers the finalizer.</returns>
    let addFinalizer
        (finalizer: Platform.Finalizer)
        : Layer<'input, 'error, unit> =
        Layer(fun (_, scope) _ ->
            scope.AddFinalizer finalizer
            Execution.ofValue ())

    /// <summary>Acquires a resource and registers its release with the layer scope.</summary>
    /// <param name="acquire">The layer that acquires the resource.</param>
    /// <param name="release">The release action to run when the layer scope closes.</param>
    /// <returns>A layer that succeeds with the acquired resource.</returns>
    /// <remarks>
    /// Use this for service implementations or provisioned resources that must live for the
    /// full <c>Flow.provide</c> boundary rather than only for the construction expression.
    /// </remarks>
    let acquireRelease
        (acquire: Layer<'input, 'error, 'resource>)
        (release: 'resource -> Platform.Finalizer)
        : Layer<'input, 'error, 'resource> =
        Layer(fun (input, scope) cancellationToken ->
            invoke acquire input scope cancellationToken
            |> Execution.bind (fun resource ->
                scope.AddFinalizer(fun ct -> release resource ct)
                Execution.ofValue resource))

    /// <summary>Maps the successful output of a layer.</summary>
    let map
        (mapper: 'output -> 'next)
        (layer: Layer<'input, 'error, 'output>)
        : Layer<'input, 'error, 'next> =
        Layer(fun (input, scope) cancellationToken ->
            invoke layer input scope cancellationToken
            |> Execution.map mapper)

    /// <summary>Maps the typed provisioning failure of a layer.</summary>
    let mapError
        (mapper: 'error -> 'nextError)
        (layer: Layer<'input, 'error, 'output>)
        : Layer<'input, 'nextError, 'output> =
        Layer(fun (input, scope) cancellationToken ->
            invoke layer input scope cancellationToken
            |> Execution.mapError mapper)

    /// <summary>Sequences layer provisioning with a dependent follow-up layer.</summary>
    let bind
        (binder: 'output -> Layer<'input, 'error, 'next>)
        (layer: Layer<'input, 'error, 'output>)
        : Layer<'input, 'error, 'next> =
        Layer(fun (input, scope) cancellationToken ->
            invoke layer input scope cancellationToken
            |> Execution.bind (fun value ->
                invoke (binder value) input scope cancellationToken))

    /// <summary>Builds two layers from the same input and scope and returns both outputs.</summary>
    /// <remarks>
    /// <c>zip</c> is sequential: the left layer is provisioned before the right layer.
    /// Use <c>zipPar</c> or <c>merge</c> for independent parallel provisioning.
    /// </remarks>
    let zip
        (left: Layer<'input, 'error, 'left>)
        (right: Layer<'input, 'error, 'right>)
        : Layer<'input, 'error, 'left * 'right> =
        Layer(fun (input, scope) cancellationToken ->
            invoke left input scope cancellationToken
            |> Execution.bind (fun leftValue ->
                invoke right input scope cancellationToken
                |> Execution.map (fun rightValue -> leftValue, rightValue)))

    let private chooseParallelExit
        (leftExit: Exit<'left, 'error>)
        (rightExit: Exit<'right, 'error>)
        : Exit<'left * 'right, 'error> =
        match leftExit, rightExit with
        | Exit.Success leftValue, Exit.Success rightValue ->
            Exit.Success (leftValue, rightValue)
        | Exit.Failure leftCause, Exit.Failure rightCause ->
            Exit.Failure (Cause.both leftCause rightCause)
        | Exit.Failure leftCause, Exit.Success _ ->
            Exit.Failure leftCause
        | Exit.Success _, Exit.Failure rightCause ->
            Exit.Failure rightCause

    /// <summary>Builds two independent layers in parallel and returns both outputs.</summary>
    /// <remarks>
    /// Each branch is provisioned in a parent-owned child scope. When the parent scope closes,
    /// child scopes are closed in deterministic left-to-right order. If both branches fail,
    /// both failures are returned as a parallel cause.
    /// </remarks>
    let zipPar
        (left: Layer<'input, 'error, 'left>)
        (right: Layer<'input, 'error, 'right>)
        : Layer<'input, 'error, 'left * 'right> =
        Layer(fun (input, scope) cancellationToken ->
            // Register right first because scopes close finalizers in reverse registration order.
            let rightScope = scope.AddChild()
            let leftScope = scope.AddChild()

            Platform.zipParAllExecution
                (fun () -> invoke left input leftScope cancellationToken)
                (fun () -> invoke right input rightScope cancellationToken)
                Execution.causeOfException
                chooseParallelExit
        )

    /// <summary>Merges two independent service layers in parallel.</summary>
    /// <remarks>
    /// <c>merge</c> is the layer-domain name for <c>zipPar</c>. Use it when combining
    /// service bundles or environment fragments that do not depend on each other.
    /// </remarks>
    let merge
        (left: Layer<'input, 'error, 'left>)
        (right: Layer<'input, 'error, 'right>)
        : Layer<'input, 'error, 'left * 'right> =
        zipPar left right

    /// <summary>Combines two layers with a mapping function.</summary>
    let map2
        (mapper: 'left -> 'right -> 'output)
        (left: Layer<'input, 'error, 'left>)
        (right: Layer<'input, 'error, 'right>)
        : Layer<'input, 'error, 'output> =
        zip left right
        |> map (fun (leftValue, rightValue) -> mapper leftValue rightValue)

    /// <summary>Applies a layer-wrapped function to a layer-wrapped value.</summary>
    let apply
        (layer: Layer<'input, 'error, 'value -> 'next>)
        (value: Layer<'input, 'error, 'value>)
        : Layer<'input, 'error, 'next> =
        map2 (fun mapper input -> mapper input) layer value

    /// <summary>Combines three layers with a mapping function.</summary>
    let map3
        (mapper: 'left -> 'middle -> 'right -> 'output)
        (left: Layer<'input, 'error, 'left>)
        (middle: Layer<'input, 'error, 'middle>)
        (right: Layer<'input, 'error, 'right>)
        : Layer<'input, 'error, 'output> =
        apply
            (map2 (fun leftValue middleValue -> fun rightValue -> mapper leftValue middleValue rightValue) left middle)
            right

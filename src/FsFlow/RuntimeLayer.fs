namespace FsFlow

open System.Threading

/// <summary>
/// Represents a provisioning step that builds an explicit environment inside a scope.
/// </summary>
/// <typeparam name="input">The input environment required to build the layer.</typeparam>
/// <typeparam name="error">The typed failure produced during provisioning.</typeparam>
/// <typeparam name="output">The environment or service bundle produced by the layer.</typeparam>
type Layer<'input, 'error, 'output> =
    | Layer of (('input * Scope) -> CancellationToken -> Effect<'output, 'error>)

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module Layer =
    let inline internal invoke
        (layer: Layer<'input, 'error, 'output>)
        (input: 'input)
        (scope: Scope)
        (cancellationToken: CancellationToken)
        : Effect<'output, 'error> =
        let (Layer operation) = layer
        operation (input, scope) cancellationToken

    /// <summary>Creates a layer from a raw effectful provisioning function.</summary>
    let effect
        (operation: ('input * Scope) -> CancellationToken -> Effect<'output, 'error>)
        : Layer<'input, 'error, 'output> =
        Layer operation

    /// <summary>Creates a layer that succeeds with a fixed output value.</summary>
    let succeed (value: 'output) : Layer<'input, 'error, 'output> =
        Layer(fun _ _ -> EffectFlow.ofValue value)

    /// <summary>Projects part of the input environment into the layer output.</summary>
    let read (projection: 'input -> 'output) : Layer<'input, 'error, 'output> =
        Layer(fun (input, _) _ -> EffectFlow.ofValue (projection input))

    /// <summary>Maps the successful output of a layer.</summary>
    let map
        (mapper: 'output -> 'next)
        (layer: Layer<'input, 'error, 'output>)
        : Layer<'input, 'error, 'next> =
        Layer(fun (input, scope) cancellationToken ->
            invoke layer input scope cancellationToken
            |> EffectFlow.map mapper)

    /// <summary>Sequences layer provisioning with a dependent follow-up layer.</summary>
    let bind
        (binder: 'output -> Layer<'input, 'error, 'next>)
        (layer: Layer<'input, 'error, 'output>)
        : Layer<'input, 'error, 'next> =
        Layer(fun (input, scope) cancellationToken ->
            invoke layer input scope cancellationToken
            |> EffectFlow.bind (fun value ->
                invoke (binder value) input scope cancellationToken))

    /// <summary>Builds two layers from the same input and scope and returns both outputs.</summary>
    let zip
        (left: Layer<'input, 'error, 'left>)
        (right: Layer<'input, 'error, 'right>)
        : Layer<'input, 'error, 'left * 'right> =
        Layer(fun (input, scope) cancellationToken ->
            invoke left input scope cancellationToken
            |> EffectFlow.bind (fun leftValue ->
                invoke right input scope cancellationToken
                |> EffectFlow.map (fun rightValue -> leftValue, rightValue)))

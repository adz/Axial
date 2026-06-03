namespace FsFlow

/// <summary>
/// Computation expression builder for composing service layers.
/// </summary>
/// <remarks>
/// Plain <c>let!</c> is dependent and sequential. Sibling <c>and!</c> bindings are independent
/// and use <see cref="M:FsFlow.Layer.merge" />, which provisions branches in parallel.
/// </remarks>
/// <exclude/>
type LayerBuilder() =
    member _.Return(value: 'value) : Layer<'input, 'error, 'value> =
        Layer.succeed value

    member _.ReturnFrom(layer: Layer<'input, 'error, 'value>) : Layer<'input, 'error, 'value> =
        layer

    member _.Zero() : Layer<'input, 'error, unit> =
        Layer.succeed ()

    member _.Bind
        (
            layer: Layer<'input, 'error, 'value>,
            binder: 'value -> Layer<'input, 'error, 'next>
        ) : Layer<'input, 'error, 'next> =
        Layer.bind binder layer

    member _.BindReturn
        (
            layer: Layer<'input, 'error, 'value>,
            mapper: 'value -> 'next
        ) : Layer<'input, 'error, 'next> =
        Layer.map mapper layer

    member _.Delay(factory: unit -> Layer<'input, 'error, 'value>) : Layer<'input, 'error, 'value> =
        Layer.effect (fun (input, scope) cancellationToken ->
            Layer.invoke (factory ()) input scope cancellationToken)

    member _.Run(layer: Layer<'input, 'error, 'value>) : Layer<'input, 'error, 'value> =
        layer

    member _.Combine
        (
            first: Layer<'input, 'error, unit>,
            second: Layer<'input, 'error, 'value>
        ) : Layer<'input, 'error, 'value> =
        Layer.bind (fun () -> second) first

    member _.MergeSources
        (
            left: Layer<'input, 'error, 'left>,
            right: Layer<'input, 'error, 'right>
        ) : Layer<'input, 'error, 'left * 'right> =
        Layer.merge left right

    member _.MergeSources3
        (
            left: Layer<'input, 'error, 'left>,
            middle: Layer<'input, 'error, 'middle>,
            right: Layer<'input, 'error, 'right>
        ) : Layer<'input, 'error, 'left * 'middle * 'right> =
        Layer.merge (Layer.merge left middle) right
        |> Layer.map (fun ((leftValue, middleValue), rightValue) -> leftValue, middleValue, rightValue)

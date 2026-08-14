namespace Axial.Telemetry

open System.ComponentModel
open Axial

/// <summary>Builds and scopes ambient telemetry attributes for Axial workflows.</summary>
/// <remarks>
/// Context is immutable and lexically scoped. Nested attributes override matching outer keys only for the wrapped
/// workflow, and child fibers inherit the context present when they are forked.
/// </remarks>
[<RequireQualifiedAccess>]
module Context =
    /// <summary>Contains typed keys for the curated OpenTelemetry semantic conventions exposed by Axial.</summary>
    [<RequireQualifiedAccess>]
    module Keys =
        /// <summary>The OpenTelemetry <c>enduser.id</c> span attribute. The convention is currently marked development and contains potentially sensitive identifying information.</summary>
        let endUserId = AttributeKey.string "enduser.id"

    /// <summary>Creates an application-defined attribute by pairing a typed key with a value.</summary>
    let attribute (key: AttributeKey<'value>) (value: 'value) : Attribute =
        { Name = key.Name
          Value = key.Encode value }

    /// <summary>An empty telemetry context.</summary>
    let empty = TelemetryContext Map.empty

    /// <summary>Creates a context from attributes. Later duplicate names replace earlier values.</summary>
    let ofAttributes (attributes: Attribute seq) : TelemetryContext =
        attributes
        |> Seq.fold (fun (TelemetryContext values) item -> TelemetryContext(Map.add item.Name item.Value values)) empty

    /// <summary>Adds or replaces one attribute.</summary>
    let add (item: Attribute) (TelemetryContext values) : TelemetryContext =
        TelemetryContext(Map.add item.Name item.Value values)

    /// <summary>Adds attributes. Later duplicate names replace earlier values.</summary>
    let addMany (attributes: Attribute seq) (context: TelemetryContext) : TelemetryContext =
        attributes |> Seq.fold (fun state item -> add item state) context

    /// <summary>Reads a typed attribute from a context.</summary>
    let tryFind (key: AttributeKey<'value>) (TelemetryContext values) : 'value option =
        values |> Map.tryFind key.Name |> Option.bind key.Decode

    /// <summary>Returns the attributes in name order.</summary>
    let toSeq (TelemetryContext values) : Attribute seq =
        values |> Seq.map (fun pair -> { Name = pair.Key; Value = pair.Value })

    /// <summary>Creates the OpenTelemetry <c>enduser.id</c> span attribute.</summary>
    let endUserId value =
        attribute Keys.endUserId value

    /// <summary>Adds the OpenTelemetry <c>enduser.id</c> span attribute to a context.</summary>
    let addEndUserId value context =
        context |> add (endUserId value)

    let private merge (TelemetryContext outer) (TelemetryContext inner) =
        TelemetryContext(Map.fold (fun state name value -> Map.add name value state) outer inner)

    let private scope (context: TelemetryContext) (Flow operation: Flow<'env, 'error, 'value>) =
        Flow(fun environment cancellationToken ->
            let current = RuntimeState.current ()
            let combined = merge current.TelemetryContext context

            for item in toSeq context do
                current.TelemetrySink item

            let runtime = current |> RuntimeContext.withTelemetryContext combined
            RuntimeState.withRuntime runtime (fun () -> operation environment cancellationToken))

    /// <summary>Scopes a telemetry context around a workflow.</summary>
    let withContext context flow =
        scope context flow

    /// <summary>Scopes one telemetry attribute around a workflow.</summary>
    let withAttribute item flow =
        scope (ofAttributes [ item ]) flow

    /// <summary>Scopes telemetry attributes around a workflow.</summary>
    let withAttributes attributes flow =
        scope (ofAttributes attributes) flow

    /// <summary>Scopes the OpenTelemetry <c>enduser.id</c> span attribute around a workflow.</summary>
    let withEndUserId value flow =
        flow |> withAttribute (endUserId value)

    /// <summary>Reads the currently scoped telemetry context.</summary>
    let current<'env, 'error> : Flow<'env, 'error, TelemetryContext> =
        Flow(fun _ _ -> RuntimeState.current().TelemetryContext |> Execution.ofValue)

    /// <exclude/>
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let withSink sink (flow: Flow<'env, 'error, 'value>) =
        let (Flow operation) = flow
        Flow(fun environment cancellationToken ->
            let runtime = RuntimeState.current() |> RuntimeContext.withTelemetrySink sink
            RuntimeState.withRuntime runtime (fun () -> operation environment cancellationToken))

    /// <exclude/>
    [<EditorBrowsable(EditorBrowsableState.Never)>]
    let addSink sink (flow: Flow<'env, 'error, 'value>) =
        let (Flow operation) = flow
        Flow(fun environment cancellationToken ->
            let runtime = RuntimeState.current() |> RuntimeContext.withComposedTelemetrySink sink
            RuntimeState.withRuntime runtime (fun () -> operation environment cancellationToken))

    let internal iter writer (TelemetryContext values) =
        for KeyValue(name, value) in values do
            writer name value

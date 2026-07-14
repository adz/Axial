namespace Axial.Flow.Telemetry.JavaScript

open System
open System.Collections.Generic
open Axial.Flow

// ---------------------------------------------------------------------------------------------
// Structural bindings for the parts of `@opentelemetry/api` this package calls.
//
// These interfaces are deliberately *structural*: the package never imports the npm module itself.
// The application obtains the api object (`import * as api from '@opentelemetry/api'` in JS, or a
// Fable import) and hands it to `Otel.install`, exactly as the .NET package leaves listener and
// exporter registration to the host. Fable erases these interfaces to plain member access, so any
// object with the right shape works — including the SDK-registered global api and test fakes.
// ---------------------------------------------------------------------------------------------

/// An opaque OpenTelemetry JS `Context`. Values are only ever obtained from and passed back to the api.
[<AllowNullLiteral>]
type Context =
    interface
    end

/// The subset of the OpenTelemetry JS `Span` interface this package writes to.
[<AllowNullLiteral>]
type Span =
    /// Sets one string attribute on the span.
    abstract setAttribute: key: string * value: string -> unit
    /// Sets the span status; the object is `{ code: SpanStatusCode; message: string }`.
    abstract setStatus: status: obj -> unit
    /// Returns the span's `SpanContext`, used for links.
    abstract spanContext: unit -> obj
    /// Ends the span.
    abstract ``end``: unit -> unit

/// The subset of the OpenTelemetry JS `Tracer` interface this package calls.
[<AllowNullLiteral>]
type Tracer =
    /// Starts a span with explicit options and parent context. Options may be an empty object.
    abstract startSpan: name: string * options: obj * context: Context -> Span

/// The subset of the OpenTelemetry JS `trace` api namespace this package calls.
[<AllowNullLiteral>]
type TraceApi =
    /// Returns a tracer for the given instrumentation scope name.
    abstract getTracer: name: string -> Tracer
    /// Returns a new context with the given span set as the active span.
    abstract setSpan: context: Context * span: Span -> Context

/// The subset of the OpenTelemetry JS `context` api namespace this package calls.
[<AllowNullLiteral>]
type ContextApi =
    /// Returns the currently active context.
    abstract active: unit -> Context
    /// Runs the function with the given context active. Cross-await propagation requires the
    /// application's context manager (Node `AsyncLocalStorageContextManager`, browser `ZoneContextManager`).
    abstract ``with``: context: Context * operation: (unit -> 'result) -> 'result

/// The `@opentelemetry/api` module surface this package consumes: its `trace` and `context` namespaces.
[<AllowNullLiteral>]
type OpenTelemetryApi =
    /// The `trace` api namespace.
    abstract trace: TraceApi
    /// The `context` api namespace.
    abstract context: ContextApi

/// One OpenTelemetry span status object, mirroring `SpanStatusCode` (0 unset, 1 ok, 2 error).
type internal SpanStatus = {| code: int; message: string |}

/// The OpenTelemetry JS adapter for the shared span vocabulary in `Axial.Flow.Telemetry.Shared`.
module internal JsTags =
    open Axial.Flow.Telemetry.Shared

    let setStatus (span: Span) (code: int) (message: string) =
        span.setStatus (box ({| code = code; message = message |}: SpanStatus))

    let writer (span: Span) : SpanWriter =
        { SetTag = fun name value -> span.setAttribute (name, value)
          SetStatus =
            function
            | SpanStatusOutcome.Ok -> setStatus span 1 ""
            | SpanStatusOutcome.Error message -> setStatus span 2 message
          DefectTypeName =
            fun defect ->
#if FABLE_COMPILER
                // Fable exceptions are JS Errors; reflection is unavailable, the constructor name is not.
                Fable.Core.JsInterop.emitJsExpr defect "($0?.constructor?.name ?? 'Error')"
#else
                defect.GetType().FullName
#endif
        }

    let tagDefect (span: Span) (defect: exn) =
        SpanConventions.tagDefect (writer span) defect

    let stampExit (renderError: 'error -> string) (span: Span) (exit: Exit<'value, 'error>) =
        SpanConventions.stampExit renderError (writer span) exit

#if FABLE_COMPILER
    // Interface type tests are erased in Fable JavaScript, so the environment traits are read
    // structurally: Fable attaches interface members by name, `string option` erases to
    // `string | undefined`, and a missing member reads as `undefined`. A string-valued member with a
    // trait's name therefore tags the span exactly as the interface would on .NET.
    let tagEnvironment (span: Span) (environment: 'env) =
        let env: obj = box environment

        if not (isNull env) then
            let stringMember (name: string) : string option =
                let value: obj = Fable.Core.JsInterop.emitJsExpr (env, name) "$0[$1]"

                match value with
                | :? string as text -> Some text
                | _ -> None

            stringMember "RequestId"
            |> Option.iter (fun id -> span.setAttribute ("axial.flow.request_id", id))

            stringMember "CorrelationId"
            |> Option.iter (fun id -> span.setAttribute ("axial.flow.correlation_id", id))

            stringMember "TenantId"
            |> Option.iter (fun id -> span.setAttribute ("axial.flow.tenant_id", id))

            let tags: obj = Fable.Core.JsInterop.emitJsExpr env "$0['TelemetryTags']"

            if Fable.Core.JsInterop.emitJsExpr tags "$0 != null" then
                for tagName, tagValue in unbox<(string * string) list> tags do
                    span.setAttribute (tagName, tagValue)
#else
    let tagEnvironment (span: Span) (environment: 'env) =
        match box environment with
        | :? IHasRequestId as req -> span.setAttribute ("axial.flow.request_id", req.RequestId)
        | _ -> ()

        match box environment with
        | :? IHasCorrelationId as corr ->
            match corr.CorrelationId with
            | Some id -> span.setAttribute ("axial.flow.correlation_id", id)
            | None -> ()
        | _ -> ()

        match box environment with
        | :? IHasTenantId as tenant ->
            match tenant.TenantId with
            | Some id -> span.setAttribute ("axial.flow.tenant_id", id)
            | None -> ()
        | _ -> ()

        match box environment with
        | :? IHasTelemetryTags as tagged ->
            for tagName, tagValue in tagged.TelemetryTags do
                span.setAttribute (tagName, tagValue)
        | _ -> ()
#endif

/// <summary>
/// OpenTelemetry JS tracing for Axial workflows compiled with Fable: the JavaScript counterpart of the
/// .NET package's <c>Activity.trace</c>, emitting through a host-supplied <c>@opentelemetry/api</c> object.
/// </summary>
[<RequireQualifiedAccess>]
module Otel =
    let mutable private installed: (OpenTelemetryApi * Tracer) option = None

    /// <summary>
    /// Installs a tracer obtained from the supplied api's <c>getTracer</c> under the instrumentation
    /// scope name <c>Axial.Flow</c>. Call once at the application edge, after the OpenTelemetry JS SDK
    /// (and its context manager) is registered. JavaScript targets only.
    /// </summary>
    /// <param name="api">The <c>@opentelemetry/api</c> module object.</param>
    let install (api: OpenTelemetryApi) : unit =
#if FABLE_COMPILER
        installed <- Some (api, api.trace.getTracer "Axial.Flow")
#else
        ignore api
        raise (NotSupportedException
            "Axial.Flow.Telemetry.JavaScript targets Fable JavaScript only. On .NET, use Axial.Flow.Telemetry.")
#endif

    /// <summary>Installs an explicit tracer, for a custom instrumentation scope name. JavaScript targets only.</summary>
    /// <param name="api">The <c>@opentelemetry/api</c> module object.</param>
    /// <param name="tracer">The tracer to emit spans through.</param>
    let installWith (api: OpenTelemetryApi) (tracer: Tracer) : unit =
#if FABLE_COMPILER
        installed <- Some (api, tracer)
#else
        ignore api
        ignore tracer
        raise (NotSupportedException
            "Axial.Flow.Telemetry.JavaScript targets Fable JavaScript only. On .NET, use Axial.Flow.Telemetry.")
#endif

    /// <summary>Removes the installed tracer; subsequent traced flows run untraced.</summary>
    let uninstall () : unit = installed <- None

    let internal current () = installed

#if FABLE_COMPILER
    /// <summary>
    /// Wraps a flow in a new OpenTelemetry span that covers the workflow's execution, maps metadata traits
    /// from the environment to attributes, and stamps the final exit onto the span.
    /// </summary>
    /// <remarks>
    /// The semantics mirror the .NET package's <c>Activity.traceWith</c>: the span ends when the workflow
    /// settles, receives the <c>axial.flow.*</c> outcome vocabulary and OpenTelemetry <c>exception.*</c>
    /// tags, and tees runtime annotations as <c>axial.flow.annotation.*</c> attributes. The workflow runs
    /// inside <c>context.with</c> with this span active, so nested spans parent to it; propagation across
    /// awaited boundaries requires the application's OpenTelemetry context manager. Without <c>Otel.install</c>
    /// the flow runs unchanged.
    /// </remarks>
    /// <param name="renderError">Renders typed errors for the <c>axial.flow.error</c> attribute.</param>
    /// <param name="name">The span name.</param>
    /// <param name="sourceFlow">The flow to trace.</param>
    /// <returns>A flow that executes within the span.</returns>
    let traceWith
        (renderError: 'error -> string)
        (name: string)
        (sourceFlow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        Flow(fun environment cancellationToken ->
            match current () with
            | None ->
                let (Flow operation) = sourceFlow
                operation environment cancellationToken
            | Some (api, tracer) ->
                // Everything runtime-context-sensitive happens here, at invoke time: under Fable the
                // ambient RuntimeContext cell is only reliably set during the workflow's synchronous
                // construction, so the fiber id, existing annotations, and the composed annotation
                // sink are all captured now. The span itself starts when the returned async runs (so
                // its duration covers the execution), and tags arriving before then are buffered.
                let runtime = RuntimeState.current ()
                let pendingTags = ResizeArray<string * string> ()
                let mutable activeSpan: Span option = None

                let setTag key value =
                    match activeSpan with
                    | Some span -> span.setAttribute (key, value)
                    | None -> pendingTags.Add (key, value)

                setTag "axial.flow.fiber.id" (string runtime.FiberId.Value)

                for KeyValue(annotationName, value) in runtime.Annotations do
                    setTag ("axial.flow.annotation." + annotationName) value

                let sinkRuntime =
                    runtime
                    |> RuntimeContext.withComposedAnnotationSink (fun annotationName value ->
                        setTag ("axial.flow.annotation." + annotationName) value)

                let causeOfThrown (error: exn) : Cause<'error> =
                    match error with
                    | :? OperationCanceledException -> Cause.Interrupt
                    | error -> Cause.Die error

                let execution =
                    RuntimeState.withRuntime sinkRuntime (fun () ->
                        try
                            let (Flow operation) = sourceFlow
                            operation environment cancellationToken
                        with error ->
                            async.Return (Exit.Failure (causeOfThrown error)))

                async {
                    let parent = api.context.active ()
                    let span = tracer.startSpan (name, obj (), parent)
                    activeSpan <- Some span
                    JsTags.tagEnvironment span environment

                    for key, value in pendingTags do
                        span.setAttribute (key, value)

                    pendingTags.Clear ()

                    // The workflow starts inside `context.with` so spans opened during its execution
                    // parent to this span; the exit is resolved through one continuation regardless of
                    // how the underlying async settles.
                    let! exit =
                        Async.FromContinuations (fun (resolve, _, _) ->
                            api.context.``with`` (
                                api.trace.setSpan (parent, span),
                                fun () ->
                                    Async.StartWithContinuations (
                                        execution,
                                        resolve,
                                        (fun error -> resolve (Exit.Failure (causeOfThrown error))),
                                        (fun _ -> resolve (Exit.Failure Cause.Interrupt))
                                    )
                            ))

                    JsTags.stampExit renderError span exit
                    span.``end`` ()
                    return exit
                })
#else
    /// <summary>
    /// JavaScript-only: on .NET this returns the flow unchanged. Use <c>Activity.traceWith</c> from
    /// <c>Axial.Flow.Telemetry</c> on .NET targets.
    /// </summary>
    let traceWith
        (renderError: 'error -> string)
        (name: string)
        (sourceFlow: Flow<'env, 'error, 'value>)
        : Flow<'env, 'error, 'value> =
        ignore renderError
        ignore name
        sourceFlow
#endif

    /// <summary>
    /// Wraps a flow in a new OpenTelemetry span covering the workflow's execution. Typed errors are
    /// rendered with <c>string</c>; see <c>traceWith</c> for a custom renderer. JavaScript targets only.
    /// </summary>
    /// <param name="name">The span name.</param>
    /// <param name="sourceFlow">The flow to trace.</param>
    /// <returns>A flow that executes within the span.</returns>
    let trace (name: string) (sourceFlow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        traceWith (fun error -> string (box error)) name sourceFlow

/// <summary>
/// Fiber-lifecycle observability on the installed OpenTelemetry tracer: the JavaScript counterpart of the
/// .NET package's <c>FiberTelemetry</c>, with the same span names and attribute vocabulary.
/// </summary>
[<RequireQualifiedAccess>]
module FiberTelemetry =
    let private tagMetadata (span: Span) (metadata: FiberMetadata) =
        span.setAttribute ("axial.flow.fiber.id", string metadata.Id.Value)

        match metadata.ParentId with
        | Some parentId -> span.setAttribute ("axial.flow.fiber.parent_id", string parentId.Value)
        | None -> ()

        span.setAttribute ("axial.flow.fiber.started_at", metadata.StartedAt.ToString "O")
        span.setAttribute ("axial.flow.fiber.status", string metadata.Status)

    /// <summary>
    /// A fiber observer that records fiber defects as spans: every fiber that settles with a defect
    /// produces an <c>axial.flow.fiber.defect</c> error span, and every defect the runtime proves
    /// unobservable produces an <c>axial.flow.fiber.unobserved_defect</c> error span. A no-op until
    /// <c>Otel.install</c> has run.
    /// </summary>
    let observer : FiberObserver =
        { FiberObserver.none with
            OnEnd =
                fun metadata defect ->
                    match defect, Otel.current () with
                    | Some exn, Some (api, tracer) ->
                        let span = tracer.startSpan ("axial.flow.fiber.defect", obj (), api.context.active ())
                        tagMetadata span metadata
                        JsTags.tagDefect span exn
                        span.``end`` ()
                    | _ -> ()
            OnUnobservedDefect =
                fun metadata defect ->
                    match Otel.current () with
                    | Some (api, tracer) ->
                        let span =
                            tracer.startSpan ("axial.flow.fiber.unobserved_defect", obj (), api.context.active ())

                        metadata |> Option.iter (tagMetadata span)
                        JsTags.tagDefect span defect
                        span.``end`` ()
                    | None -> () }

    /// <summary>Installs the defect-only telemetry fiber observer, typically once at the application edge.</summary>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow whose forked fibers report defects through the installed tracer.</returns>
    let observe (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow.withFiberObserver observer flow

    // Fiber spans opened at fork and closed at settle, keyed by reference identity of the metadata
    // record both hooks receive. Entries are removed on settle; there is no GC net under Fable.
    let private fiberSpans = Dictionary<FiberMetadata, Span>(HashIdentity.Reference)

    /// <summary>
    /// A fiber observer that gives every forked fiber a real <c>axial.flow.fiber</c> span: opened at the
    /// fork site (parented to whatever span is active there), closed when the fiber settles, and stamped
    /// with the fiber's status and any defect. Unobservable defects produce a linked
    /// <c>axial.flow.fiber.unobserved_defect</c> span. Span-per-fiber is opt-in; hot paths forking many
    /// fibers can stay on the defect-only <c>observer</c>.
    /// </summary>
    let observerWithSpans : FiberObserver =
        {
            OnStart =
                fun metadata ->
                    match Otel.current () with
                    | Some (api, tracer) ->
                        let span = tracer.startSpan ("axial.flow.fiber", obj (), api.context.active ())
                        tagMetadata span metadata
                        fiberSpans[metadata] <- span
                    | None -> ()
            OnEnd =
                fun metadata defect ->
                    match fiberSpans.TryGetValue metadata with
                    | true, span ->
                        fiberSpans.Remove metadata |> ignore
                        Axial.Flow.Telemetry.Shared.SpanConventions.stampFiberEnd (JsTags.writer span) metadata.Status defect
                        span.``end`` ()
                    | _ -> ()
            OnUnobservedDefect =
                fun metadata defect ->
                    match Otel.current () with
                    | Some (api, tracer) ->
                        let options =
                            match metadata with
                            | Some m ->
                                match fiberSpans.TryGetValue m with
                                | true, fiberSpan ->
                                    box {| links = [| {| context = fiberSpan.spanContext () |} |] |}
                                | _ -> obj ()
                            | None -> obj ()

                        let span =
                            tracer.startSpan ("axial.flow.fiber.unobserved_defect", options, api.context.active ())

                        metadata |> Option.iter (tagMetadata span)
                        JsTags.tagDefect span defect
                        span.``end`` ()
                    | None -> ()
        }

    /// <summary>
    /// Installs the span-per-fiber telemetry observer: every forked fiber becomes an
    /// <c>axial.flow.fiber</c> span covering fork to settle. See <c>observerWithSpans</c>.
    /// </summary>
    /// <param name="flow">The source flow.</param>
    /// <returns>A flow whose forked fibers each produce a span on the installed tracer.</returns>
    let observeWithSpans (flow: Flow<'env, 'error, 'value>) : Flow<'env, 'error, 'value> =
        Flow.withFiberObserver observerWithSpans flow

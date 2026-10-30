module Axial.Fable.Tests.TelemetryHostTests

open Axial
open Axial.PlatformService
open Axial.Telemetry
open Axial.Telemetry.JavaScript
open Fable.Core
open Axial.Fable.Tests.Harness

[<Emit("(() => { const spans=[]; let active={}; const tracer={ startSpan(name){ const span={name,tags:{},setAttribute(k,v){this.tags[k]=v;},setStatus(s){this.status=s;},spanContext(){return {};},end(){this.ended=true;}}; spans.push(span); return span; }}; return {spans, trace:{getTracer(){return tracer;},setSpan(c,s){return {span:s};}},context:{active(){return active;},with(c,f){const old=active;active=c;try{return f();}finally{active=old;}}}}; })()")>]
let private fake () : OpenTelemetryApi = jsNative

[<Emit("$0.spans[0].tags[$1]")>]
let private tag (_api: OpenTelemetryApi) (_key: string) : obj = jsNative

let tests =
    [ test "host telemetry JavaScript adapter tees both slots without replacing logging" (flow {
          let api = fake ()
          Otel.installNamed api "test-host"
          try
              let annotations = ResizeArray<string * string>()
              let attributes = ResizeArray<Attribute>()
              let telemetry =
                  { new ITelemetry with
                      member _.Annotate(name, value) = annotations.Add(name, value)
                      member _.Attribute attribute = attributes.Add attribute }
              let log = Log.memory ()
              let sink = LogSink.minLevel LogLevel.Information log
              let local = FiberLocal.named "job" "unset"
              let key = AttributeKey.string "tee.attribute"
              let error = System.InvalidOperationException("tee-error")
              let source: Flow<unit, Never, unit> = flow {
                  do! Log.debug "disabled"
                  do! Log.info "root" |> Flow.annotate "tee.annotation" "root" |> Context.withAttribute (Context.attribute key "root")
                  let! child = Log.errorExn error "child"
                               |> Flow.annotate "tee.annotation" "child"
                               |> Context.withAttribute (Context.attribute key "child")
                               |> Flow.forkNamed "js-telemetry-log-child"
                  do! Fiber.join child
              }
              do! source |> Otel.trace "tee-span" |> Flow.withTelemetry telemetry |> Flow.withLogSink sink |> FiberLocal.locally "tee-job" local
              return [ isTrue "outer annotation sink sees root" (annotations.Contains("tee.annotation", "root"))
                       isTrue "outer annotation sink sees child" (annotations.Contains("tee.annotation", "child"))
                       equal "outer attribute sink receives both" 2 attributes.Count
                       equal "span receives attributes" (box "child") (tag api "tee.attribute")
                       equal "span receives annotations" (box "child") (tag api "axial.flow.annotation.tee.annotation")
                       equal "logging preserved and disabled entry absent" ["root"; "child"] (log.Entries |> Seq.map _.Message |> Seq.toList)
                       isTrue "log locals survive tracing and fork" (log.Entries |> Seq.forall (fun entry -> List.contains ("job", box "tee-job") entry.Locals))
                       equal "error preserved" "tee-error" (log.Entries |> Seq.item 1).Error.Value.Message ]
          finally Otel.uninstall ()
      }) ]

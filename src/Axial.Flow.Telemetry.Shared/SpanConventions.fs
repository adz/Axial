/// The platform-neutral half of Axial's span vocabulary, shared by source inclusion (not a package):
/// `Axial.Flow.Telemetry` (over `System.Diagnostics.Activity`) and `Axial.Flow.Telemetry.JavaScript`
/// (over OpenTelemetry JS) both compile this file and supply a `SpanWriter` for their span type. The
/// `axial.flow.*` and `exception.*` tags written here are one cross-platform contract: a trace spanning
/// a .NET backend and a Fable frontend must answer the same dashboard queries on both halves.
namespace Axial.Flow.Telemetry.Shared

open Axial.Flow

/// The status outcome a span can settle with. `Interrupt` exits deliberately leave status unset.
[<RequireQualifiedAccess>]
type internal SpanStatusOutcome =
    | Ok
    | Error of message: string

/// Writes onto one concrete span. `DefectTypeName` is platform-varying: reflection on .NET, the
/// constructor name in JavaScript.
type internal SpanWriter =
    { SetTag: string -> string -> unit
      SetStatus: SpanStatusOutcome -> unit
      DefectTypeName: exn -> string }

module internal SpanConventions =
    /// Marks the span failed with OpenTelemetry-convention `exception.*` tags for a defect.
    let tagDefect (writer: SpanWriter) (defect: exn) =
        writer.SetStatus (SpanStatusOutcome.Error defect.Message)
        writer.SetTag "exception.type" (writer.DefectTypeName defect)
        writer.SetTag "exception.message" defect.Message
        writer.SetTag "exception.stacktrace" (string defect)

    let private isComposite (cause: Cause<'error>) =
        match cause with
        | Cause.Fail _
        | Cause.Die _
        | Cause.Interrupt -> false
        | Cause.Then _
        | Cause.Both _
        | Cause.Traced _ -> true

    /// Stamps an exit onto a span: status, `axial.flow.outcome`, typed-error and defect tags,
    /// cancellation tagging, and the pretty-printed cause tree for composite causes.
    let stampExit (renderError: 'error -> string) (writer: SpanWriter) (exit: Exit<'value, 'error>) =
        match exit with
        | Exit.Success _ ->
            writer.SetStatus SpanStatusOutcome.Ok
            writer.SetTag "axial.flow.outcome" "success"
        | Exit.Failure cause ->
            let defects = Cause.defects cause
            let failures = Cause.failures cause
            let interrupted = Cause.isInterrupted cause

            let outcome =
                if not (List.isEmpty defects) then "die"
                elif not (List.isEmpty failures) then "fail"
                else "interrupt"

            writer.SetTag "axial.flow.outcome" outcome

            if interrupted then
                writer.SetTag "axial.flow.interrupted" "true"

            match failures with
            | firstError :: _ ->
                writer.SetStatus (SpanStatusOutcome.Error (renderError firstError))
                writer.SetTag "axial.flow.error" (renderError firstError)
            | [] -> ()

            // Defects dominate typed errors for status/exception tags.
            match defects with
            | firstDefect :: _ -> tagDefect writer firstDefect
            | [] -> ()

            if isComposite cause then
                writer.SetTag "axial.flow.cause" (Cause.prettyPrint renderError cause)

    /// Stamps a settling fiber's status and outcome onto its span, using the same outcome vocabulary
    /// as workflow exits.
    let stampFiberEnd (writer: SpanWriter) (status: FiberStatus) (defect: exn option) =
        writer.SetTag "axial.flow.fiber.status" (string status)

        match status, defect with
        | FiberStatus.Succeeded, _ ->
            writer.SetStatus SpanStatusOutcome.Ok
            writer.SetTag "axial.flow.outcome" "success"
        | FiberStatus.Interrupted, _ ->
            writer.SetTag "axial.flow.outcome" "interrupt"
            writer.SetTag "axial.flow.interrupted" "true"
        | _, Some defect ->
            writer.SetTag "axial.flow.outcome" "die"
            tagDefect writer defect
        | _, None ->
            writer.SetStatus (SpanStatusOutcome.Error "")
            writer.SetTag "axial.flow.outcome" "fail"

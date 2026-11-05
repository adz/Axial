/// Per-rule isolation and coverage for the full CLI. The build owns the run directory;
/// independent files avoid interleaved writes from the CLI's parallel dispatch.
module Axial.Guardrails.Execution

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Xml
open System.Xml.Linq
open FSharp.Analyzers.SDK

// XML 1.0 cannot represent every string an analyzer or exception can produce.
// Keep valid surrogate pairs, and replace invalid characters without dropping the finding.
let private xmlText (value: string) =
    let result = StringBuilder(value.Length)
    let mutable index = 0
    while index < value.Length do
        let ch = value[index]
        if Char.IsHighSurrogate ch && index + 1 < value.Length && Char.IsLowSurrogate value[index + 1] then
            result.Append(ch).Append(value[index + 1]) |> ignore
            index <- index + 2
        else
            result.Append(if XmlConvert.IsXmlChar ch then ch else '\uFFFD') |> ignore
            index <- index + 1
    result.ToString()

let private attribute name (value: obj) =
    let safe = match value with :? string as text -> box (xmlText text) | _ -> value
    XAttribute(XName.Get name, safe)

/// `record` adds per-file data to the rule's coverage entry (TaskSurface: `axial check`'s trend counts).
let trackWith (record: CliContext -> XElement list) (rule: string) (analyzer: Analyzer<CliContext>) : Analyzer<CliContext> =
    fun ctx -> async {
        // Invalid user configuration is not an analyzer failure.
        GuardrailConfig.rejectRemovedSeverity ()
        let directory = Environment.GetEnvironmentVariable "AXIAL_GUARDRAILS_RUN"
        let timed = Environment.GetEnvironmentVariable "AXIAL_GUARDRAILS_TIMINGS" = "true"
        let timer = if timed then Some(Stopwatch.StartNew()) else None
        let! outcome = async {
            try
                let config = GuardrailConfig.load ()
                match GuardrailConfig.shouldRunAnalyzer config rule with
                // A generated source is complete coverage with no findings; its records (trend counts) are omitted.
                | Ok _ when GuardrailConfig.isGeneratedSource config ctx.FileName -> return Ok([], [], true)
                | Ok true ->
                    let! findings = analyzer ctx
                    return Ok(GuardrailConfig.capSeverity config findings, record ctx, false)
                // A policy-selected empty result is complete coverage, not an infrastructure skip.
                | Ok false -> return Ok([], record ctx, false)
                | Error reason -> return Error reason
            with error -> return Error error.Message
        }
        let findings, records, generated, skipped, reason =
            match outcome with
            | Ok(findings, records, generated) -> findings, records, generated, false, ""
            | Error reason -> [], [], false, true, reason
        if not (String.IsNullOrEmpty directory) then
            try
                let entry = XElement(XName.Get "coverage",
                                     attribute "rule" rule,
                                     attribute "file" (Path.GetFullPath ctx.FileName),
                                     attribute "status" (if skipped then "skipped" elif generated then GuardrailConfig.ExcludedGeneratedStatus else "ran"),
                                     attribute "reason" reason)
                timer |> Option.iter (fun watch -> entry.Add(attribute "milliseconds" watch.Elapsed.TotalMilliseconds))
                // The build replays this record as a warning on every build, including cache hits. A failure here is
                // an infrastructure failure (missing coverage), never a silently dropped warning; `attribute` keeps
                // the reason XML-safe.
                match GuardrailConfig.skipReason (GuardrailConfig.load ()) ctx.FileName with
                | Some skip -> entry.Add(XElement(XName.Get "skip", attribute "file" ctx.FileName, attribute "reason" skip))
                | None -> ()
                for element in records do entry.Add element
                for finding in findings do
                    entry.Add(XElement(XName.Get "finding",
                                       attribute "code" finding.Code,
                                       attribute "severity" (string finding.Severity),
                                       attribute "line" finding.Range.StartLine,
                                       attribute "column" finding.Range.StartColumn,
                                       xmlText finding.Message))
                let path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".xml")
                let temporary = path + ".tmp"
                try
                    entry.Save temporary
                    File.Move(temporary, path)
                finally
                    if File.Exists temporary then File.Delete temporary
            with error ->
                // Missing coverage makes the build report incomplete; the CLI still receives findings.
                Console.Error.WriteLine($"Axial.Guardrails infrastructure: {rule} could not record {ctx.FileName}: {error.Message}")
        elif skipped then
            Console.Error.WriteLine($"Axial.Guardrails infrastructure: {rule} skipped {ctx.FileName}: {reason}")
        return findings
    }

let track rule analyzer = trackWith (fun _ -> []) rule analyzer

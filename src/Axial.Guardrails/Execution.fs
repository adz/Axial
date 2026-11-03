/// Per-rule isolation and coverage for the full CLI. The build owns the run directory;
/// independent files avoid interleaved writes from the CLI's parallel dispatch.
module Axial.Guardrails.Execution

open System
open System.Diagnostics
open System.IO
open System.Xml.Linq
open FSharp.Analyzers.SDK

let private attribute name (value: obj) = XAttribute(XName.Get name, value)

let track (rule: string) (analyzer: Analyzer<CliContext>) : Analyzer<CliContext> =
    fun ctx -> async {
        // Invalid user configuration is not an analyzer failure.
        GuardrailConfig.rejectRemovedSeverity ()
        let directory = Environment.GetEnvironmentVariable "AXIAL_GUARDRAILS_RUN"
        let timed = Environment.GetEnvironmentVariable "AXIAL_GUARDRAILS_TIMINGS" = "true"
        let timer = if timed then Some(Stopwatch.StartNew()) else None
        let! outcome = async {
            try
                let! findings = analyzer ctx
                return Ok findings
            with error -> return Error error.Message
        }
        let findings, skipped, reason =
            match outcome with
            | Ok findings -> findings, false, ""
            | Error reason -> [], true, reason
        if not (String.IsNullOrEmpty directory) then
            let entry = XElement(XName.Get "coverage",
                                 attribute "rule" rule,
                                 attribute "file" (Path.GetFullPath ctx.FileName),
                                 attribute "status" (if skipped then "skipped" else "ran"),
                                 attribute "reason" reason)
            timer |> Option.iter (fun watch -> entry.Add(attribute "milliseconds" watch.Elapsed.TotalMilliseconds))
            for finding in findings do
                entry.Add(XElement(XName.Get "finding",
                                   attribute "code" finding.Code,
                                   attribute "severity" (string finding.Severity),
                                   attribute "line" finding.Range.StartLine,
                                   attribute "column" finding.Range.StartColumn,
                                   finding.Message))
            let path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".xml")
            let temporary = path + ".tmp"
            entry.Save temporary
            File.Move(temporary, path)
        elif skipped then
            Console.Error.WriteLine($"warning AXGINF001: {rule} skipped {ctx.FileName}: {reason}")
        return findings
    }

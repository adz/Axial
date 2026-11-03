namespace Axial.Guardrails.Tests

open System
open System.IO
open System.Reflection
open System.Text.Json
open FSharp.Analyzers.SDK
open Xunit

module FullDiagnosticCorpus =
    type Snapshot = { Source: string; Findings: string array }

    let sources () =
        [ "let f () = Cache.make (fun k -> Flow.ok k) |> Flow.run ()"
          "let f () = Flow.run () (Hub.makeScoped ())\nlet g () = Flow.run () (Queue.makeScoped QueueStrategy.Unbounded)"
          "let f () = Flow.run () (Cache.make (fun k -> Flow.ok k) |> Flow.map id)"
          "module C = Axial.Cache\nlet f () = C.make (fun k -> Flow.ok k) |> Flow.run ()"
          "module Cache =\n    let make x = x\nmodule Flow =\n    let run x = x\nlet f () = Cache.make 1 |> Flow.run"
          "let f () = Task.FromResult 1 |> ignore\nlet g () = ignore (Task.FromResult 2)\nlet h () = let _ = ValueTask<int>(1) in ()"
          "let ignore (pending: Task<int>) = pending\nlet f () = Task.FromResult 1 |> ignore\nlet g () = [ Task.FromResult 1; Task.FromResult 2 ]"
          "let f () = try failwith \"x\" with :? System.IO.FileNotFoundException as unused -> ()\nlet g () = try failwith \"x\" with :? exn -> ()"
          "type Failure = exn\nlet f () = try failwith \"x\" with :? Failure -> ()\nlet g () = try failwith \"x\" with error -> Error error"
          "let f () = Task.Run(fun () -> 1)\nlet token = System.Threading.CancellationToken.None\nlet source = TaskCompletionSource<int>()"
          "type Item = { Name: string }\nlet f () = sprintf \"%A\" { Name = \"x\" }\nlet g () = string { Name = \"x\" }"
          "let f () = flow { return raise (System.Exception()) }\nlet g () = System.DateTimeOffset.UtcNow" ]
        |> List.map (fun body -> "module M\nopen Axial\nopen System.Threading.Tasks\n" + body + "\n")

    let capture (assembly: Assembly) source =
        task {
            let! ctx = AnalyzerTestContext.createWithAxial true source
            let errors = ctx.CheckFileResults.Diagnostics |> Array.filter (fun d -> d.Severity = FSharp.Compiler.Diagnostics.FSharpDiagnosticSeverity.Error)
            Assert.True(Array.isEmpty errors, sprintf "Invalid corpus: %A" errors)
            let plugins =
                assembly.GetTypes() |> Array.collect (fun t -> t.GetMethods(BindingFlags.Public ||| BindingFlags.Static))
                |> Array.filter (fun m -> m.GetCustomAttributes(false) |> Array.exists (fun a -> a.GetType().Name = "CliAnalyzerAttribute"))
            let findings = ResizeArray<string>()
            for plugin in plugins do
                let! messages = (plugin.Invoke(null, [| box ctx |]) :?> Async<Message list>) |> Async.StartAsTask
                for m in messages do
                    // Only AXG010's guidance changes in this slice; its sites/severity must not change.
                    let text = if m.Code = "AXG010" then "narrowing guidance" else m.Message
                    findings.Add($"{m.Code}|{m.Severity}|{m.Range.StartLine}:{m.Range.StartColumn}-{m.Range.EndLine}:{m.Range.EndColumn}|{text}")
            return findings.ToArray() |> Array.sort
        }

/// full-diagnostic-corpus.json freezes every finding for the sources above. After a deliberate message or site
/// change, regenerate it from the current analyzers and review the diff:
///   AXIAL_GUARDRAILS_CORPUS_CAPTURE=$PWD/tests/Axial.Guardrails.Tests/full-diagnostic-corpus.json \
///     dotnet test tests/Axial.Guardrails.Tests --filter "FullyQualifiedName~FullDiagnosticEquivalenceTests"
/// Setting AXIAL_GUARDRAILS_REFERENCE_ANALYZER to an older Axial.Guardrails.dll instead compares against, and
/// captures from, that build.
[<Collection("Concurrency policy environment")>]
type FullDiagnosticEquivalenceTests() =
    [<Fact>]
    member _.``Full corpus matches pre optimization diagnostics exactly`` () =
        task {
            let output = Environment.GetEnvironmentVariable "AXIAL_GUARDRAILS_CORPUS_CAPTURE"
            let capturing = not (String.IsNullOrEmpty output)
            let snapshots =
                if capturing then
                    FullDiagnosticCorpus.sources () |> List.map (fun source -> { FullDiagnosticCorpus.Source = source; FullDiagnosticCorpus.Findings = [||] }) |> List.toArray
                else JsonSerializer.Deserialize<FullDiagnosticCorpus.Snapshot array>(File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "full-diagnostic-corpus.json")))
            Assert.Equal<string list>(FullDiagnosticCorpus.sources (), snapshots |> Array.map _.Source |> Array.toList)
            let referencePath = Environment.GetEnvironmentVariable "AXIAL_GUARDRAILS_REFERENCE_ANALYZER"
            let reference =
                if String.IsNullOrEmpty referencePath then None else Some(Assembly.LoadFile referencePath)
            let captured = ResizeArray<FullDiagnosticCorpus.Snapshot>()
            for snapshot in snapshots do
                let! findings = FullDiagnosticCorpus.capture typeof<Axial.Guardrails.GuardrailConfig.Config>.Assembly snapshot.Source
                match reference with
                | Some assembly ->
                    let! original = FullDiagnosticCorpus.capture assembly snapshot.Source
                    Assert.Equal<string>(original, findings)
                    captured.Add { Source = snapshot.Source; Findings = original }
                | None when capturing -> captured.Add { Source = snapshot.Source; Findings = findings }
                | None -> Assert.Equal<string>(snapshot.Findings, findings)
            if capturing then
                File.WriteAllText(output, JsonSerializer.Serialize(captured.ToArray(), JsonSerializerOptions(WriteIndented = true)))
        }

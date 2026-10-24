namespace Axial.Guardrails.Tests

open System.IO
open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open Axial.Guardrails
open Swensen.Unquote
open Xunit

module HardeningTests =
    let private run analyzer source =
        task {
            let! ctx = AnalyzerTestContext.create source
            return! analyzer ctx |> Async.StartAsTask
        }

    let private runWithAxial analyzer source =
        task {
            let! ctx = AnalyzerTestContext.createWithAxial true source
            let! messages = analyzer ctx |> Async.StartAsTask
            return ctx, messages
        }

    let private applyFix (source: string) (fix: Fix) =
        let lines = source.Split('\n')
        let index = fix.FromRange.StartLine - 1
        let line = lines[index]
        lines[index] <- line.Substring(0, fix.FromRange.StartColumn) + fix.ToText + line.Substring(fix.FromRange.EndColumn)
        System.String.Join("\n", lines)

    [<Fact>]
    let ``suppression requires rule reason and declared boundary`` () =
        task {
            let! ctx = AnalyzerTestContext.create "module M\nlet value = System.DateTime.UtcNow // axial-allow-AXG001: live clock boundary\n"
            let directives = Suppressions.validDirectives ctx.SourceText
            let directive = Assert.Single directives
            Assert.Equal(Some "AXG001", directive.RuleId)
            Assert.Equal("live clock boundary", directive.Reason)
            Assert.False(Suppressions.isAllowed Set.empty ctx.FileName ctx.SourceText 2 "AXG001")
            Assert.True(Suppressions.isAllowed (Set.singleton ctx.FileName) ctx.FileName ctx.SourceText 2 "AXG001")
        }

    [<Fact>]
    let ``suppression-like text in strings XML docs and block comments is ignored`` () =
        task {
            let! ctx =
                AnalyzerTestContext.create
                    "module M\nlet text = \"axial-allow-AXG003: example\"\n/// axial-allow-AXG004: docs\n(* axial-allow-AXG005: block *)\nlet value = 1\n"
            Assert.Empty(Suppressions.allDirectives ctx.SourceText)
        }

    [<Theory>]
    [<InlineData("AXG003")>]
    [<InlineData("AXG004")>]
    [<InlineData("AXG005")>]
    [<InlineData("AXG006")>]
    [<InlineData("AXG007")>]
    [<InlineData("AXG008")>]
    let ``AXG002 rejects stale suppressions for every structural rule`` ruleId =
        task {
            let source = $"module M\n// axial-allow-{ruleId}: obsolete boundary\nlet value = 1\n"
            let! messages = run SuppressionIntegrityAnalyzer.suppressionIntegrityAnalyzer source
            let message = Assert.Single messages
            Assert.Equal("AXG002", message.Code)
            Assert.Contains("stale suppression", message.Message)
        }

    [<Fact>]
    let ``AXG002 rejects legacy syntax and offers removal for a comment line`` () =
        task {
            let! messages = run SuppressionIntegrityAnalyzer.suppressionIntegrityAnalyzer "module M\n// axial-allow-raise\nlet x = 1\n"
            let message = Assert.Single messages
            Assert.Equal("AXG002", message.Code)
            Assert.Equal(Severity.Error, message.Severity)
            let fix = Assert.Single message.Fixes
            Assert.Equal("// axial-allow-raise", fix.FromText)
            Assert.Equal("", fix.ToText)
        }

    [<Fact>]
    let ``AXG003 offers a Flow fail fix only when the flow accepts the error and the result type-checks`` () =
        task {
            let source =
                "module M\nopen Axial\nlet operation : Flow<unit, string, int> =\n    flow {\n        failwith \"nope\"\n        return 1\n    }\n"
            let! _, messages = runWithAxial RaiseInFlowAnalyzer.raiseInFlowAnalyzer source
            let fix = (Assert.Single messages).Fixes |> Assert.Single
            Assert.Equal("return! Flow.fail \"nope\"", fix.ToText)
            let fixedSource = applyFix source fix
            let! _ = AnalyzerTestContext.createWithAxial true fixedSource
            ()
        }

    [<Fact>]
    let ``AXG003 uses Flow die for a proven exception and the result type-checks`` () =
        task {
            let source =
                "module M\nopen System\nopen Axial\nlet operation (error: Exception) : Flow<unit, string, int> =\n    flow {\n        raise error\n        return 1\n    }\n"
            let! _, messages = runWithAxial RaiseInFlowAnalyzer.raiseInFlowAnalyzer source
            let fix = (Assert.Single messages).Fixes |> Assert.Single
            Assert.Equal("return! Flow.die error", fix.ToText)
            let! _ = AnalyzerTestContext.createWithAxial true (applyFix source fix)
            ()
        }

    [<Fact>]
    let ``AXG003 with a constrained non-string error has no fix`` () =
        task {
            let source =
                "module M\nopen Axial\ntype DomainError = Bad\nlet operation : Flow<unit, DomainError, int> =\n    flow {\n        failwith \"nope\"\n        return 1\n    }\n"
            let! _, messages = runWithAxial RaiseInFlowAnalyzer.raiseInFlowAnalyzer source
            Assert.Empty((Assert.Single messages).Fixes)
        }

    [<Fact>]
    let ``AXG003 inside a local try with has no fix`` () =
        task {
            let source =
                "module M\nopen Axial\nlet operation : Flow<unit, string, int> =\n    flow {\n        try\n            failwith \"nope\"\n            return 0\n        with _ ->\n            return 1\n    }\n"
            let! _, messages = runWithAxial RaiseInFlowAnalyzer.raiseInFlowAnalyzer source
            Assert.Empty((Assert.Single messages).Fixes)
        }

    [<Theory>]
    [<InlineData("AXG001")>]
    [<InlineData("AXG002")>]
    [<InlineData("AXG003")>]
    [<InlineData("AXG004")>]
    [<InlineData("AXG005")>]
    [<InlineData("AXG006")>]
    [<InlineData("AXG007")>]
    [<InlineData("AXG008")>]
    let ``every rule uses configured default error severity`` code =
        task {
            let! ctx = AnalyzerTestContext.create "module M\nlet value = 1\n"
            let diagnostic =
                { Type = "test"
                  Message = "replace the offending construct"
                  Code = code
                  Severity = Severity.Warning
                  Range = Range.mkRange ctx.FileName (Position.mkPos 2 4) (Position.mkPos 2 9)
                  Fixes = [] }
            let actual = GuardrailConfig.applyWith GuardrailConfig.defaults EffectCatalog.CoreGuardrail ctx [ diagnostic ]
            Assert.Equal(Severity.Error, (Assert.Single actual).Severity)

            let file = Path.GetRelativePath(GuardrailConfig.defaults.BaselineRoot, ctx.FileName).Replace('\\', '/')
            let hash = GuardrailConfig.fingerprint ctx.SourceText diagnostic
            let baselined =
                { GuardrailConfig.defaults with Baseline = [ code, file, hash ] }
                |> fun config -> GuardrailConfig.applyWith config EffectCatalog.CoreGuardrail ctx [ diagnostic ]
            Assert.Equal(Severity.Warning, (Assert.Single baselined).Severity)
        }

    [<Fact>]
    let ``baseline fingerprint survives line movement and includes the enclosing symbol`` () =
        task {
            let! first = AnalyzerTestContext.create "module M\nlet operation () = System.DateTime.UtcNow\n"
            let! moved = AnalyzerTestContext.create "module M\n\n\nlet operation () = System.DateTime.UtcNow\n"
            let! other = AnalyzerTestContext.create "module M\nlet different () = System.DateTime.UtcNow\n"
            let diagnostic (ctx: CliContext) line =
                { Type = "test"
                  Message = "use IClock"
                  Code = "AXG001"
                  Severity = Severity.Error
                  Range = Range.mkRange ctx.FileName (Position.mkPos line 19) (Position.mkPos line 41)
                  Fixes = [] }
            let firstHash = GuardrailConfig.fingerprint first.SourceText (diagnostic first 2)
            let movedHash = GuardrailConfig.fingerprint moved.SourceText (diagnostic moved 4)
            let otherHash = GuardrailConfig.fingerprint other.SourceText (diagnostic other 2)
            test <@ firstHash = movedHash @>
            test <@ firstHash <> otherHash @>
        }

    [<Fact>]
    let ``duplicate baseline matching counts diagnostics and ignores source occurrences`` () =
        task {
            let! ctx =
                AnalyzerTestContext.create
                    "module M\nlet operation () =\n    // unrelated System.DateTime.UtcNow text\n    System.DateTime.UtcNow, System.DateTime.UtcNow\n"
            let diagnostic startColumn =
                { Type = "test"
                  Message = "use IClock"
                  Code = "AXG001"
                  Severity = Severity.Error
                  Range = Range.mkRange ctx.FileName (Position.mkPos 4 startColumn) (Position.mkPos 4 (startColumn + 22))
                  Fixes = [] }
            let firstDiagnostic = diagnostic 4
            let secondDiagnostic = diagnostic 28
            let hash = GuardrailConfig.fingerprint ctx.SourceText firstDiagnostic
            test <@ hash = GuardrailConfig.fingerprint ctx.SourceText secondDiagnostic @>

            let file = Path.GetRelativePath(GuardrailConfig.defaults.BaselineRoot, ctx.FileName).Replace('\\', '/')
            let config =
                { GuardrailConfig.defaults with Baseline = [ "AXG001", file, hash; "AXG001", file, hash ] }

            let two = GuardrailConfig.applyWith config EffectCatalog.CoreGuardrail ctx [ firstDiagnostic; secondDiagnostic ]
            test <@ two |> List.map _.Severity = [ Severity.Warning; Severity.Warning ] @>

            let thirdDiagnostic = { secondDiagnostic with Range = Range.mkRange ctx.FileName (Position.mkPos 4 28) (Position.mkPos 4 50) }
            let three = GuardrailConfig.applyWith config EffectCatalog.CoreGuardrail ctx [ firstDiagnostic; secondDiagnostic; thirdDiagnostic ]
            test <@ three |> List.map _.Severity = [ Severity.Warning; Severity.Warning; Severity.Error ] @>
            Assert.Contains("2 baselined, 3 found", three[2].Message)

            let one = GuardrailConfig.applyWith config EffectCatalog.CoreGuardrail ctx [ secondDiagnostic ]
            test <@ one |> List.map _.Severity = [ Severity.Warning ] @>
        }

    [<Fact>]
    let ``counted baseline entries parse as a multiset`` () =
        let hash = String.replicate 64 "a"
        let config = GuardrailConfig.parse [ $"baseline\tAXG001\tsrc/File.fs\t{hash} x2" ]
        test <@ config.Baseline = [ ("AXG001", "src/File.fs", hash); ("AXG001", "src/File.fs", hash) ] @>

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
    let ``AXG003 offers Flow fail for a standalone mechanical failure`` () =
        task {
            let source =
                "module M\n"
                + "type FlowBuilder() =\n"
                + "    member _.Delay f = f ()\n"
                + "    member _.Zero() = ()\n"
                + "let flow = FlowBuilder()\n"
                + "let operation =\n"
                + "    flow {\n"
                + "        failwith \"nope\"\n"
                + "    }\n"
            let! messages = run RaiseInFlowAnalyzer.raiseInFlowAnalyzer source
            let message = Assert.Single messages
            let fix = Assert.Single message.Fixes
            Assert.Equal("failwith \"nope\"", fix.FromText)
            Assert.Equal("return! Flow.fail \"nope\"", fix.ToText)
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
                { GuardrailConfig.defaults with Baseline = Set.singleton(code, file, hash) }
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

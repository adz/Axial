namespace Axial.Guardrails.Tests

open System
open System.IO
open FSharp.Analyzers.SDK
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open Axial.Guardrails
open Axial.Guardrails.EffectCatalog
open Swensen.Unquote
open Xunit

module CapabilityTests =
    let private payments level =
        $"capability\tpayments\tIPayments\t\t\tPayments.charge order\tPayments.live\tTestPayments()\tany:M.Gateway|ctor:M.Refunds\t{level}\t"

    let private configWith lines = GuardrailConfig.parse ("guardrail\tAxial" :: lines)

    let private gateway =
        "module M\n"
        + "type Gateway() =\n"
        + "    member _.Charge() = 1\n"
        + "type Refunds() =\n"
        + "    member _.Issue() = 2\n"

    let private analyze (config: GuardrailConfig.Config) (source: string) =
        task {
            let! ctx = AnalyzerTestContext.create source
            return EffectBoundaryAnalyzer.analyzeWithConfig config ctx
        }

    [<Fact>]
    let ``a declared capability fills its defaults, derives a rule per replaced API, and enables its guardrail`` () =
        let config = configWith [ payments "" ]
        let capability = config.AllCapabilities |> List.find (fun capability -> capability.Name = "payments")
        test <@ capability.Env = "IHasPayments" && capability.Field = "Payments: IPayments" @>
        test <@ capability.Level = Guide && capability.Guardrail = "payments" @>
        test <@ config.EnabledGuardrails.Contains "payments" @>
        let rules = config.Rules |> List.filter (fun rule -> rule.Capability = Some "payments")
        test <@ rules |> List.map _.Match = [ AnyMemberOf "M.Gateway"; ConstructorOf "M.Refunds" ] @>
        test <@ rules |> List.forall (fun rule -> config.CapabilityOf rule = Some capability) @>

    [<Theory>]
    [<InlineData("capability\tpayments\tIPayments\t\t\t\t\t\t\t\t", "replaces nothing")>]
    [<InlineData("capability\tpayments\tIPayments\t\t\t\t\t\tany:M.Gateway\tstrict\t", "guide or enforce")>]
    [<InlineData("capability\tpayments\tIPayments\t\t\t\t\t\tM.Gateway\t\t", "cannot read Replaces entry")>]
    [<InlineData("capability\tpayments\t\t\t\t\t\t\tany:M.Gateway\t\t", "has no Service")>]
    [<InlineData("capability\tfilesystem\tIFiles\t\t\t\t\t\tany:M.Gateway\t\t", "more than once")>]
    let ``a malformed declaration fails with its reason`` (line: string) (reason: string) =
        let error = Assert.Throws<ArgumentException>(fun () -> configWith [ line ] |> ignore)
        test <@ error.Message.Contains reason @>

    [<Fact>]
    let ``a guide capability reports information that names the service, its field, an example and the fake`` () =
        task {
            let! messages = analyze (configWith [ payments "guide" ]) (gateway + "let pay () = Gateway().Charge()\nlet back () = Refunds()\n")
            test <@ messages |> List.map _.Range.StartLine |> List.distinct = [ 6; 7 ] @>
            for message in messages do
                test <@ message.Severity = Severity.Info && message.Type = EffectBoundaryAnalyzer.CapabilityGuide @>
                test <@ message.Message.Contains "`Payments: IPayments`" && message.Message.Contains "IHasPayments" @>
                test <@ message.Message.Contains "Payments.charge order" && message.Message.Contains "TestPayments()" @>
        }

    [<Fact>]
    let ``an enforced capability reports at the ambient-effect level like Axial's own rules`` () =
        task {
            let! messages = analyze (configWith [ payments "enforce" ]) (gateway + "let pay () = Gateway().Charge()\n")
            test <@ not messages.IsEmpty && messages |> List.forall (fun message -> message.Severity = Severity.Error && message.Type = "Axial Effect Boundary") @>
        }

    [<Fact>]
    let ``a file that implements the service gets no guide finding`` () =
        task {
            let implementation =
                gateway
                + "type IPayments =\n"
                + "    abstract Charge: unit -> int\n"
                + "let live = { new IPayments with member _.Charge() = Gateway().Charge() }\n"
            let! messages = analyze (configWith [ payments "guide" ]) implementation
            test <@ messages = [] @>
        }

    [<Fact>]
    let ``built-in advice names the environment field and fake, or says the runtime provides the service`` () =
        let config = GuardrailConfig.parse [ "guardrail\tAxial"; "guardrail\tAxial.FileSystem" ]
        let message id =
            let rule = builtInRules |> List.find (fun rule -> rule.Id = id)
            (EffectBoundaryAnalyzer.toMessage config rule Range.range0).Message
        let file = message "filesystem.file"
        test <@ file.Contains "`FileSystem: IFileSystem`" && file.Contains "IHasFileSystem" && file.Contains "TestFileSystem()" @>
        let clock = message "clock.datetimeoffset"
        test <@ clock.Contains "Clock.now" && clock.Contains "environment does not change" @>
        let uncovered = message "environment.processor-count"
        test <@ uncovered.Contains "axial add service" @>

    [<Fact>]
    let ``Fast mode reports a guide capability as a hint and skips the implementing file`` () =
        let parse (source: string) =
            let checker = FSharpChecker.Create()
            let options = { FSharpParsingOptions.Default with SourceFiles = [| "/tmp/Fast.fs" |] }
            checker.ParseFile("/tmp/Fast.fs", SourceText.ofString source, options) |> Async.RunSynchronously |> _.ParseTree
        let config = configWith [ payments "guide" ]
        let findings source =
            FastSyntax.analyze config "/tmp/Fast.fs" (SourceText.ofString source) (parse source)
            |> List.map snd |> List.filter (fun message -> message.Code = "AXG001")
        let caller = "module N\nlet back () = M.Refunds()\n"
        test <@ findings caller |> List.map (fun message -> message.Severity, message.Message.Contains "IPayments") = [ Severity.Info, true ] @>
        test <@ findings (caller + "type IPayments = abstract Charge: unit -> int\n") = [] @>

    [<Fact>]
    let ``add service writes a capability whose declaration Guardrails reads back`` () =
        let directory = Path.Combine(Path.GetTempPath(), "axial-capability-" + Guid.NewGuid().ToString("N"))
        try
            match Axial.Tool.Capabilities.scaffold directory "Payments" (Some "any:Stripe.ChargeService|ctor:Stripe.RefundService") with
            | Error error -> failwith error
            | Ok message ->
                let source = File.ReadAllText(Path.Combine(directory, "Payments.fs"))
                test <@ source.Contains "type IPayments =" && source.Contains "type IHasPayments =" && source.Contains "type TestPayments(" @>
                test <@ message.Contains "<AxialCapability Include=\"payments\"" @>
                // The printed item, as MSBuild writes it into the rules file, parses into a capability.
                let attribute name =
                    let marker = name + "=\""
                    let start = message.IndexOf marker + marker.Length
                    message.Substring(start, message.IndexOf('"', start) - start)
                let line =
                    String.concat "\t" [ "capability"; "payments"; attribute "Service"; ""; ""; attribute "Example"; attribute "Live"; attribute "Fake"; attribute "Replaces"; ""; "" ]
                let capability = (configWith [ line ]).AllCapabilities |> List.find (fun capability -> capability.Name = "payments")
                test <@ capability.Service = "IPayments" && capability.Fake = "TestPayments(fun input -> Ok input)" @>
                test <@ Axial.Tool.Capabilities.scaffold directory "Payments" None |> Result.isError @>
                test <@ Axial.Tool.Capabilities.scaffold directory "payments" None |> Result.isError @>
                test <@ Axial.Tool.Capabilities.scaffold directory "Orders" (Some "Stripe.ChargeService") |> Result.isError @>
        finally
            if Directory.Exists directory then Directory.Delete(directory, true)

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
    let private paymentsLine level implementation =
        String.concat "\t"
            [ "capability"; "payments"; "IPayments"; ""; ""; "Payments.charge order"; "Payments.live"; "TestPayments()"
              "any:M.Gateway|ctor:M.Refunds"; level; ""; implementation ]

    let private configWith lines = GuardrailConfig.parse ("guardrail\tAxial" :: lines)

    /// A config whose payments capability is implemented by `implementation` (a full path).
    let private payments level implementation =
        configWith [ paymentsLine level "Payments.fs"; $"capabilityFile\tpayments\t{implementation}" ]

    let private gateway =
        "module M\n"
        + "type Gateway() =\n"
        + "    member _.Charge() = 1\n"
        + "type Refunds() =\n"
        + "    member _.Issue() = 2\n"

    let private fixture name = Path.Combine(Path.GetTempPath(), "axial-capability-" + Guid.NewGuid().ToString("N"), name)

    let private analyzeAt (config: GuardrailConfig.Config) fileName (source: string) =
        task {
            let! ctx = AnalyzerTestContext.createAt fileName source
            return EffectBoundaryAnalyzer.analyzeWithConfig config ctx
        }

    [<Fact>]
    let ``a declaration blocks by default, enables its guardrail and lists its implementation files`` () =
        let implementation = fixture "Payments.fs"
        let config = payments "" implementation
        let capability = config.AllCapabilities |> List.find (fun capability -> capability.Name = "payments")
        test <@ capability.Level = Enforce && capability.Implementation = [ implementation ] @>
        test <@ capability.Env = "IHasPayments" && capability.Field = "Payments: IPayments" @>
        test <@ config.EnabledGuardrails.Contains "payments" @>
        let rules = config.Rules |> List.filter (fun rule -> rule.Capability = Some "payments")
        test <@ rules |> List.map _.Match = [ AnyMemberOf "M.Gateway"; ConstructorOf "M.Refunds" ] @>

    [<Theory>]
    [<InlineData("capability\tpayments\tIPayments\t\t\t\t\t\t\t\t\tPayments.fs", "replaces nothing")>]
    [<InlineData("capability\tpayments\tIPayments\t\t\t\t\t\tany:M.Gateway\tstrict\t\tPayments.fs", "guide or enforce")>]
    [<InlineData("capability\tpayments\tIPayments\t\t\t\t\t\tM.Gateway\t\t\tPayments.fs", "cannot read Replaces entry")>]
    [<InlineData("capability\tpayments\t\t\t\t\t\t\tany:M.Gateway\t\t\tPayments.fs", "has no Service")>]
    [<InlineData("capability\tpayments\tIPayments\t\t\t\t\t\tany:M.Gateway\t\t\t", "has no Implementation")>]
    [<InlineData("capability\tfilesystem\tIFiles\t\t\t\t\t\tany:M.Gateway\t\t\tFiles.fs", "more than once")>]
    [<InlineData("capability\tfiles\tIFiles\t\t\t\t\t\tany:System.IO.File\t\t\tFiles.fs", "use Axial.FileSystem's IFileSystem service instead")>]
    let ``a declaration that cannot be honoured fails with its reason`` (line: string) (reason: string) =
        let error = Assert.Throws<ArgumentException>(fun () -> configWith [ line ] |> ignore)
        test <@ error.Message.Contains reason @>

    [<Fact>]
    let ``two declarations cannot own the same API`` () =
        let other = "capability\tbilling\tIBilling\t\t\t\t\t\tany:M.Gateway\t\t\tBilling.fs"
        let error = Assert.Throws<ArgumentException>(fun () -> configWith [ paymentsLine "" "Payments.fs"; other ] |> ignore)
        test <@ error.Message.Contains "claim the same API" @>

    [<Fact>]
    let ``a use outside the implementation is a blocking error that tells the agent what to change`` () =
        task {
            let config = payments "" (fixture "Payments.fs")
            let! messages = analyzeAt config (fixture "Checkout.fs") (gateway + "let pay () = Gateway().Charge()\nlet back () = Refunds()\n")
            test <@ messages |> List.map _.Range.StartLine |> List.distinct = [ 6; 7 ] @>
            for message in messages do
                test <@ message.Severity = Severity.Error @>
                test <@ message.Message.Contains "Replace it with IPayments" && message.Message.Contains "`Payments: IPayments`" @>
                test <@ message.Message.Contains "IHasPayments" && message.Message.Contains "TestPayments()" @>
        }

    [<Fact>]
    let ``a guide-level capability reports the same advice as information`` () =
        task {
            let! messages = analyzeAt (payments "guide" (fixture "Payments.fs")) (fixture "Checkout.fs") (gateway + "let back () = Refunds()\n")
            test <@ messages |> List.map (fun message -> message.Severity, message.Type) = [ Severity.Info, EffectUse.CapabilityGuide ] @>
        }

    [<Fact>]
    let ``the implementation may call only its own API`` () =
        task {
            let implementation = fixture "Payments.fs"
            let source = gateway + "let pay () = Gateway().Charge()\nlet stamp () = System.DateTimeOffset.UtcNow\n"
            let! messages = analyzeAt (payments "" implementation) implementation source
            test <@ messages |> List.map _.Range.StartLine = [ 7 ] @>
            test <@ messages.Head.Message.Contains "This file implements the payments capability, which may call only its own API" @>
        }

    [<Fact>]
    let ``a capabilities-role package owns its own capability's API and nothing else`` () =
        task {
            let root = Path.Combine(Path.GetTempPath(), "axial-capability-role-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory root |> ignore
            let policy = Path.Combine(root, "Axial.Guardrails.toml")
            File.WriteAllText(policy, "version = 2\n[default]\nrole = \"domain\"\n[projects]\ncapabilities = [\"Axial.FileSystem.fsproj\"]\n")
            File.WriteAllText(Path.Combine(root, "Axial.FileSystem.fsproj"), "<Project />")
            try
                let config =
                    GuardrailConfig.parse
                        [ "guardrail\tAxial"; "guardrail\tAxial.FileSystem"; $"setting\tpolicyRoot\t{root}"; $"setting\tpolicyFile\t{policy}"
                          $"setting\tprojectPath\t{root}/Axial.FileSystem.fsproj" ]
                let source = "module Live\nlet read path = System.IO.File.ReadAllText path\nlet stamp () = System.DateTimeOffset.UtcNow\n"
                let! messages = analyzeAt config (Path.Combine(root, "Live.fs")) source
                test <@ messages |> List.map _.Range.StartLine = [ 3 ] @>
                test <@ messages.Head.Message.Contains "This file implements the filesystem capability" @>
            finally
                TestDirectory.delete root
        }

    [<Fact>]
    let ``every allowed call is counted with its reason`` () =
        let parse (source: string) =
            let checker = FSharpChecker.Create()
            let options = { FSharpParsingOptions.Default with SourceFiles = [| "/tmp/Fast.fs" |] }
            checker.ParseFile("/tmp/Fast.fs", SourceText.ofString source, options) |> Async.RunSynchronously |> _.ParseTree
        let implementation = "/tmp/Payments.fs"
        let config = configWith [ paymentsLine "" "Payments.fs"; $"capabilityFile\tpayments\t{implementation}"; $"boundary\t{implementation}" ]
        let source =
            "module Payments\n"
            + "let a () = M.Refunds()\n"
            + "let b () = M.Refunds()\n"
            + "// axial-allow-AXG001: the live clock of the payment gateway's signature\n"
            + "let c () = System.DateTimeOffset.UtcNow\n"
            + "let d () = System.DateTimeOffset.UtcNow\n"
        let record = FastSyntax.effectUses config implementation (SourceText.ofString source) (parse source)
        let attribute name = record.Attribute(Xml.Linq.XName.Get name).Value
        test <@ (attribute "owned", attribute "boundary", attribute "host", attribute "reported") = ("2", "1", "0", "1") @>
        test <@ record.Elements() |> Seq.map (fun entry -> entry.Attribute(Xml.Linq.XName.Get "name").Value) |> List.ofSeq = [ "payments" ] @>

    [<Fact>]
    let ``Fast reports a use outside the implementation and allows the implementation's own calls`` () =
        let parse (source: string) =
            let checker = FSharpChecker.Create()
            let options = { FSharpParsingOptions.Default with SourceFiles = [| "/tmp/Fast.fs" |] }
            checker.ParseFile("/tmp/Fast.fs", SourceText.ofString source, options) |> Async.RunSynchronously |> _.ParseTree
        let config = payments "" "/tmp/Payments.fs"
        let findings fileName source =
            FastSyntax.analyze config fileName (SourceText.ofString source) (parse source)
            |> List.map snd |> List.filter (fun message -> message.Code = "AXG001")
        let caller = "module N\nlet back () = M.Refunds()\n"
        test <@ findings "/tmp/Checkout.fs" caller |> List.map (fun message -> message.Message.Contains "IPayments") = [ true ] @>
        test <@ findings "/tmp/Payments.fs" caller = [] @>

    [<Fact>]
    let ``advice names the environment field, says the runtime provides platform services, and offers a capability only for effects`` () =
        let config = GuardrailConfig.parse [ "guardrail\tAxial"; "guardrail\tAxial.FileSystem" ]
        let message id =
            let rule = builtInRules |> List.find (fun rule -> rule.Id = id)
            (EffectUse.toMessage config "/tmp/Domain.fs" rule Range.range0).Message
        let file = message "filesystem.file"
        test <@ file.Contains "`FileSystem: IFileSystem`" && file.Contains "IHasFileSystem" && file.Contains "TestFileSystem()" @>
        let clock = message "clock.datetimeoffset"
        test <@ clock.Contains "Clock.now" && clock.Contains "environment does not change" @>
        let uncovered = message "environment.processor-count"
        test <@ uncovered.Contains "declare a capability" && uncovered.Contains "Replaces=\"member:System.Environment::get_ProcessorCount,ProcessorCount\"" @>
        let blocking = message "blocking.task-wait"
        test <@ not (blocking.Contains "capability") @>

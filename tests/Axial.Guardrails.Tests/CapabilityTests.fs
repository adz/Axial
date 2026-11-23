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
    let private directory () =
        let path = Path.Combine(Path.GetTempPath(), "axial-capability-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory path |> ignore
        path

    let private paymentsDefinition level =
        "# The payments gateway, owned by IPayments.\n"
        + "name = \"payments\"\n"
        + "service = \"IPayments\"\n"
        + "example = \"Payments.charge order\"\n"
        + "live = \"Payments.live\"\n"
        + "fake = \"TestPayments()\"\n"
        + (if level = "" then "" else $"level = \"{level}\"\n")
        + "replaces = [\n"
        + "    \"any:M.Gateway\",\n"
        + "    \"ctor:M.Refunds\",\n"
        + "]\n"

    /// Writes a definition file and returns its config line.
    let private definitionLine (folder: string) (fileName: string) (text: string) =
        let path = Path.Combine(folder, fileName)
        File.WriteAllText(path, text)
        $"capabilityDefinition\t{path}"

    let private configWith lines = GuardrailConfig.parse ("guardrail\tAxial" :: lines)

    /// A config defining payments, claimed by `claimed` (full paths).
    let private payments level (claimed: string list) =
        let folder = directory ()
        configWith (definitionLine folder "payments.capability.toml" (paymentsDefinition level) :: (claimed |> List.map (fun path -> $"capabilityFile\tpayments\t{path}")))

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

    let private parse (source: string) =
        let checker = FSharpChecker.Create()
        let options = { FSharpParsingOptions.Default with SourceFiles = [| "/tmp/Fast.fs" |] }
        checker.ParseFile("/tmp/Fast.fs", SourceText.ofString source, options) |> Async.RunSynchronously |> _.ParseTree

    [<Fact>]
    let ``a definition blocks by default, enables its guardrail, and lists the files that claim it`` () =
        let claimed = fixture "Payments.fs"
        let config = payments "" [ claimed ]
        let capability = config.AllCapabilities |> List.find (fun capability -> capability.Name = "payments")
        test <@ capability.Level = Enforce && capability.Implementation = [ claimed ] @>
        test <@ capability.Env = "IHasPayments" && capability.Field = "Payments: IPayments" @>
        test <@ config.EnabledGuardrails.Contains "payments" @>
        let rules = config.Rules |> List.filter (fun rule -> rule.Capability = Some "payments")
        test <@ rules |> List.map _.Match = [ AnyMemberOf "M.Gateway"; ConstructorOf "M.Refunds" ] @>

    [<Theory>]
    [<InlineData("name = \"payments\"\nservice = \"IPayments\"\n", "replaces nothing")>]
    [<InlineData("name = \"payments\"\nservice = \"IPayments\"\nlevel = \"strict\"\nreplaces = [\"any:M.Gateway\"]\n", "guide or enforce")>]
    [<InlineData("name = \"payments\"\nservice = \"IPayments\"\nreplaces = [\"M.Gateway\"]\n", "cannot read replaces entry")>]
    [<InlineData("name = \"payments\"\nreplaces = [\"any:M.Gateway\"]\n", "has no service")>]
    [<InlineData("name = \"payments\"\nservice = \"IPayments\"\nimplementation = \"Payments.fs\"\nreplaces = [\"any:M.Gateway\"]\n", "unknown key implementation")>]
    [<InlineData("name = \"payments\"\nname = \"other\"\n", "set more than once")>]
    [<InlineData("name = \"payments\"\nservice = \"IPayments\"\nreplaces = [\"any:M.Gateway\"\n", "no closing ]")>]
    [<InlineData("name = \"filesystem\"\nservice = \"IFiles\"\nreplaces = [\"any:M.Gateway\"]\n", "more than once")>]
    [<InlineData("name = \"files\"\nservice = \"IFiles\"\nreplaces = [\"any:System.IO.File\"]\n", "use Axial.FileSystem's IFileSystem service instead")>]
    let ``a definition that cannot be honoured fails with its reason`` (text: string) (reason: string) =
        let line = definitionLine (directory ()) "broken.capability.toml" text
        let error = Assert.Throws<ArgumentException>(fun () -> configWith [ line ] |> ignore)
        test <@ error.Message.Contains reason @>

    [<Fact>]
    let ``a definition file is named for what it is`` () =
        let line = definitionLine (directory ()) "payments.toml" (paymentsDefinition "")
        let error = Assert.Throws<ArgumentException>(fun () -> configWith [ line ] |> ignore)
        test <@ error.Message.Contains "name the file <name>.capability.toml" @>

    [<Fact>]
    let ``two definitions cannot own the same API`` () =
        let folder = directory ()
        let other = "name = \"billing\"\nservice = \"IBilling\"\nreplaces = [ \"any:M.Gateway\" ]\n"
        let lines = [ definitionLine folder "payments.capability.toml" (paymentsDefinition ""); definitionLine folder "billing.capability.toml" other ]
        let error = Assert.Throws<ArgumentException>(fun () -> configWith lines |> ignore)
        test <@ error.Message.Contains "claim the same API" @>

    [<Fact>]
    let ``a file cannot claim a capability nobody defined`` () =
        let error = Assert.Throws<ArgumentException>(fun () -> configWith [ "capabilityFile\tpaymnets\t/tmp/Payments.fs" ] |> ignore)
        test <@ error.Message.Contains "claims capability 'paymnets', which is not defined" @>

    [<Fact>]
    let ``a use outside the claimed files is a blocking error that tells the agent what to change`` () =
        task {
            let config = payments "" [ fixture "Payments.fs" ]
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
            let! messages = analyzeAt (payments "guide" [ fixture "Payments.fs" ]) (fixture "Checkout.fs") (gateway + "let back () = Refunds()\n")
            test <@ messages |> List.map (fun message -> message.Severity, message.Type) = [ Severity.Info, EffectUse.CapabilityGuide ] @>
        }

    [<Fact>]
    let ``a claimed file may call only its own capability's API`` () =
        task {
            let claimed = fixture "Payments.fs"
            let source = gateway + "let pay () = Gateway().Charge()\nlet stamp () = System.DateTimeOffset.UtcNow\n"
            let! messages = analyzeAt (payments "" [ claimed ]) claimed source
            test <@ messages |> List.map _.Range.StartLine = [ 7 ] @>
            test <@ messages.Head.Message.Contains "This file claims the payments capability, so it may call only that API" @>
        }

    let private integrityAt (config: GuardrailConfig.Config) fileName (source: string) =
        task {
            let! ctx = AnalyzerTestContext.createAt fileName source
            return SuppressionIntegrityAnalyzer.analyzeWithConfig config ctx
        }

    [<Fact>]
    let ``an allowance in a file that claims the capability is reported as unneeded`` () =
        task {
            let claimed = fixture "Payments.fs"
            let source = gateway + "// axial-allow-AXG001: (why: charges directly)\nlet pay () = Gateway().Charge()\n"
            let! messages = integrityAt (payments "" [ claimed ]) claimed source
            let message = Assert.Single messages
            test <@ message.Code = "AXG002" && message.Range.StartLine = 6 && message.Severity = Severity.Error @>
            test <@ message.Message = "Remove this allowance: this file claims the payments capability, so the call needs no comment." @>
        }

    [<Fact>]
    let ``an allowance that is what allows the call stays valid`` () =
        task {
            let source = gateway + "// axial-allow-AXG001: (why: charges directly)\nlet pay () = Gateway().Charge()\n"
            let! messages = integrityAt (payments "" [ fixture "Payments.fs" ]) (fixture "Checkout.fs") source
            test <@ messages = [] @>
        }

    [<Fact>]
    let ``a claim on a file with no raw call for it is a warning naming the claim to remove`` () =
        task {
            let claimed = fixture "Payments.fs"
            let! unused = integrityAt (payments "" [ claimed ]) claimed (gateway + "let total = 1\n")
            let message = Assert.Single unused
            test <@ message.Code = "AXG002" && message.Severity = Severity.Warning && message.Range.StartLine = 1 @>
            test <@ message.Message.StartsWith "Remove the payments claim from this file's Compile item" @>
            let! used = integrityAt (payments "" [ claimed ]) claimed (gateway + "let pay () = Gateway().Charge()\n")
            test <@ used = [] @>
        }

    [<Fact>]
    let ``Axial's own capabilities are claimed by file, not granted to a whole package`` () =
        task {
            let root = directory ()
            let policy = Path.Combine(root, "Axial.Guardrails.toml")
            File.WriteAllText(policy, "version = 2\n[default]\nrole = \"domain\"\n[projects]\ncapabilities = [\"Axial.FileSystem.fsproj\"]\n")
            File.WriteAllText(Path.Combine(root, "Axial.FileSystem.fsproj"), "<Project />")
            let live = Path.Combine(root, "Live.fs")
            try
                let config =
                    GuardrailConfig.parse
                        [ "guardrail\tAxial"; "guardrail\tAxial.FileSystem"; $"setting\tpolicyRoot\t{root}"; $"setting\tpolicyFile\t{policy}"
                          $"setting\tprojectPath\t{root}/Axial.FileSystem.fsproj"; $"capabilityFile\tfilesystem\t{live}" ]
                let source = "module Live\nlet read path = System.IO.File.ReadAllText path\nlet stamp () = System.DateTimeOffset.UtcNow\n"
                let! claimed = analyzeAt config live source
                test <@ claimed |> List.map _.Range.StartLine = [ 3 ] @>
                test <@ claimed.Head.Message.Contains "This file claims the filesystem capability" @>
                // The same package's other files get nothing from its role.
                let! other = analyzeAt config (Path.Combine(root, "Paths.fs")) source
                test <@ other |> List.map _.Range.StartLine = [ 2; 3 ] @>
            finally
                TestDirectory.delete root
        }

    [<Fact>]
    let ``every allowed call is counted with its reason`` () =
        let claimed = "/tmp/Payments.fs"
        let config = payments "" [ claimed ]
        let source =
            "module Payments\n"
            + "let a () = M.Refunds()\n"
            + "let b () = M.Refunds()\n"
            + "// axial-allow-AXG001: the gateway signs requests with the wall clock (gap: payments-clock)\n"
            + "let c () = System.DateTimeOffset.UtcNow\n"
            + "let d () = System.DateTimeOffset.UtcNow\n"
        let record = FastSyntax.effectUses config claimed (SourceText.ofString source) (parse source)
        let attribute name = record.Attribute(Xml.Linq.XName.Get name).Value
        test <@ (attribute "owned", attribute "allowance", attribute "host", attribute "reported") = ("2", "1", "0", "1") @>
        let named element = record.Elements(Xml.Linq.XName.Get element) |> Seq.map (fun entry -> entry.Attribute(Xml.Linq.XName.Get "name").Value) |> List.ofSeq
        test <@ named "capability" = [ "payments" ] && named "gap" = [ "payments-clock" ] @>

    [<Fact>]
    let ``an AXG001 allowance works in any file, but only when it names its gap`` () =
        let config = configWith []
        let findings source =
            FastSyntax.analyze config "/tmp/Ipc.fs" (SourceText.ofString source) (parse source)
            |> List.map snd |> List.filter (fun message -> message.Code = "AXG001")
        let call = "let now () = System.DateTimeOffset.UtcNow"
        test <@ findings ("module Ipc\n" + call + " // axial-allow-AXG001: signed with the wall clock (gap: signing-clock)\n") = [] @>
        test <@ (findings ("module Ipc\n" + call + " // axial-allow-AXG001: signed with the wall clock\n")).Length = 1 @>

    [<Fact>]
    let ``Fast reports a use outside the claimed files and allows the claimed file's own calls`` () =
        let config = payments "" [ "/tmp/Payments.fs" ]
        let findings fileName source =
            FastSyntax.analyze config fileName (SourceText.ofString source) (parse source)
            |> List.map snd |> List.filter (fun message -> message.Code = "AXG001")
        let caller = "module N\nlet back () = M.Refunds()\n"
        test <@ findings "/tmp/Checkout.fs" caller |> List.map (fun message -> message.Message.Contains "IPayments") = [ true ] @>
        test <@ findings "/tmp/Payments.fs" caller = [] @>

    [<Fact>]
    let ``advice names the environment field, says what platform services need, and offers a capability only for effects`` () =
        let config =
            GuardrailConfig.parse
                [ "guardrail\tAxial"; "guardrail\tAxial.FileSystem"
                  "rule\tdns.lookup\tAxial\tany:System.Net.Dns\tanywhere\tresolves names through System.Net.Dns directly\tan explicit resolver" ]
        let message (rule: EffectRule) = (EffectUse.toMessage config "/tmp/Domain.fs" rule Range.range0).Message
        let builtIn id = message (builtInRules |> List.find (fun rule -> rule.Id = id))
        let file = builtIn "filesystem.file"
        test <@ file.Contains "`FileSystem: IFileSystem`" && file.Contains "IHasFileSystem" && file.Contains "TestFileSystem()" @>
        let clock = builtIn "clock.datetimeoffset"
        test <@ clock.Contains "Clock.now" && clock.Contains "needs no environment change" @>
        let uncovered = message (config.AllRules |> List.find (fun rule -> rule.Id = "dns.lookup"))
        test <@ uncovered.Contains "define a capability" && uncovered.Contains "\"any:System.Net.Dns\"" && uncovered.Contains "AxialCapability=" @>
        let blocking = builtIn "blocking.task-wait"
        test <@ not (blocking.Contains "capability") @>

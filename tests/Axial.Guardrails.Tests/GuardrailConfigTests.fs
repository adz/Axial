namespace Axial.Guardrails.Tests

open FSharp.Analyzers.SDK
open FSharp.Analyzers.SDK.Testing
open Axial.Guardrails
open Axial.Guardrails.EffectCatalog
open Swensen.Unquote
open Xunit

module GuardrailConfigTests =
    let private findings (source: string) =
        task {
            let! options = mkOptionsFromProject "net10.0" []

            let prelude =
                "module M\n"
                + "type FlowBuilder() =\n"
                + "    member _.Return x = x\n"
                + "    member _.Zero() = ()\n"
                + "    member _.Delay f = f ()\n"
                + "let flow = FlowBuilder()\n"

            let ctx = getContext options (prelude + source)
            let! messages = EffectBoundaryAnalyzer.effectBoundaryAnalyzer ctx |> Async.StartAsTask
            return messages |> List.map (fun message -> message.Range.StartLine - 6) |> List.sort
        }

    [<Fact>]
    let ``parse reads enabled guardrails, severities, and contributed rules`` () =
        let config =
            GuardrailConfig.parse
                [ "guardrail\tAxial\terror"
                  "guardrail\tAxial.HttpClient\twarning"
                  "rule\tacme.clock\tAcme\tacme-clock\tmember:Acme.Clock::Now,Today\tinsideFlow\treads the Acme clock\tAcme.IClock" ]

        test <@ config.Severities = Map.ofList [ "Axial", Severity.Error; "Axial.HttpClient", Severity.Warning ] @>
        test <@ config.Rules |> List.forall (fun rule -> rule.Guardrail = "Axial" || rule.Guardrail = "Axial.HttpClient") @>
        test <@ config.Rules |> List.exists (fun rule -> rule.Id = "http.new-client") @>
        test <@ not (config.Rules |> List.exists (fun rule -> rule.Guardrail = "Axial.Console")) @>
        test <@ config.KnownCategories.Contains "acme-clock" && config.KnownCategories.Contains "console" @>

        let contributed = config.AllRules |> List.find (fun rule -> rule.Id = "acme.clock")
        test <@ contributed.Match = MembersOf("Acme.Clock", [ "Now"; "Today" ]) && contributed.Scope = InsideFlow @>

    [<Fact>]
    let ``parseMatch reads constructor, member, and wildcard forms`` () =
        test <@ parseMatch "ctor:System.Net.Http.HttpClient" = Some(ConstructorOf "System.Net.Http.HttpClient") @>
        test <@ parseMatch "any:System.Console" = Some(AnyMemberOf "System.Console") @>
        test <@ parseMatch "member:System.Guid::NewGuid" = Some(MembersOf("System.Guid", [ "NewGuid" ])) @>
        test <@ parseMatch "nonsense" = None @>

    [<Fact>]
    let ``blocking task waits are flagged only inside flow`` () =
        task {
            let! found =
                findings (
                    "let outside (t: System.Threading.Tasks.Task<int>) = t.GetAwaiter().GetResult()\n"
                    + "let inside (t: System.Threading.Tasks.Task<int>) =\n"
                    + "    flow {\n"
                    + "        return t.GetAwaiter().GetResult() + t.Result\n"
                    + "    }\n")

            test <@ found = [ 4; 4 ] @>
        }

    [<Fact>]
    let ``constructing HttpClient is flagged`` () =
        task {
            let! found = findings "let client () = new System.Net.Http.HttpClient()\n"
            test <@ found = [ 1 ] @>
        }

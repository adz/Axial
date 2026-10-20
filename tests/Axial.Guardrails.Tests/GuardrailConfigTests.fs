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
            let prelude =
                "module M\n"
                + "type FlowBuilder() =\n"
                + "    member _.Return x = x\n"
                + "    member _.Zero() = ()\n"
                + "    member _.Delay f = f ()\n"
                + "let flow = FlowBuilder()\n"

            let! ctx = AnalyzerTestContext.create (prelude + source)
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
    let ``blocking task waits are flagged inside and outside flow once`` () =
        task {
            let! found =
                findings (
                    "let outside (t: System.Threading.Tasks.Task<int>) = t.GetAwaiter().GetResult()\n"
                    + "let inside (t: System.Threading.Tasks.Task<int>) =\n"
                    + "    flow {\n"
                    + "        return t.GetAwaiter().GetResult() + t.Result\n"
                    + "    }\n")

            test <@ found = [ 1; 4; 4 ] @>
        }

    [<Fact>]
    let ``completed task guards exempt only the guarded value and branch`` () =
        task {
            let! found = findings (
                "let guarded (t: System.Threading.Tasks.Task<int>) = if t.IsCompletedSuccessfully then t.Result else 0\n"
                + "let unguarded (t: System.Threading.Tasks.Task<int>) = t.Result\n"
                + "let different (t: System.Threading.Tasks.Task<int>) (other: System.Threading.Tasks.Task<int>) = if other.IsCompleted then t.Result else 0\n"
                + "let wrongBranch (t: System.Threading.Tasks.Task<int>) = if t.IsCompleted then 0 else t.Result\n"
                + "let valueTask (t: System.Threading.Tasks.ValueTask<int>) = if t.IsCompleted then t.Result else 0\n"
                + "let awaiter (t: System.Threading.Tasks.Task<int>) = if t.IsCompletedSuccessfully then t.GetAwaiter().GetResult() else 0\n"
                + "let matchGuard (t: System.Threading.Tasks.Task<int>) = match 1 with | _ when t.IsCompletedSuccessfully -> t.Result | _ -> 0\n"
                + "let differentGuard (t: System.Threading.Tasks.Task<int>) (other: System.Threading.Tasks.Task<int>) = match 1 with | _ when other.IsCompletedSuccessfully -> t.Result | _ -> 0\n")
            test <@ found = [ 2; 3; 4; 8 ] @>
        }

    [<Fact>]
    let ``continuation parameter result is complete`` () =
        task {
            let! found = findings (
                "let continued (t: System.Threading.Tasks.Task<int>) =\n"
                + "    t.ContinueWith(fun (doneTask: System.Threading.Tasks.Task<int>) -> doneTask.Result)\n")
            test <@ found = [] @>
        }

    [<Fact>]
    let ``outside waits are typed and suppressible`` () =
        task {
            let! found =
                findings (
                    "let a (t: System.Threading.Tasks.Task<int>) = t.Result\n"
                    + "let b (t: System.Threading.Tasks.Task) = t.Wait(10)\n"
                    + "let c (v: System.Threading.Tasks.ValueTask<int>) = v.Result\n"
                    + "let d (a: Async<int>) = Async.RunSynchronously a\n"
                    + "let e (t: System.Threading.Tasks.Task) = t.GetAwaiter().GetResult()\n"
                    + "let f (t: System.Threading.Tasks.Task<int>) = t.Result // axial-allow-effect: blocking\n"
                    + "let g (x: int) = x\n")
            test <@ found = [ 1; 2; 3; 4; 5 ] @>
        }

    [<Fact>]
    let ``unrelated Result fields and synchronization waits are not blocking task waits`` () =
        task {
            let! found =
                findings (
                    "type Record = { Result: int }\n"
                    + "let a (r: Result<int, string>) = r.IsOk\n"
                    + "let b (r: Record) = r.Result\n"
                    + "let c (s: System.Threading.SemaphoreSlim) = s.Wait()\n"
                    + "let d (e: System.Threading.ManualResetEventSlim) = e.Wait()\n")
            test <@ found = [] @>
        }

    [<Fact>]
    let ``cryptographic random static and instance generation is flagged`` () =
        task {
            let! found =
                findings (
                    "let create () = System.Security.Cryptography.RandomNumberGenerator.Create()\n"
                    + "let bytes (rng: System.Security.Cryptography.RandomNumberGenerator) (buffer: byte array) = rng.GetBytes buffer\n"
                    + "let nonzero (rng: System.Security.Cryptography.RandomNumberGenerator) (buffer: byte array) = rng.GetNonZeroBytes buffer\n"
                    + "let integer () = System.Security.Cryptography.RandomNumberGenerator.GetInt32(10)\n")
            test <@ found = [ 1; 2; 3; 4 ] @>
        }

    [<Fact>]
    let ``cryptographic random generation permits line-marked boundaries`` () =
        task {
            let! found =
                findings (
                    "let create () = System.Security.Cryptography.RandomNumberGenerator.Create() // axial-allow-effect: secure-random\n"
                    + "let bytes (rng: System.Security.Cryptography.RandomNumberGenerator) (buffer: byte array) =\n"
                    + "    rng.GetBytes buffer // axial-allow-effect: secure-random\n")
            test <@ found = [] @>
            let rule = builtInRules |> List.find (fun rule -> rule.Id = "random.cryptographic")
            test <@ rule.Guardrail = "Axial.PlatformService" @>
            test <@ rule.Category = "secure-random" @>
            test <@ rule.Replacement.Contains("ISecureRandom") && rule.Replacement.Contains("SecureRandom.live") @>
        }

    [<Fact>]
    let ``local time zone is flagged but explicit conversion is not`` () =
        task {
            let! found =
                findings (
                    "let local () = System.TimeZoneInfo.Local\n"
                    + "let explicitZone (utc: System.DateTime) (zone: System.TimeZoneInfo) =\n"
                    + "    System.TimeZoneInfo.ConvertTimeFromUtc(utc, zone)\n")
            test <@ found = [ 1 ] @>
            let rule = builtInRules |> List.find (fun rule -> rule.Id = "clock.local-timezone")
            test <@ rule.Guardrail = "Axial.PlatformService" && rule.Category = "environment" @>
        }

    [<Fact>]
    let ``constructing FileSystemWatcher is flagged`` () =
        task {
            let! found = findings "let watcher () = new System.IO.FileSystemWatcher()\n"
            test <@ found = [ 1 ] @>
        }

    [<Fact>]
    let ``constructing HttpClient is flagged`` () =
        task {
            let! found = findings "let client () = new System.Net.Http.HttpClient()\n"
            test <@ found = [ 1 ] @>
        }

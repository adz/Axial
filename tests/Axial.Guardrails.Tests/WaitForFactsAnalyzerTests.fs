namespace Axial.Guardrails.Tests

open Axial.Guardrails
open FSharp.Analyzers.SDK
open Swensen.Unquote
open Xunit

module WaitForFactsAnalyzerTests =
    // Explicit roles avoid inferring policy from a filename or an xUnit attribute.
    let private findings role body : System.Threading.Tasks.Task<Message list> =
        task {
            let! ctx = AnalyzerTestContext.createWithAxial true (
                "module M\nopen System\nopen System.Threading\nopen System.Threading.Tasks\nopen Axial\n" + body)
            let root = System.IO.Path.GetDirectoryName ctx.FileName
            System.IO.Directory.CreateDirectory root |> ignore
            let policyFile = System.IO.Path.Combine(root, "Axial.Guardrails.toml")
            let roleName = string role |> fun value -> value.ToLowerInvariant()
            System.IO.File.WriteAllText(policyFile, $"version = 2\n[default]\nrole = \"{roleName}\"\n[boundaries]\nfiles = [\"{System.IO.Path.GetFileName ctx.FileName}\"]\n")
            try
                // Axial.Tool also compiles Policy.fs; parsing avoids mixing assembly-local types.
                let project = System.IO.Path.ChangeExtension(ctx.FileName, ".fsproj")
                let config = GuardrailConfig.parse [
                    "guardrail\tAxial"
                    $"setting\tpolicyRoot\t{root}"
                    $"setting\tpolicyFile\t{policyFile}"
                    $"setting\tprojectPath\t{project}" ]
                return WaitForFactsAnalyzer.analyzeWithConfig config ctx
            finally
                System.IO.Directory.Delete(root, true)
        }

    [<Theory>]
    [<InlineData("let check () = Thread.Sleep 10\n               ignore 42")>]
    [<InlineData("let check () = task { do! Task.Delay 10\n                            return 42 }")>]
    [<InlineData("let check () = task { let! result = Task.Delay 10\n                            return result }")>]
    [<InlineData("let check () = async { do! Async.Sleep 10\n                             return 42 }")>]
    [<InlineData("let check () = async { let! result = Async.Sleep 10\n                             return result }")>]
    [<InlineData("let check () = System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds 10.)\n               ignore 42")>]
    let ``guessed waits are denied only in the test role`` body =
        task {
            let! reported = findings Policy.Test body
            test <@ reported |> List.map _.Code = [ "AXG013" ] @>
            test <@ reported |> List.forall (fun message -> message.Severity = Severity.Error) @>
            for role in [ Policy.Domain; Policy.Capabilities; Policy.Services; Policy.Host ] do
                let! allowed = findings role body
                test <@ allowed = [] @>
        }

    [<Fact>]
    let ``diagnostic explains the fact gate deadline and controlled clock alternatives`` () =
        task {
            let! found = findings Policy.Test "let check () = task { do! Task.Delay 10\n                            return 42 }"
            test <@ found.Length = 1 @>
            let message = found.Head.Message
            test <@ message.Contains("Deferred") @>
            test <@ message.Contains("TaskCompletionSource") @>
            test <@ message.ToLowerInvariant().Contains("deadline") @>
            test <@ message.ToLowerInvariant().Contains("clock") @>
        }

    [<Theory>]
    [<InlineData("let check (work: Task) = task { let! winner = Task.WhenAny(work, Task.Delay 1000)\n                                       return winner }")>]
    [<InlineData("let check (work: Task) = task { let deadline = Task.Delay 1000\n                                       let! winner = Task.WhenAny(work, deadline)\n                                       return winner }")>]
    [<InlineData("let check (work: Task) = task { let! winner = Task.WhenAny [| work; Task.Delay 1000 |]\n                                       return winner }")>]
    [<InlineData("let check (work: Task) = work.Wait(1000)")>]
    [<InlineData("let check (work: Task) = task { do! work.WaitAsync(TimeSpan.FromSeconds 1.) }")>]
    [<InlineData("let check (ready: unit -> bool) = SpinWait.SpinUntil(Func<bool>(ready), 1000)")>]
    [<InlineData("let rec waitUntil ready (deadline: DateTimeOffset) =\n    if ready () then true\n    elif DateTimeOffset.UtcNow >= deadline then false\n    else Thread.Yield() |> ignore; waitUntil ready deadline")>]
    [<InlineData("let check () = flow { do! Flow.sleep (TimeSpan.FromSeconds 1.)\n                            return 42 } |> Flow.timeout (TimeSpan.FromSeconds 2.)")>]
    [<InlineData("let check () = Flow.race (Flow.ok ()) (Flow.sleep (TimeSpan.FromSeconds 1.))")>]
    let ``failure deadlines and fact polling are allowed`` body =
        task {
            let! found = findings Policy.Test body
            test <@ found = [] @>
        }

    // A flow does not syntactically reveal whether its environment supplies a live or manual clock.
    // AXG013 therefore deliberately leaves Flow.sleep and Clock.sleep out of scope.
    [<Theory>]
    [<InlineData("let check () = flow { do! Flow.sleep (TimeSpan.FromSeconds 1.)\n                            return 42 }")>]
    let ``clock dependent sleeps stay out of scope rather than rejecting controlled time`` body =
        task {
            let! found = findings Policy.Test body
            test <@ found = [] @>
        }

    [<Theory>]
    [<InlineData("Thread.Sleep 10")>]
    [<InlineData("System.Threading.Thread.Sleep 10")>]
    let ``reasoned site suppression permits an intentional wait`` expression =
        task {
            let! found = findings Policy.Test (
                "let check () = " + expression + " // axial-allow-AXG013: exercises the foreign blocking boundary\n               ignore 42")
            test <@ found = [] @>
        }

    [<Fact>]
    let ``suppression applies only to its site`` () =
        task {
            let! found = findings Policy.Test (
                "let check () =\n    Thread.Sleep 10 // axial-allow-AXG013: exercises the foreign blocking boundary\n\n    Thread.Sleep 20\n    ignore 42")
            test <@ found |> List.map _.Code = [ "AXG013" ] @>
            test <@ found.Head.Range.StartLine = 9 @>
        }

    [<Fact>]
    let ``unrelated sleep lookalikes are not reported`` () =
        task {
            let! found = findings Policy.Test "module Local =\n    let Sleep (_: int) = ()\nlet check () = Local.Sleep 10\n               ignore 42"
            test <@ found = [] @>
        }

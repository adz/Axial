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
            System.IO.File.WriteAllText(ctx.FileName, ctx.SourceText.ToString())
            let errors = ctx.CheckFileResults.Diagnostics |> Array.filter (fun diagnostic -> diagnostic.Severity = FSharp.Compiler.Diagnostics.FSharpDiagnosticSeverity.Error)
            Assert.True(Array.isEmpty errors, sprintf "%A" errors)
            let policyFile = System.IO.Path.Combine(root, "Axial.Guardrails.toml")
            let roleName =
                match role with
                | Policy.Test -> "test"
                | Policy.Domain -> "domain"
                | Policy.Capabilities -> "capabilities"
                | Policy.Services -> "services"
                | Policy.Host -> "host"
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
    [<InlineData("let check () =\n    Thread.Sleep 10\n    ignore 42")>]
    [<InlineData("let check () = task {\n    do! Task.Delay 10\n    return 42\n}")>]
    [<InlineData("let check () = task {\n    let! result = Task.Delay 10\n    return result\n}")>]
    [<InlineData("let check () = async {\n    do! Async.Sleep 10\n    return 42\n}")>]
    [<InlineData("let check () = async {\n    let! result = Async.Sleep 10\n    return result\n}")>]
    [<InlineData("let check () =\n    System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds 10.)\n    ignore 42")>]
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
            let! found = findings Policy.Test "let check () = task {\n    do! Task.Delay 10\n    return 42\n}"
            test <@ found.Length = 1 @>
            let message = found.Head.Message
            test <@ message.Contains("Deferred") @>
            test <@ message.Contains("TaskCompletionSource") @>
            test <@ message.ToLowerInvariant().Contains("deadline") @>
            test <@ message.ToLowerInvariant().Contains("clock") @>
        }

    [<Theory>]
    [<InlineData("let check (work: Task) = task {\n    let! winner = Task.WhenAny(work, Task.Delay 1000)\n    return winner\n}")>]
    [<InlineData("let check (work: Task) = task {\n    let deadline = Task.Delay 1000\n    let! winner = Task.WhenAny(work, deadline)\n    return winner\n}")>]
    [<InlineData("let check (work: Task) = task {\n    let! winner = Task.WhenAny [| work; Task.Delay 1000 |]\n    return winner\n}")>]
    [<InlineData("let check (work: Task) = work.Wait(1000)")>]
    [<InlineData("let check (work: Task) = task { do! work.WaitAsync(TimeSpan.FromSeconds 1.) }")>]
    [<InlineData("let check (ready: unit -> bool) = SpinWait.SpinUntil(Func<bool>(ready), 1000)")>]
    [<InlineData("let rec waitUntil ready (deadline: DateTimeOffset) =\n    if ready () then true\n    elif DateTimeOffset.UtcNow >= deadline then false\n    else Thread.Yield() |> ignore; waitUntil ready deadline")>]
    [<InlineData("let check () =\n    flow {\n        do! Flow.sleep (TimeSpan.FromSeconds 1.)\n        return 42\n    } |> Flow.timeout (TimeSpan.FromSeconds 2.)")>]
    [<InlineData("let check () = Flow.race (Flow.ok ()) (Flow.sleep (TimeSpan.FromSeconds 1.))")>]
    let ``failure deadlines and fact polling are allowed`` body =
        task {
            let! found = findings Policy.Test body
            test <@ found = [] @>
        }

    [<Theory>]
    [<InlineData("Thread.Sleep Timeout.Infinite")>]
    [<InlineData("Thread.Sleep Timeout.InfiniteTimeSpan")>]
    [<InlineData("Thread.Sleep -1")>]
    [<InlineData("do! Task.Delay(Timeout.Infinite, token)")>]
    [<InlineData("do! Task.Delay(Timeout.InfiniteTimeSpan, token)")>]
    [<InlineData("do! Task.Delay(-1, token)")>]
    [<InlineData("let! result = Task.Delay(Timeout.Infinite, token)\n    return result")>]
    [<InlineData("do! Async.Sleep Timeout.Infinite")>]
    [<InlineData("do! Async.Sleep Timeout.InfiniteTimeSpan")>]
    [<InlineData("do! Async.Sleep -1")>]
    let ``cancellation waits are allowed`` (statement: string) =
        task {
            let builder = if statement.Contains("Async.Sleep") then "async" else "task"
            let! found = findings Policy.Test ("let check (token: CancellationToken) = " + builder + " {\n    " + statement + "\n}")
            test <@ found = [] @>
        }

    [<Theory>]
    [<InlineData("let check ready =\n    while not (ready ()) do\n        Thread.Sleep 10")>]
    [<InlineData("let check ready = task {\n    while not (ready ()) do\n        do! Task.Delay 10\n}")>]
    [<InlineData("let check ready = async {\n    while not (ready ()) do\n        let! result = Async.Sleep 10\n        ignore result\n}")>]
    [<InlineData("let check ready =\n    for i in 1..2 do\n        while not (ready ()) do\n            Thread.Sleep 10")>]
    [<InlineData("let check ready =\n    while not (ready ()) do\n        if ready () then () else Thread.Sleep 10")>]
    let ``nearest while loop permits condition polling`` body =
        task {
            let! found = findings Policy.Test body
            test <@ found = [] @>
        }

    [<Theory>]
    [<InlineData("let check () =\n    for i in 1..2 do\n        Thread.Sleep 10")>]
    [<InlineData("let check ready = task {\n    while not (ready ()) do\n        for i in 1..2 do\n            do! Task.Delay 10\n}")>]
    [<InlineData("let check ready =\n    while not (ready ()) do\n        for item in [1; 2] do\n            Thread.Sleep 10")>]
    [<InlineData("let check ready =\n    while not (ready ()) do\n        ignore 42\n    Thread.Sleep 10")>]
    [<InlineData("let check () = task {\n    do! Task.Delay 10000\n}")>]
    [<InlineData("let check () =\n    let milliseconds = 10\n    Thread.Sleep milliseconds")>]
    [<InlineData("module Local =\n    let Infinite = 10\nlet check () = Thread.Sleep Local.Infinite")>]
    let ``finite simulations and non polling waits remain reported`` body =
        task {
            let! found = findings Policy.Test body
            test <@ found |> List.map _.Code = [ "AXG013" ] @>
        }

    // A flow does not syntactically reveal whether its environment supplies a live or manual clock.
    // AXG013 therefore deliberately leaves Flow.sleep and Clock.sleep out of scope.
    [<Theory>]
    [<InlineData("let check () = flow {\n    do! Flow.sleep (TimeSpan.FromSeconds 1.)\n    return 42\n}")>]
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
                "let check () =\n    " + expression + " // axial-allow-AXG013: exercises the foreign blocking boundary\n    ignore 42")
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
            let! found = findings Policy.Test "module Local =\n    let Sleep (_: int) = ()\nlet check () =\n    Local.Sleep 10\n    ignore 42"
            test <@ found = [] @>
        }

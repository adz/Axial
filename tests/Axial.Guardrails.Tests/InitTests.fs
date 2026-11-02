namespace Axial.Guardrails.Tests

open System
open System.Diagnostics
open System.IO
open Xunit

[<CollectionDefinition("Init", DisableParallelization = true)>]
type InitCollection() = class end

[<Collection("Init")>]
module InitTests =
    let private tool () = typeof<Axial.Tool.PolicyLock.Snapshot>.Assembly.Location

    let private runWithEnvironment (environment: (string * string) list) root args =
        let info = ProcessStartInfo("dotnet")
        info.WorkingDirectory <- root
        info.RedirectStandardOutput <- true
        info.RedirectStandardError <- true
        info.UseShellExecute <- false
        info.Environment["DOTNET_ROLL_FORWARD"] <- "Major"
        for key, value in environment do info.Environment[key] <- value
        info.ArgumentList.Add(tool ())
        for arg in args do info.ArgumentList.Add(arg)
        use child = Process.Start info
        let output = child.StandardOutput.ReadToEndAsync()
        let errors = child.StandardError.ReadToEndAsync()
        child.WaitForExit()
        child.ExitCode, output.Result, errors.Result

    let private run root args = runWithEnvironment [] root args

    let private tempRepo (configure: string -> unit) =
        let root = Path.Combine(Path.GetTempPath(), "Axial.Init.Tests", Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        configure root
        root

    let private write (root: string) (name: string) (text: string) = File.WriteAllText(Path.Combine(root, name), text)
    let private read (root: string) (name: string) = File.ReadAllText(Path.Combine(root, name))

    let private defaultToml = "version = 2\nbaseline = \"Axial.Guardrails.baseline\"\n[default]\nrole = \"domain\"\n"

    [<Fact>]
    let ``existing nondefault toml bootstraps configured baseline and lock`` () =
        let root = tempRepo (fun root ->
            write root "Axial.Guardrails.toml" "version = 2\nbaseline = \"custom.baseline\"\n[default]\nrole = \"domain\"\n")
        try
            let toml = read root "Axial.Guardrails.toml"
            let code, output, errors = run root ["init"]
            Assert.Equal(0, code)
            Assert.Equal(toml, read root "Axial.Guardrails.toml")
            Assert.True(File.Exists(Path.Combine(root, "custom.baseline")))
            Assert.False(File.Exists(Path.Combine(root, "Axial.Guardrails.baseline")))
            Assert.True(File.Exists(Path.Combine(root, "Axial.Guardrails.lock")))
            Assert.Contains("created", output)
            Assert.Equal("", errors)
            let code, _, _ = run root ["check"]
            Assert.Equal(0, code)
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``existing baseline entries are preserved and included in the new lock`` () =
        let root = tempRepo (fun root ->
            write root "Axial.Guardrails.toml" defaultToml
            write root "Axial.Guardrails.baseline" "effects.ambient\tsrc/App.fs\tabc\n")
        try
            let baseline = read root "Axial.Guardrails.baseline"
            let code, _, _ = run root ["init"]
            Assert.Equal(0, code)
            Assert.Equal(baseline, read root "Axial.Guardrails.baseline")
            let lock = Axial.Tool.PolicyLock.parseLock (Path.Combine(root, "Axial.Guardrails.lock")) |> Result.defaultWith failwith
            Assert.Contains(lock.Baseline, fun entry -> entry.Rule = "effects.ambient" && entry.File = "src/App.fs" && entry.Fingerprint = "abc")
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``repeated init is a byte-for-byte no-op`` () =
        let root = tempRepo ignore
        try
            Assert.Equal(0, (run root ["init"] |> fun (code, _, _) -> code))
            let names = ["Axial.Guardrails.toml"; "Axial.Guardrails.baseline"; "Axial.Guardrails.lock"]
            let before = names |> List.map (fun name -> name, read root name, File.GetLastWriteTimeUtc(Path.Combine(root, name)))
            let code, output, _ = run root ["init"]
            Assert.Equal(0, code)
            Assert.Contains("reused", output)
            for name, text, stamp in before do
                Assert.Equal(text, read root name)
                Assert.Equal(stamp, File.GetLastWriteTimeUtc(Path.Combine(root, name)))
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``nested configured baseline parents are created`` () =
        let root = tempRepo (fun root ->
            write root "Axial.Guardrails.toml" "version = 2\nbaseline = \"policy/new.baseline\"\n[default]\nrole = \"domain\"\n")
        try
            let code, output, errors = run root ["init"; "--json"]
            Assert.Equal(0, code)
            Assert.Contains("\"status\":\"ok\"", output)
            Assert.True(File.Exists(Path.Combine(root, "policy", "new.baseline")), errors)
            Assert.True(File.Exists(Path.Combine(root, "Axial.Guardrails.lock")))
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``invalid existing toml fails without creating artifacts`` () =
        let root = tempRepo (fun root -> write root "Axial.Guardrails.toml" "version = nope\n")
        try
            let toml = read root "Axial.Guardrails.toml"
            let code, _, errors = run root ["init"]
            Assert.NotEqual(0, code)
            Assert.False(String.IsNullOrWhiteSpace errors)
            Assert.Equal(toml, read root "Axial.Guardrails.toml")
            Assert.False(File.Exists(Path.Combine(root, "Axial.Guardrails.baseline")))
            Assert.False(File.Exists(Path.Combine(root, "Axial.Guardrails.lock")))
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``existing lock is never refreshed while a missing baseline is created`` () =
        let root = tempRepo (fun root ->
            write root "Axial.Guardrails.toml" defaultToml
            write root "Axial.Guardrails.lock" "version = 2\n")
        try
            let lock = read root "Axial.Guardrails.lock"
            let code, _, _ = run root ["init"]
            Assert.Equal(0, code)
            Assert.Equal(lock, read root "Axial.Guardrails.lock")
            Assert.True(File.Exists(Path.Combine(root, "Axial.Guardrails.baseline")))
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``missing lock check and approve point at the bootstrap command`` () =
        let root = tempRepo (fun root ->
            write root "Axial.Guardrails.toml" defaultToml
            write root "Axial.Guardrails.baseline" "")
        try
            let code, _, errors = run root ["check"]
            Assert.NotEqual(0, code)
            Assert.Contains("axial init", errors)
            Assert.Contains("without overwriting the TOML", errors)
            let code, _, errors = run root ["policy"; "approve"; "--yes-i-reviewed"]
            Assert.NotEqual(0, code)
            Assert.Contains("axial init", errors)
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``create-new never replaces a concurrently created artifact`` () =
        let root = tempRepo ignore
        try
            let path = Path.Combine(root, "Axial.Guardrails.lock")
            match Axial.Tool.PolicyLock.writeNew path "first" with
            | Ok () -> ()
            | Error error -> failwith error
            match Axial.Tool.PolicyLock.writeNew path "second" with
            | Ok () -> failwith "writeNew replaced an existing artifact"
            | Error _ -> ()
            Assert.Equal("first", File.ReadAllText path)
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``json init reports the created and reused artifacts`` () =
        let root = tempRepo ignore
        try
            let code, output, _ = run root ["init"; "--json"]
            Assert.Equal(0, code)
            Assert.Contains("\"status\":\"ok\"", output)
            Assert.Contains("created:", output)
            let code, output, _ = run root ["init"; "--json"]
            Assert.Equal(0, code)
            Assert.Contains("reused:", output)
        finally Directory.Delete(root, true)


    [<Fact>]
    let ``external linked props is not hashed into the policy inventory`` () =
        if not (OperatingSystem.IsWindows()) then
            let outside = Path.Combine(Path.GetTempPath(), "Axial.Init.Outside", Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory outside |> ignore
            let target = Path.Combine(outside, "Outside.props")
            File.WriteAllText(target, "<Project />")
            let root = tempRepo (fun root ->
                write root "Axial.Guardrails.toml" defaultToml
                File.CreateSymbolicLink(Path.Combine(root, "Linked.props"), target) |> ignore)
            try
                let code, _, errors = run root ["init"]
                Assert.Equal(0, code)
                let lock = Axial.Tool.PolicyLock.parseLock (Path.Combine(root, "Axial.Guardrails.lock")) |> Result.defaultWith failwith
                Assert.False(lock.Hashes.ContainsKey "Linked.props", errors)
            finally
                Directory.Delete(root, true)
                Directory.Delete(outside, true)

    [<Fact>]
    let ``directory symlink is not traversed for inventory`` () =
        if not (OperatingSystem.IsWindows()) then
            let outside = Path.Combine(Path.GetTempPath(), "Axial.Init.OutsideDir", Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory outside |> ignore
            File.WriteAllText(Path.Combine(outside, "Secret.props"), "<Project />")
            let root = tempRepo (fun root ->
                write root "Axial.Guardrails.toml" defaultToml
                File.CreateSymbolicLink(Path.Combine(root, "LinkedDir"), outside) |> ignore)
            try
                let code, _, errors = run root ["init"]
                Assert.Equal(0, code)
                let lock = Axial.Tool.PolicyLock.parseLock (Path.Combine(root, "Axial.Guardrails.lock")) |> Result.defaultWith failwith
                Assert.DoesNotContain(lock.Hashes.Keys, fun path -> path.Contains "Secret.props")
            finally
                Directory.Delete(root, true)
                Directory.Delete(outside, true)

    [<Fact>]
    let ``implicit default baseline rejects external symlink before parsing inventory`` () =
        if not (OperatingSystem.IsWindows()) then
            let outside = Path.GetTempFileName()
            File.WriteAllText(outside, "malformed-external-baseline-marker")
            let root = tempRepo (fun root ->
                write root "Axial.Guardrails.toml" "version = 2\n[default]\nrole = \"domain\"\n"
                File.CreateSymbolicLink(Path.Combine(root, "Axial.Guardrails.baseline"), outside) |> ignore)
            try
                match Axial.Tool.PolicyLock.inspect root with
                | Error message ->
                    Assert.Contains("outside the repository root", message)
                    Assert.DoesNotContain("malformed", message)
                | Ok _ -> failwith "Inventory accepted an external default baseline"
            finally
                Directory.Delete(root, true)
                File.Delete outside

    [<Theory>]
    [<InlineData(false, false)>]
    [<InlineData(false, true)>]
    [<InlineData(true, false)>]
    [<InlineData(true, true)>]
    let ``init rejects external config before parsing even with an existing lock`` (existingLock: bool) (validConfig: bool) =
        if not (OperatingSystem.IsWindows()) then
            let outside = Path.GetTempFileName()
            File.WriteAllText(outside, if validConfig then defaultToml else "malformed-external-config-marker")
            let root = tempRepo (fun root ->
                File.CreateSymbolicLink(Path.Combine(root, "Axial.Guardrails.toml"), outside) |> ignore
                if existingLock then write root "Axial.Guardrails.lock" "unchanged-lock")
            try
                let code, output, errors = run root ["init"; "--json"]
                Assert.NotEqual(0, code)
                Assert.Contains("outside the repository root", output + errors)
                Assert.DoesNotContain("malformed-external-config-marker", output + errors)
                Assert.False(File.Exists(Path.Combine(root, "Axial.Guardrails.baseline")))
                if existingLock then Assert.Equal("unchanged-lock", read root "Axial.Guardrails.lock")
            finally
                Directory.Delete(root, true)
                File.Delete outside

    [<Fact>]
    let ``tool dll runs on a newer runtime without a roll-forward override`` () =
        let root = tempRepo ignore
        try
            let info = ProcessStartInfo("dotnet")
            info.WorkingDirectory <- root
            info.RedirectStandardOutput <- true
            info.RedirectStandardError <- true
            info.UseShellExecute <- false
            info.Environment.Remove "DOTNET_ROLL_FORWARD" |> ignore
            info.Environment.Remove "DOTNET_ROLL_FORWARD_TO_PRERELEASE" |> ignore
            info.ArgumentList.Add(tool ())
            info.ArgumentList.Add("help")
            use child = Process.Start info
            let output = child.StandardOutput.ReadToEndAsync()
            let errors = child.StandardError.ReadToEndAsync()
            child.WaitForExit()
            Assert.True(child.ExitCode = 0, output.Result + errors.Result)
        finally Directory.Delete(root, true)

    [<Fact>]
    let ``baseline help permits preparation but approval remains a human decision`` () =
        let root = tempRepo ignore
        try
            let code, output, _ = run root ["policy"; "baseline"; "--help"]
            Assert.Equal(0, code)
            Assert.DoesNotContain("agents must not run this", output)
            Assert.Contains("candidate", output)
            let code, output, _ = run root ["policy"; "approve"; "--help"]
            Assert.Equal(0, code)
            Assert.Contains("HUMAN DECISION", output)
            Assert.Contains("--baseline-rule", output)
            let code, _, _ = run root ["policy"; "baseline"; "--rule"; "effects.ambient"; "--yes-i-reviewed"]
            Assert.NotEqual(0, code)
            Assert.False(File.Exists(Path.Combine(root, "Axial.Guardrails.lock")))
        finally Directory.Delete(root, true)

    [<Theory>]
    [<InlineData("AXIAL_GUARDRAILS_SEVERITY")>]
    [<InlineData("AxialGuardrailsSeverity")>]
    let ``standalone cli rejects the legacy severity environment spelling`` (setting: string) =
        let root = tempRepo ignore
        try
            let code, output, errors = runWithEnvironment [ setting, "warning" ] root ["help"; "--json"]
            Assert.NotEqual(0, code)
            Assert.Contains("axial.policy.v1", output)
            Assert.Contains("AXIALGR001", errors)
            Assert.Contains("error AXIALGR001", errors)
            Assert.Contains("policy-roles", errors)
            Assert.Contains("Axial.Guardrails.toml", errors)
            Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(errors, "AXIALGR001").Count)
        finally Directory.Delete(root, true)

namespace Axial.Tests

open System
open System.IO
open System.Threading
open Axial
open Axial.Process
open Xunit

module ProcessLaunchTests =
    [<Fact>]
    let ``launch hands a detached command to the process service without running it`` () =
        let host = ProcessHostEnvironment.test ()
        let editor = Process.commandArgs "code" [ "--goto"; "notes.txt:12" ] |> Process.workingDirectory "/repo"
        let outcome = Process.launch editor |> Flow.run host.Environment
        Assert.Equal(Exit.Success (), outcome)
        Assert.Empty(host.Process.RunSpecs)
        match List.ofSeq host.Process.Launches with
        | [ ProcessLaunch.Command command ] ->
            Assert.Equal("code", Process.fileName command)
            Assert.Equal<string list>([ "--goto"; "notes.txt:12" ], Process.arguments command)
            Assert.Equal(Some "/repo", Process.tryWorkingDirectory command)
        | other -> failwith ("Unexpected launches: " + string other.Length)

    [<Fact>]
    let ``shellOpen asks the process service to open a target with its default handler`` () =
        let host = ProcessHostEnvironment.test ()
        let outcome = Process.shellOpen "https://example.invalid/docs" |> Flow.run host.Environment
        Assert.Equal(Exit.Success (), outcome)
        match List.ofSeq host.Process.Launches with
        | [ ProcessLaunch.ShellOpen target ] -> Assert.Equal("https://example.invalid/docs", target)
        | other -> failwith ("Unexpected launches: " + string other.Length)

    [<Fact>]
    let ``a failed launch is a typed StartFailed error`` () =
        let host = ProcessHostEnvironment.test ()
        host.Process.LaunchHandler <-
            Some(fun _ -> Flow.fail (ProcessError.StartFailed { Command = "xdg-open"; Message = "not installed" }))
        let outcome = Process.shellOpen "/tmp" |> Flow.run host.Environment
        match outcome with
        | Exit.Failure(Cause.Fail(ProcessError.StartFailed failure)) -> Assert.Equal("not installed", failure.Message)
        | _ -> failwith "Expected StartFailed"
        Assert.Equal(1, host.Process.Launches.Count)

    let private rejected (outcome: Exit<unit, ProcessError>) =
        match outcome with
        | Exit.Failure(Cause.Fail(ProcessError.ShellOpenRejected rejection)) -> rejection
        | other -> failwithf "Expected ShellOpenRejected, got %A" other

    [<Fact>]
    let ``shellOpen fails an empty target inside the flow without launching`` () =
        let host = ProcessHostEnvironment.test ()
        // Building the flow is pure; an invalid target is a typed failure when it runs.
        let opening = Process.shellOpen "   "
        let rejection = opening |> Flow.run host.Environment |> rejected
        Assert.Equal("   ", rejection.Target)
        Assert.Empty(host.Process.Launches)

    [<Fact>]
    let ``shellOpen rejects a target that an opener would read as an option`` () =
        let host = ProcessHostEnvironment.test ()
        let rejection = Process.shellOpen "--help" |> Flow.run host.Environment |> rejected
        Assert.Equal("--help", rejection.Target)
        Assert.Contains("'-'", rejection.Reason)
        Assert.Contains("--help", ProcessError.describe (ProcessError.ShellOpenRejected rejection))
        Assert.Empty(host.Process.Launches)
        // A relative path that starts with '-' is still reachable as ./-name.
        Assert.Equal(Exit.Success (), Process.shellOpen "./-notes.txt" |> Flow.run host.Environment)

    [<Fact>]
    let ``the live service rejects an option-like target handed to Launch directly`` () =
        let rejection =
            ProcessHostEnvironment.live.Process.Launch(ProcessLaunch.ShellOpen "-a") |> Flow.run () |> rejected
        Assert.Equal("-a", rejection.Target)

    [<Fact>]
    let ``a live launch gives a console program its own window`` () =
        let info = ProcessRun.launchStartInfo (ProcessLaunch.Command(Process.commandArgs "htop" []))
        Assert.False(info.CreateNoWindow)
        Assert.False(info.UseShellExecute)
        Assert.False(info.RedirectStandardOutput)

    [<Fact>]
    let ``live launch of a missing executable fails with StartFailed`` () =
        let missing = "axial-missing-" + Guid.NewGuid().ToString("N")
        let outcome = Process.launch (Process.commandArgs missing []) |> Flow.run ProcessHostEnvironment.live
        match outcome with
        | Exit.Failure(Cause.Fail(ProcessError.StartFailed failure)) -> Assert.Equal(missing, failure.Command)
        | _ -> failwith "Expected StartFailed"

    [<Fact>]
    let ``live launch returns before the child finishes`` () =
        if not (OperatingSystem.IsWindows()) then
            let directory = Path.Combine(Path.GetTempPath(), "axial-launch-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory directory |> ignore
            try
                let release = Path.Combine(directory, "release")
                let marker = Path.Combine(directory, "done")
                // The child waits for a file only this test creates, so a launch that waited for it would hang.
                let script = "while [ ! -e \"$1\" ]; do sleep 0.05; done; echo done > \"$2\""
                let child = Process.commandArgs "/bin/sh" [ "-c"; script; "sh"; release; marker ]
                let outcome = Process.launch child |> Flow.run ProcessHostEnvironment.live
                Assert.Equal(Exit.Success (), outcome)
                Assert.False(File.Exists marker)
                File.WriteAllText(release, "")
                Assert.True(SpinWait.SpinUntil((fun () -> File.Exists marker), 10000), "the detached child did not run")
            finally
                Directory.Delete(directory, true)

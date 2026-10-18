namespace Axial.Files.Tests

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Threading
open Xunit
open Axial
open Axial.FileSystem
open Axial.Files

type CrashTests() =
    let supported = OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
    let probeDll () =
        let configuration = if AppContext.BaseDirectory.Contains("/release/") then "release" else "debug"
        let path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../Axial.Files.CrashProbe", configuration, "Axial.Files.CrashProbe.dll"))
        Assert.True(File.Exists path, $"Probe missing: {path}")
        path
    let withTemp test =
        let root = Path.Combine(Path.GetTempPath(), "axial-crash-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory root |> ignore
        try test (Path.Combine(root, "target"))
        finally Directory.Delete(root, true)
    let runProbe dll mode path check = 
        for iteration in 1 .. 20 do
            let start = ProcessStartInfo("dotnet")
            start.ArgumentList.Add dll
            start.ArgumentList.Add mode
            start.ArgumentList.Add path
            start.UseShellExecute <- false
            start.RedirectStandardOutput <- true
            use child = Process.Start start
            try
                let ready = child.StandardOutput.ReadLineAsync()
                Assert.True(ready.Wait(10000), "Probe never reported ready")
                Assert.Equal("ready", ready.Result)
                Thread.Sleep(Random.Shared.Next(50, 301))
                if not child.HasExited then child.Kill(entireProcessTree = true)
                Assert.True(child.WaitForExit(5000), "Probe failed to exit after kill")
            finally
                if not child.HasExited then
                    child.Kill(entireProcessTree = true)
                    child.WaitForExit()
            check ()
    let replay path =
        let env = { new IHasFileSystem with member _.FileSystem = FileSystem.live }
        let operation = flow {
            let! log = AppendLog.openLog path 256L |> Flow.scopeResource
            return! AppendLog.readAll log |> FlowStream.runCollect
        }
        match Flow.run env operation with
        | Exit.Success records -> records
        | other -> failwithf "Replay failed: %A" other

    [<Fact>]
    member _.``atomic target is always a complete payload after process kills``() =
        if not supported then () // Process-tree kill semantics are tested on Unix hosts only.
        else withTemp (fun path ->
            let dll = probeDll ()
            runProbe dll "atomic" path (fun () ->
                Assert.True(File.Exists path, "Probe did not write an atomic target")
                if File.Exists path then
                    let contents = File.ReadAllBytes path |> Encoding.ASCII.GetString
                    let split = contents.IndexOf '\n'
                    Assert.True(split > 0, "Missing length header")
                    let length = Int32.Parse(contents.Substring(0, split))
                    let body = contents.Substring(split + 1)
                    Assert.Equal(length, body.Length)
                    Assert.InRange(length, 1000, 50999)
                    Assert.True(body |> Seq.forall (fun c -> c = body[0] && c >= 'a' && c <= 'z')))
            Assert.True(File.Exists path, "Probe never wrote an atomic target"))

    [<Fact>]
    member _.``log replays consecutive complete records after process kills``() =
        if not supported then () // Process-tree kill semantics are tested on Unix hosts only.
        else withTemp (fun path ->
            let dll = probeDll ()
            let mutable previous = -1
            runProbe dll "log" path (fun () ->
                let records = replay path |> List.map Encoding.ASCII.GetString
                Assert.NotEmpty(records)
                Assert.True(int (List.last records) > previous, "Probe made no progress")
                previous <- int (List.last records)
                records |> List.pairwise |> List.iter (fun (a, b) -> Assert.Equal(int a + 1, int b)))
            Assert.NotEmpty(replay path))

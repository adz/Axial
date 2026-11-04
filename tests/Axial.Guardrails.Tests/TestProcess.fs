namespace Axial.Guardrails.Tests

open System
open System.Diagnostics
open System.Threading.Tasks

/// Bounded child-process collection for tests. A started process may leave a descendant (a reused MSBuild node, a
/// backgrounded shell job) holding the inherited stdout/stderr pipes after it exits; reading to end-of-file then
/// never finishes and a CI test step hangs for hours. Every wait here is bounded and a timeout is a failure with output.
module TestProcess =
    /// Starts `info` with redirected output and returns (exit code, stdout, stderr). Kills the tree on timeout.
    let run (timeoutMilliseconds: int) (info: ProcessStartInfo) =
        info.UseShellExecute <- false
        info.RedirectStandardOutput <- true
        info.RedirectStandardError <- true
        use child = Process.Start info
        let stdout = child.StandardOutput.ReadToEndAsync()
        let stderr = child.StandardError.ReadToEndAsync()
        let exited = child.WaitForExit(timeoutMilliseconds)
        if not exited then
            try child.Kill(true) with :? InvalidOperationException -> ()
            child.WaitForExit(10_000) |> ignore
        // After exit, give inherited pipes a bounded grace period instead of waiting for end-of-file forever.
        let drained = Task.WaitAll([| stdout :> Task; stderr :> Task |], 10_000)
        let text (reader: Task<string>) = if reader.IsCompletedSuccessfully then reader.Result else "<output still open: a descendant holds the pipe>"
        let out, err = text stdout, text stderr
        if not exited then
            failwith $"{info.FileName} {String.Join(' ', info.ArgumentList)} timed out after {timeoutMilliseconds} ms:\n{out}\n{err}"
        child.ExitCode, out, err

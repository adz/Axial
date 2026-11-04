namespace Axial.Guardrails.Tests

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks

/// Bounded child-process collection for tests. A started process may leave a descendant (a reused MSBuild node, a
/// backgrounded shell job) holding the inherited stdout/stderr pipes after it exits; reading to end-of-file then
/// never finishes and a CI test step hangs for hours. Every wait here is bounded and a timeout is a failure with output.
module TestProcess =
    /// Starts `info` with redirected output and returns (exit code, stdout, stderr). Kills the tree on timeout.
    /// Output is buffered as it arrives, as Axial.Tool's Command.run does, so a failure always carries what was read.
    let run (timeoutMilliseconds: int) (info: ProcessStartInfo) =
        info.UseShellExecute <- false
        info.RedirectStandardOutput <- true
        info.RedirectStandardError <- true
        use child = Process.Start info
        use cancellation = new CancellationTokenSource()
        let pump (reader: StreamReader) =
            let buffer = StringBuilder()
            let work = task {
                try
                    let chars = Array.zeroCreate<char> 4096
                    let mutable reading = true
                    while reading do
                        let! count = reader.ReadAsync(chars.AsMemory(), cancellation.Token)
                        if count = 0 then reading <- false
                        else lock buffer (fun () -> buffer.Append(chars, 0, count) |> ignore)
                with :? OperationCanceledException | :? IOException -> ()
            }
            buffer, work
        let stdout, readingOut = pump child.StandardOutput
        let stderr, readingErr = pump child.StandardError
        let exited = child.WaitForExit(timeoutMilliseconds)
        if not exited then
            try child.Kill(true) with :? InvalidOperationException -> ()
            child.WaitForExit(10_000) |> ignore
        // After exit, give inherited pipes a bounded grace period instead of waiting for end-of-file forever.
        let drained = Task.WaitAll([| readingOut :> Task; readingErr :> Task |], 10_000)
        if not drained then cancellation.Cancel()
        let text (buffer: StringBuilder) = lock buffer (fun () -> buffer.ToString())
        let out, err = text stdout, text stderr
        let command = $"{info.FileName} {String.Join(' ', info.ArgumentList)}"
        if not exited then failwith $"{command} timed out after {timeoutMilliseconds} ms:\n{out}\n{err}"
        if not drained then failwith $"{command} exited but its output stayed open (a descendant holds the pipe):\n{out}\n{err}"
        child.ExitCode, out, err

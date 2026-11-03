namespace Axial.Tool

open System
open System.Diagnostics
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open System.Xml.Linq

/// Shared bounded process execution. Live diagnostics go to stderr so JSON stdout stays parseable.
module Command =
    let run directory executable arguments (timeoutMilliseconds: int) live =
        let info = ProcessStartInfo(executable, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true)
        info.WorkingDirectory <- directory
        for argument: string in arguments do info.ArgumentList.Add argument
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
                        else
                            if live then Console.Error.Write(String(chars, 0, count))
                            else lock buffer (fun () -> buffer.Append(chars, 0, count) |> ignore)
                with :? OperationCanceledException | :? IOException -> ()
            }
            buffer, work
        let output, stdout = pump child.StandardOutput
        let errors, stderr = pump child.StandardError
        if not (child.WaitForExit timeoutMilliseconds) then
            try child.Kill(true) with :? InvalidOperationException -> ()
            child.WaitForExit(5000) |> ignore
            cancellation.Cancel()
            failwith $"{executable} timed out"
        // A descendant may inherit a pipe after the parent exits. Never wait indefinitely for EOF.
        if not (Task.WaitAll([| stdout :> Task; stderr :> Task |], 2000)) then cancellation.Cancel()
        child.ExitCode, lock output (fun () -> output.ToString()), lock errors (fun () -> errors.ToString())

module Coverage =
    let attribute name (element: XElement) =
        element.Attribute(XName.Get name) |> Option.ofObj |> Option.map _.Value

    let complete mode path =
        let report = XDocument.Load(path: string)
        attribute "complete" report.Root = Some "true" &&
        (mode |> Option.forall (fun requested -> attribute "mode" report.Root = Some requested)) &&
        (report.Root.Elements(XName.Get "coverage") |> Seq.forall (fun entry ->
            attribute "status" entry = Some "ran" &&
            (mode |> Option.forall (fun requested -> attribute "mode" entry = Some requested))))

    let identity path =
        let report = XDocument.Load(path: string)
        match attribute "project" report.Root, attribute "targetFramework" report.Root with
        | Some project, Some framework -> project + "|" + framework
        | _ -> failwith ("Full analysis coverage is incomplete: missing project/TFM identity in " + path)

    let errors path =
        XDocument.Load(path: string).Descendants(XName.Get "finding")
        |> Seq.exists (fun finding -> attribute "severity" finding = Some "Error")

namespace Axial.Tool

open System
open System.IO
open System.Xml.Linq

/// Allowed raw effect calls per project for `axial check`: the leniency the analyzers granted, as a number. Every
/// Guardrails build records, per file, how many raw effect calls it allowed and why (`effectUses` on the EffectBoundary
/// coverage entry): in a file that claims the capability, by a gap allowance, or in a host-role file. This
/// tool reads only the latest complete result of each project, parses no source and never gates.
module EffectCount =
    type Counts =
        { Owned: int
          Allowance: int
          Host: int
          /// Owned calls per capability.
          Capabilities: (string * int) list
          /// Allowed calls per gap.
          Gaps: (string * int) list }

    type ProjectCount = { Project: string; Role: string; Counts: Counts option }

    let private xn name = XName.Get name
    let private value name (element: XElement) = element.Attribute(xn name) |> Option.ofObj |> Option.map _.Value

    let private recorded (project: string) (path: string) =
        try
            let result = XDocument.Load(path).Root
            let uses =
                result.Elements(xn "coverage")
                |> Seq.filter (fun entry -> value "rule" entry = Some "EffectBoundary" && value "status" entry = Some "ran")
                |> Seq.map (fun entry -> entry.Element(xn "effectUses"))
                |> Seq.toList
            let samePath (other: string) = Path.GetFullPath other = project
            if not (value "project" result |> Option.exists samePath) || value "complete" result <> Some "true"
               || uses |> List.exists isNull then None
            else
                let total name = uses |> List.sumBy (fun record -> int (record.Attribute(xn name).Value))
                let named element countName =
                    uses
                    |> List.collect (fun record -> record.Elements(xn element) |> Seq.toList)
                    |> List.map (fun entry -> entry.Attribute(xn "name").Value, int (entry.Attribute(xn countName).Value))
                    |> List.groupBy fst
                    |> List.map (fun (name, entries) -> name, entries |> List.sumBy snd)
                    |> List.sort
                Some { Owned = total "owned"; Allowance = total "allowance"; Host = total "host"
                       Capabilities = named "capability" "owned"; Gaps = named "gap" "uses" }
        with _ -> None

    let private latest (root: string) (project: string) =
        [ Path.Combine(Path.GetDirectoryName project, "obj")
          Path.Combine(root, "artifacts", "obj", Path.GetFileNameWithoutExtension project) ]
        |> List.filter Directory.Exists
        |> Seq.collect (fun directory -> Directory.EnumerateFiles(directory, "axial-guardrails.*.result.xml", SearchOption.AllDirectories))
        |> Seq.sortByDescending File.GetLastWriteTimeUtc
        |> Seq.tryPick (recorded project)

    let count (root: string) (projects: RawTaskCount.ProjectCount list) =
        projects
        |> List.map (fun project ->
            { Project = project.Project; Role = project.Role; Counts = latest root (Path.GetFullPath(Path.Combine(root, project.Project))) })

    let format (counts: ProjectCount list) =
        let lenient = counts |> List.choose (fun count -> count.Counts |> Option.map (fun c -> count, c)) |> List.filter (fun (_, c) -> c.Owned + c.Allowance + c.Host > 0)
        let total f = lenient |> List.sumBy (snd >> f)
        let width = lenient |> List.map (fst >> _.Project.Length) |> List.fold max 0
        let role (count: ProjectCount) = if count.Role = "" then "" else $" [{count.Role}]"
        let listed (entries: (string * int) list) =
            entries |> List.map (fun (name, uses) -> $"{name} {uses}") |> String.concat ", "
        let detail (c: Counts) =
            [ if not c.Capabilities.IsEmpty then listed c.Capabilities
              if not c.Gaps.IsEmpty then "gaps: " + listed c.Gaps ]
            |> function [] -> "" | parts -> "  (" + String.concat "; " parts + ")"
        [ yield $"Allowed raw effects (leniency, not a gate): {total _.Owned} in files that claim a capability, {total _.Allowance} by gap allowances, {total _.Host} in host files."
          for count, c in lenient ->
              $"  {count.Project.PadRight width}  {c.Owned,4} owned  {c.Allowance,4} allowance  {c.Host,4} host{role count}{detail c}" ]
        |> String.concat "\n"

    let json (jsonString: string -> string) (counts: ProjectCount list) =
        counts
        |> List.map (fun count ->
            let number f = count.Counts |> Option.map (f >> string) |> Option.defaultValue "null"
            let map f =
                count.Counts
                |> Option.map (fun c -> f c |> List.map (fun (name, uses) -> jsonString name + ":" + string uses) |> String.concat ",")
                |> Option.defaultValue ""
            "{\"project\":" + jsonString count.Project + ",\"owned\":" + number _.Owned + ",\"allowance\":" + number _.Allowance
            + ",\"host\":" + number _.Host + ",\"capabilities\":{" + map _.Capabilities + "},\"gaps\":{" + map _.Gaps + "}}")
        |> String.concat "," |> fun content -> "[" + content + "]"

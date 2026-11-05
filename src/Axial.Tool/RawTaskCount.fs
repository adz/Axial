namespace Axial.Tool

open System
open System.IO
open System.Xml.Linq

/// A per-project raw-Task trend for `axial check`, on AXG014's scale. The Guardrails build records it: every Fast or
/// Full result adds Fast's count to each file's TaskSurface coverage (`taskSites`: AXG014 findings in scope, how many
/// are acknowledged, and every raw task expression and Task-returning signature). This tool only reads the latest
/// complete result of each project; it parses no source and never gates. A project without one is "not built yet".
module RawTaskCount =
    type Counts = { Findings: int; Acknowledged: int; Sites: int }

    type ProjectCount =
        { Project: string
          Role: string
          /// None when no build has recorded a result for the project.
          Counts: Counts option }

    let NotBuilt = "not built yet — run a build"

    let private xn name = XName.Get name
    let private value name (element: XElement) = element.Attribute(xn name) |> Option.ofObj |> Option.map _.Value

    /// The trend of one result file, when it is a complete result of `project` that recorded it for every file.
    let private recorded (project: string) (path: string) =
        try
            let result = XDocument.Load(path).Root
            let surface =
                result.Elements(xn "coverage")
                |> Seq.filter (fun entry -> value "rule" entry = Some "TaskSurface" && value "status" entry = Some "ran")
                |> Seq.map (fun entry -> entry.Element(xn "taskSites"))
                |> Seq.toList
            let samePath (other: string) = Path.GetFullPath other = project
            if not (value "project" result |> Option.exists samePath) || value "complete" result <> Some "true"
               || surface |> List.exists isNull then None
            else
                let total name = surface |> List.sumBy (fun record -> int (record.Attribute(xn name).Value))
                Some { Findings = total "findings"; Acknowledged = total "acknowledged"; Sites = total "sites" }
        with _ -> None

    /// The latest recorded trend: result files under the project's `obj/` or the repository's `artifacts/obj/<name>/`.
    let latest (root: string) (project: string) =
        [ Path.Combine(Path.GetDirectoryName project, "obj")
          Path.Combine(root, "artifacts", "obj", Path.GetFileNameWithoutExtension project) ]
        |> List.filter Directory.Exists
        |> Seq.collect (fun directory -> Directory.EnumerateFiles(directory, "axial-guardrails.*.result.xml", SearchOption.AllDirectories))
        |> Seq.sortByDescending File.GetLastWriteTimeUtc
        |> Seq.tryPick (recorded project)

    let count (root: string) (files: string array) (policy: Axial.Guardrails.Policy.Config option) =
        files
        |> Array.filter (fun path -> path.EndsWith(".fsproj", StringComparison.Ordinal))
        |> Array.map (fun project ->
            let relative = Path.GetRelativePath(root, project).Replace('\\', '/')
            let role =
                policy
                |> Option.bind (fun policy -> Axial.Guardrails.Policy.projectRole policy relative |> Result.toOption)
                |> Option.map Axial.Guardrails.Policy.roleName
                |> Option.defaultValue ""
            { Project = relative; Role = role; Counts = latest root (Path.GetFullPath project) })
        |> Array.sortBy _.Project
        |> Array.toList

    let format (counts: ProjectCount list) =
        let built = counts |> List.choose (fun count -> count.Counts |> Option.map (fun c -> count, c))
        let used = built |> List.filter (fun (_, c) -> c.Sites > 0)
        let unbuilt = counts |> List.filter _.Counts.IsNone
        let total f = built |> List.sumBy (snd >> f)
        let width = used |> List.map (fst >> _.Project.Length) |> List.append (unbuilt |> List.map _.Project.Length) |> List.fold max 0
        let role (count: ProjectCount) = if count.Role = "" then "" else $" [{count.Role}]"
        let pending = if unbuilt.IsEmpty then "" else $" {unbuilt.Length} not built yet — run a build."
        [ yield $"Raw Task (AXG014 trend, not a gate): {total _.Findings} AXG014 findings ({total _.Acknowledged} acknowledged); {total _.Sites} raw task sites in {used.Length} of {counts.Length} projects.{pending}"
          for count, c in used ->
              $"  {count.Project.PadRight width}  {c.Findings,4} AXG014 ({c.Acknowledged} acknowledged)  {c.Sites,4} sites{role count}"
          for count in unbuilt -> $"  {count.Project.PadRight width}  {NotBuilt}{role count}" ]
        |> String.concat "\n"

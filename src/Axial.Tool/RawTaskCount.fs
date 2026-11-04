namespace Axial.Tool

open System
open System.IO
open System.Xml.Linq
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open Axial.Guardrails

/// A per-project raw-Task trend for `axial check`, on AXG014's scale: Fast's TaskSurface visitor with AXG014's file
/// roles, tiers, `[files]` overrides and `[runtime].files` exemptions. Fixing (or acknowledging) an AXG014 finding
/// lowers `Findings`; `Sites` counts every raw task expression and Task-returning signature, in scope or not. It never
/// gates. Projects are not evaluated by MSBuild: the role comes from policy and `AxialGuardrailsInTests` from the
/// project file, and every explicit `Compile` item counts whether or not Guardrails is enabled for the project.
module RawTaskCount =
    type ProjectCount =
        { Project: string
          Role: string
          Findings: int
          Acknowledged: int
          Sites: int }

    let private checker = lazy (FSharpChecker.Create(projectCacheSize = 1, keepAssemblyContents = false))

    let private property (document: XDocument) name =
        document.Descendants()
        |> Seq.tryFind (fun node -> node.Name.LocalName = name)
        |> Option.map _.Value.Trim()

    /// Explicit `Compile Include` items of an F# project, resolved to files that exist.
    let private sources (project: string) (document: XDocument) =
        let directory = Path.GetDirectoryName project
        document.Descendants()
        |> Seq.filter (fun node -> node.Name.LocalName = "Compile")
        |> Seq.choose (fun node -> node.Attribute(XName.Get "Include") |> Option.ofObj |> Option.map _.Value)
        |> Seq.collect (fun value -> value.Split(';'))
        |> Seq.filter (fun value -> value <> "" && not (value.Contains '$') && not (value.Contains '*'))
        |> Seq.map (fun value -> Path.GetFullPath(Path.Combine(directory, value.Replace('\\', Path.DirectorySeparatorChar))))
        |> Seq.filter File.Exists
        |> Seq.distinct
        |> Seq.toList

    /// One source counted as Fast reports AXG014 for it under `config`.
    let countSource (config: GuardrailConfig.Config) (path: string) (text: string) : FastSyntax.TaskSiteCount =
        let source = SourceText.ofString text
        let options = { FSharpParsingOptions.Default with SourceFiles = [| path |]; LangVersionText = "preview" }
        let tree = checker.Value.ParseFile(path, source, options) |> Async.RunSynchronously |> _.ParseTree
        FastSyntax.taskSiteCount config path source tree

    let private configFor root policy project inTests : GuardrailConfig.Config =
        { GuardrailConfig.defaults with
            BoundaryFiles = Set.empty
            BaselineRoot = root
            InTests = defaultArg inTests ""
            Policy = policy
            PolicyRoot = Some root
            ProjectPath = Some project }

    let count (root: string) (files: string array) (policy: Policy.Config option) =
        files
        |> Array.filter (fun path -> path.EndsWith(".fsproj", StringComparison.Ordinal))
        |> Array.map (fun project ->
            let relative = Path.GetRelativePath(root, project).Replace('\\', '/')
            let role =
                policy
                |> Option.bind (fun policy -> Policy.projectRole policy relative |> Result.toOption)
                |> Option.map Policy.roleName
                |> Option.defaultValue ""
            let document = try Some(XDocument.Load project) with _ -> None
            let config = configFor root policy project (document |> Option.bind (fun d -> property d "AxialGuardrailsInTests"))
            let counts =
                document
                |> Option.map (sources project)
                |> Option.defaultValue []
                |> List.choose (fun source -> try Some(countSource config source (File.ReadAllText source)) with _ -> None)
            { Project = relative
              Role = role
              Findings = counts |> List.sumBy _.Findings
              Acknowledged = counts |> List.sumBy _.Acknowledged
              Sites = counts |> List.sumBy _.Sites })
        |> Array.sortBy _.Project
        |> Array.toList

    let format (counts: ProjectCount list) =
        let used = counts |> List.filter (fun count -> count.Sites > 0)
        let total f = counts |> List.sumBy f
        let width = if used.IsEmpty then 0 else used |> List.map _.Project.Length |> List.max
        [ yield $"Raw Task (AXG014 trend, not a gate): {total _.Findings} AXG014 findings ({total _.Acknowledged} acknowledged); {total _.Sites} raw task sites in {used.Length} of {counts.Length} projects."
          for count in used ->
              let role = if count.Role = "" then "" else $" [{count.Role}]"
              $"  {count.Project.PadRight width}  {count.Findings,4} AXG014 ({count.Acknowledged} acknowledged)  {count.Sites,4} sites{role}" ]
        |> String.concat "\n"

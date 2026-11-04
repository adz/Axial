namespace Axial.Tool

open System
open System.IO
open System.Text.RegularExpressions
open System.Xml.Linq

/// A per-project trend of raw Task use for `axial check`. It is a lexical count, not analysis: it never gates and
/// AXG014 (Fast and Full) remains the source of guidance. Comments and string literals are ignored.
module RawTaskCount =
    type ProjectCount =
        { Project: string
          Role: string
          TaskExpressions: int
          TaskSignatures: int }

    let private expression = Regex(@"\b(?:task|backgroundTask)\s*\{", RegexOptions.Compiled)
    // A declaration whose annotated final return type is Task, Task<_>, ValueTask or ValueTask<_>.
    let private signature =
        Regex(@"^\s*(?:let|member|abstract|static\s+member|default)\b[^=]*:\s*(?:[^=]*->\s*)?(?:global\.)?(?:System\.Threading\.Tasks\.)?(?:Value)?Task\b(?:\s*<[^=]*>)?\s*(?:=|$)", RegexOptions.Compiled)

    /// Source text with comments and string literals blanked, preserving line structure.
    let private code (text: string) =
        let output = Text.StringBuilder(text.Length)
        let mutable index = 0
        let mutable blockDepth = 0
        let at (token: string) = String.CompareOrdinal(text, index, token, 0, token.Length) = 0
        while index < text.Length do
            let ch = text[index]
            if blockDepth > 0 then
                if at "(*" then blockDepth <- blockDepth + 1; index <- index + 2
                elif at "*)" then blockDepth <- blockDepth - 1; index <- index + 2
                else
                    output.Append(if ch = '\n' then '\n' else ' ') |> ignore
                    index <- index + 1
            elif at "//" then
                while index < text.Length && text[index] <> '\n' do index <- index + 1
            elif at "(*" && not (at "(*)") then
                blockDepth <- 1
                index <- index + 2
            elif at "\"\"\"" then
                index <- index + 3
                while index < text.Length && not (at "\"\"\"") do
                    if text[index] = '\n' then output.Append('\n') |> ignore
                    index <- index + 1
                index <- index + 3
                output.Append("\"\"") |> ignore
            elif ch = '"' then
                index <- index + 1
                while index < text.Length && text[index] <> '"' do
                    if text[index] = '\\' then index <- index + 1
                    elif text[index] = '\n' then output.Append('\n') |> ignore
                    index <- index + 1
                index <- index + 1
                output.Append("\"\"") |> ignore
            elif ch = '\'' && index + 2 < text.Length && (text[index + 2] = '\'' || text[index + 1] = '\\') then
                // A character literal such as '"' or '\n'; a type variable ('T) has no closing quote here.
                let close = text.IndexOf('\'', index + 2)
                index <- if close > index then close + 1 else index + 1
                output.Append("' '") |> ignore
            else
                output.Append(ch) |> ignore
                index <- index + 1
        output.ToString()

    let countSource (text: string) =
        let stripped = code text
        let expressions = expression.Matches(stripped).Count
        let signatures = stripped.Split('\n') |> Array.filter signature.IsMatch |> Array.length
        expressions, signatures

    /// Explicit `Compile Include` items of an F# project, resolved to files that exist.
    let private sources (project: string) =
        try
            let directory = Path.GetDirectoryName project
            XDocument.Load(project).Descendants()
            |> Seq.filter (fun node -> node.Name.LocalName = "Compile")
            |> Seq.choose (fun node -> node.Attribute(XName.Get "Include") |> Option.ofObj |> Option.map _.Value)
            |> Seq.collect (fun value -> value.Split(';'))
            |> Seq.filter (fun value -> value <> "" && not (value.Contains '$') && not (value.Contains '*'))
            |> Seq.map (fun value -> Path.GetFullPath(Path.Combine(directory, value.Replace('\\', Path.DirectorySeparatorChar))))
            |> Seq.filter File.Exists
            |> Seq.distinct
            |> Seq.toList
        with _ -> []

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
            let expressions, signatures =
                sources project
                |> List.map (fun source -> try countSource (File.ReadAllText source) with _ -> 0, 0)
                |> List.fold (fun (a, b) (x, y) -> a + x, b + y) (0, 0)
            { Project = relative; Role = role; TaskExpressions = expressions; TaskSignatures = signatures })
        |> Array.sortBy _.Project
        |> Array.toList

    let format (counts: ProjectCount list) =
        let used = counts |> List.filter (fun count -> count.TaskExpressions + count.TaskSignatures > 0)
        let total f = counts |> List.sumBy f
        let width = if used.IsEmpty then 0 else used |> List.map _.Project.Length |> List.max
        [ yield $"Raw Task (lexical trend, not a gate): {total _.TaskExpressions} task expressions, {total _.TaskSignatures} Task-returning signatures in {used.Length} of {counts.Length} projects."
          for count in used ->
              let role = if count.Role = "" then "" else $" [{count.Role}]"
              $"  {count.Project.PadRight width}  {count.TaskExpressions,4} task  {count.TaskSignatures,4} signatures{role}" ]
        |> String.concat "\n"

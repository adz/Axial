namespace Axial.Schema.Contracts.Build

open System
open System.IO
open System.Text.RegularExpressions
open Microsoft.Build.Framework
open Microsoft.Build.Utilities
open Axial.Schema.Contracts

/// Discovers schema declarations, generates changed files, and preserves F# compile order.
type GenerateSchemas() =
    inherit Task()

    let mutable sources: ITaskItem array = [||]
    let mutable contracts: ITaskItem array = [||]
    let mutable compileItems: ITaskItem array = [||]
    let mutable generatedCompileItems: ITaskItem array = [||]
    let mutable naming = "camel"
    let mutable contractNamespace = ""
    let mutable projectDirectory = ""
    let mutable intermediateOutputPath = ""
    let mutable outputMode = "Intermediate"

    [<Required>]
    member _.Sources with get () = sources and set value = sources <- value

    member _.Contracts with get () = contracts and set value = contracts <- value

    [<Required>]
    member _.CompileItems with get () = compileItems and set value = compileItems <- value

    member _.Naming with get () = naming and set value = naming <- value
    member _.ContractNamespace with get () = contractNamespace and set value = contractNamespace <- value

    [<Required>]
    member _.ProjectDirectory with get () = projectDirectory and set value = projectDirectory <- value

    [<Required>]
    member _.IntermediateOutputPath with get () = intermediateOutputPath and set value = intermediateOutputPath <- value

    member _.OutputMode with get () = outputMode and set value = outputMode <- value

    [<Output>]
    member _.GeneratedCompileItems with get () = generatedCompileItems and set value = generatedCompileItems <- value

    member private this.Fail(message: string) =
        this.Log.LogError message
        false

    override this.Execute() =
        try
            let schemaNaming =
                match naming.ToLowerInvariant() with
                | "camel" -> Some SchemaNaming.CamelCase
                | "snake" -> Some SchemaNaming.SnakeCase
                | "verbatim" -> Some SchemaNaming.Verbatim
                | _ -> None

            match schemaNaming with
            | None -> this.Fail $"Unknown AxialSchemaNaming '{naming}' (expected camel, snake, or verbatim)."
            | Some schemaNaming ->
                let sourcePaths =
                    sources
                    |> Array.map (fun item -> Path.GetFullPath item.ItemSpec)
                    |> Array.filter (fun path -> not (path.EndsWith(".g.fs", StringComparison.OrdinalIgnoreCase)))
                    // Avoid asking the declaration frontend to parse unrelated application files. In
                    // particular, a final Program.fs may legally omit a namespace/module declaration.
                    |> Array.filter (fun path ->
                        File.ReadLines path
                        |> Seq.exists (fun line ->
                            let trimmed = line.TrimStart()

                            not (trimmed.StartsWith("//", StringComparison.Ordinal))
                            && Regex.IsMatch(
                                line,
                                @"\[<\s*(?:[A-Za-z0-9_.]+\.)?DeriveSchema(?:Attribute)?(?:\s|[;(>,])"
                            )))
                    |> Array.distinct

                let contractPaths = contracts |> Array.map (fun item -> Path.GetFullPath item.ItemSpec) |> Array.distinct

                if contractPaths.Length > 0 && String.IsNullOrWhiteSpace contractNamespace then
                    this.Fail "AxialContractNamespace must be set when AxialContract items are declared."
                else
                    let parsed =
                        [ for path in contractPaths -> path, Parser.parse path (File.ReadAllText path)
                          for path in sourcePaths -> path, Records.parse schemaNaming path (File.ReadAllText path) ]

                    let parseErrors =
                        parsed
                        |> List.collect (fun (_, result) ->
                            match result with
                            | Error diagnostics -> diagnostics
                            | Ok _ -> [])

                    let files =
                        parsed
                        |> List.choose (fun (_, result) ->
                            match result with
                            | Ok file when not file.Contracts.IsEmpty -> Some file
                            | _ -> None)

                    let errors = if parseErrors.IsEmpty then Resolver.resolve files else parseErrors

                    if not errors.IsEmpty then
                        for error in errors do
                            this.Log.LogError(string error)

                        false
                    else
                        let checkedIn = outputMode.Equals("CheckedIn", StringComparison.OrdinalIgnoreCase)

                        if not checkedIn && not (outputMode.Equals("Intermediate", StringComparison.OrdinalIgnoreCase)) then
                            this.Fail $"Unknown AxialSchemaGeneratedFiles value '{outputMode}' (expected Intermediate or CheckedIn)."
                        else
                            let projectRoot = Path.GetFullPath projectDirectory
                            let generatedRoot = Path.GetFullPath(Path.Combine(projectRoot, intermediateOutputPath, "Axial.Schema"))
                            let fallbackNamespace = if String.IsNullOrWhiteSpace contractNamespace then "Generated" else contractNamespace

                            let outputPath (inputPath: string) =
                                if checkedIn then
                                    Path.ChangeExtension(inputPath, ".g.fs")
                                else
                                    let relative = Path.GetRelativePath(projectRoot, inputPath)
                                    let safeRelative =
                                        if relative.StartsWith(".." + string Path.DirectorySeparatorChar, StringComparison.Ordinal) then
                                            Path.Combine("external", string (uint (inputPath.GetHashCode())), Path.GetFileName inputPath)
                                        else
                                            relative

                                    Path.ChangeExtension(Path.Combine(generatedRoot, safeRelative), ".g.fs")

                            let outputsByInput =
                                files
                                |> List.map (fun file ->
                                    let input = Path.GetFullPath file.FilePath
                                    let output = outputPath input
                                    Directory.CreateDirectory(Path.GetDirectoryName output) |> ignore
                                    let emitted = Emitter.emit fallbackNamespace files file
                                    let existing = if File.Exists output then File.ReadAllText(output).Replace("\r\n", "\n") else ""

                                    if existing <> emitted then
                                        File.WriteAllText(output, emitted)
                                        this.Log.LogMessage(MessageImportance.High, $"Generated {output}")

                                    input, output)
                                |> Map.ofList

                            let generatedPaths = outputsByInput |> Map.toSeq |> Seq.map snd |> Set.ofSeq
                            let checkedInSiblings = outputsByInput |> Map.toSeq |> Seq.map (fun (input, _) -> Path.ChangeExtension(input, ".g.fs")) |> Set.ofSeq
                            let result = ResizeArray<ITaskItem>()

                            // Preserve AxialContract item order: generated declarations can reference contracts
                            // from earlier files, just like ordinary F# Compile items.
                            for input in contractPaths do
                                match Map.tryFind input outputsByInput with
                                | Some output -> result.Add(TaskItem output)
                                | None -> ()

                            for item in compileItems do
                                let path = Path.GetFullPath item.ItemSpec

                                if not (generatedPaths.Contains path || checkedInSiblings.Contains path) then
                                    result.Add(TaskItem item)

                                    match Map.tryFind path outputsByInput with
                                    | Some output -> result.Add(TaskItem output)
                                    | None -> ()

                            generatedCompileItems <- result.ToArray()
                            true
        with ex ->
            this.Log.LogErrorFromException(ex, true)
            false

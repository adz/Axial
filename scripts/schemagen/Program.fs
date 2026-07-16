module Axial.Schema.Contracts.SchemaGen

open System
open System.IO
open Axial.Schema.Contracts

/// axial schemagen: generates checked-in F# from .contract files and [<WireSchema>]-marked records.
///
/// Usage:
///   schemagen [--namespace <ns>] [--wire-naming camel|snake|verbatim] [--check] <file-or-directory>...
///
/// Each x.contract emits a sibling x.g.fs into the namespace given by --namespace (required for .contract
/// inputs). Each .fs file containing [<WireSchema>] records emits a sibling x.g.fs into the source file's
/// own namespace; .fs files without marked records are skipped. With --check, no files are written; the
/// tool exits with code 2 if any generated file is missing or differs from what would be emitted.
let private usage () =
    eprintfn "usage: schemagen [--namespace <ns>] [--wire-naming camel|snake|verbatim] [--check] <file-or-directory>..."
    1

let private collectInputFiles (paths: string list) =
    paths
    |> List.collect (fun path ->
        if Directory.Exists path then
            let contracts = Directory.EnumerateFiles(path, "*.contract", SearchOption.AllDirectories)
            let sources = Directory.EnumerateFiles(path, "*.fs", SearchOption.AllDirectories)
            Seq.append contracts sources |> List.ofSeq
        elif File.Exists path then
            [ path ]
        else
            failwith $"no such file or directory: {path}")
    |> List.filter (fun path -> not (path.EndsWith ".g.fs"))
    |> List.distinct
    |> List.sort

[<EntryPoint>]
let main argv =
    let mutable namespaceName = None
    let mutable naming = WireNaming.CamelCase
    let mutable check = false
    let mutable paths = []
    let mutable badArgs = false

    let rec parseArgs args =
        match args with
        | [] -> true
        | "--namespace" :: value :: rest ->
            namespaceName <- Some value
            parseArgs rest
        | "--wire-naming" :: value :: rest ->
            match value with
            | "camel" -> naming <- WireNaming.CamelCase
            | "snake" -> naming <- WireNaming.SnakeCase
            | "verbatim" -> naming <- WireNaming.Verbatim
            | other ->
                eprintfn $"unknown wire naming '{other}' (expected camel, snake, or verbatim)"
                badArgs <- true

            parseArgs rest
        | "--check" :: rest ->
            check <- true
            parseArgs rest
        | ("--help" | "-h") :: _ -> false
        | path :: rest ->
            paths <- paths @ [ path ]
            parseArgs rest

    if not (parseArgs (List.ofArray argv)) || badArgs then
        usage ()
    else
        match paths with
        | [] -> usage ()
        | paths ->
            let inputFiles = collectInputFiles paths

            let contractInputs = inputFiles |> List.filter (fun path -> path.EndsWith ".contract")
            let sourceInputs = inputFiles |> List.filter (fun path -> path.EndsWith ".fs")

            if not (List.isEmpty contractInputs) && Option.isNone namespaceName then
                eprintfn "--namespace is required when .contract files are among the inputs"
                usage ()
            else
                let parsed =
                    [ for path in contractInputs -> path, Parser.parse path (File.ReadAllText path)
                      for path in sourceInputs -> path, Records.parse naming path (File.ReadAllText path) ]

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
                        | Ok file when not (List.isEmpty file.Contracts) -> Some file
                        | _ -> None)

                let resolveErrors = if List.isEmpty parseErrors then Resolver.resolve files else []
                let allErrors = parseErrors @ resolveErrors

                if not (List.isEmpty allErrors) then
                    for diagnostic in allErrors do
                        eprintfn $"{diagnostic}"

                    1
                else
                    let fallbackNamespace = namespaceName |> Option.defaultValue "Generated"
                    let mutable drifted = false

                    for file in files do
                        let outputPath =
                            if file.FilePath.EndsWith ".fs" then
                                file.FilePath.Substring(0, file.FilePath.Length - 3) + ".g.fs"
                            else
                                Path.ChangeExtension(file.FilePath, ".g.fs")

                        let emitted = Emitter.emit fallbackNamespace files file

                        if check then
                            let existing =
                                if File.Exists outputPath then
                                    (File.ReadAllText outputPath).Replace("\r\n", "\n")
                                else
                                    ""

                            if existing <> emitted then
                                eprintfn $"drift: {outputPath} is out of date; run schemagen to regenerate"
                                drifted <- true
                        else
                            File.WriteAllText(outputPath, emitted)
                            printfn $"generated {outputPath}"

                    if drifted then 2 else 0

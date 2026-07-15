module Axial.Schema.Contracts.SchemaGen

open System
open System.IO
open Axial.Schema.Contracts

/// axial schemagen: generates checked-in F# from .contract files.
///
/// Usage:
///   schemagen --namespace <ns> [--check] <file-or-directory>...
///
/// Each x.contract emits a sibling x.g.fs. With --check, no files are written; the tool exits
/// with code 2 if any generated file is missing or differs from what would be emitted (drift guard).
let private usage () =
    eprintfn "usage: schemagen --namespace <ns> [--check] <file-or-directory>..."
    1

let private collectContractFiles (paths: string list) =
    paths
    |> List.collect (fun path ->
        if Directory.Exists path then
            Directory.EnumerateFiles(path, "*.contract", SearchOption.AllDirectories) |> List.ofSeq
        elif File.Exists path then
            [ path ]
        else
            failwith $"no such file or directory: {path}")
    |> List.sort

[<EntryPoint>]
let main argv =
    let mutable namespaceName = None
    let mutable check = false
    let mutable paths = []

    let rec parseArgs args =
        match args with
        | [] -> true
        | "--namespace" :: value :: rest ->
            namespaceName <- Some value
            parseArgs rest
        | "--check" :: rest ->
            check <- true
            parseArgs rest
        | ("--help" | "-h") :: _ -> false
        | path :: rest ->
            paths <- paths @ [ path ]
            parseArgs rest

    if not (parseArgs (List.ofArray argv)) then
        usage ()
    else
        match namespaceName, paths with
        | None, _ -> usage ()
        | _, [] -> usage ()
        | Some namespaceName, paths ->
            let contractFiles = collectContractFiles paths

            let parsed =
                contractFiles
                |> List.map (fun path -> path, Parser.parse path (File.ReadAllText path))

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
                    | Ok file -> Some file
                    | Error _ -> None)

            let resolveErrors = if List.isEmpty parseErrors then Resolver.resolve files else []
            let allErrors = parseErrors @ resolveErrors

            if not (List.isEmpty allErrors) then
                for diagnostic in allErrors do
                    eprintfn $"{diagnostic}"

                1
            else
                let mutable drifted = false

                for file in files do
                    let outputPath = Path.ChangeExtension(file.FilePath, ".g.fs")
                    let emitted = Emitter.emit namespaceName files file

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

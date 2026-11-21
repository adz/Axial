/// `axial capabilities` lists the services that own raw APIs: Axial's own and every declared one.
module Axial.Tool.Capabilities

open System
open System.IO
open Axial.Guardrails.EffectCatalog

/// A capability and where it was declared: `None` for Axial's own, otherwise the project directory.
type internal Listed =
    { Capability: Capability
      Rules: EffectRule list
      DeclaredIn: string option
      /// The files that implement it, relative to the repository; a package's own files for Axial's capabilities.
      Implementation: string list }

let private skippedDirectories = set [ ".git"; "node_modules"; "bin"; ".livedocs"; "output" ]

let rec private rulesFiles (directory: string) : string seq =
    seq {
        let files = try Directory.GetFiles(directory, "axial-guardrails.*.rules") with _ -> [||]
        yield! files
        let children = try Directory.GetDirectories directory with _ -> [||]
        for child in children do
            if not (skippedDirectories.Contains(Path.GetFileName child)) then yield! rulesFiles child
    }

/// The project directory a rules file belongs to: the nearest ancestor holding a project file, else the file's folder.
let private projectOf (root: string) (rulesFile: string) =
    let rec up (directory: string) =
        if isNull directory || not (directory.StartsWith root) then Path.GetDirectoryName rulesFile
        elif (try Directory.GetFiles(directory, "*.??proj").Length > 0 with _ -> false) then directory
        else up (Path.GetDirectoryName directory)
    Path.GetRelativePath(root, up (Path.GetDirectoryName rulesFile)).Replace('\\', '/')

/// Built-in capabilities, then every capability a built project declared, read from the rules files the build wrote.
let internal discover (root: string) : Listed list * string list =
    let declared, problems =
        rulesFiles root
        |> Seq.collect (fun file ->
            let lines = File.ReadAllLines file |> Array.map (fun line -> line.Split('\t'))
            // The build records the project when a policy is configured; otherwise the rules file sits under its obj/.
            let project =
                lines
                |> Array.tryPick (function [| "setting"; "projectPath"; path |] when path <> "" -> Some path | _ -> None)
                |> Option.map (fun path -> Path.GetRelativePath(root, path).Replace('\\', '/'))
                |> Option.defaultWith (fun () -> projectOf root file)
            lines
            |> Seq.filter (fun fields -> fields.Length >= 2 && fields[0] = "capability")
            |> Seq.map (fun fields ->
                match parseCapability fields[1] (fields |> Array.skip 2 |> List.ofArray) with
                | Ok(capability, rules) ->
                    let files =
                        lines
                        |> Array.choose (function [| "capabilityFile"; name; path |] when name = capability.Name -> Some(Path.GetRelativePath(root, path).Replace('\\', '/')) | _ -> None)
                        |> List.ofArray
                    Ok { Capability = capability; Rules = rules; DeclaredIn = Some project; Implementation = files }
                | Error error -> Error $"{Path.GetRelativePath(root, file)}: {error}"))
        |> Seq.toList
        |> List.partition Result.isOk
    let declared =
        declared
        |> List.choose (function Ok listed -> Some listed | Error _ -> None)
        // One project builds a rules file per target framework; list each declaration once.
        |> List.distinctBy (fun listed -> listed.Capability, listed.DeclaredIn)
    let builtIn =
        builtInCapabilities
        |> List.map (fun capability ->
            { Capability = capability
              Rules = builtInRules |> List.filter (fun rule -> rule.Capability = Some capability.Name)
              DeclaredIn = None
              Implementation = (if capability.Env = "" then [] else [ $"the {capability.Guardrail} package" ]) })
    builtIn @ declared, problems |> List.choose (function Error problem -> Some problem | Ok _ -> None)

let private targets (rules: EffectRule list) = rules |> List.map ruleTarget |> List.distinct

/// The listing as text: one block per capability saying what it replaces and how to use it.
let internal format (listed: Listed list) (problems: string list) =
    let block (item: Listed) =
        let c = item.Capability
        let source = match item.DeclaredIn with None -> "Axial" | Some project -> project
        let level = match c.Level with Guide -> "guide" | Enforce -> "enforce"
        let reach = if c.Env = "" then "provided by the runtime; no environment change" else $"field `{c.Field}`, implements {c.Env}"
        [ yield $"{c.Name} ({source}, {level})"
          yield $"  use      {c.Service}: {reach}"
          if c.Example <> "" then yield $"  example  {c.Example}"
          if c.Live <> "" then yield $"  live     {c.Live}"
          if c.Fake <> "" then yield $"  tests    {c.Fake}"
          if not item.Implementation.IsEmpty then yield $"""  files    {String.concat ", " item.Implementation}"""
          yield $"""  replaces {String.concat ", " (targets item.Rules)}""" ]
        |> String.concat "\n"
    let declaredCount = listed |> List.filter (fun item -> item.DeclaredIn.IsSome) |> List.length
    [ yield! listed |> List.map block
      if declaredCount = 0 then
          yield "No project declares its own capability yet, or none has been built. Declare one with an <AxialCapability> item naming its Service, the APIs it Replaces and its Implementation file."
      for problem in problems do
          yield $"warning: {problem}" ]
    |> String.concat "\n\n"

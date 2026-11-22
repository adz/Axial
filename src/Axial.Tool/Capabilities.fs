/// `axial capabilities` lists the services that own raw APIs: Axial's own and every declared one.
module Axial.Tool.Capabilities

open System
open System.IO
open Axial.Guardrails.EffectCatalog

/// A capability, where it was defined, and the files that claim it.
type internal Listed =
    { Capability: Capability
      Rules: EffectRule list
      /// `None` for Axial's own, otherwise the definition file relative to the repository.
      DeclaredIn: string option
      /// The files that claim it with `AxialCapability` metadata, relative to the repository.
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

/// Built-in capabilities, then every capability a built project defines, each with the files that claim it, read
/// from the rules files the builds wrote.
let internal discover (root: string) : Listed list * string list =
    let relative (path: string) = Path.GetRelativePath(root, path).Replace('\\', '/')
    let lines = rulesFiles root |> Seq.collect (fun file -> File.ReadAllLines file |> Seq.map (fun line -> line.Split('\t'))) |> Seq.toList
    let claims =
        lines
        |> List.choose (function [| "capabilityFile"; name; path |] -> Some(name, relative path) | _ -> None)
        |> List.distinct
    let claimed name = claims |> List.filter (fst >> (=) name) |> List.map snd |> List.sort
    let defined, problems =
        lines
        |> List.choose (function [| "capabilityDefinition"; path |] -> Some path | _ -> None)
        |> List.distinct
        |> List.map (fun path ->
            match (try parseDefinition (File.ReadAllText path) with error -> Error error.Message) with
            | Ok(capability, rules) ->
                Ok { Capability = capability; Rules = rules; DeclaredIn = Some(relative path); Implementation = claimed capability.Name }
            | Error error -> Error $"{relative path}: {error}")
        |> List.partition Result.isOk
    let defined = defined |> List.choose (function Ok listed -> Some listed | Error _ -> None)
    let builtIn =
        builtInCapabilities
        |> List.map (fun capability ->
            { Capability = capability
              Rules = builtInRules |> List.filter (fun rule -> rule.Capability = Some capability.Name)
              DeclaredIn = None
              Implementation = claimed capability.Name })
    builtIn @ defined, problems |> List.choose (function Error problem -> Some problem | Ok _ -> None)

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
          yield "No project defines its own capability yet, or none has been built. Define one in a <name>.capability.toml file (name, service, replaces), name it with an <AxialCapabilityDefinition> item, and claim it with AxialCapability=\"<name>\" on the Compile items that implement it."
      for problem in problems do
          yield $"warning: {problem}" ]
    |> String.concat "\n\n"

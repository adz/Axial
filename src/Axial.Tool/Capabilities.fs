/// `axial capabilities` lists the services that replace raw APIs; `axial add service` scaffolds a new one.
module Axial.Tool.Capabilities

open System
open System.IO
open Axial.Guardrails.EffectCatalog

/// A capability and where it was declared: `None` for Axial's own, otherwise the project directory.
type internal Listed = { Capability: Capability; Rules: EffectRule list; DeclaredIn: string option }

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
                | Ok(capability, rules) -> Ok { Capability = capability; Rules = rules; DeclaredIn = Some project }
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
              DeclaredIn = None })
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
          yield $"""  replaces {String.concat ", " (targets item.Rules)}""" ]
        |> String.concat "\n"
    let declaredCount = listed |> List.filter (fun item -> item.DeclaredIn.IsSome) |> List.length
    [ yield! listed |> List.map block
      if declaredCount = 0 then
          yield "No project declares its own capability yet, or none has been built. Declare one with an <AxialCapability> item; `axial add service <Name>` scaffolds it."
      for problem in problems do
          yield $"warning: {problem}" ]
    |> String.concat "\n\n"

let private identifier (name: string) =
    not (String.IsNullOrEmpty name) && Char.IsUpper name[0] && name |> Seq.forall Char.IsLetterOrDigit

/// The F# source of a new capability: service interface, environment interface, accessor, live implementation and fake.
let source (name: string) =
    $"""/// The {name} capability: the one place that calls the API it wraps. The rest of the code reaches it through
/// I{name} in its environment, and tests replace it with Test{name}.
module {name}

open Axial

/// The failures {name} reports. Replace these with the ones callers can act on.
type {name}Error =
    | Unavailable of reason: string

/// What the rest of the code may ask of {name}. Replace Example with the operations you need.
type I{name} =
    abstract Example: input: string -> Flow<unit, {name}Error, string>

/// An environment that carries {name}. Implement it on your environment record with a `{name}: I{name}` field.
type IHas{name} =
    abstract {name}: I{name}

/// Calls Example on the {name} service in the environment.
let example<'env when 'env :> IHas{name}> (input: string) : Flow<'env, {name}Error, string> =
    Flow.envWithFlow (fun (environment: 'env) -> environment.{name}.Example input |> Flow.provide ())

/// The production implementation: the only code that calls the wrapped API.
let live: I{name} =
    {{ new I{name} with
        member _.Example input =
            // Call the wrapped API here; lift a .NET task with Flow.attemptTask and map its exception to {name}Error.
            Flow.fail (Unavailable "not implemented yet") }}

/// A test double that answers from a function.
type Test{name}(answer: string -> Result<string, {name}Error>) =
    interface I{name} with
        member _.Example input = Flow.fromResult (answer input)
"""

/// The project-file item that declares the capability, so Guardrails points callers of the wrapped API at it.
let declaration (name: string) (replaces: string option) =
    let replaces = defaultArg replaces "any:Vendor.Namespace.Client"
    $"""<ItemGroup>
  <AxialCapability Include="{name.ToLowerInvariant()}"
      Service="I{name}"
      Example="{name}.example input"
      Live="{name}.live"
      Fake="Test{name}(fun input -> Ok input)"
      Replaces="{replaces}" />
</ItemGroup>"""

/// Writes `<name>.fs` into the directory and returns the declaration to add to the project file.
let scaffold (directory: string) (name: string) (replaces: string option) : Result<string, string> =
    if not (identifier name) then
        Error $"'{name}' is not a service name: use letters and digits starting with an upper-case letter, such as Payments"
    else
        match replaces |> Option.map (fun text -> text, text.Split('|') |> Array.forall (parseMatch >> Option.isSome)) with
        | Some(text, false) -> Error $"--replaces '{text}' is not a match: use ctor:Type, member:Type::A,B or any:Type, separated by |"
        | _ ->
            let path = Path.Combine(directory, name + ".fs")
            if File.Exists path then Error $"Refusing to overwrite {path}"
            else
                Directory.CreateDirectory directory |> ignore
                File.WriteAllText(path, source name)
                let placeholder = if replaces.IsNone then "\nSet Replaces to the API the service wraps; until then the declaration guides nobody." else ""
                Ok(
                    $"Wrote {path}.\n\n"
                    + $"1. Add it to the project's <Compile> items, then replace Example with the operations you need.\n"
                    + $"2. Add this declaration to the project file (or Directory.Build.props). Guardrails then points every\n"
                    + $"   direct use of the wrapped API at I{name}, as information that never fails a build:{placeholder}\n\n"
                    + declaration name replaces
                    + "\n\n3. Run `axial capabilities` after the next build to see it listed.")

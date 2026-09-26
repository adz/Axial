/// Which guardrails run, at which severity, and which extra rules packages contributed.
///
/// `build/Axial.Guardrails.targets` writes the resolved `@(AxialGuardrail)` and `@(AxialGuardrailRule)` items to a
/// rules file and passes its path in `AXIAL_GUARDRAILS_CONFIG`. One line per entry, tab-separated:
///
///   guardrail  <name>  <severity>
///   rule       <id>  <guardrail>  <category>  <match>  <scope>  <message>  <replacement>
///
/// Without the variable (the analyzer run directly, or from its own tests), every built-in guardrail runs at warning
/// severity, which is the behavior before guardrails were configurable.
module Axial.Guardrails.GuardrailConfig

open System
open System.IO
open FSharp.Analyzers.SDK
open Axial.Guardrails.EffectCatalog

[<Literal>]
let EnvironmentVariable = "AXIAL_GUARDRAILS_CONFIG"

type Config =
    { /// Enabled guardrails and their severity. A guardrail that is absent does not run.
      Severities: Map<string, Severity>
      /// Every rule, built-in and contributed, whether or not its guardrail is enabled.
      AllRules: EffectRule list }

    /// The rules whose guardrail is enabled.
    member this.Rules = this.AllRules |> List.filter (fun rule -> this.Severities.ContainsKey rule.Guardrail)

    /// Every category a suppression comment may name.
    member this.KnownCategories = this.AllRules |> List.map _.Category |> Set.ofList

let private parseSeverity (text: string) =
    match text.Trim().ToLowerInvariant() with
    | "error" -> Severity.Error
    | "info" -> Severity.Info
    | "hint" -> Severity.Hint
    | _ -> Severity.Warning

let private parseScope (text: string) =
    match text.Trim().ToLowerInvariant() with
    | "insideflow" -> InsideFlow
    | _ -> Anywhere

let defaults: Config =
    { Severities = builtInGuardrails |> Seq.map (fun name -> name, Severity.Warning) |> Map.ofSeq
      AllRules = builtInRules }

let parse (lines: string seq) : Config =
    let entries = lines |> Seq.map (fun line -> line.Split('\t')) |> Seq.toList

    let severities =
        entries
        |> List.choose (function
            | [| "guardrail"; name; severity |] -> Some(name.Trim(), parseSeverity severity)
            | [| "guardrail"; name |] -> Some(name.Trim(), Severity.Warning)
            | _ -> None)
        |> Map.ofList

    let contributed =
        entries
        |> List.choose (function
            | [| "rule"; id; guardrail; category; matching; scope; message; replacement |] ->
                parseMatch matching
                |> Option.map (fun parsed ->
                    { Id = id.Trim()
                      Guardrail = guardrail.Trim()
                      Category = category.Trim().ToLowerInvariant()
                      Match = parsed
                      Scope = parseScope scope
                      Message = message.Trim()
                      Replacement = replacement.Trim() })
            | _ -> None)

    { Severities = severities
      AllRules = builtInRules @ contributed }

let private gate = obj ()
let mutable private cached: (string * DateTime * Config) option = None

/// The configuration for this analyzer run, read once per rules file version.
let load () : Config =
    match Environment.GetEnvironmentVariable EnvironmentVariable with
    | null
    | "" -> defaults
    | path when not (File.Exists path) -> defaults
    | path ->
        let stamp = File.GetLastWriteTimeUtc path

        lock gate (fun () ->
            match cached with
            | Some(cachedPath, cachedStamp, config) when cachedPath = path && cachedStamp = stamp -> config
            | _ ->
                let config = parse (File.ReadAllLines path)
                cached <- Some(path, stamp, config)
                config)

/// Applies the core guardrail's configuration to a structural analyzer's messages: nothing when the guardrail is
/// disabled, otherwise the messages at its severity.
let applyCore (messages: Message list) : Message list =
    match (load ()).Severities.TryFind CoreGuardrail with
    | Some severity -> messages |> List.map (fun message -> { message with Severity = severity })
    | None -> []

/// How every raw effect call is judged, the same way in Fast and Full: allowed for a stated reason and counted, or
/// reported for the agent to fix. No file is exempt as a whole. A file that claims a capability (`AxialCapability`
/// metadata on its `Compile` item) may call only that capability's API; every other effect in it is reported like
/// anywhere else.
module Axial.Guardrails.EffectUse

open System
open System.IO
open System.Xml.Linq
open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open Axial.Guardrails.EffectCatalog

/// Why a raw effect call is allowed, or that it is not.
type Verdict =
    /// In a file that claims the capability owning the API.
    | Owned of capability: string
    /// An `axial-allow-raw-effect` comment ending in `(gap: name)` or `(why: text)`.
    | Allowance of Suppressions.Allowed
    /// A `host`-role file, or a role whose `effects.ambient` level is `allow`.
    | Host
    /// Not allowed: the agent must use the service.
    | Reported

let private fullPath (fileName: string) = try Some(Path.GetFullPath fileName) with _ -> None

/// Whether this file claims the capability.
let owns fileName (capability: Capability) =
    fullPath fileName |> Option.exists (fun path -> List.contains path capability.Implementation)

/// What a valid AXG001 allowance covering `line` stands for, if there is one.
let private allowanceAt (source: ISourceText) line =
    if not (Suppressions.isAllowed source line "AXG001") then None
    else
        Suppressions.validDirectives source
        |> List.tryPick (fun directive ->
            if directive.RuleId = Some "AXG001" && (directive.Line = line || directive.Line = line - 1) then Suppressions.allowanceOf directive
            else None)

/// The verdict for one raw effect call at `line`.
let judge (config: GuardrailConfig.Config) fileName (source: ISourceText) line (rule: EffectRule) : Verdict =
    let policy = GuardrailConfig.resolvedFilePolicy config fileName
    let role = policy |> Option.map _.Role
    let level = policy |> Option.bind (fun resolved -> resolved.Rules.TryFind "effects.ambient") |> Option.defaultValue Policy.Deny
    let capability = config.CapabilityOf rule
    if level = Policy.Forbid then Reported
    else
        match capability with
        | Some capability when owns fileName capability -> Owned capability.Name
        | _ when level = Policy.Allow || role = Some Policy.Host -> Host
        | _ ->
            match allowanceAt source line with
            | Some allowed -> Allowance allowed
            | None -> Reported

/// The finding text, written as an instruction to whoever edits the code: what to change and where.
let advice (config: GuardrailConfig.Config) fileName (rule: EffectRule) =
    let capability = config.CapabilityOf rule
    let claimed = config.AllCapabilities |> List.filter (owns fileName)
    let fix =
        match capability with
        // A blocking wait is a code shape with a direct rewrite: there is nothing to fake.
        | Some capability when capability.Env = "" && capability.Fake = "" -> $"Replace it with {rule.Replacement}."
        | Some capability when capability.Env = "" ->
            $"Replace it with {rule.Replacement}. {capability.Service} needs no environment change; tests use {capability.Fake}."
        | Some capability ->
            let example = if capability.Example = "" then "" else $" (for example `{capability.Example}`)"
            let fake = if capability.Fake = "" then "" else $" Tests supply {capability.Fake}."
            $"Replace it with {rule.Replacement}: add a `{capability.Field}` field to the environment record, implement "
            + $"{capability.Env}, and call the service{example}.{fake}"
        | None ->
            $"Replace it with {rule.Replacement}. If no service can do this, define a capability that owns the API in a "
            + $"<name>.capability.toml file (replaces = [ \"{formatMatch rule.Match}\" ]), name it with an "
            + "<AxialCapabilityDefinition> item, and move the raw call into a file whose Compile item claims it "
            + "with AxialCapability=\"<name>\"."
    let here =
        match claimed |> List.filter (fun owner -> Some owner.Name <> (capability |> Option.map _.Name)) with
        | [] -> ""
        | owners ->
            let names = owners |> List.map _.Name |> String.concat ", "
            $" This file claims the {names} capability, so it may call only that API; take what else it needs "
            + "as a parameter of its live implementation or through the environment."
    $"This call {rule.Message}. {fix}{here}"

/// The finding type of a guide-level capability: reported as information, never baselined or escalated.
[<Literal>]
let CapabilityGuide = "Axial Capability Guide"

let toMessage (config: GuardrailConfig.Config) fileName (rule: EffectRule) (range: range) : Message =
    let guide = config.CapabilityOf rule |> Option.exists (fun capability -> capability.Level = Guide)
    { Type = if guide then CapabilityGuide else "Axial Effect Boundary"
      Message = advice config fileName rule
      Code = "AXG001"
      Severity = if guide then Severity.Info else Severity.Error
      Range = range
      // Introducing an explicit service changes the containing function's environment and often its return type;
      // replacing only this expression is therefore not mechanically safe.
      Fixes = [] }

/// The `effectUses` record each mode adds to a file's EffectBoundary coverage: how many raw effect calls were allowed,
/// and why, so `axial check` shows leniency as a number instead of hiding it.
let record (verdicts: Verdict list) =
    let owned = verdicts |> List.choose (function Owned name -> Some name | _ -> None)
    let allowances = verdicts |> List.choose (function Allowance allowed -> Some allowed | _ -> None)
    let gaps = allowances |> List.choose (function Suppressions.Gap name -> Some name | _ -> None)
    let count verdict = verdicts |> List.filter ((=) verdict) |> List.length
    XElement(
        XName.Get "effectUses",
        XAttribute(XName.Get "owned", owned.Length),
        XAttribute(XName.Get "allowance", allowances.Length),
        XAttribute(XName.Get "why", allowances.Length - gaps.Length),
        XAttribute(XName.Get "host", count Host),
        XAttribute(XName.Get "reported", count Reported),
        (owned
         |> List.countBy id
         |> List.sort
         |> List.map (fun (name, uses) -> XElement(XName.Get "capability", XAttribute(XName.Get "name", name), XAttribute(XName.Get "owned", uses)))),
        (gaps
         |> List.countBy id
         |> List.sort
         |> List.map (fun (name, uses) -> XElement(XName.Get "gap", XAttribute(XName.Get "name", name), XAttribute(XName.Get "uses", uses)))))

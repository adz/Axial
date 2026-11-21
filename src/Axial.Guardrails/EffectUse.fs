/// How every raw effect call is judged, the same way in Fast and Full: allowed for a stated reason and counted, or
/// reported for the agent to fix. No file is exempt as a whole. A file that implements a capability may call only that
/// capability's API; every other effect in it is reported like anywhere else.
module Axial.Guardrails.EffectUse

open System
open System.IO
open System.Xml.Linq
open FSharp.Analyzers.SDK
open FSharp.Compiler.Text
open Axial.Guardrails.EffectCatalog

/// Why a raw effect call is allowed, or that it is not.
type Verdict =
    /// Inside the implementation of the capability that owns the API.
    | Owned of capability: string
    /// A reasoned `axial-allow-AXG001` comment in a file listed under `[boundaries]`.
    | Boundary
    /// A `host`-role file, or a role whose `effects.ambient` level is `allow`.
    | Host
    /// Not allowed: the agent must use the service, or declare the capability that owns the API.
    | Reported

let private projectName (config: GuardrailConfig.Config) =
    config.ProjectPath |> Option.map Path.GetFileNameWithoutExtension

let private fullPath (fileName: string) = try Some(Path.GetFullPath fileName) with _ -> None

/// Whether this file implements the capability. A declared capability lists its files; Axial's own capabilities are
/// implemented by their package, a `capabilities`-role project named after the capability's guardrail.
let owns (config: GuardrailConfig.Config) (role: Policy.Role option) fileName (capability: Capability) =
    match capability.Implementation with
    | [] -> role = Some Policy.Capabilities && projectName config = Some capability.Guardrail
    | files -> fullPath fileName |> Option.exists (fun path -> List.contains path files)

/// The verdict for one raw effect call at `line`.
let judge (config: GuardrailConfig.Config) fileName (source: ISourceText) line (rule: EffectRule) : Verdict =
    let policy = GuardrailConfig.resolvedFilePolicy config fileName
    let role = policy |> Option.map _.Role
    let level = policy |> Option.bind (fun resolved -> resolved.Rules.TryFind "effects.ambient") |> Option.defaultValue Policy.Deny
    let capability = config.CapabilityOf rule
    if level = Policy.Forbid then Reported
    else
        match capability with
        | Some capability when owns config role fileName capability -> Owned capability.Name
        | _ when level = Policy.Allow || role = Some Policy.Host -> Host
        | _ when Suppressions.isAllowed config.BoundaryFiles fileName source line "AXG001" -> Boundary
        | _ -> Reported

let private structural = set [ "blocking"; "concurrency"; "exception"; "cancellation"; "test-wait"; "scope" ]

/// The finding text, written as an instruction to whoever edits the code: what to change and where.
let advice (config: GuardrailConfig.Config) fileName (rule: EffectRule) =
    let role = GuardrailConfig.resolvedFilePolicy config fileName |> Option.map _.Role
    let capability = config.CapabilityOf rule
    let implementing =
        config.AllCapabilities |> List.tryFind (fun candidate -> owns config role fileName candidate)
    let fix =
        match capability with
        | Some capability when capability.Env = "" ->
            $"Replace it with {rule.Replacement}. The runtime provides {capability.Service}, so the environment does not "
            + $"change; tests control it with {capability.Fake}."
        | Some capability ->
            let example = if capability.Example = "" then "" else $" (for example `{capability.Example}`)"
            let fake = if capability.Fake = "" then "" else $" Tests supply {capability.Fake}."
            $"Replace it with {rule.Replacement}: add a `{capability.Field}` field to the environment record, implement "
            + $"{capability.Env}, and call the service{example}.{fake}"
        // Waits, raw concurrency and similar are code shapes with a direct rewrite, not effects a capability could own.
        | None when structural.Contains rule.Category -> $"Replace it with {rule.Replacement}."
        | None ->
            $"Replace it with {rule.Replacement}. If no service can do this, declare a capability that owns the API: add "
            + $"<AxialCapability Include=\"<name>\" Service=\"I<Name>\" Replaces=\"{formatMatch rule.Match}\" Implementation=\"<file>\" /> "
            + "to the project file and move the raw call into that file, where its uses are counted."
    let here =
        match implementing with
        | Some owner when owner.Name <> (capability |> Option.map _.Name |> Option.defaultValue "") ->
            $" This file implements the {owner.Name} capability, which may call only its own API; take what it needs "
            + "as a parameter of its live implementation or through the environment."
        | _ -> ""
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
    let count verdict = verdicts |> List.filter ((=) verdict) |> List.length
    let owned = verdicts |> List.choose (function Owned name -> Some name | _ -> None)
    XElement(
        XName.Get "effectUses",
        XAttribute(XName.Get "owned", owned.Length),
        XAttribute(XName.Get "boundary", count Boundary),
        XAttribute(XName.Get "host", count Host),
        XAttribute(XName.Get "reported", count Reported),
        owned
        |> List.countBy id
        |> List.sort
        |> List.map (fun (name, uses) -> XElement(XName.Get "capability", XAttribute(XName.Get "name", name), XAttribute(XName.Get "owned", uses))))

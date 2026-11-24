/// Each rule's short name: what people read in a finding and write in an allowance, the toml and the docs. The number
/// (AXG001) stays the build code, because editors and CI find build warnings by a code that ends in digits.
module Axial.Guardrails.RuleNames

open System
open System.Text.RegularExpressions

let all =
    [ "AXG001", "raw-effect"
      "AXG002", "stale-allow"
      "AXG003", "raise-in-flow"
      "AXG004", "shared-fixture"
      "AXG005", "dropped-cancel"
      "AXG006", "reflection-format"
      "AXG007", "fiber-resource"
      "AXG008", "escaped-scope"
      "AXG009", "raw-concurrency"
      "AXG010", "swallowed-exn"
      "AXG011", "cancel-none"
      "AXG012", "dropped-work"
      "AXG013", "guessed-wait"
      "AXG014", "task-surface" ]

let private byId = Map.ofList all
let private byName = all |> List.map (fun (id, name) -> name, id) |> Map.ofList

/// The rule's name, or the code itself when it has none.
let nameOf (code: string) = byId.TryFind(code.ToUpperInvariant()) |> Option.defaultValue code

/// The rule id a name stands for.
let idOf (name: string) = byName.TryFind(name.ToLowerInvariant())

/// An old numeric spelling, such as `AXG001`.
let isNumber (text: string) = Regex.IsMatch(text, @"^AXG[0-9]{3}$", RegexOptions.IgnoreCase)

/// A finding's text, led by its rule's name: `raw-effect: Replace File.ReadAllText with …`.
let label (code: string) (message: string) =
    match byId.TryFind(code.ToUpperInvariant()) with
    | Some name when not (message.StartsWith(name + ": ", StringComparison.Ordinal)) -> name + ": " + message
    | _ -> message

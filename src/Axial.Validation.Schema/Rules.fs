namespace Axial.Validation.Schema

open Axial.Validation

/// <summary>
/// A collection of contextual rules evaluated over an already-trusted model.
/// </summary>
/// <remarks>
/// <para>
/// Schema constraints describe field-local value requirements that can run during input parsing or intrinsic model
/// validation. A <c>RuleSet</c> is reserved for contextual requirements that need the completed model and may attach
/// failures anywhere in a diagnostics path.
/// </para>
/// <para>
/// Rule sets do not construct models. They evaluate the supplied model and either accept it or return path-aware
/// diagnostics.
/// </para>
/// </remarks>
type RuleSet<'model, 'error> =
    internal
        {
            Rules: ('model -> Result<unit, Diagnostics<'error>>) list
        }

/// <summary>Core authoring functions for contextual rule sets.</summary>
/// <remarks>
/// <para>
/// This module is the explicit core API for building rule sets. Higher-level rule builders can layer field paths,
/// messages, and domain-specific syntax over these functions without changing the underlying representation.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module Rules =
    let private ensureRule rule =
        if isNull (box rule) then
            nullArg (nameof rule)

    let private ensureRuleSet name (ruleSet: RuleSet<'model, 'error>) =
        if isNull (box ruleSet) then
            nullArg name

    let private ensureRuleSets ruleSets =
        if isNull (box ruleSets) then
            nullArg (nameof ruleSets)

        let ruleSets = ruleSets |> Seq.toList
        ruleSets |> List.iter (ensureRuleSet (nameof ruleSets))
        ruleSets

    let private ensureRules rules =
        if isNull (box rules) then
            nullArg (nameof rules)

        let rules = rules |> Seq.toList
        rules |> List.iter ensureRule
        rules

    /// <summary>Creates an empty contextual rule set.</summary>
    let empty<'model, 'error> : RuleSet<'model, 'error> =
        { Rules = [] }

    /// <summary>Creates a contextual rule set from one executable model rule.</summary>
    /// <param name="rule">A rule that accepts the model or returns path-aware diagnostics.</param>
    let create
        (rule: 'model -> Result<unit, Diagnostics<'error>>)
        : RuleSet<'model, 'error> =
        ensureRule rule
        { Rules = [ rule ] }

    /// <summary>Creates a contextual rule set from executable model rules in evaluation order.</summary>
    /// <param name="rules">Rules that accept the model or return path-aware diagnostics.</param>
    let ofSeq
        (rules: ('model -> Result<unit, Diagnostics<'error>>) seq)
        : RuleSet<'model, 'error> =
        { Rules = ensureRules rules }

    /// <summary>Creates a contextual rule set from executable model rules in evaluation order.</summary>
    /// <param name="rules">Rules that accept the model or return path-aware diagnostics.</param>
    let ofList
        (rules: ('model -> Result<unit, Diagnostics<'error>>) list)
        : RuleSet<'model, 'error> =
        ofSeq rules

    /// <summary>Appends two contextual rule sets, preserving left-to-right rule order.</summary>
    let append
        (left: RuleSet<'model, 'error>)
        (right: RuleSet<'model, 'error>)
        : RuleSet<'model, 'error> =
        ensureRuleSet (nameof left) left
        ensureRuleSet (nameof right) right
        { Rules = left.Rules @ right.Rules }

    /// <summary>Combines contextual rule sets in sequence, preserving rule order.</summary>
    let concat (ruleSets: RuleSet<'model, 'error> seq) : RuleSet<'model, 'error> =
        let ruleSets = ensureRuleSets ruleSets

        {
            Rules =
                ruleSets
                |> List.collect (fun ruleSet -> ruleSet.Rules)
        }

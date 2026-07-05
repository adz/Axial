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
    let private ensureString name value =
        if isNull value then
            nullArg name

        value

    let private diagnosticsAt path diagnostics =
        let rec attach path graph =
            match path with
            | [] -> graph
            | segment :: rest ->
                {
                    Errors = []
                    Children = Map.add segment (attach rest graph) Map.empty
                }

        attach path diagnostics

    let private ensureRule rule =
        if isNull (box rule) then
            nullArg (nameof rule)

    let private ensurePath path =
        if isNull (box path) then
            nullArg (nameof path)

        path

    let private ensureRuleSet name (ruleSet: RuleSet<'model, 'error>) =
        if isNull (box ruleSet) then
            nullArg name

        ruleSet

    let private ensureRuleSets ruleSets =
        if isNull (box ruleSets) then
            nullArg (nameof ruleSets)

        let ruleSets = ruleSets |> Seq.toList
        ruleSets |> List.iter (ensureRuleSet (nameof ruleSets) >> ignore)
        ruleSets

    let private ensureRules rules =
        if isNull (box rules) then
            nullArg (nameof rules)

        let rules = rules |> Seq.toList
        rules |> List.iter ensureRule
        rules

    let private mergeDiagnostics diagnostics =
        diagnostics |> List.reduce Diagnostics.merge

    /// <summary>Creates an empty contextual rule set.</summary>
    let empty<'model, 'error> : RuleSet<'model, 'error> =
        { Rules = [] }

    /// <summary>Creates a rule failure attached to the current diagnostics node.</summary>
    /// <param name="error">The rule error to attach.</param>
    /// <example>
    /// <code>
    /// let result = Rules.fail HighPriorityNeedsAssignee
    /// </code>
    /// </example>
    let fail (error: 'error) : Result<unit, Diagnostics<'error>> =
        Error(Diagnostics.singleton error)

    /// <summary>Creates a custom schema rule error with a stable code and display message.</summary>
    /// <param name="code">The stable machine-readable rule code.</param>
    /// <param name="message">The human-readable rule message.</param>
    /// <example>
    /// <code>
    /// let error = Rules.custom "ticket.assignee.required" "High-priority tickets need an assignee."
    /// </code>
    /// </example>
    let custom (code: string) (message: string) : SchemaError =
        SchemaError.Custom(ensureString (nameof code) code, Some(ensureString (nameof message) message))

    /// <summary>Creates a custom schema rule failure attached to the current diagnostics node.</summary>
    /// <param name="code">The stable machine-readable rule code.</param>
    /// <param name="message">The human-readable rule message.</param>
    /// <example>
    /// <code>
    /// let result = Rules.failCustom "ticket.assignee.required" "High-priority tickets need an assignee."
    /// </code>
    /// </example>
    let failCustom (code: string) (message: string) : Result<unit, Diagnostics<SchemaError>> =
        fail (custom code message)

    /// <summary>Creates a rule failure attached to the supplied diagnostics path.</summary>
    /// <param name="path">The diagnostics path that should receive the failure.</param>
    /// <param name="error">The rule error to attach.</param>
    /// <example>
    /// <code>
    /// let result = Rules.failAt [ PathSegment.Name "assignee" ] HighPriorityNeedsAssignee
    /// </code>
    /// </example>
    let failAt (path: Path) (error: 'error) : Result<unit, Diagnostics<'error>> =
        Error(Diagnostics.singleton error |> diagnosticsAt (ensurePath path))

    /// <summary>Creates a custom schema rule failure attached to the supplied diagnostics path.</summary>
    /// <param name="path">The diagnostics path that should receive the failure.</param>
    /// <param name="code">The stable machine-readable rule code.</param>
    /// <param name="message">The human-readable rule message.</param>
    /// <example>
    /// <code>
    /// let result =
    ///     Rules.failCustomAt
    ///         [ PathSegment.Name "assignee" ]
    ///         "ticket.assignee.required"
    ///         "High-priority tickets need an assignee."
    /// </code>
    /// </example>
    let failCustomAt
        (path: Path)
        (code: string)
        (message: string)
        : Result<unit, Diagnostics<SchemaError>> =
        failAt path (custom code message)

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

    /// <summary>Scopes a rule's diagnostics under the supplied path when the rule fails.</summary>
    /// <param name="path">The path segments to prefix to the rule's diagnostics.</param>
    /// <param name="rule">The rule whose failures should be scoped.</param>
    /// <example>
    /// <code>
    /// let scoped = needsReview |> Rules.at [ PathSegment.Name "approval"; PathSegment.Name "reviewer" ]
    /// </code>
    /// </example>
    let at
        (path: Path)
        (rule: 'model -> Result<unit, Diagnostics<'error>>)
        : 'model -> Result<unit, Diagnostics<'error>> =
        let path = ensurePath path
        ensureRule rule

        fun model ->
            match rule model with
            | Ok () -> Ok ()
            | Error diagnostics -> Error(diagnosticsAt path diagnostics)

    /// <summary>Scopes a rule's diagnostics under a named field when the rule fails.</summary>
    /// <param name="name">The field name to prefix to the rule's diagnostics.</param>
    /// <param name="rule">The rule whose failures should be scoped.</param>
    /// <example>
    /// <code>
    /// let scoped = needsAssignee |> Rules.name "assignee"
    /// </code>
    /// </example>
    let name
        (name: string)
        (rule: 'model -> Result<unit, Diagnostics<'error>>)
        : 'model -> Result<unit, Diagnostics<'error>> =
        at [ PathSegment.Name name ] rule

    /// <summary>Scopes a rule's diagnostics under a keyed branch when the rule fails.</summary>
    /// <param name="key">The branch key to prefix to the rule's diagnostics.</param>
    /// <param name="rule">The rule whose failures should be scoped.</param>
    /// <example>
    /// <code>
    /// let scoped = needsReview |> Rules.key "regional"
    /// </code>
    /// </example>
    let key
        (key: string)
        (rule: 'model -> Result<unit, Diagnostics<'error>>)
        : 'model -> Result<unit, Diagnostics<'error>> =
        at [ PathSegment.Key key ] rule

    /// <summary>Scopes a rule's diagnostics under an indexed branch when the rule fails.</summary>
    /// <param name="index">The branch index to prefix to the rule's diagnostics.</param>
    /// <param name="rule">The rule whose failures should be scoped.</param>
    /// <example>
    /// <code>
    /// let scoped = needsReview |> Rules.index 0
    /// </code>
    /// </example>
    let index
        (index: int)
        (rule: 'model -> Result<unit, Diagnostics<'error>>)
        : 'model -> Result<unit, Diagnostics<'error>> =
        at [ PathSegment.Index index ] rule

    /// <summary>Appends two contextual rule sets, preserving left-to-right rule order.</summary>
    let append
        (left: RuleSet<'model, 'error>)
        (right: RuleSet<'model, 'error>)
        : RuleSet<'model, 'error> =
        ensureRuleSet (nameof left) left |> ignore
        ensureRuleSet (nameof right) right |> ignore
        { Rules = left.Rules @ right.Rules }

    /// <summary>Combines contextual rule sets in sequence, preserving rule order.</summary>
    let concat (ruleSets: RuleSet<'model, 'error> seq) : RuleSet<'model, 'error> =
        let ruleSets = ensureRuleSets ruleSets

        {
            Rules =
                ruleSets
                |> List.collect (fun ruleSet -> ruleSet.Rules)
        }

    /// <summary>Evaluates contextual rules over an already-trusted model.</summary>
    /// <remarks>
    /// <para>
    /// The supplied model is not constructed, parsed, or transformed. Every rule is evaluated against the same trusted
    /// instance and any diagnostics are accumulated.
    /// </para>
    /// </remarks>
    /// <param name="ruleSet">The rule set to evaluate.</param>
    /// <param name="model">The already-trusted model to check.</param>
    let validate
        (ruleSet: RuleSet<'model, 'error>)
        (model: 'model)
        : Axial.Validation.Validation<'model, 'error> =
        let ruleSet = ensureRuleSet (nameof ruleSet) ruleSet

        let diagnostics =
            ruleSet.Rules
            |> List.choose (fun rule ->
                match rule model with
                | Ok () -> None
                | Error diagnostics -> Some diagnostics)

        match diagnostics with
        | [] -> Axial.Validation.Validation.ok model
        | failures -> failures |> mergeDiagnostics |> Axial.Validation.Validation.error

    /// <summary>Applies contextual rules to an already-trusted model, returning a plain result.</summary>
    /// <remarks>
    /// <para>
    /// The supplied model is returned unchanged on success. Rules never construct, parse, or transform the model;
    /// they only decide whether the same trusted instance is acceptable in the current context.
    /// </para>
    /// </remarks>
    /// <param name="ruleSet">The rule set to evaluate.</param>
    /// <param name="model">The already-trusted model to check.</param>
    /// <example>
    /// <code>
    /// match Rules.apply ticketRules ticket with
    /// | Ok trusted -> handle trusted
    /// | Error diagnostics -> reject diagnostics
    /// </code>
    /// </example>
    let apply
        (ruleSet: RuleSet<'model, 'error>)
        (model: 'model)
        : Result<'model, Diagnostics<'error>> =
        validate ruleSet model |> Axial.Validation.Validation.toResult

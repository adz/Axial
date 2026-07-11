namespace Axial.Schema

open Axial.Validation

/// <summary>Minimal helpers for contextual rules over an already-trusted model.</summary>
/// <remarks>
/// <para>
/// A contextual rule is a plain function: <c>'model -> Result&lt;unit, Diagnostics&lt;'error&gt;&gt;</c>. Schema
/// constraints and constructor invariants describe what is always true of a model; contextual rules describe
/// what a workflow, tenant, or feature flag additionally requires of a model that is already valid. There is no
/// rule-set container type: a set of rules is an ordinary list, and selecting which rules apply in which context
/// is ordinary F# — a <c>match</c> over your own context type, or a <c>Map</c>.
/// </para>
/// <para>
/// This module only supplies what plain functions can't: failure constructors that produce path-aware
/// diagnostics, scoping combinators that attach a rule's failures to a field path (preferably through a typed
/// <see cref="T:Axial.Schema.FieldRef`2" /> so names cannot drift from the schema), and <c>apply</c> to evaluate
/// a list of rules with accumulated diagnostics.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module ContextRules =
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

    let private ensureRules rules =
        if isNull (box rules) then
            nullArg (nameof rules)

        let rules = rules |> Seq.toList
        rules |> List.iter ensureRule
        rules

    /// <summary>Creates a rule failure attached to the current diagnostics node.</summary>
    /// <param name="error">The rule error to attach.</param>
    /// <example>
    /// <code>
    /// let result = ContextRules.fail HighPriorityNeedsAssignee
    /// </code>
    /// </example>
    let fail (error: 'error) : Result<unit, Diagnostics<'error>> =
        Error(Diagnostics.singleton error)

    /// <summary>Creates a custom schema rule error with a stable code and display message.</summary>
    /// <param name="code">The stable machine-readable rule code.</param>
    /// <param name="message">The human-readable rule message.</param>
    /// <example>
    /// <code>
    /// let error = ContextRules.custom "ticket.assignee.required" "High-priority tickets need an assignee."
    /// </code>
    /// </example>
    let custom (code: string) (message: string) : SchemaError =
        SchemaError.Custom(ensureString (nameof code) code, Some(ensureString (nameof message) message))

    /// <summary>Creates a custom schema rule failure attached to the current diagnostics node.</summary>
    /// <param name="code">The stable machine-readable rule code.</param>
    /// <param name="message">The human-readable rule message.</param>
    /// <example>
    /// <code>
    /// let result = ContextRules.failCustom "ticket.assignee.required" "High-priority tickets need an assignee."
    /// </code>
    /// </example>
    let failCustom (code: string) (message: string) : Result<unit, Diagnostics<SchemaError>> =
        fail (custom code message)

    /// <summary>Creates a rule failure attached to the supplied diagnostics path.</summary>
    /// <param name="path">The diagnostics path that should receive the failure.</param>
    /// <param name="error">The rule error to attach.</param>
    /// <example>
    /// <code>
    /// let result = ContextRules.failAt [ PathSegment.Name "assignee" ] HighPriorityNeedsAssignee
    /// </code>
    /// </example>
    let failAt (path: Path) (error: 'error) : Result<unit, Diagnostics<'error>> =
        Error(Diagnostics.singleton error |> diagnosticsAt (ensurePath path))

    /// <summary>Creates a rule failure attached to a schema field reference's diagnostics path.</summary>
    /// <param name="field">The schema field reference that should receive the failure.</param>
    /// <param name="error">The rule error to attach.</param>
    /// <example>
    /// <code>
    /// let result = ContextRules.failAtField Ticket.Fields.assignee HighPriorityNeedsAssignee
    /// </code>
    /// </example>
    let failAtField (field: FieldRef<'model, 'field>) (error: 'error) : Result<unit, Diagnostics<'error>> =
        if isNull (box field) then
            nullArg (nameof field)

        failAt field.Path error

    /// <summary>Scopes a rule's diagnostics under the supplied path when the rule fails.</summary>
    /// <param name="path">The path segments to prefix to the rule's diagnostics.</param>
    /// <param name="rule">The rule whose failures should be scoped.</param>
    /// <example>
    /// <code>
    /// let scoped = needsReview |> ContextRules.at [ PathSegment.Name "approval"; PathSegment.Name "reviewer" ]
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

    /// <summary>Scopes a rule's diagnostics under a schema field reference when the rule fails.</summary>
    /// <remarks>
    /// Unlike <c>ContextRules.name</c>, the path comes from a typed <see cref="T:Axial.Schema.FieldRef`2" />
    /// declared next to the schema, so the field name cannot silently drift from the schema's actual wire name.
    /// </remarks>
    /// <param name="field">The schema field reference whose path should scope the rule's diagnostics.</param>
    /// <param name="rule">The rule whose failures should be scoped.</param>
    /// <example>
    /// <code>
    /// let scoped = needsAssignee |> ContextRules.atField Ticket.Fields.assignee
    /// </code>
    /// </example>
    let atField
        (field: FieldRef<'model, 'field>)
        (rule: 'model -> Result<unit, Diagnostics<'error>>)
        : 'model -> Result<unit, Diagnostics<'error>> =
        if isNull (box field) then
            nullArg (nameof field)

        at field.Path rule

    /// <summary>Scopes a rule's diagnostics under a named field when the rule fails.</summary>
    /// <param name="name">The field name to prefix to the rule's diagnostics.</param>
    /// <param name="rule">The rule whose failures should be scoped.</param>
    /// <example>
    /// <code>
    /// let scoped = needsAssignee |> ContextRules.name "assignee"
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
    /// let scoped = needsReview |> ContextRules.key "regional"
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
    /// let scoped = needsReview |> ContextRules.index 0
    /// </code>
    /// </example>
    let index
        (index: int)
        (rule: 'model -> Result<unit, Diagnostics<'error>>)
        : 'model -> Result<unit, Diagnostics<'error>> =
        at [ PathSegment.Index index ] rule

    /// <summary>Applies contextual rules to an already-trusted model, accumulating any diagnostics.</summary>
    /// <remarks>
    /// The model is not constructed, parsed, or transformed. Every rule is evaluated against the same trusted
    /// instance; all failures merge into one diagnostics graph. An empty rule list accepts every model.
    /// </remarks>
    /// <param name="rules">The rules to evaluate, in order.</param>
    /// <param name="model">The already-trusted model to check.</param>
    /// <example>
    /// <code>
    /// let rulesFor stage =
    ///     match stage with
    ///     | ManagerReview -> [ needsAssignee; mustHaveDueDate ]
    ///     | FinalAudit -> [ mustHaveApprovalTrail ]
    ///     | Draft -> []
    ///
    /// match ContextRules.apply (rulesFor ticket.Stage) ticket with
    /// | Ok trusted -> approve trusted
    /// | Error diagnostics -> reject diagnostics
    /// </code>
    /// </example>
    let apply
        (rules: ('model -> Result<unit, Diagnostics<'error>>) list)
        (model: 'model)
        : Result<'model, Diagnostics<'error>> =
        let rules = ensureRules rules

        let failures =
            rules
            |> List.choose (fun rule ->
                match rule model with
                | Ok () -> None
                | Error diagnostics -> Some diagnostics)

        match failures with
        | [] -> Ok model
        | diagnostics -> Error(diagnostics |> List.reduce Diagnostics.merge)

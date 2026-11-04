/// AXG014 TaskSurface, shared by Fast and Full: where raw `task { }` and Task-returning signatures are reported, how
/// a computation expression composes, and the guidance text. AXG014 is a guide: findings are Info, never block a
/// build (not even under warnings-as-errors) and accept `// axial-note AXG014: <reason>` acknowledgements.
module Axial.Guardrails.TaskSurfaceSyntax

open FSharp.Analyzers.SDK
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

[<Literal>]
let Code = "AXG014"

/// Strict: every raw task expression and Task-returning public signature. Bridge: only a task expression that
/// composes; a thin bridge awaiting one .NET call is the intended shape.
type Tier =
    | Strict
    | Bridge
    | Off

/// Domain and service code is strict; capability and host files may bridge. Declared runtime files implement Flow on
/// raw tasks and are exempt. Test-role files run only with AxialGuardrailsInTests=all, and then as bridges: test
/// frameworks require Task-returning tests.
let tier (config: GuardrailConfig.Config) fileName =
    let role = GuardrailConfig.resolvedFilePolicy config fileName |> Option.map _.Role
    let allTests = System.String.Equals(config.InTests, "all", System.StringComparison.OrdinalIgnoreCase)
    match role with
    | _ when GuardrailConfig.isRuntimeFile config fileName -> Off
    | Some Policy.Test when not allTests -> Off
    | Some Policy.Test -> Bridge
    | Some(Policy.Capabilities | Policy.Host) -> Bridge
    | _ -> Strict

/// One `builder { body }` application and what its own body (not nested lambdas or computations) does.
type Expression =
    { Builder: SynExpr
      Range: range
      Awaits: int
      Loops: int
      Branches: int }

    member this.Composes = this.Awaits > 1 || this.Loops > 0 || this.Branches > 0

let private key (r: range) = r.StartLine, r.StartColumn, r.EndLine, r.EndColumn

/// Every computation-expression application in parse order, from ParsedInput.fold nodes (nearest parent first).
///
/// Branching is composition only when it decides what to await: an `if`/`match` that contains an await, or precedes
/// the first one. An `if` without `else` and without an await (argument validation) never does. Translating the one awaited result (a `match` on it, `try/with` mapping its exceptions) is what a
/// bridge is for, so `try/with` never counts as branching, though awaits in its handlers still count as awaits.
let expressions (nodes: (SyntaxNode list * SyntaxNode) list) : Expression list =
    let owner (path: SyntaxNode list) =
        path
        |> List.tryPick (function
            | SyntaxNode.SynExpr(SynExpr.App(argExpr = SynExpr.ComputationExpr _) as app) -> Some(Some(key app.Range))
            | SyntaxNode.SynExpr(SynExpr.Lambda _ | SynExpr.MatchLambda _) -> Some None
            | _ -> None)
        |> Option.flatten
    let awaits = System.Collections.Generic.Dictionary<_, ResizeArray<range>>()
    let branches = System.Collections.Generic.Dictionary<_, ResizeArray<range>>()
    // `if` without `else`: argument validation that throws or falls through, deciding nothing before the await.
    let guards = System.Collections.Generic.Dictionary<_, ResizeArray<range>>()
    let loops = System.Collections.Generic.Dictionary<_, int>()
    let add (table: System.Collections.Generic.Dictionary<_, ResizeArray<range>>) path range count =
        match owner path with
        | Some owner ->
            if not (table.ContainsKey owner) then table[owner] <- ResizeArray()
            for _ in 1 .. count do table[owner].Add range
        | None -> ()
    let applications = ResizeArray<SynExpr * range>()
    for path, node in nodes do
        match node with
        | SyntaxNode.SynExpr expr ->
            match expr with
            | SynExpr.App(funcExpr = builder; argExpr = SynExpr.ComputationExpr _) -> applications.Add((builder, expr.Range))
            | _ -> ()
            match expr with
            | SynExpr.LetOrUse letOrUse when letOrUse.IsBang -> add awaits path expr.Range letOrUse.Bindings.Length
            | SynExpr.DoBang _
            | SynExpr.YieldOrReturnFrom _
            | SynExpr.MatchBang _ -> add awaits path expr.Range 1
            | SynExpr.While _
            | SynExpr.For _
            | SynExpr.ForEach _ ->
                match owner path with
                | Some owner -> loops[owner] <- (match loops.TryGetValue owner with | true, n -> n + 1 | _ -> 1)
                | None -> ()
            | SynExpr.IfThenElse(elseExpr = None) -> add guards path expr.Range 1
            | SynExpr.IfThenElse _
            | SynExpr.Match _ -> add branches path expr.Range 1
            | _ -> ()
        | _ -> ()
    let get (table: System.Collections.Generic.Dictionary<_, ResizeArray<range>>) owner =
        match table.TryGetValue owner with | true, values -> List.ofSeq values | _ -> []
    [ for builder, range in applications ->
          let owner = key range
          let awaited = get awaits owner
          let first = awaited |> List.map (fun r -> r.Start) |> List.sortWith (fun a b -> compare (a.Line, a.Column) (b.Line, b.Column)) |> List.tryHead
          let deciding (branch: range) =
              awaited |> List.exists (fun a -> Range.rangeContainsRange branch a)
              || first |> Option.forall (fun first -> (branch.StartLine, branch.StartColumn) < (first.Line, first.Column))
          { Builder = builder
            Range = range
            Awaits = awaited.Length
            Loops = (match loops.TryGetValue owner with | true, n -> n | _ -> 0)
            Branches =
                (get branches owner |> List.filter deciding |> List.length)
                + (get guards owner |> List.filter (fun guard -> awaited |> List.exists (fun a -> Range.rangeContainsRange guard a)) |> List.length) } ]

/// The spelling of a builder expression: `task`, `backgroundTask`, `t`.
let rec builderName = function
    | SynExpr.Ident id -> Some id.idText
    | SynExpr.LongIdent(longDotId = id) -> Some(id.LongIdent |> List.map _.idText |> String.concat ".")
    | SynExpr.Paren(expr = inner)
    | SynExpr.TypeApp(expr = inner) -> builderName inner
    | _ -> None

let private isPublic = function
    | None
    | Some(SynAccess.Public _) -> true
    | Some _ -> false

let rec private finalReturn = function
    | SynType.Fun(returnType = result) -> finalReturn result
    | SynType.Paren(innerType = inner) -> finalReturn inner
    | t -> t

/// A declared signature: module-level let, type member or abstract slot, with its annotated final return type.
type Signature = { Name: string; Range: range; ReturnType: SynType }

/// Public module-level and member bindings with a return annotation, and public abstract slots. Class `let` bindings
/// are private to the type and never part of its surface. Overrides, interface
/// implementations, object expressions and anything inside a private or internal module or type are skipped: their
/// signature is not this file's public surface. Return types are syntax; the caller decides what they name.
let signatures (nodes: (SyntaxNode list * SyntaxNode) list) : Signature list =
    let enclosingPublic (path: SyntaxNode list) =
        path |> List.forall (function
            | SyntaxNode.SynModule(SynModuleDecl.NestedModule(moduleInfo = SynComponentInfo(accessibility = access))) -> isPublic access
            | SyntaxNode.SynTypeDefn(SynTypeDefn(typeInfo = SynComponentInfo(accessibility = access))) -> isPublic access
            | SyntaxNode.SynMemberDefn(SynMemberDefn.Interface _) -> false
            | SyntaxNode.SynExpr _ -> false
            | _ -> true)
    [ for path, node in nodes do
          match node, path with
          | SyntaxNode.SynBinding(SynBinding(accessibility = access; headPat = head; valData = SynValData(memberFlags = flags); returnInfo = Some(SynBindingReturnInfo(typeName = returnType)))),
            (SyntaxNode.SynModule(SynModuleDecl.Let _) | SyntaxNode.SynMemberDefn(SynMemberDefn.Member _)) :: _ when isPublic access && enclosingPublic path ->
              let overrides = flags |> Option.exists (fun flags -> flags.IsOverrideOrExplicitImpl)
              let named =
                  match head with
                  | SynPat.LongIdent(longDotId = id; accessibility = headAccess) when isPublic headAccess ->
                      id.LongIdent |> List.tryLast |> Option.map (fun ident -> ident.idText, ident.idRange)
                  | SynPat.Named(ident = SynIdent(ident, _); accessibility = headAccess) when isPublic headAccess -> Some(ident.idText, ident.idRange)
                  | _ -> None
              match named with
              | Some(name, range) when not overrides -> yield { Name = name; Range = range; ReturnType = finalReturn returnType }
              | _ -> ()
          | SyntaxNode.SynMemberDefn(SynMemberDefn.AbstractSlot(slotSig = SynValSig(ident = SynIdent(ident, _); synType = slotType; accessibility = access))), _
              when isPublic (access.SingleAccess()) && enclosingPublic path ->
              yield { Name = ident.idText; Range = ident.idRange; ReturnType = finalReturn slotType }
          | _ -> () ]

let private why = "Raw Task escapes Flow's cancellation, supervision and testability"

/// Strict-tier message for a task expression.
let expressionMessage builder =
    $"AXG014 guide: `{builder} {{ }}` in domain/service code. {why}. Write the logic as `flow {{ }}`; where it must await a .NET Task API, lift that one call at the edge with `Flow.fromTask (fun ct -> ...)` (or `Flow.fromValueTask`), ideally in a capability-role file."

/// Bridge-tier message for a task expression that composes.
let bridgeMessage builder (expression: Expression) =
    let parts =
        [ if expression.Awaits > 1 then $"{expression.Awaits} awaits"
          if expression.Loops > 0 then "a loop"
          if expression.Branches > 0 then "branching" ]
        |> String.concat ", "
    $"AXG014 guide: this `{builder} {{ }}` bridge composes ({parts}). {why}. Keep the bridge to one awaited .NET call lifted with `Flow.fromTask (fun ct -> ...)`, and move the sequencing, branching and loops into `flow {{ }}`."

/// Strict-tier message for a public Task/ValueTask-returning signature.
let signatureMessage name taskType =
    $"AXG014 guide: public `{name}` returns {taskType}. {why}, and every caller inherits it. Return a `Flow<'env, 'error, 'value>` and let the host run it (`flow.StartAsTask(env, ct)`); lift inbound .NET tasks with `Flow.fromTask`."

/// The reason of a valid `axial-note AXG014` on the finding's line or the line above.
let acknowledgement (source: ISourceText) (range: range) =
    Suppressions.allNotes source
    |> List.tryFind (fun note -> note.RuleId = Some Code && note.Reason <> "" && (note.Line = range.StartLine || note.Line + 1 = range.StartLine))
    |> Option.map _.Reason

/// A guide finding; a valid note keeps it visible as acknowledged.
let finding (source: ISourceText) kind (range: range) (text: string) : Message =
    { Type = kind
      Code = Code
      Range = range
      Message = (match acknowledgement source range with Some reason -> $"{text} Acknowledged: {reason}" | None -> text)
      Severity = Severity.Info
      Fixes = [] }

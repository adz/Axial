/// Deliberately conservative syntax advice. Names are candidates, never symbol identities.
module Axial.Guardrails.FastSyntax

open FSharp.Analyzers.SDK
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open EffectCatalog

let rules = [ "EffectBoundary"; "RaiseInFlow"; "Fixture"; "ReflectionFormatting"; "RawConcurrency"; "WaitForFacts"; "TaskSurface" ]

let private names (ids: Ident list) = ids |> List.map _.idText |> String.concat "."
let rec private name = function
    | SynExpr.Ident id -> Some id.idText
    | SynExpr.LongIdent(longDotId = id) -> Some(names id.LongIdent)
    | SynExpr.TypeApp(expr = inner)
    | SynExpr.Paren(expr = inner) -> name inner
    | _ -> None

let rec private strip = function
    | SynExpr.Paren(expr = inner)
    | SynExpr.Typed(expr = inner) -> strip inner
    | expr -> expr

/// The member a call or property chain ends with: `x.GetAwaiter`, `(f x).GetAwaiter`.
let rec private lastMember = function
    | SynExpr.LongIdent(longDotId = id) when id.LongIdent.Length > 1 -> Some (List.last id.LongIdent).idText
    | SynExpr.DotGet(longDotId = id) -> id.LongIdent |> List.tryLast |> Option.map _.idText
    | SynExpr.TypeApp(expr = inner)
    | SynExpr.Paren(expr = inner) -> lastMember inner
    | _ -> None

let private typeName = function
    | SynType.LongIdent id -> Some(names id.LongIdent)
    | SynType.App(typeName = SynType.LongIdent id) -> Some(names id.LongIdent)
    | _ -> None

// F# auto-opens these, and spells FSharpAsync as Async.
let private implicitlyOpened = set [ "Microsoft.FSharp.Core"; "Microsoft.FSharp.Control"; "Microsoft.FSharp.Collections" ]
let private sourceNames = Map [ "Microsoft.FSharp.Control.FSharpAsync", "Microsoft.FSharp.Control.Async" ]

/// Spellings of a CLR type name that can resolve to it: fully or global-qualified, or a suffix whose namespace
/// prefix the file opens (`open System` permits `IO.File`). Unopened short names are never claimed.
let private spellingsIn (opened: Set<string>) (full: string) =
    let full = full.Split('`')[0]
    let full = sourceNames.TryFind full |> Option.defaultValue full
    let parts = full.Split('.')
    [ yield full
      yield "global." + full
      for index in 1 .. parts.Length - 1 do
          if opened.Contains(String.concat "." parts[.. index - 1]) then yield String.concat "." parts[index..] ]

/// Namespaces and modules opened anywhere in the file, including those declared by it and relative opens.
let private openedNamespaces nodes =
    let mutable opened = implicitlyOpened
    // A relative open resolves against an earlier open as written. Combining with derived entries instead grows the
    // set exponentially in the number of opens (a 30-open file exhausted memory).
    let written = ResizeArray<string>()
    let seen = System.Collections.Generic.HashSet<string>()
    for _, node in nodes do
        let add text =
            if seen.Add text then
                opened <- written |> Seq.fold (fun result prefix -> Set.add (prefix + "." + text) result) (Set.add text opened)
                written.Add text
        match node with
        | SyntaxNode.SynModule(SynModuleDecl.Open(target = SynOpenDeclTarget.ModuleOrNamespace(longId = id))) ->
            let text = names id.LongIdent
            add (if text.StartsWith("global.", System.StringComparison.Ordinal) then text.Substring 7 else text)
        | SyntaxNode.SynModuleOrNamespace(SynModuleOrNamespace(longId = ids)) ->
            for count in 1 .. ids.Length do add (names (List.take count ids))
        | _ -> ()
    opened

/// Names bound anywhere in the file. A shadowed or aliased spelling never claims identity.
let private boundNames nodes =
    let shadowed = System.Collections.Generic.HashSet<string>()
    let first (ids: Ident list) = ids |> List.tryHead |> Option.iter (fun id -> shadowed.Add id.idText |> ignore)
    for _, node in nodes do
        match node with
        | SyntaxNode.SynPat(SynPat.Named(ident = SynIdent(id, _))) -> shadowed.Add id.idText |> ignore
        | SyntaxNode.SynPat(SynPat.LongIdent(longDotId = id)) -> first id.LongIdent
        | SyntaxNode.SynModule(SynModuleDecl.ModuleAbbrev(ident = id)) -> shadowed.Add id.idText |> ignore
        | SyntaxNode.SynModule(SynModuleDecl.NestedModule(moduleInfo = SynComponentInfo(longId = ids))) -> first ids
        | SyntaxNode.SynModuleOrNamespace(SynModuleOrNamespace(longId = ids)) -> first ids
        | SyntaxNode.SynTypeDefn(SynTypeDefn(typeInfo = SynComponentInfo(longId = ids))) -> first ids
        | _ -> ()
    shadowed

/// The module path (`Axial.Tests.TestSupport`) and binding names enclosing a node.
let private enclosing (path: SyntaxNode list) =
    let modules =
        path |> List.rev |> List.collect (function
            | SyntaxNode.SynModuleOrNamespace(SynModuleOrNamespace(longId = ids)) -> [ names ids ]
            | SyntaxNode.SynModule(SynModuleDecl.NestedModule(moduleInfo = SynComponentInfo(longId = ids))) -> [ names ids ]
            | _ -> [])
        |> String.concat "."
    let bindings =
        path |> List.choose (function
            | SyntaxNode.SynBinding(SynBinding(headPat = SynPat.LongIdent(longDotId = id))) -> Some(names id.LongIdent)
            | SyntaxNode.SynBinding(SynBinding(headPat = SynPat.Named(ident = SynIdent(id, _)))) -> Some id.idText
            | _ -> None)
    modules, bindings

/// A raw task site by Fast's syntax: a `task`/`backgroundTask` expression or a public signature annotated to return
/// Task/ValueTask. `Guide` is the AXG014 text when the file's role and tier report the site.
type TaskSite = { Range: range; Guide: string option }

let private taskSitesIn (config: GuardrailConfig.Config) fileName (opened: Set<string>) (shadowed: System.Collections.Generic.HashSet<string>) nodes =
    let unshadowed (text: string) = not (shadowed.Contains(text.Split('.')[0]))
    let spells full (text: string) = unshadowed text && List.contains text (spellingsIn opened full)
    let inScope =
        config.EnabledGuardrails.Contains CoreGuardrail
        && GuardrailConfig.runsInProject config TaskSurfaceSyntax.Code
        && not (GuardrailConfig.isSkippedSource config fileName)
    let tier = if inScope then TaskSurfaceSyntax.tier config fileName else TaskSurfaceSyntax.Off
    // Ply and TaskBuilder.fs define their own `task` under FSharp.Control.Tasks. Full reports only FSharp.Core's
    // builders, so Fast never claims a `task` such an open may rebind (Fast stays a subset of Full).
    let foreignTask = opened |> Set.exists (fun name -> name = "FSharp.Control.Tasks" || name.StartsWith("FSharp.Control.Tasks.", System.StringComparison.Ordinal))
    [ for expression in TaskSurfaceSyntax.expressions nodes do
          match TaskSurfaceSyntax.builderName expression.Builder with
          | Some("task" | "backgroundTask" as builder) when unshadowed builder && not (builder = "task" && foreignTask) ->
              let guide =
                  match tier with
                  | TaskSurfaceSyntax.Strict -> Some(TaskSurfaceSyntax.expressionMessage builder)
                  | TaskSurfaceSyntax.Bridge when expression.Composes -> Some(TaskSurfaceSyntax.bridgeMessage builder expression)
                  | _ -> None
              yield { Range = expression.Builder.Range; Guide = guide }
          | _ -> ()
      for signature in TaskSurfaceSyntax.signatures nodes do
          let returned =
              typeName signature.ReturnType |> Option.bind (fun text ->
                  let generic = match signature.ReturnType with SynType.App _ -> true | _ -> false
                  if spells "System.Threading.Tasks.Task" text then Some(if generic then "Task<'T>" else "Task")
                  elif spells "System.Threading.Tasks.ValueTask" text then Some(if generic then "ValueTask<'T>" else "ValueTask")
                  else None)
          match returned with
          | Some taskType ->
              let guide = if tier = TaskSurfaceSyntax.Strict then Some(TaskSurfaceSyntax.signatureMessage signature.Name taskType) else None
              yield { Range = signature.Range; Guide = guide }
          | None -> () ]

/// One file's raw task sites and AXG014 guides, counted as Fast reports them (for `axial check`'s trend).
type TaskSiteCount = { Sites: int; Findings: int; Acknowledged: int }

let taskSiteCount (config: GuardrailConfig.Config) fileName (source: ISourceText) tree =
    let nodes = ParsedInput.fold (fun nodes path node -> (path, node) :: nodes) [] tree |> List.rev
    let sites =
        taskSitesIn config fileName (openedNamespaces nodes) (boundNames nodes) nodes
        |> List.distinctBy (fun site -> site.Range.StartLine, site.Range.StartColumn)
    let guided = sites |> List.filter _.Guide.IsSome
    { Sites = sites.Length
      Findings = guided.Length
      Acknowledged = guided |> List.filter (fun site -> (TaskSurfaceSyntax.acknowledgement source site.Range).IsSome) |> List.length }

let analyzeRule requested (config: GuardrailConfig.Config) fileName (source: ISourceText) tree =
    let wants rule = requested |> Option.forall ((=) rule)
    let nodes = ParsedInput.fold (fun nodes path node -> (path, node) :: nodes) [] tree |> List.rev
    let opened = openedNamespaces nodes
    let shadowed = boundNames nodes
    let spellingCache = System.Collections.Generic.Dictionary<string, string list>()
    let spellings full =
        match spellingCache.TryGetValue full with
        | true, cached -> cached
        | _ ->
            let computed = spellingsIn opened full
            spellingCache[full] <- computed
            computed
    let unshadowed (text: string) = not (shadowed.Contains(text.Split('.')[0]))
    let spells full (text: string) = unshadowed text && List.contains text (spellings full)
    let spellsMember full (memberName: string) (text: string) =
        text.EndsWith("." + memberName, System.StringComparison.Ordinal)
        && spells full (text.Substring(0, text.Length - memberName.Length - 1))
    let exactEffects = System.Collections.Generic.Dictionary<string, ResizeArray<EffectRule>>()
    let wildEffects = ResizeArray<string * EffectRule>()
    // Instance members on awaiters are recognized only in the unmistakable `.GetAwaiter().GetResult()` chain.
    let awaiterEffects = System.Collections.Generic.Dictionary<string, ResizeArray<EffectRule>>()
    if wants "EffectBoundary" then
        let add (table: System.Collections.Generic.Dictionary<_, ResizeArray<_>>) key rule =
            if not (table.ContainsKey key) then table[key] <- ResizeArray()
            table[key].Add rule
        for rule in config.Rules do
            match rule.Match with
            | ConstructorOf full -> for spelling in spellings full do add exactEffects spelling rule
            | MembersOf(full, members) when (full.Split('`')[0]).EndsWith("Awaiter", System.StringComparison.Ordinal) ->
                for memberName in members do add awaiterEffects memberName rule
            | MembersOf(full, members) ->
                for spelling in spellings full do
                    for memberName in members do add exactEffects (spelling + "." + memberName.Replace("get_", "")) rule
            | AnyMemberOf full -> for spelling in spellings full do wildEffects.Add(spelling + ".", rule)
    let policy = GuardrailConfig.resolvedFilePolicy config fileName
    let role = policy |> Option.map _.Role
    let level = policy |> Option.bind (fun p -> p.Rules.TryFind "effects.ambient") |> Option.defaultValue Policy.Deny
    // Some true for a flow computation, Some false for any other computation expression.
    let flowComputation = function
        | SyntaxNode.SynExpr(SynExpr.App(funcExpr = func; argExpr = SynExpr.ComputationExpr _)) -> Some(name func = Some "flow" && unshadowed "flow")
        | _ -> None
    let insideFlow path = path |> List.exists (fun node -> flowComputation node = Some true)
    // A raise belongs to the nearest enclosing computation or lambda: inside a nested task/async it is that
    // builder's exception (Flow.fromTask converts it), not a flow bypass.
    let directlyInFlow path =
        path |> List.tryPick (function
            | SyntaxNode.SynExpr(SynExpr.Lambda _ | SynExpr.MatchLambda _) -> Some false
            | node -> flowComputation node)
        |> Option.defaultValue false
    let hint code range text =
        { Type = "Axial fast/unverified hint"; Code = code; Range = range; Message = "[fast/unverified] " + text
          Severity = Severity.Info; Fixes = [] }
    let allowed code (range: range) =
        GuardrailConfig.runsInProject config code
        && not (GuardrailConfig.isSkippedSource config fileName)
        && (code = "AXG001" && level = Policy.Forbid || not (Suppressions.isAllowed config.BoundaryFiles fileName source range.StartLine code))
    let core = config.EnabledGuardrails.Contains CoreGuardrail
    let rawExempt = if wants "RawConcurrency" then GuardrailConfig.exemptsRawConcurrency config fileName else false
    let findings = ResizeArray<string * Message>()
    let add rule code range text = if allowed code range then findings.Add(rule, hint code range text)
    if core && wants "Fixture" then
        let remaining = System.Collections.Generic.Dictionary<string, int>()
        for entry in config.Baseline do
            if entry.RuleId = "AXG004" && entry.File = GuardrailConfig.relativeFile config fileName then remaining[entry.Fingerprint] <- entry.Count
        for range in FixtureSyntax.ranges tree do
            if allowed "AXG004" range then
                let finding = FixtureAnalyzer.message range
                let fingerprint = GuardrailConfig.fingerprint source finding
                let count = match remaining.TryGetValue fingerprint with true, count -> count | _ -> 0
                let baselined = count > 0
                if baselined then remaining[fingerprint] <- count - 1
                // Ordinary (Fast) builds never fail on guidance: exact fixture findings warn; Full keeps policy severity.
                findings.Add("Fixture", { finding with Message = "[fast hint; syntax-exact] " + finding.Message + (if baselined then " (baselined)" else ""); Severity = Severity.Warning })
    let effectFile = level <> Policy.Allow && (level = Policy.Forbid || (role <> Some Policy.Capabilities && role <> Some Policy.Host))
    let reportEffects path (expr: SynExpr) (candidates: EffectRule seq) =
        for rule in Seq.distinct candidates do
            let scoped = match rule.Scope with Anywhere -> true | InsideFlow -> insideFlow path | OutsideFlow -> not (insideFlow path)
            if scoped then add "EffectBoundary" "AXG001" expr.Range (EffectBoundaryAnalyzer.toMessage rule expr.Range).Message
    // Mirrors WaitForFacts' syntax: Thread.Sleep anywhere; Task.Delay/Async.Sleep only when awaited directly; never an
    // infinite wait for cancellation, nor the shared bounded-polling helpers.
    let delayCall expr =
        match strip expr with
        | SynExpr.App(funcExpr = func; argExpr = argument) ->
            name func |> Option.bind (fun text ->
                if spellsMember "System.Threading.Tasks.Task" "Delay" text then Some(true, argument)
                elif spellsMember "Microsoft.FSharp.Control.FSharpAsync" "Sleep" text then Some(false, argument)
                else None)
        | _ -> None
    // An infinite first argument; Task.Delay must also take the cancellation token (a two-argument call).
    let infinite argument =
        let first, cancellable =
            match strip argument with
            | SynExpr.Tuple(exprs = first :: _) -> strip first, true
            | single -> single, false
        let isInfinite =
            match first with
            | SynExpr.Const(SynConst.Int32 -1, _) -> true
            | candidate ->
                name candidate |> Option.exists (fun text ->
                    spellsMember "System.Threading.Timeout" "Infinite" text || spellsMember "System.Threading.Timeout" "InfiniteTimeSpan" text)
        isInfinite, cancellable
    let guessedDelay expr =
        match delayCall expr with
        | Some(isTask, argument) ->
            let isInfinite, cancellable = infinite argument
            not (isInfinite && (cancellable || not isTask))
        | None -> false
    let approvedHelper path =
        let modules, bindings = enclosing path
        modules = "Axial.Tests.TestSupport" && bindings |> List.exists (fun b -> b = "waitUntil" || b = "occupyThread")
    for path, node in nodes do
        match node with
        | SyntaxNode.SynExpr expr ->
            let target =
                match expr with
                | SynExpr.LongIdent _ -> name expr |> Option.map (fun n -> n, false)
                | SynExpr.New(targetType = typ) -> typeName typ |> Option.map (fun n -> n, true)
                | SynExpr.App(funcExpr = func) -> name func |> Option.map (fun n -> n, false)
                | _ -> None
            match target with
            | Some(text, constructor) when unshadowed text ->
                if wants "EffectBoundary" && effectFile then
                    reportEffects path expr [
                        match exactEffects.TryGetValue text with
                        | true, entries -> yield! entries
                        | _ -> ()
                        if not constructor then
                            for prefix, rule in wildEffects do
                                // BCL members are PascalCase; `Console.live` is an F# module function, not System.Console.
                                if text.StartsWith(prefix, System.StringComparison.Ordinal) && text.Length > prefix.Length
                                   && System.Char.IsUpper text[prefix.Length] then yield rule ]
                if core then
                    if wants "RaiseInFlow" && directlyInFlow path && RaiseInFlowAnalyzer.raiseNames.Contains text then
                        add "RaiseInFlow" "AXG003" expr.Range "A direct raise in flow bypasses typed errors. Use Flow.fail for a typed failure, or Flow.die for a defect; Full checks the error type."
                    let raw =
                        [ "System.Threading.Thread"; "System.Threading.CancellationTokenSource"; "System.Threading.Tasks.Task"; "System.Threading.Tasks.TaskCompletionSource" ]
                        |> List.exists (fun full -> spells full text)
                        || (not constructor
                            && (spellsMember "System.Threading.Tasks.Task" "Run" text
                                || spellsMember "System.Threading.Tasks.Task.Factory" "StartNew" text
                                || (text.Contains '.' && spells "System.Threading.ThreadPool" (text.Substring(0, text.LastIndexOf '.')))))
                    if wants "RawConcurrency" && not rawExempt && raw then
                        add "RawConcurrency" "AXG009" expr.Range "Use Flow concurrency rather than raw TPL coordination; move an OS bridge into a capability-role module."
                    if wants "WaitForFacts" && role = Some Policy.Test && not constructor && spellsMember "System.Threading.Thread" "Sleep" text
                       && (match expr with SynExpr.App _ -> true | _ -> false) && not (approvedHelper path) then
                        add "WaitForFacts" "AXG013" expr.Range "Wait for a fact, not elapsed time. Complete a Deferred or TaskCompletionSource and wait with a deadline, or step the test clock. Full checks cancellation waits and timeout constants."
            | _ -> ()
            if wants "EffectBoundary" && effectFile then
                match expr with
                | SynExpr.DotGet(expr = inner; longDotId = id) ->
                    match strip inner, id.LongIdent with
                    | SynExpr.App(funcExpr = awaiter), [ memberName ] when lastMember awaiter = Some "GetAwaiter" ->
                        match awaiterEffects.TryGetValue memberName.idText with
                        | true, entries -> reportEffects path expr entries
                        | _ -> ()
                    | _ -> ()
                | _ -> ()
            if core && wants "WaitForFacts" && role = Some Policy.Test && not (approvedHelper path) then
                let awaited =
                    match expr with
                    | SynExpr.DoBang(inner, _, _) -> [ inner ]
                    | SynExpr.LetOrUse letOrUse when letOrUse.IsBang -> letOrUse.Bindings |> List.map (fun (SynBinding(expr = rhs)) -> rhs)
                    | _ -> []
                for delay in awaited do
                    if guessedDelay delay then
                        add "WaitForFacts" "AXG013" delay.Range "Wait for a fact, not elapsed time. Complete a Deferred or TaskCompletionSource and wait with a deadline, or step the test clock. Full checks timeout constants."
            if core && wants "ReflectionFormatting" then
                match expr with
                | SynExpr.App(funcExpr = func; argExpr = SynExpr.Const(SynConst.String(format, _, _), _)) when name func |> Option.exists (fun n -> unshadowed n && List.contains n ["sprintf"; "printf"; "printfn"; "eprintf"; "eprintfn"; "failwithf"; "fprintf"; "fprintfn"]) ->
                    if System.Text.RegularExpressions.Regex.IsMatch(format.Replace("%%", ""), @"%\+?A") then
                        add "ReflectionFormatting" "AXG006" expr.Range "Structural %A formatting can require runtime reflection. Use explicit formatting or a typed renderer."
                | _ -> ()
        | _ -> ()
    // AXG014: CE name and signature text only. Aliased builders and abbreviated return types are Full-only.
    if core && wants "TaskSurface" then
        for site in taskSitesIn config fileName opened shadowed nodes do
            site.Guide |> Option.iter (fun text ->
                let message = TaskSurfaceSyntax.finding source "Axial fast/unverified hint" site.Range text
                findings.Add("TaskSurface", { message with Message = "[fast/unverified] " + message.Message }))
    findings |> Seq.distinctBy (fun (rule, m) -> rule, m.Range.StartLine, m.Range.StartColumn) |> Seq.toList

let analyze config fileName source tree = analyzeRule None config fileName source tree

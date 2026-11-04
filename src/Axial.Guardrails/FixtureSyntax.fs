/// Parse-only fixture detection shared by Fast and Full; no typed context is required.
module Axial.Guardrails.FixtureSyntax

open FSharp.Compiler.Syntax

let private testAttributeNames = set [ "Fact"; "Theory"; "Property" ]

let private attributeShortName (attr: SynAttribute) : string =
    let name = attr.TypeName.LongIdent |> List.last |> (fun i -> i.idText)
    if name.EndsWith("Attribute") then name.Substring(0, name.Length - "Attribute".Length) else name

let private hasTestAttribute (attributeLists: SynAttributeList list) : bool =
    attributeLists
    |> List.collect (fun list -> list.Attributes)
    |> List.exists (fun a -> Set.contains (attributeShortName a) testAttributeNames)

/// A module-level binding is a "fixture" risk when it takes no arguments (so it's a value,
/// computed once and shared, not a function re-invoked per call) and its right-hand side isn't a
/// bare constant (`let tolerance = 0.0001` isn't the shared-mutable-state hazard this targets) or
/// a point-free function (`let f = function | ... -> ...` has no parameter in its binding
/// pattern, so SynValInfo reports arity 0, but it's a stateless function value like any other).
let private isFixtureRisk (binding: SynBinding) : bool =
    let (SynBinding(valData = valData; expr = expr; attributes = attrs)) = binding
    let arity = valData.SynValInfo.CurriedArgInfos.Length

    let isSafeShape =
        match expr with
        | SynExpr.Const _
        | SynExpr.Lambda _
        | SynExpr.MatchLambda _ -> true
        | _ -> false

    arity = 0 && not isSafeShape && not (hasTestAttribute attrs)

let rec private rawDeclRanges (decls: SynModuleDecl list) =
    let isTestModule =
        decls
        |> List.exists (function
            | SynModuleDecl.Let(bindings = bindings) ->
                bindings |> List.exists (fun (SynBinding(attributes = attrs)) -> hasTestAttribute attrs)
            | _ -> false)
    [ if isTestModule then
          for declaration in decls do
              match declaration with
              | SynModuleDecl.Let(bindings = bindings) ->
                  for (SynBinding(range = range) as binding) in bindings do
                      if isFixtureRisk binding then yield range
              | _ -> ()
      for declaration in decls do
          match declaration with
          | SynModuleDecl.NestedModule(decls = nested) -> yield! rawDeclRanges nested
          | _ -> () ]

let ranges tree =
    match tree with
    | ParsedInput.ImplFile(ParsedImplFileInput(contents = modules)) ->
        modules |> List.collect (fun (SynModuleOrNamespace(decls = decls)) -> rawDeclRanges decls)
    | _ -> []

module Axial.Tool.Parser.Program

open System
open System.Collections
open Microsoft.FSharp.Reflection
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

let private checker = FSharpChecker.Create()

let private children (value: obj) =
    if isNull value || value :? string then [||]
    elif value :? IEnumerable then (value :?> IEnumerable) |> Seq.cast<obj> |> Seq.toArray
    else
        let t=value.GetType()
        if FSharpType.IsUnion(t,true) then FSharpValue.GetUnionFields(value,t,true) |> snd
        elif FSharpType.IsRecord(t,true) then FSharpValue.GetRecordFields(value,true)
        elif t.Namespace<>null && t.Namespace.StartsWith("FSharp.Compiler.Syntax",StringComparison.Ordinal) then
            t.GetProperties() |> Array.filter _.CanRead |> Array.choose(fun p->try Some(p.GetValue value) with _->None)
        else [||]

let private descendants<'a> (value: obj) =
    let found=ResizeArray<'a>()
    let rec walk (item: obj) =
        if not(isNull item) then
            match item with :? 'a as x -> found.Add x | _ -> ()
            for child in children item do walk child
    walk value
    found |> Seq.toList

let rec private headName = function
    | SynExpr.Ident id -> Some id.idText
    | SynExpr.LongIdent(longDotId=ids) -> ids.LongIdent |> List.map _.idText |> String.concat "." |> Some
    | SynExpr.App(funcExpr=f) | SynExpr.TypeApp(expr=f) | SynExpr.Paren(expr=f) | SynExpr.Typed(expr=f) -> headName f
    | _ -> None

let private bindingName (SynBinding(headPat=pattern)) =
    descendants<Ident>(box pattern) |> List.tryLast |> Option.map _.idText

let private attributeNames (SynBinding(attributes=attributes)) =
    descendants<Ident>(box attributes) |> List.map _.idText |> Set.ofList

let private countParsed (tree: ParsedInput) =
    let bindings=descendants<SynBinding>(box tree)
    let aliases =
        bindings |> List.choose(fun (SynBinding(expr=expr) as binding) ->
            match bindingName binding, headName expr with
            | Some name, Some target when target.StartsWith("Assert.") || target.StartsWith("Expect.") -> Some name
            | _ -> None) |> Set.ofList
    let assertion expr =
        match expr with
        | SynExpr.App _ ->
            match headName expr with
            | Some name when name.StartsWith("Assert.") || name.StartsWith("Expect.") || Set.contains name aliases || name="test" -> true
            | _ -> false
        | _ -> false
    let countBody body =
        descendants<SynExpr>(box body) |> List.filter assertion
        |> List.distinctBy(fun expr -> headName expr,expr.Range.StartLine,expr.Range.StartColumn)
        |> List.length
    let attributed =
        bindings |> List.choose(fun (SynBinding(expr=body) as binding) ->
            let attrs=attributeNames binding
            if ["Fact";"FactAttribute";"Theory";"TheoryAttribute";"Test";"TestAttribute";"Property";"PropertyAttribute"] |> List.exists attrs.Contains
            then Some body else None)
    let expecto =
        descendants<SynExpr>(box tree)
        |> List.choose(fun expr -> match headName expr with Some "testCase" | Some "testList" -> Some expr | _ -> None)
    let bodies=attributed @ expecto
    bodies.Length, bodies |> List.sumBy countBody

[<EntryPoint>]
let main argv =
    match argv with
    | [|fileName; defines|] ->
        let options={ FSharpParsingOptions.Default with SourceFiles=[|fileName|]; ConditionalDefines=defines.Split(',',StringSplitOptions.RemoveEmptyEntries) |> Array.toList }
        let parsed=checker.ParseFile(fileName,SourceText.ofString(IO.File.ReadAllText fileName),options) |> Async.RunSynchronously
        let tests,assertions=countParsed parsed.ParseTree
        printfn "%d\t%d" tests assertions
        0
    | _ -> eprintfn "usage: Axial.Tool.Parser <file> <comma-separated-defines>"; 2

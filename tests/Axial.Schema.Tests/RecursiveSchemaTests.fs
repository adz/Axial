namespace Axial.Schema.Tests

open Axial

open Axial.Schema.Json
open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

module RecursiveSchemaTests =
    type Category = { Name: string; Children: Category list }

    let private categorySchema () =
        let rec schema: Lazy<Schema<Category>> =
            lazy
                (Schema.define<Category>
                 |> fieldWith Schema.text "name" _.Name
                 |> fieldWith (Schema.listWith (Schema.defer (fun () -> schema.Value))) "children" _.Children
                 |> construct (fun name children -> { Name = name; Children = children }))

        schema.Value

    let private rawCategory name children =
        Data.objectOfMap (Map.ofList [ "name", Data.Text name; "children", Data.List children ])

    let private sample =
        { Name = "root"
          Children =
            [ { Name = "one"; Children = [] }
              { Name = "two"; Children = [ { Name = "leaf"; Children = [] } ] } ] }

    [<Fact>]
    let ``recursive schema parses and reconstructs finite trees`` () =
        let schema = categorySchema ()
        let input =
            rawCategory "root"
                [ rawCategory "one" []
                  rawCategory "two" [ rawCategory "leaf" [] ] ]

        test <@ (Schema.parse schema input) = Ok sample @>
        test <@ Schema.check schema sample = Ok sample @>

    [<Fact>]
    let ``deferred model schemas remain parseable beneath refinement`` () =
        let schema = categorySchema ()
        let refinedDeferred =
            Schema.defer (fun () -> schema)
            |> Schema.convert id id

        let input = rawCategory "root" []
        test <@ (Schema.parse refinedDeferred input) = Ok { Name = "root"; Children = [] } @>

    [<Fact>]
    let ``nested and defer reject value schemas with argument errors`` () =
        raises<System.ArgumentException> <@ ValueSchema.nested Schema.text @>
        let deferred = ValueSchema.lazyOf (fun () -> Schema.text)
        raises<System.ArgumentException> <@ Schema.parseRetainingInput deferred (Data.Text "value") @>

    [<Fact>]
    let ``recursive schema compiles to a reusable codec`` () =
        let codec = Json.compile (categorySchema ())
        let encoded = Json.serialize codec sample
        test <@ Json.deserialize codec encoded = sample @>

    [<Fact>]
    let ``inspection terminates with a stable recursive marker`` () =
        let description = Inspect.model (categorySchema ())

        match description.Fields[1].Schema.Shape with
        | SchemaShape.Many item ->
            match item.Shape with
            | SchemaShape.Deferred(reference, expanded) ->
                match expanded.Shape with
                | SchemaShape.Nested category ->
                    match category.Fields[1].Schema.Shape with
                    | SchemaShape.Many nestedItem -> test <@ nestedItem.Shape = SchemaShape.Recursive reference @>
                    | shape -> failwithf "Expected recursive children, got %A" shape
                | shape -> failwithf "Expected expanded nested model, got %A" shape
            | shape -> failwithf "Expected deferred item, got %A" shape
        | shape -> failwithf "Expected children collection, got %A" shape

    [<Fact>]
    let ``JSON Schema hoists recursion into defs and emits refs`` () =
        let document = JsonSchema.generate (categorySchema ())
        test <@ document.Contains "\"$defs\":{\"recursive1\"" @>
        test <@ document.Contains "\"$ref\":\"#/$defs/recursive1\"" @>

    [<Fact>]
    let ``deep finite input does not confuse recursion with cyclic data`` () =
        let depth = 200
        let model =
            [ depth - 1 .. -1 .. 1 ]
            |> List.fold (fun child index -> { Name = string index; Children = [ child ] }) { Name = string depth; Children = [] }

        test <@ Schema.check (categorySchema ()) model = Ok model @>

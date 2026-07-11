namespace Axial.Schema.Tests

open Axial.Codec
open Axial.Schema
open Axial.Validation
open Swensen.Unquote
open Xunit

module RecursiveSchemaTests =
    type Category = { Name: string; Children: Category list }

    let private categorySchema () =
        let rec schema: Lazy<Schema<Category>> =
            lazy
                (Schema.recordFor<Category, _> (fun name children -> { Name = name; Children = children })
                 |> Schema.text "name" _.Name
                 |> Schema.field "children" _.Children (Value.manyOf (Value.lazyOf (fun () -> schema.Value)))
                 |> Schema.build)

        schema.Value

    let private rawCategory name children =
        RawInput.Object(Map.ofList [ "name", RawInput.Scalar name; "children", RawInput.Many children ])

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

        test <@ (Model.parse schema input).Result = Ok sample @>
        test <@ Model.reconstruct schema sample = Ok sample @>

    [<Fact>]
    let ``recursive schema compiles to a reusable codec`` () =
        let codec = Json.compile (categorySchema ())
        let encoded = Json.serialize codec sample
        test <@ Json.deserialize codec encoded = sample @>

    [<Fact>]
    let ``inspection terminates with a stable recursive marker`` () =
        let description = Inspect.model (categorySchema ())

        match description.Fields[1].Value.Shape with
        | ValueShape.Many item ->
            match item.Shape with
            | ValueShape.Deferred(reference, expanded) ->
                match expanded.Shape with
                | ValueShape.Nested category ->
                    match category.Fields[1].Value.Shape with
                    | ValueShape.Many nestedItem -> test <@ nestedItem.Shape = ValueShape.Recursive reference @>
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

        test <@ Model.reconstruct (categorySchema ()) model = Ok model @>

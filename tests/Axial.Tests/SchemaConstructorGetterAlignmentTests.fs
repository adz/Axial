namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that `Schema.map2` / `Schema.map3` bind each field's getter to the constructor argument at that field's
/// declared position, not to the field's name or its own pre-assigned default order. Fields use same-typed values so a
/// misaligned implementation would produce a wrong-but-still-typed result instead of a crash.
/// </summary>
module SchemaConstructorGetterAlignmentTests =
    type private FullName = { First: string; Last: string }

    type private Address =
        { Line1: string
          Line2: string
          City: string }

    let private modelDefinition (schema: Schema<'model>) =
        match schema.Definition with
        | ModelDefinition model -> model
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    [<Fact>]
    let ``map2 aligns each field's getter with its constructor argument position`` () =
        let first = Schema.field "first" (fun (name: FullName) -> name.First) Value.text
        let last = Schema.field "last" (fun (name: FullName) -> name.Last) Value.text
        let schema = Schema.map2 (fun first last -> { First = first; Last = last }) first last
        let source = { First = "Ada"; Last = "Lovelace" }

        test <@ Field.getValue first source = "Ada" @>
        test <@ Field.getValue last source = "Lovelace" @>

        let model = modelDefinition schema
        let values = model.Fields |> List.map (fun field -> field.Getter source)

        test <@ model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) = [ "first"; "last" ] @>
        test <@ model.Fields |> List.map (fun field -> FieldOrder.value field.Order) = [ 0; 1 ] @>
        test <@ values = [ box "Ada"; box "Lovelace" ] @>
        test <@ ConstructorApplication.apply model.Constructor (values |> List.toArray) = source @>

    [<Fact>]
    let ``map2 binds argument position to declaration order, not to a field's own default order`` () =
        // Both fields start with FieldOrder 0 from Schema.field; passing "last" first to map2 must make it
        // constructor argument 0, regardless of the field's name or its own pre-assigned order.
        let first = Schema.field "first" (fun (name: FullName) -> name.First) Value.text
        let last = Schema.field "last" (fun (name: FullName) -> name.Last) Value.text

        test <@ Field.order first |> FieldOrder.value = 0 @>
        test <@ Field.order last |> FieldOrder.value = 0 @>

        let swapped = Schema.map2 (fun a b -> { First = a; Last = b }) last first
        let source = { First = "Ada"; Last = "Lovelace" }

        let model = modelDefinition swapped
        let values = model.Fields |> List.map (fun field -> field.Getter source)

        test <@ model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) = [ "last"; "first" ] @>
        test <@ model.Fields |> List.map (fun field -> FieldOrder.value field.Order) = [ 0; 1 ] @>
        test <@ values = [ box "Lovelace"; box "Ada" ] @>
        test <@ ConstructorApplication.apply model.Constructor (values |> List.toArray) = { First = "Lovelace"; Last = "Ada" } @>

    [<Fact>]
    let ``map3 aligns each of three same-typed fields with its constructor argument position`` () =
        let line1 = Schema.field "line1" (fun (address: Address) -> address.Line1) Value.text
        let line2 = Schema.field "line2" (fun (address: Address) -> address.Line2) Value.text
        let city = Schema.field "city" (fun (address: Address) -> address.City) Value.text

        let schema =
            Schema.map3 (fun line1 line2 city -> { Line1 = line1; Line2 = line2; City = city }) line1 line2 city

        let source = { Line1 = "221B Baker Street"; Line2 = "Flat 2"; City = "London" }

        test <@ Field.getValue line1 source = "221B Baker Street" @>
        test <@ Field.getValue line2 source = "Flat 2" @>
        test <@ Field.getValue city source = "London" @>

        let model = modelDefinition schema
        let values = model.Fields |> List.map (fun field -> field.Getter source)

        test <@
            model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) =
                [ "line1"; "line2"; "city" ]
        @>
        test <@ model.Fields |> List.map (fun field -> FieldOrder.value field.Order) = [ 0; 1; 2 ] @>
        test <@ values = [ box "221B Baker Street"; box "Flat 2"; box "London" ] @>
        test <@ ConstructorApplication.apply model.Constructor (values |> List.toArray) = source @>

    [<Fact>]
    let ``map3 preserves alignment under a reordered declaration`` () =
        let line1 = Schema.field "line1" (fun (address: Address) -> address.Line1) Value.text
        let line2 = Schema.field "line2" (fun (address: Address) -> address.Line2) Value.text
        let city = Schema.field "city" (fun (address: Address) -> address.City) Value.text

        // Declare city first and construct the record accordingly; each getter must still land on the argument
        // matching its declared position rather than its original field order or name.
        let reordered =
            Schema.map3 (fun city line1 line2 -> { Line1 = line1; Line2 = line2; City = city }) city line1 line2

        let source = { Line1 = "221B Baker Street"; Line2 = "Flat 2"; City = "London" }

        let model = modelDefinition reordered
        let values = model.Fields |> List.map (fun field -> field.Getter source)

        test <@
            model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) =
                [ "city"; "line1"; "line2" ]
        @>
        test <@ model.Fields |> List.map (fun field -> FieldOrder.value field.Order) = [ 0; 1; 2 ] @>
        test <@ values = [ box "London"; box "221B Baker Street"; box "Flat 2" ] @>
        test <@ ConstructorApplication.apply model.Constructor (values |> List.toArray) = source @>

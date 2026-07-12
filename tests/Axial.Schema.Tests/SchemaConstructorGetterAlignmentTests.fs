namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Proves that the progressive schema builder binds each field's getter to the constructor argument at that field's
/// declared position, not to the record field order or external field name. Fields use same-typed values so a misaligned
/// implementation would produce a wrong-but-still-typed result instead of a crash.
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
    let ``builder aligns each field's getter with its constructor argument position`` () =
        let schema =
            Schema.recordFor<FullName, _> (fun first last -> { First = first; Last = last })
            |> Schema.field "first" _.First Schema.text
            |> Schema.field "last" _.Last Schema.text
            |> Schema.build
        let source = { First = "Ada"; Last = "Lovelace" }

        let model = modelDefinition schema
        let values = model.Fields |> List.map (fun field -> field.Getter source)

        test <@ model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) = [ "first"; "last" ] @>
        test <@ model.Fields |> List.map (fun field -> FieldOrder.value field.Order) = [ 0; 1 ] @>
        test <@ values = [ box "Ada"; box "Lovelace" ] @>
        test <@ ConstructorApplication.apply model.Constructor (values |> List.toArray) = source @>

    [<Fact>]
    let ``builder binds argument position to declaration order, not external field name`` () =
        let swapped =
            Schema.recordFor<FullName, _> (fun a b -> { First = a; Last = b })
            |> Schema.field "last" _.Last Schema.text
            |> Schema.field "first" _.First Schema.text
            |> Schema.build
        let source = { First = "Ada"; Last = "Lovelace" }

        let model = modelDefinition swapped
        let values = model.Fields |> List.map (fun field -> field.Getter source)

        test <@ model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) = [ "last"; "first" ] @>
        test <@ model.Fields |> List.map (fun field -> FieldOrder.value field.Order) = [ 0; 1 ] @>
        test <@ values = [ box "Lovelace"; box "Ada" ] @>
        test <@ ConstructorApplication.apply model.Constructor (values |> List.toArray) = { First = "Lovelace"; Last = "Ada" } @>

    [<Fact>]
    let ``builder aligns each of three same-typed fields with its constructor argument position`` () =
        let schema =
            Schema.recordFor<Address, _> (fun line1 line2 city -> { Line1 = line1; Line2 = line2; City = city })
            |> Schema.field "line1" _.Line1 Schema.text
            |> Schema.field "line2" _.Line2 Schema.text
            |> Schema.field "city" _.City Schema.text
            |> Schema.build

        let source = { Line1 = "221B Baker Street"; Line2 = "Flat 2"; City = "London" }

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
    let ``builder preserves alignment under a reordered declaration`` () =
        // Declare city first and construct the record accordingly; each getter must still land on the argument
        // matching its declared position rather than the record's source order or external field name.
        let reordered =
            Schema.recordFor<Address, _> (fun city line1 line2 -> { Line1 = line1; Line2 = line2; City = city })
            |> Schema.field "city" _.City Schema.text
            |> Schema.field "line1" _.Line1 Schema.text
            |> Schema.field "line2" _.Line2 Schema.text
            |> Schema.build

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

namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

/// <summary>
/// Proves that a collection of nested model values can be inspected as portable field metadata from an outer model
/// schema, without constructing either model, so interpreters can walk collection structure the same way they walk
/// nested and primitive fields.
/// </summary>
module SchemaManyValueTests =
    type private ContactMethod = { Kind: string; Value: string }

    type private Customer = { Name: string; Contacts: ContactMethod list }

    let private modelDefinition (schema: Schema<'model>) =
        match schema.Definition with
        | ModelDefinition model -> model
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    let private buildContactMethodSchema () =
        schema<ContactMethod> {
            field "kind" _.Kind {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            field "value" _.Value {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            construct (fun kind value -> { Kind = kind; Value = value })
        }

    [<Fact>]
    let ``many field getter reads the item collection from an already trusted outer model`` () =
        let contactMethodSchema = buildContactMethodSchema ()

        let schema =
            schema<Customer> {
                field "name" _.Name {
                    withSchema (Schema.text |> Schema.constrain Constraint.required)
                }
                field "contacts" _.Contacts {
                    withSchema (Schema.listWith contactMethodSchema)
                }
                construct (fun name contacts -> { Name = name; Contacts = contacts })
            }

        let model = modelDefinition schema

        let contactsField =
            model.Fields
            |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "contacts")

        let customer =
            { Name = "Ada"
              Contacts = [ { Kind = "email"; Value = "ada@example.com" } ] }

        test <@ contactsField.Getter customer = box customer.Contacts @>

    [<Fact>]
    let ``many field carries the constraints attached at the field, such as minCount`` () =
        let contactMethodSchema = buildContactMethodSchema ()

        let schema =
            schema<Customer> {
                field "name" _.Name {
                    withSchema (Schema.text |> Schema.constrain Constraint.required)
                }
                field "contacts" _.Contacts {
                    withSchema (
                        Schema.listWith contactMethodSchema
                        |> Schema.constrainAll [ Constraint.minCount 1 ]
                    )
                }
                construct (fun name contacts -> { Name = name; Contacts = contacts })
            }

        let model = modelDefinition schema

        let contactsField =
            model.Fields
            |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "contacts")

        test <@ contactsField.ValueSchema.Constraints |> List.map Constraint.code = [ "minCount" ] @>

    [<Fact>]
    let ``a many value schema built from Schema.listWith is not a refined or primitive value schema`` () =
        let contactMethodSchema = buildContactMethodSchema ()
        let manyValue = Schema.listWith contactMethodSchema

        test <@ not (Schema.isRefined manyValue) @>

    [<Fact>]
    let ``manyOf builds a collection value schema from primitive and refined item schemas`` () =
        let names = Schema.listWith (Schema.text |> Schema.constrain Constraint.required)

        match names.ValueDefinition.Shape with
        | ManyValueDefinition collection ->
            match collection.Item.Shape with
            | PrimitiveValueDefinition PrimitiveValueKind.Text -> ()
            | _ -> failwith "Expected the manyOf item to keep the supplied primitive value schema."

            test <@ collection.Item.Constraints |> List.map Constraint.code = [ "required" ] @>
        | PrimitiveValueDefinition _
        | RefinedValueDefinition _
        | NestedValueDefinition _
        | UnionValueDefinition _
        | OptionValueDefinition _ -> failwith "Expected a many/collection value schema."

    [<Fact>]
    let ``inspection interpreters can walk into each item of a many value schema using getters, without reflection`` () =
        let contactMethodSchema = buildContactMethodSchema ()

        let schema =
            schema<Customer> {
                field "name" _.Name {
                    withSchema (Schema.text |> Schema.constrain Constraint.required)
                }
                field "contacts" _.Contacts {
                    withSchema (Schema.listWith contactMethodSchema)
                }
                construct (fun name contacts -> { Name = name; Contacts = contacts })
            }

        let model = modelDefinition schema

        let contactsField =
            model.Fields
            |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "contacts")

        let customer =
            { Name = "Ada"
              Contacts = [ { Kind = "email"; Value = "ada@example.com" } ] }

        // An inspection interpreter reads the item collection with the outer field's getter, then reads each item's
        // own fields with the item schema's getters, all without reflection.
        match contactsField.ValueSchema.Shape with
        | ManyValueDefinition collectionDefinition ->
            match collectionDefinition.Item.Shape with
            | NestedValueDefinition(itemModel, _) ->
                let items = contactsField.Getter customer |> unbox<ContactMethod list>

                let kind =
                    itemModel.Fields
                    |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "kind")

                let value =
                    itemModel.Fields
                    |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "value")

                test <@ items |> List.map (fun item -> kind.Getter (box item)) = (customer.Contacts |> List.map (fun c -> box c.Kind)) @>
                test <@ items |> List.map (fun item -> value.Getter (box item)) = (customer.Contacts |> List.map (fun c -> box c.Value)) @>
            | PrimitiveValueDefinition _
            | RefinedValueDefinition _
            | ManyValueDefinition _
            | UnionValueDefinition _
            | OptionValueDefinition _ -> failwith "Expected the many value schema's item to be a nested model value schema."
        | PrimitiveValueDefinition _
        | RefinedValueDefinition _
        | NestedValueDefinition _
        | UnionValueDefinition _
        | OptionValueDefinition _ -> failwith "Expected a many/collection value schema."

namespace Axial.Tests

open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

/// <summary>
/// Proves that a nested model schema can be inspected as portable field metadata from an outer model schema, without
/// constructing either model, so interpreters can walk nested structure the same way they walk primitive fields.
/// </summary>
module SchemaNestedValueTests =
    type private Address = { Street: string; City: string }

    type private Customer = { Name: string; Address: Address }

    let private modelDefinition (schema: Schema<'model>) =
        match schema.Definition with
        | ModelDefinition model -> model
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    let private buildAddressSchema () =
        schema<Address> {
            field "street" _.Street {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            field "city" _.City {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            construct (fun street city -> { Street = street; City = city })
        }

    [<Fact>]
    let ``nested field getter reads the nested model from an already trusted outer model`` () =
        let addressSchema = buildAddressSchema ()

        let schema =
            schema<Customer> {
                field "name" _.Name {
                    withSchema (Schema.text |> Schema.constrain Constraint.required)
                }
                field "address" _.Address {
                    withSchema addressSchema
                }
                construct (fun name address -> { Name = name; Address = address })
            }

        let model = modelDefinition schema

        let addressField =
            model.Fields
            |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "address")

        let customer = { Name = "Ada"; Address = { Street = "1 Infinite Loop"; City = "Cupertino" } }

        test <@ addressField.Getter customer = box customer.Address @>

    [<Fact>]
    let ``nested field carries the constraints attached at the field, such as required`` () =
        let addressSchema = buildAddressSchema ()

        let schema =
            schema<Customer> {
                field "name" _.Name {
                    withSchema (Schema.text |> Schema.constrain Constraint.required)
                }
                field "address" _.Address {
                    withSchema (addressSchema |> Schema.constrainAll [ Constraint.required ])
                }
                construct (fun name address -> { Name = name; Address = address })
            }

        let model = modelDefinition schema

        let addressField =
            model.Fields
            |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "address")

        test <@ addressField.ValueSchema.Constraints |> List.map Constraint.code = [ "required" ] @>

    [<Fact>]
    let ``a nested value schema built from is not a refined or primitive value schema`` () =
        let addressSchema = buildAddressSchema ()
        let nestedValue = addressSchema

        test <@ not (Schema.isRefined nestedValue) @>

    [<Fact>]
    let ``inspection interpreters can walk into a nested value schema using getters, without reflection`` () =
        let addressSchema = buildAddressSchema ()

        let schema =
            schema<Customer> {
                field "name" _.Name {
                    withSchema (Schema.text |> Schema.constrain Constraint.required)
                }
                field "address" _.Address {
                    withSchema addressSchema
                }
                construct (fun name address -> { Name = name; Address = address })
            }

        let model = modelDefinition schema

        let addressField =
            model.Fields
            |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "address")

        let customer = { Name = "Ada"; Address = { Street = "1 Infinite Loop"; City = "Cupertino" } }

        // An inspection interpreter reads the nested model with the outer field's getter, then reads the nested
        // model's own fields with the nested schema's getters, all without reflection.
        match addressField.ValueSchema.Shape with
        | NestedValueDefinition(nestedModel, _) ->
            let nestedValue = addressField.Getter customer

            let street =
                nestedModel.Fields
                |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "street")

            let city =
                nestedModel.Fields
                |> List.find (fun field -> ExternalFieldName.value field.ExternalName = "city")

            test <@ street.Getter nestedValue = box customer.Address.Street @>
            test <@ city.Getter nestedValue = box customer.Address.City @>
        | PrimitiveValueDefinition _
        | RefinedValueDefinition _
        | ManyValueDefinition _
        | UnionValueDefinition _
        | OptionValueDefinition _ -> failwith "Expected a nested model value schema."

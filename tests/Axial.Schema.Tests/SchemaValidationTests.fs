namespace Axial.Tests

open Axial.Data

open Axial.Constraint

open System
open Axial.Schema
open Xunit
open Axial.Schema.Syntax
open Axial.Constraint.ConstraintDSL
open Swensen.Unquote

module SchemaValidationTests =
    type private Signup = { Email: string; Age: int }

    let private issues result =
        result |> Result.mapError SchemaErrors.toList

    type private IGeneratedBuilder<'model> =
        abstract member Build: obj array -> 'model

    type private IGeneratedBuildChain<'model, 'constructorIn, 'constructorOut> =
        abstract member Apply: 'constructorIn -> obj array -> 'constructorOut

    type private GeneratedFieldsEmpty<'model, 'constructor>() =
        interface IGeneratedBuildChain<'model, 'constructor, 'constructor> with
            member _.Apply constructor _ = constructor

    type private GeneratedFieldsCons<'model, 'constructorIn, 'field, 'next, 'head
        when 'head :> IGeneratedBuildChain<'model, 'constructorIn, 'field -> 'next>>
        (
            order: int,
            head: 'head
        ) =

        interface IGeneratedBuildChain<'model, 'constructorIn, 'next> with
            member _.Apply constructor values =
                let constructorForField = head.Apply constructor values
                constructorForField (unbox<'field> values[order])

    type private GeneratedBuildResult<'model, 'constructorIn, 'constructorOut>(value: obj) =
        interface IRecordPlanState<'model, 'constructorIn, 'constructorOut> with
            member _.Value = value

    type private GeneratedBuilder<'model, 'constructor, 'constructed>
        (
            constructor: 'constructor,
            chain: IGeneratedBuildChain<'model, 'constructor, 'constructed>,
            finish: 'constructed -> Result<'model, string>
        ) =

        interface IGeneratedBuilder<'model> with
            member _.Build values =
                match finish (chain.Apply constructor values) with
                | Ok model -> model
                | Error message -> invalidOp message

    type private GeneratedBuilderFactory<'model>() =
        interface IRecordPlanCompiler<'model, IGeneratedBuilder<'model>> with
            member _.OnEnd() =
                let chain =
                    GeneratedFieldsEmpty<'model, 'constructor>()
                    :> IGeneratedBuildChain<'model, 'constructor, 'constructor>

                GeneratedBuildResult<'model, 'constructor, 'constructor>(box chain) :> IRecordPlanState<_, _, _>

            member _.OnField(order, _field: Field<'model, 'field>, head) =
                let headChain = head.Value :?> IGeneratedBuildChain<'model, 'constructorIn, 'field -> 'next>

                let chain =
                    GeneratedFieldsCons<'model, 'constructorIn, 'field, 'next, _>(order, headChain)
                    :> IGeneratedBuildChain<'model, 'constructorIn, 'next>

                GeneratedBuildResult<'model, 'constructorIn, 'next>(box chain) :> IRecordPlanState<_, _, _>

            member _.OnComplete<'constructor, 'constructed>
                (
                    constructor: 'constructor,
                    chain: IRecordPlanState<'model, 'constructor, 'constructed>,
                    finish: 'constructed -> Result<'model, string>
                ) =
                let generatedChain = chain.Value :?> IGeneratedBuildChain<'model, 'constructor, 'constructed>
                GeneratedBuilder<'model, 'constructor, 'constructed>(constructor, generatedChain, finish)
                :> IGeneratedBuilder<'model>

    type private SwappedFields =
        { Primary: string
          Secondary: string }

    type private Address =
        { Street: string
          City: string }

    type private Customer =
        { Name: string
          Address: Address }

    type private ContactMethod = { Kind: string; Value: string }

    type private ContactBook =
        { Name: string
          Contacts: ContactMethod list }

    type private Tags = { Values: string list }

    type private DateRange =
        private
            { Start: DateOnly
              End: DateOnly }

        static member Create start endDate =
            if start <= endDate then
                Ok { Start = start; End = endDate }
            else
                Error "End date must be on or after start date."

    let private signupSchema =
        schema<Signup> {
            field "email" _.Email {
                withSchema (
                    Schema.text
                    |> Schema.constrainAll [ Constraint.present; Constraint.email; Constraint.maxLength 254 ]
                )
            }
            field "age" _.Age {
                withSchema (Schema.int |> Schema.constrain (Constraint.atLeast 18))
            }
            construct (fun email age -> { Email = email; Age = age })
        }

    let private contactMethodSchema =
        schema<ContactMethod> {
            field "kind" _.Kind {
                withSchema (Schema.text |> Schema.constrain Constraint.present)
            }
            field "value" _.Value {
                withSchema (Schema.text |> Schema.constrain Constraint.present)
            }
            construct (fun kind value -> { Kind = kind; Value = value })
        }

    let private contactBookSchema =
        schema<ContactBook> {
            field "name" _.Name {
                withSchema (Schema.text |> Schema.constrain Constraint.present)
            }
            field "contacts" _.Contacts {
                withSchema (
                    Schema.listWith contactMethodSchema
                    |> Schema.constrainAll [ Constraint.minLength 1; Constraint.maxLength 2 ]
                )
            }
            construct (fun name contacts -> { Name = name; Contacts = contacts })
        }

    let private generatedBuilder signupSchema =
        Schema.compilePlan (GeneratedBuilderFactory()) signupSchema

    [<Fact>]
    let ``validate returns the original model when schema constraints pass`` () =
        let model = { Email = "ada@example.com"; Age = 42 }

        let validation = Schema.check signupSchema model

        test <@ validation = Ok model @>

    [<Fact>]
    let ``validate reports diagnostics for existing model values that violate schema constraints`` () =
        let validation =
            Schema.check signupSchema { Email = ""; Age = 10 }

        test
            <@
                issues validation =
                    Error
                        [ { Path = Path.key "age"
                            Error = SchemaError.Violation(Atomic(Expected(RelationAtom(Compared(AtLeast, ConstraintValue.Integer 18L)), Some(ConstraintValue.Integer 10L)))) }
                          // Two constraints on one field accumulate into one violation tree at that path, not two
                          // diagnostics: grouping is the constraint layer's job and the path is Schema's.
                          { Path = Path.key "email"
                            Error =
                              SchemaError.Violation(
                                  All(
                                      Atomic(Expected(PresenceAtom Present, None)),
                                      [ Atomic(Expected(FormatAtom Format.Email, Some(ConstraintValue.Text ""))) ]
                                  )
                              ) } ]
            @>

    [<Fact>]
    let ``validate reports diagnostics for imported hand-built values that bypass input parsing`` () =
        let imported = { Email = "not-an-email"; Age = 16 }

        let validation = Schema.check signupSchema imported

        test
            <@
                issues validation =
                    Error
                        [ { Path = Path.key "age"
                            Error = SchemaError.Violation(Atomic(Expected(RelationAtom(Compared(AtLeast, ConstraintValue.Integer 18L)), Some(ConstraintValue.Integer 16L)))) }
                          { Path = Path.key "email"
                            Error =
                              SchemaError.Violation(
                                  Atomic(Expected(FormatAtom Format.Email, Some(ConstraintValue.Text "not-an-email")))
                              ) } ]
            @>

    [<Fact>]
    let ``check surfaces an opaque constraint's authored prose`` () =
        let messageSchema =
            schema<Signup> {
                field "email" _.Email {
                    withSchema (
                        Schema.text
                        |> Schema.constrain (Constraint.custom "Email is required." (Constraint.test Constraint.present))
                    )
                }
                field "age" _.Age {
                    withSchema (
                        Schema.int
                        |> Schema.constrain (Constraint.custom "Must be an adult." (fun value -> value >= 18))
                    )
                }
                construct (fun email age -> { Email = email; Age = age })
            }

        let validation =
            Schema.check messageSchema { Email = ""; Age = 10 }

        test
            <@
                issues validation =
                    Error
                        [ { Path = Path.key "age"
                            Error = SchemaError.Violation(Atomic(Described("Must be an adult.", None))) }
                          { Path = Path.key "email"
                            Error = SchemaError.Violation(Atomic(Described("Email is required.", None))) } ]
            @>

    [<Fact>]
    let ``validate reads existing model values through schema getters`` () =
        let swappedSchema =
            schema<SwappedFields> {
                field "secondary-on-wire" _.Primary {
                    withSchema (Schema.text |> Schema.constrain (Constraint.oneOf [ "primary-value" ]))
                }
                field "primary-on-wire" _.Secondary {
                    withSchema (Schema.text |> Schema.constrain (Constraint.oneOf [ "secondary-value" ]))
                }
                construct (fun primary secondary ->
                    { Primary = primary
                      Secondary = secondary })
            }

        let validation =
            Schema.check
                swappedSchema
                { Primary = "primary-value"
                  Secondary = "wrong-secondary" }

        test
            <@
                issues validation =
                    Error
                        [ { Path = Path.key "primary-on-wire"
                            Error = SchemaError.Violation(Atomic(Expected(MembershipAtom(OneOf [ ConstraintValue.Text "secondary-value" ]), Some(ConstraintValue.Text "wrong-secondary")))) } ]
            @>

    [<Fact>]
    let ``validate checks nested model values through their nested schema`` () =
        let addressSchema =
            schema<Address> {
                field "street" _.Street {
                    withSchema (Schema.text |> Schema.constrain Constraint.present)
                }
                field "city" _.City {
                    withSchema (Schema.text |> Schema.constrain Constraint.present)
                }
                construct (fun street city -> { Street = street; City = city })
            }

        let customerSchema =
            schema<Customer> {
                field "name" (fun (value: Customer) -> value.Name) {
                    withSchema (Schema.text |> Schema.constrain Constraint.present)
                }
                field "address" _.Address {
                    withSchema addressSchema
                }
                construct (fun name address -> { Name = name; Address = address })
            }

        let validation =
            Schema.check
                customerSchema
                { Name = "Ada"
                  Address = { Street = "1 Main Street"; City = "" } }

        test
            <@
                issues validation =
                    Error
                        [ { Path = Path.append (Path.key "address") (Path.key "city")
                            Error = SchemaError.Violation(Atomic(Expected(PresenceAtom Present, None))) } ]
            @>

    [<Fact>]
    let ``validate checks collection item values through their item schema`` () =
        let model =
            { Name = "Ada"
              Contacts =
                [ { Kind = ""; Value = "ada@example.com" }
                  { Kind = "phone"; Value = "" } ] }

        let validation =
            Schema.check contactBookSchema model

        test
            <@
                issues validation =
                    Error
                        [ { Path =
                                TestPath.fromLegacy
                                    [ PathSegment.Name "contacts"
                                      PathSegment.Index 0
                                      PathSegment.Name "kind" ]
                            Error = SchemaError.Violation(Atomic(Expected(PresenceAtom Present, None))) }
                          { Path =
                                TestPath.fromLegacy
                                    [ PathSegment.Name "contacts"
                                      PathSegment.Index 1
                                      PathSegment.Name "value" ]
                            Error = SchemaError.Violation(Atomic(Expected(PresenceAtom Present, None))) } ]
            @>

    [<Fact>]
    let ``validate reports collection count constraints at the collection field path`` () =
        let model =
            { Name = "Ada"
              Contacts =
                [ { Kind = "email"; Value = "ada@example.com" }
                  { Kind = "phone"; Value = "+61 400 000 000" }
                  { Kind = "sms"; Value = "+61 400 000 000" } ] }

        let validation =
            Schema.check contactBookSchema model

        test
            <@
                issues validation =
                    Error
                        [ { Path = Path.key "contacts"
                            Error = SchemaError.Violation(Atomic(Expected(CardinalityAtom(Cardinality.Maximum 2), Some(ConstraintValue.Integer 3L)))) } ]
            @>

    [<Fact>]
    let ``validate reports primitive collection item constraints at index paths`` () =
        let tagsSchema =
            schema<Tags> {
                field "values" _.Values {
                    withSchema (Schema.listWith (Schema.text |> Schema.constrain Constraint.present))
                }
                construct (fun values -> { Values = values })
            }

        let validation =
            Schema.check tagsSchema { Values = [ "fsharp"; "" ] }

        test
            <@
                issues validation =
                    Error
                        [ { Path = Path.append (Path.key "values") (Path.index 1)
                            Error = SchemaError.Violation(Atomic(Expected(PresenceAtom Present, None))) } ]
            @>

    [<Fact>]
    let ``values produced by input parsing validate through the same schema`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "ada@example.com"
                      "age", Data.Text "42" ]
            )

        let parsed = Schema.parseRetainingInput signupSchema raw
        let validation = Schema.check signupSchema parsed.Value

        test <@ parsed.IsValid @>
        test <@ validation = Ok parsed.Value @>

    [<Fact>]
    let ``values produced by a generated builder validate through the same schema`` () =
        let builder = generatedBuilder signupSchema
        let generated = builder.Build [| box "ada@example.com"; box 42 |]

        let validation = Schema.check signupSchema generated

        test <@ generated = { Email = "ada@example.com"; Age = 42 } @>
        test <@ validation = Ok generated @>

    [<Fact>]
    let ``validate reports diagnostics for generated builder values that bypass input parsing`` () =
        let builder = generatedBuilder signupSchema
        let generated = builder.Build [| box ""; box 17 |]

        let validation = Schema.check signupSchema generated

        test
            <@
                issues validation =
                    Error
                        [ { Path = Path.key "age"
                            Error = SchemaError.Violation(Atomic(Expected(RelationAtom(Compared(AtLeast, ConstraintValue.Integer 18L)), Some(ConstraintValue.Integer 17L)))) }
                          // Two constraints on one field accumulate into one violation tree at that path, not two
                          // diagnostics: grouping is the constraint layer's job and the path is Schema's.
                          { Path = Path.key "email"
                            Error =
                              SchemaError.Violation(
                                  All(
                                      Atomic(Expected(PresenceAtom Present, None)),
                                      [ Atomic(Expected(FormatAtom Format.Email, Some(ConstraintValue.Text ""))) ]
                                  )
                              ) } ]
            @>

    [<Fact>]
    let ``values produced by input parsing with constructor invariants validate through the same schema`` () =
        let rangeSchema =
            schema<DateRange> {
                field "start" _.Start
                field "end" _.End
                constructResult DateRange.Create
            }

        let raw =
            Data.objectOfMap (Map.ofList
                    [ "start", Data.Text "2026-01-10"
                      "end", Data.Text "2026-01-12" ]
            )

        let parsed = Schema.parseRetainingInput rangeSchema raw
        let validation = Schema.check rangeSchema parsed.Value

        test <@ parsed.IsValid @>
        test <@ validation = Ok parsed.Value @>

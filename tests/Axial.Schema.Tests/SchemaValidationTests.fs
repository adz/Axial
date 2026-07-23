namespace Axial.Tests

open Axial

open Axial.ErrorHandling

open System
open Axial.Schema
open Swensen.Unquote
open Xunit
open Axial.Schema.Syntax

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

    let private schema =
        SchemaCE.schema<Signup> {
            SchemaCE.field "email" _.Email {
                withSchema (
                    Schema.text
                    |> Schema.constrainAll [ Constraint.required; Constraint.email; Constraint.maxLength 254 ]
                )
            }
            SchemaCE.field "age" _.Age {
                withSchema (Schema.int |> Schema.constrain (Constraint.atLeast 18))
            }
            SchemaCE.construct (fun email age -> { Email = email; Age = age })
        }

    let private contactMethodSchema =
        SchemaCE.schema<ContactMethod> {
            SchemaCE.field "kind" _.Kind {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.field "value" _.Value {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.construct (fun kind value -> { Kind = kind; Value = value })
        }

    let private contactBookSchema =
        SchemaCE.schema<ContactBook> {
            SchemaCE.field "name" _.Name {
                withSchema (Schema.text |> Schema.constrain Constraint.required)
            }
            SchemaCE.field "contacts" _.Contacts {
                withSchema (
                    Schema.listWith contactMethodSchema
                    |> Schema.constrainAll [ Constraint.minCount 1; Constraint.maxCount 2 ]
                )
            }
            SchemaCE.construct (fun name contacts -> { Name = name; Contacts = contacts })
        }

    let private generatedBuilder schema =
        Schema.compilePlan (GeneratedBuilderFactory()) schema

    [<Fact>]
    let ``validate returns the original model when schema constraints pass`` () =
        let model = { Email = "ada@example.com"; Age = 42 }

        let validation = Schema.check schema model

        test <@ validation = Ok model @>

    [<Fact>]
    let ``validate reports diagnostics for existing model values that violate schema constraints`` () =
        let validation =
            Schema.check schema { Email = ""; Age = 10 }

        test
            <@
                issues validation =
                    Error
                        [ { Path = Path.key "age"
                            Error = SchemaError.OutOfRange(CheckRangeExpectation.AtLeast "18", Some "10") }
                          { Path = Path.key "email"; Error = SchemaError.Required }
                          { Path = Path.key "email"; Error = SchemaError.InvalidFormat "email" } ]
            @>

    [<Fact>]
    let ``validate reports diagnostics for imported hand-built values that bypass input parsing`` () =
        let imported = { Email = "not-an-email"; Age = 16 }

        let validation = Schema.check schema imported

        test
            <@
                issues validation =
                    Error
                        [ { Path = Path.key "age"
                            Error = SchemaError.OutOfRange(CheckRangeExpectation.AtLeast "18", Some "16") }
                          { Path = Path.key "email"; Error = SchemaError.InvalidFormat "email" } ]
            @>

    [<Fact>]
    let ``validate surfaces schema constraint custom messages through Check lowering`` () =
        let messageSchema =
            SchemaCE.schema<Signup> {
                SchemaCE.field "email" _.Email {
                    withSchema (
                        Schema.text
                        |> Schema.constrain (Constraint.required |> Constraint.withMessage "Email is required.")
                    )
                }
                SchemaCE.field "age" _.Age {
                    withSchema (
                        Schema.int
                        |> Schema.constrain (Constraint.atLeast 18 |> Constraint.withMessage "Must be an adult.")
                    )
                }
                SchemaCE.construct (fun email age -> { Email = email; Age = age })
            }

        let validation =
            Schema.check messageSchema { Email = ""; Age = 10 }

        test
            <@
                issues validation =
                    Error
                        [ { Path = Path.key "age"
                            Error = SchemaError.Custom("atLeast", Some "Must be an adult.") }
                          { Path = Path.key "email"
                            Error = SchemaError.Custom("required", Some "Email is required.") } ]
            @>

    [<Fact>]
    let ``validate reads existing model values through schema getters`` () =
        let swappedSchema =
            SchemaCE.schema<SwappedFields> {
                SchemaCE.field "secondary-on-wire" _.Primary {
                    withSchema (Schema.text |> Schema.constrain (Constraint.oneOf [ "primary-value" ]))
                }
                SchemaCE.field "primary-on-wire" _.Secondary {
                    withSchema (Schema.text |> Schema.constrain (Constraint.oneOf [ "secondary-value" ]))
                }
                SchemaCE.construct (fun primary secondary ->
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
                            Error = SchemaError.NotOneOf "secondary-value" } ]
            @>

    [<Fact>]
    let ``validate checks nested model values through their nested schema`` () =
        let addressSchema =
            SchemaCE.schema<Address> {
                SchemaCE.field "street" _.Street {
                    withSchema (Schema.text |> Schema.constrain Constraint.required)
                }
                SchemaCE.field "city" _.City {
                    withSchema (Schema.text |> Schema.constrain Constraint.required)
                }
                SchemaCE.construct (fun street city -> { Street = street; City = city })
            }

        let customerSchema =
            SchemaCE.schema<Customer> {
                SchemaCE.field "name" (fun (value: Customer) -> value.Name) {
                    withSchema (Schema.text |> Schema.constrain Constraint.required)
                }
                SchemaCE.field "address" _.Address {
                    withSchema addressSchema
                }
                SchemaCE.construct (fun name address -> { Name = name; Address = address })
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
                            Error = SchemaError.Required } ]
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
                            Error = SchemaError.Required }
                          { Path =
                                TestPath.fromLegacy
                                    [ PathSegment.Name "contacts"
                                      PathSegment.Index 1
                                      PathSegment.Name "value" ]
                            Error = SchemaError.Required } ]
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
                            Error = SchemaError.InvalidCount(CheckCountExpectation.MaximumCount 2, Some 3) } ]
            @>

    [<Fact>]
    let ``validate reports primitive collection item constraints at index paths`` () =
        let schema =
            SchemaCE.schema<Tags> {
                SchemaCE.field "values" _.Values {
                    withSchema (Schema.listWith (Schema.text |> Schema.constrain Constraint.required))
                }
                SchemaCE.construct (fun values -> { Values = values })
            }

        let validation =
            Schema.check schema { Values = [ "fsharp"; "" ] }

        test
            <@
                issues validation =
                    Error
                        [ { Path = Path.append (Path.key "values") (Path.index 1)
                            Error = SchemaError.Required } ]
            @>

    [<Fact>]
    let ``values produced by input parsing validate through the same schema`` () =
        let raw =
            Data.objectOfMap (Map.ofList
                    [ "email", Data.Text "ada@example.com"
                      "age", Data.Text "42" ]
            )

        let parsed = Schema.parseRetainingInput schema raw
        let validation = Schema.check schema parsed.Value

        test <@ parsed.IsValid @>
        test <@ validation = Ok parsed.Value @>

    [<Fact>]
    let ``values produced by a generated builder validate through the same schema`` () =
        let builder = generatedBuilder schema
        let generated = builder.Build [| box "ada@example.com"; box 42 |]

        let validation = Schema.check schema generated

        test <@ generated = { Email = "ada@example.com"; Age = 42 } @>
        test <@ validation = Ok generated @>

    [<Fact>]
    let ``validate reports diagnostics for generated builder values that bypass input parsing`` () =
        let builder = generatedBuilder schema
        let generated = builder.Build [| box ""; box 17 |]

        let validation = Schema.check schema generated

        test
            <@
                issues validation =
                    Error
                        [ { Path = Path.key "age"
                            Error = SchemaError.OutOfRange(CheckRangeExpectation.AtLeast "18", Some "17") }
                          { Path = Path.key "email"; Error = SchemaError.Required }
                          { Path = Path.key "email"; Error = SchemaError.InvalidFormat "email" } ]
            @>

    [<Fact>]
    let ``values produced by input parsing with constructor invariants validate through the same schema`` () =
        let rangeSchema =
            SchemaCE.schema<DateRange> {
                SchemaCE.field "start" _.Start
                SchemaCE.field "end" _.End
                SchemaCE.constructResult DateRange.Create
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

open System
open Axial.Constraint
open Axial.Schema
open type Axial.Schema.Syntax

type ProbeFailure(message: string) =
    inherit Exception(message)

module Assert =
    let equal<'value when 'value : equality> (expected: 'value) (actual: 'value) =
        if actual <> expected then
            raise (ProbeFailure(sprintf "Expected %+A but got %+A." expected actual))

type SchemaContact =
    {
        Name: string
        Age: int
    }

type SchemaFieldSummary =
    {
        Order: int
        ExternalName: string
    }

type SummaryChainResult<'model, 'constructorIn, 'constructorOut>(value: obj) =
    interface IRecordPlanState<'model, 'constructorIn, 'constructorOut> with
        member _.Value = value

type SummaryFactory<'model>() =
    interface IRecordPlanCompiler<'model, SchemaFieldSummary list> with
        member _.OnEnd() =
            SummaryChainResult<'model, 'constructor, 'constructor>(box ([]: SchemaFieldSummary list))
            :> IRecordPlanState<_, _, _>

        member _.OnField(order, field: Field<'model, 'field>, head) =
            let fields = head.Value :?> SchemaFieldSummary list
            let name = Field.externalName field |> ExternalFieldName.value
            let fieldSummary = { Order = order; ExternalName = name }

            SummaryChainResult<'model, 'constructorIn, 'next>(box (fields @ [ fieldSummary ]))
            :> IRecordPlanState<_, _, _>

        member _.OnComplete<'constructor, 'constructed>
            (
                _: 'constructor,
                chain: IRecordPlanState<'model, 'constructor, 'constructed>,
                _: 'constructed -> Result<'model, string>
            ) =
            chain.Value :?> SchemaFieldSummary list

let probeSchemaPlan () =
    let schema =
        schema<SchemaContact> {
            field "name" _.Name
            field "age" _.Age
            construct (fun name age -> { Name = name; Age = age })
        }

    Schema.compilePlan (SummaryFactory<SchemaContact>()) schema

let probeBareGetterFields () =
    // The bare field form derives wire names from getter quotations; this proves the quotation
    // pattern-match and the compiled-getter extraction both survive native AOT.
    let schema =
        schema<SchemaContact> {
            field _.Name {
                constrain (Constraint.minLength 1)
            }
            field _.Age
            construct (fun name age -> { Name = name; Age = age })
        }

    let description = Inspect.model schema
    description.Fields |> List.map _.Name |> Assert.equal [ "name"; "age" ]

    let checked' =
        Schema.check schema { Name = "Ada"; Age = 36 }

    checked' |> Assert.equal (Ok { Name = "Ada"; Age = 36 })

let private satisfied (constraint': Constraint<'value>) (value: 'value) =
    Constraint.check constraint' value

let probeTypeDirectedConstraints () =
    // The type-directed catalogue resolves through SRTP dispatchers, which is the part of the design most at risk
    // under native AOT: every shape below must select its overload with no reflection and no generic dictionary.
    satisfied Constraint.present "Ada" |> Assert.equal (Ok())
    satisfied Constraint.blank "  " |> Assert.equal (Ok())
    satisfied Constraint.present (Some 1) |> Assert.equal (Ok())
    satisfied Constraint.blank (None: int option) |> Assert.equal (Ok())
    satisfied Constraint.present (ValueSome 1) |> Assert.equal (Ok())
    satisfied Constraint.blank (ValueNone: int voption) |> Assert.equal (Ok())
    satisfied Constraint.present (System.Nullable 1) |> Assert.equal (Ok())
    satisfied Constraint.blank (System.Nullable<int>()) |> Assert.equal (Ok())
    satisfied Constraint.present [ 1 ] |> Assert.equal (Ok())
    satisfied Constraint.blank ([]: int list) |> Assert.equal (Ok())
    satisfied Constraint.present [| 1 |] |> Assert.equal (Ok())
    satisfied Constraint.blank ([||]: int array) |> Assert.equal (Ok())

    satisfied (Constraint.minLength 2) "Ada" |> Assert.equal (Ok())
    satisfied (Constraint.maxLength 2) [ 1 ] |> Assert.equal (Ok())
    satisfied (Constraint.lengthBetween 1 2) [| 1 |] |> Assert.equal (Ok())

    satisfied (Constraint.optional (Constraint.minLength 2)) (None: string option) |> Assert.equal (Ok())
    satisfied (Constraint.optional (Constraint.minLength 2)) (Some "Ada") |> Assert.equal (Ok())
    satisfied (Constraint.optional (Constraint.greaterThan 0)) (ValueSome 1) |> Assert.equal (Ok())
    satisfied (Constraint.optional (Constraint.greaterThan 0)) (System.Nullable 1) |> Assert.equal (Ok())

    satisfied Constraint.distinct [ 1; 2 ] |> Assert.equal (Ok())
    satisfied (Constraint.contains 1) [ 1; 2 ] |> Assert.equal (Ok())

let probe () =
    probeSchemaPlan ()
    |> Assert.equal
        [
            { Order = 0; ExternalName = "name" }
            { Order = 1; ExternalName = "age" }
        ]

    probeBareGetterFields ()
    probeTypeDirectedConstraints ()

[<EntryPoint>]
let main _ =
    probe ()
    0

open System
open Axial.ErrorHandling
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
                constrain (Syntax.minLength 1)
            }
            field _.Age
            construct (fun name age -> { Name = name; Age = age })
        }

    let description = Inspect.model schema
    description.Fields |> List.map _.Name |> Assert.equal [ "name"; "age" ]

    let checked' =
        Schema.check schema { Name = "Ada"; Age = 36 }

    checked' |> Assert.equal (Ok { Name = "Ada"; Age = 36 })

let inline eraseCheckedValue check value =
    check value |> Result.map (fun _ -> ())

let probeTypeDirectedChecks () =
    eraseCheckedValue Check.present "Ada" |> Assert.equal (Ok())
    eraseCheckedValue Check.empty "" |> Assert.equal (Ok())
    eraseCheckedValue Check.notEmpty "Ada" |> Assert.equal (Ok())
    eraseCheckedValue Check.present (Some 1) |> Assert.equal (Ok())
    eraseCheckedValue Check.empty (None: int option) |> Assert.equal (Ok())
    eraseCheckedValue Check.notEmpty (Some 1) |> Assert.equal (Ok())
    eraseCheckedValue Check.present (ValueSome 1) |> Assert.equal (Ok())
    eraseCheckedValue Check.empty (ValueNone: int voption) |> Assert.equal (Ok())
    eraseCheckedValue Check.notEmpty (ValueSome 1) |> Assert.equal (Ok())
    eraseCheckedValue Check.present (System.Nullable 1) |> Assert.equal (Ok())
    eraseCheckedValue Check.empty (System.Nullable<int>()) |> Assert.equal (Ok())
    eraseCheckedValue Check.notEmpty (System.Nullable 1) |> Assert.equal (Ok())
    eraseCheckedValue Check.present [ 1 ] |> Assert.equal (Ok())
    eraseCheckedValue Check.empty ([]: int list) |> Assert.equal (Ok())
    eraseCheckedValue Check.notEmpty [ 1 ] |> Assert.equal (Ok())
    eraseCheckedValue Check.present [| 1 |] |> Assert.equal (Ok())
    eraseCheckedValue Check.empty ([||]: int array) |> Assert.equal (Ok())
    eraseCheckedValue Check.notEmpty [| 1 |] |> Assert.equal (Ok())

let probe () =
    probeSchemaPlan ()
    |> Assert.equal
        [
            { Order = 0; ExternalName = "name" }
            { Order = 1; ExternalName = "age" }
        ]

    probeBareGetterFields ()
    probeTypeDirectedChecks ()

[<EntryPoint>]
let main _ =
    probe ()
    0

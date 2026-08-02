namespace Axial.Benchmarks.Fable

open System
open System.Threading
open Axial.Flow
open Axial.Result
open Axial.Constraint
open Axial.Schema
open Axial.Schema.Syntax
open Axial.Schema.Json

[<RequireQualifiedAccess>]
module Shared =
    [<Literal>]
    let SyncDepth = 20

    [<Literal>]
    let AsyncDepth = 20

    [<Literal>]
    let ReaderDepth = 10

    type ReaderEnv =
        {
            Prefix: string
        }

    type SchemaContact =
        {
            Name: string
            Age: int
        }

    type private SchemaFieldSummary =
        {
            Order: int
            ExternalName: string
        }

    type private SummaryChainResult<'model, 'constructorIn, 'constructorOut>(value: obj) =
        interface IRecordPlanState<'model, 'constructorIn, 'constructorOut> with
            member _.Value = value

    type private SummaryFactory<'model>() =
        interface IRecordPlanCompiler<'model, string list> with
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
                chain.Value
                :?> SchemaFieldSummary list
                |> List.map (fun field -> $"{field.Order}:{field.ExternalName}")

    let consumeResult (result: Result<int, string>) =
        match result with
        | Ok value -> value
        | Error error -> error.Length

    let consumeExit (exit: Exit<int, string>) =
        match exit with
        | Exit.Success value -> value
        | Exit.Failure (Cause.Fail error) -> error.Length
        | Exit.Failure _ -> -1

    let measure iterations (name: string) (work: unit -> 'value) =
        let start = DateTime.UtcNow.Ticks
        let mutable last = Unchecked.defaultof<'value>

        for _ in 1 .. iterations do
            last <- work ()

        let elapsedTicks = DateTime.UtcNow.Ticks - start
        let averageNs = float elapsedTicks * 100.0 / float iterations

        printfn "%s: %.2f ns" name averageNs
        last |> ignore

    let buildSyncManual () =
        let mutable result = Ok 0

        for index in 1 .. SyncDepth do
            result <- result |> Result.bind (fun value -> Ok(value + index))

        result

    let buildSyncFlow () =
        let mutable flow = Flow.succeed 0

        for index in 1 .. SyncDepth do
            flow <- flow |> Flow.bind (fun value -> Flow.succeed(value + index))

        flow

    let buildAsyncManual () =
        let rec loop index value =
            async {
                if index > AsyncDepth then
                    return Ok value
                else
                    let! next = async.Return(value + index)
                    return! loop (index + 1) next
            }

        fun () -> loop 1 0

    let buildReaderManual () =
        fun (environment: ReaderEnv) ->
            async {
                let mutable value = environment.Prefix.Length

                for index in 1 .. ReaderDepth do
                    value <- value + index

                return Ok value
            }

    let buildAsyncFlow () =
        let mutable workflow = Flow.succeed 0

        for index in 1 .. AsyncDepth do
            workflow <-
                workflow
                |> Flow.bind (fun value ->
                    flow {
                        let! next = async { return value + index }
                        return next
                    })

        workflow

    let buildReaderFlow () =
        let mutable workflow =
            Flow.env
            |> Flow.map (fun environment -> environment.Prefix.Length)

        for index in 1 .. ReaderDepth do
            workflow <- workflow |> Flow.map (fun value -> value + index)

        workflow

    let private contactSchema =
        schema<SchemaContact> {
            field "name" _.Name
            field "age" _.Age
            construct (fun name age -> { Name = name; Age = age })
        }

    let buildSchemaPlanSummary () =
        Schema.compilePlan (SummaryFactory<SchemaContact>()) contactSchema

    /// Exercises the type-directed constraint catalogue under Fable. The SRTP dispatchers behind `present`,
    /// `blank`, the cardinality family, and `optional` are the part of the design most at risk on this target,
    /// and code-point text sizing must agree with .NET on supplementary characters.
    /// Fable erases a Guid to a plain string and a TimeSpan to a number, so a boxed type test labels them
    /// `Text` and `Integer` here while .NET labels them correctly. Operand projection resolves on the static
    /// type instead; this asserts the two platforms describe the same constraint the same way.
    let runOperandAgreement () =
        let guid = Guid.Parse "4f489f3b-cd3c-4f53-b99b-fca552f8994d"
        let span = TimeSpan.FromMinutes 1.0

        let describes (expected: ConstraintAtom) (constraint': Constraint<'value>) =
            (Constraint.inspect constraint').Expression = ConstraintExpression.Atom expected

        [ describes (RelationAtom(Compared(Equal, ConstraintValue.Guid guid))) (Constraint.equalTo guid)
          describes (RelationAtom(Compared(AtLeast, ConstraintValue.TimeSpan span))) (Constraint.atLeast span)
          describes (MembershipAtom(OneOf [ ConstraintValue.Guid guid ])) (Constraint.oneOf [ guid ])
          describes (RelationAtom(Compared(Equal, ConstraintValue.Text "ada"))) (Constraint.equalTo "ada")
          describes (RelationAtom(Compared(AtLeast, ConstraintValue.Integer 3L))) (Constraint.atLeast 3) ]
        |> List.forall id

    let runConstraintSurface () =
        let name: Constraint<string> = Constraint.all [ Constraint.present; Constraint.lengthBetween 2 40 ]
        let tags: Constraint<string list> = Constraint.all [ Constraint.minLength 1; Constraint.distinct ]
        let nickname: Constraint<string option> = Constraint.optional (Constraint.minLength 2)
        let ttl: Constraint<int> = Constraint.any (Constraint.equalTo -1) [ Constraint.atLeast 1 ]
        let emoji: Constraint<string> = Constraint.length 1
        let reserved: Constraint<string> = Constraint.noneOf [ "admin"; "root" ]
        let excluded: Constraint<string list> = Constraint.notContains "internal"

        [ Constraint.test name "Ada"
          not (Constraint.test name " ")
          Constraint.test tags [ "a"; "b" ]
          not (Constraint.test tags [ "a"; "a" ])
          Constraint.test nickname None
          Constraint.test nickname (Some "Ada")
          not (Constraint.test nickname (Some "A"))
          Constraint.test (Constraint.blank: Constraint<int voption>) ValueNone
          Constraint.test (Constraint.present: Constraint<int voption>) (ValueSome 1)
          Constraint.test ttl -1
          Constraint.test ttl 5
          not (Constraint.test ttl 0)
          // One code point, two UTF-16 units: JavaScript and .NET must agree.
          Constraint.test emoji "\U0001F600"
          Constraint.test Constraint.numeric "345"
          not (Constraint.test Constraint.numeric "\u0663\u0664\u0665")
          // Blankness must mean the same thing on both runtimes, or the exported pattern is sound on one and
          // not the other. U+FEFF is the character JavaScript calls whitespace and .NET Core does not.
          not (Constraint.test (Constraint.present: Constraint<string>) "\ufeff")
          not (Constraint.test (Constraint.present: Constraint<string>) "\u0085")
          not (Constraint.test Constraint.trimmed "\ufeffAda")
          Constraint.test Constraint.trimmed "Ada"
          Constraint.test reserved "ada"
          not (Constraint.test reserved "admin")
          Constraint.test excluded [ "public" ]
          not (Constraint.test excluded [ "internal" ])
          (match Constraint.check name "" with
           | Error violation -> Violation.render violation <> ""
           | Ok() -> false) ]
        |> List.forall id

    let runCodecRoundTrip () =
        let codec = Json.compile contactSchema
        let original = { Name = "Ada"; Age = 37 }
        let json = Json.serialize codec original
        Json.deserialize codec json

    let runAsyncResult (workflow: unit -> Async<Result<int, string>>) =
        let mutable completed = false
        let mutable result = 0

        Async.StartWithContinuations(
            workflow (),
            (fun value ->
                result <- consumeResult value
                completed <- true),
            (fun ex -> raise ex),
            (fun _ -> raise (OperationCanceledException())))

        if completed then
            result
        else
            failwith "The Fable async benchmark workflow did not complete synchronously."

#if FABLE_COMPILER
    let runFlow (environment: 'env) (flow: Flow<'env, string, int>) =
        let mutable completed = false
        let mutable result = 0

        Async.StartWithContinuations(
            flow.ToAsync(environment),
            (fun value ->
                result <- consumeExit value
                completed <- true),
            (fun ex -> raise ex),
            (fun _ -> raise (OperationCanceledException())))

        if completed then
            result
        else
            failwith "The Fable flow benchmark did not complete synchronously."
#else
    let runFlow _ _ =
        failwith "Build Axial.Benchmarks.Fable with Fable to run this benchmark runner."
#endif

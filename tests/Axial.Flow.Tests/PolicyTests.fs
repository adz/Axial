namespace Axial.Tests

open System
open Axial.Flow
open Axial.Tests.TestSupport
open Swensen.Unquote
open Xunit

module PolicyTests =
    type PolicyTestEnv = { EnforceLimit: bool; Limit: int }

    type PolicyTestError =
        | Required
        | TooLarge
        | PolicyDisabled

    let private keepIf predicate error value =
        if predicate value then Ok value else Error error

    [<Fact>]
    let ``Policy adapts result functions to flow verification`` () =
            let requireNonBlank value =
                value |> keepIf (String.IsNullOrWhiteSpace >> not) ()

            let requireName =
                Policy.withError requireNonBlank Required

            let withinLimit =
                Policy.context
                    (fun env value -> if value <= env.Limit then Ok value else Error ())
                    (fun _ -> TooLarge)

            let optionalLimit =
                Policy.optional (fun env -> env.EnforceLimit) withinLimit

            let workflow name count =
                flow {
                    let! checkedName = name |> Flow.verify requireName
                    let! _ = count |> Flow.verify optionalLimit
                    return checkedName
                }

            test <@ workflow "Ada" 3 |> Flow.runSync { EnforceLimit = true; Limit = 5 } = Exit.Success "Ada" @>
            test <@ workflow "" 3 |> Flow.runSync { EnforceLimit = true; Limit = 5 } = Exit.Failure(Cause.Fail Required) @>
            test <@ workflow "Ada" 6 |> Flow.runSync { EnforceLimit = true; Limit = 5 } = Exit.Failure(Cause.Fail TooLarge) @>
            test <@ workflow "Ada" 6 |> Flow.runSync { EnforceLimit = false; Limit = 5 } = Exit.Success "Ada" @>
            test <@ Policy.pass { EnforceLimit = true; Limit = 5 } "value" = Ok "value" @>
            test <@ Policy.compose requireName (Policy.withError (fun value -> Ok(value.Length)) PolicyDisabled) { EnforceLimit = true; Limit = 5 } "Ada" = Ok 3 @>

    [<Fact>]
    let ``Flow verify injects the current workflow environment into the policy`` () =
        let seenLimits = ResizeArray<int>()

        let recordLimit : Policy<PolicyTestEnv, PolicyTestError, int, int> =
            fun env value ->
                seenLimits.Add env.Limit
                Ok(value + env.Limit)

        let workflow = flow {
            let! first = 1 |> Flow.verify recordLimit
            return! first |> Flow.verify recordLimit
        }

        test <@ workflow |> Flow.runSync { EnforceLimit = true; Limit = 7 } = Exit.Success 15 @>
        test <@ List.ofSeq seenLimits = [ 7; 7 ] @>

    [<Fact>]
    let ``Flow verify short-circuits the workflow when a policy fails`` () =
        let reachedAfterFailure = ref false

        let alwaysFail : Policy<PolicyTestEnv, PolicyTestError, string, string> =
            fun _ _ -> Error Required

        let workflow = flow {
            let! value = "input" |> Flow.verify alwaysFail
            reachedAfterFailure.Value <- true
            return value
        }

        test <@ workflow |> Flow.runSync { EnforceLimit = true; Limit = 5 } = Exit.Failure(Cause.Fail Required) @>
        test <@ not reachedAfterFailure.Value @>

    [<Fact>]
    let ``context-aware policies read the environment they are verified against`` () =
        let withinLimit =
            Policy.context
                (fun env value -> if value <= env.Limit then Ok value else Error value)
                (fun _ -> TooLarge)

        test <@ withinLimit { EnforceLimit = true; Limit = 5 } 5 = Ok 5 @>
        test <@ withinLimit { EnforceLimit = true; Limit = 5 } 6 = Error TooLarge @>
        test <@ withinLimit { EnforceLimit = true; Limit = 9 } 6 = Ok 6 @>

    [<Fact>]
    let ``optional policies run only when the environment predicate holds`` () =
        let evaluated = ref 0

        let countingLimit : Policy<PolicyTestEnv, PolicyTestError, int, int> =
            fun env value ->
                evaluated.Value <- evaluated.Value + 1
                if value <= env.Limit then Ok value else Error TooLarge

        let optionalLimit = Policy.optional _.EnforceLimit countingLimit

        test <@ optionalLimit { EnforceLimit = false; Limit = 5 } 99 = Ok 99 @>
        test <@ evaluated.Value = 0 @>
        test <@ optionalLimit { EnforceLimit = true; Limit = 5 } 99 = Error TooLarge @>
        test <@ evaluated.Value = 1 @>

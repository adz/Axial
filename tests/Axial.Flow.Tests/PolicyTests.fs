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

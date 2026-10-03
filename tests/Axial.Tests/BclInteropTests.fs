namespace Axial.Tests

open System
open System.Threading.Tasks
open Axial
open Swensen.Unquote
open Xunit

module BclInteropTests =
    [<Fact>]
    let ``SemaphoreSlim acquire and release`` () =
        let semaphore = new System.Threading.SemaphoreSlim(1)

        let workflow =
            flow {
                do! SemaphoreSlim.acquire semaphore
                do! SemaphoreSlim.release semaphore
            }

        test <@ Flow.runSync () workflow = Exit.Success () @>
        test <@ semaphore.CurrentCount = 1 @>

    [<Fact>]
    let ``SemaphoreSlim withPermit releases on failure`` () =
        let semaphore = new System.Threading.SemaphoreSlim(1)

        let workflow = SemaphoreSlim.withPermit semaphore (Flow.fail "boom")

        test <@ Flow.runSync () workflow = Exit.Failure(Cause.Fail "boom") @>
        test <@ semaphore.CurrentCount = 1 @>

    [<Fact>]
    let ``Deferred fromTask completes from a completed task`` () =
        let result =
            flow {
                let! deferred = Deferred.fromTask (Task.FromResult 42)
                return! Deferred.await deferred
            }
            |> Flow.runSync ()

        test <@ result = Exit.Success 42 @>

    [<Fact>]
    let ``Deferred fromTask surfaces a faulted task as a defect`` () =
        let result =
            flow {
                let! deferred = Deferred.fromTask (Task.FromException<int>(InvalidOperationException "boom"))
                return! Deferred.await deferred
            }
            |> Flow.runSync ()

        test <@ match result with
                | Exit.Failure cause -> Cause.defects cause |> List.exists (fun error -> error.Message = "boom")
                | _ -> false @>

    [<Fact>]
    let ``Deferred toTask completes from a deferred`` () =
        let deferred =
            match Flow.runSync () (Deferred.make<unit, string, int> ()) with
            | Exit.Success value -> value
            | other -> failwithf "Expected a deferred, got %A" other

        let task = Deferred.toTask id deferred
        test <@ not task.IsCompleted @>

        test <@ Flow.runSync () (Deferred.succeed 7 deferred) = Exit.Success true @>
        test <@ task.Result = 7 @>

    [<Fact>]
    let ``Deferred toTask propagates a typed failure as an exception`` () =
        let deferred =
            match Flow.runSync () (Deferred.make<unit, string, int> ()) with
            | Exit.Success value -> value
            | other -> failwithf "Expected a deferred, got %A" other

        let task = Deferred.toTask id deferred

        test <@ Flow.runSync () (Deferred.fail "bad" deferred) = Exit.Success true @>

        let error =
            try
                task.GetAwaiter().GetResult() |> ignore
                null
            with error -> error

        test <@ error <> null && error.Message = "bad" @>

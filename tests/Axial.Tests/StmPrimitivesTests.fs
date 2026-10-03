namespace Axial.Tests

open Axial
open Axial.State
open Swensen.Unquote
open Xunit

module StmPrimitivesTests =
    [<Fact>]
    let ``TRef modify returns the produced value and writes the new state`` () =
        let result =
            stm {
                let! tref = TRef.make 10
                let! produced = TRef.modify (fun n -> n * 2, n + 1) tref
                let! current = TRef.get tref
                return produced, current
            }
            |> STM.atomically
            |> Flow.runSync ()

        test <@ result = Exit.Success(20, 11) @>

    [<Fact>]
    let ``TRef getAndSet getAndUpdate and updateAndGet return the right values`` () =
        let result =
            stm {
                let! tref = TRef.make 1
                let! previous = TRef.getAndSet 5 tref
                let! beforeUpdate = TRef.getAndUpdate (fun n -> n + 1) tref
                let! afterUpdate = TRef.updateAndGet (fun n -> n * 10) tref
                return previous, beforeUpdate, afterUpdate
            }
            |> STM.atomically
            |> Flow.runSync ()

        test <@ result = Exit.Success(1, 5, 60) @>

    [<Fact>]
    let ``TQueue offers and takes in FIFO order`` () =
        let result =
            stm {
                let! queue = TQueue.unbounded
                do! TQueue.offer 1 queue
                do! TQueue.offer 2 queue
                let! size = TQueue.size queue
                let! first = TQueue.take queue
                let! second = TQueue.take queue
                let! empty = TQueue.isEmpty queue
                return size, first, second, empty
            }
            |> STM.atomically
            |> Flow.runSync ()

        test <@ result = Exit.Success(2, 1, 2, true) @>

    [<Fact>]
    let ``bounded TQueue offer retries when full`` () =
        let result =
            stm {
                let! queue = TQueue.bounded 1
                do! TQueue.offer 1 queue
                let! outcome =
                    STM.orElse
                        (stm {
                            do! TQueue.offer 2 queue
                            return "offered"
                        })
                        (stm { return "full" })
                return outcome
            }
            |> STM.atomically
            |> Flow.runSync ()

        test <@ result = Exit.Success "full" @>

    [<Fact>]
    let ``TSemaphore acquires and releases permits`` () =
        let result =
            stm {
                let! semaphore = TSemaphore.make 1
                do! TSemaphore.acquire semaphore
                let! available = TSemaphore.available semaphore
                do! TSemaphore.release semaphore
                let! after = TSemaphore.available semaphore
                return available, after
            }
            |> STM.atomically
            |> Flow.runSync ()

        test <@ result = Exit.Success(0, 1) @>

    [<Fact>]
    let ``TPromise completes with a value once`` () =
        let result =
            stm {
                let! promise = TPromise.make<string, int>
                let! polled = TPromise.poll promise
                let! completed = TPromise.succeed 42 promise
                let! again = TPromise.succeed 99 promise
                let! value = TPromise.await promise
                return polled, completed, again, value
            }
            |> STM.atomically
            |> Flow.runSync ()

        test <@ result = Exit.Success(None, true, false, Ok 42) @>

    [<Fact>]
    let ``TPromise can fail and await the typed error`` () =
        let result =
            stm {
                let! promise = TPromise.make<string, int>
                let! completed = TPromise.fail "boom" promise
                let! value = TPromise.await promise
                return completed, value
            }
            |> STM.atomically
            |> Flow.runSync ()

        test <@ result = Exit.Success(true, Error "boom") @>

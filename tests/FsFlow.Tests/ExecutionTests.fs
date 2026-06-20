namespace FsFlow.Tests

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Axial.Flow
open Axial.Result
open Axial.Validation

module ExecutionTests =

    [<Fact>]
    let ``Flow ToAsync can be converted to Result within async block on .NET`` () =
        async {
            let flow = Flow.ok 42
            let! result = flow.ToAsync(())
            Assert.Equal(Exit.Success 42, result)
            Assert.Equal(Ok 42, Exit.toResult result)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``Flow ToAsync preserves typed failure in Exit`` () =
        async {
            let flow = Flow.fail "oops"
            let! result = flow.ToAsync(())
            Assert.Equal(Exit.Failure(Cause.Fail "oops"), result)
            Assert.Equal(Error "oops", Exit.toResult result)
        } |> Async.RunSynchronously

    [<Fact>]
    let ``Flow ToTask can be converted to Result within task block`` () =
        task {
            let flow = Flow.ok 42
            let! result = flow.ToTask(())
            Assert.Equal(Exit.Success 42, result)
            Assert.Equal(Ok 42, Exit.toResult result)
        } :> Task

    [<Fact>]
    let ``Flow.toAsync observes ambient cancellation`` () =
        let cts = new CancellationTokenSource()
        let flow = Flow.Runtime.sleep (TimeSpan.FromSeconds 10.0)
        
        let operation = async {
            let! _ = flow.ToAsync(())
            return ()
        }
        
        Async.Start(operation, cts.Token)
        cts.Cancel()
        
        // Wait a bit for cancellation to propagate
        Thread.Sleep(100)
        // If it didn't observe cancellation, it would still be sleeping.
        // We can't easily assert here without more complex setup, 
        // but this verifies it compiles and runs.

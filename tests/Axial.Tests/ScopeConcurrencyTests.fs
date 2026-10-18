namespace Axial.Tests

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Axial
open Xunit

module ScopeConcurrencyTests =
    let private doneTask () = Task.CompletedTask

    [<Fact>]
    let ``external monitor on a scope does not block registration`` () =
        let scope = new Scope()
        use started = new ManualResetEventSlim(false)
        let mutable work = Task.CompletedTask
        Monitor.Enter scope
        try
            work <- Task.Run(fun () ->
                started.Set()
                scope.AddFinalizer(fun _ -> doneTask ()))
            Assert.True(started.Wait(TimeSpan.FromSeconds 10.0))
            Assert.True(work.Wait(TimeSpan.FromSeconds 10.0), "Scope used its public instance as a monitor")
        finally
            Monitor.Exit scope
            work.GetAwaiter().GetResult()
        scope.Close(CancellationToken.None).GetAwaiter().GetResult()

    [<Theory>]
    [<InlineData(false, false)>]
    [<InlineData(false, true)>]
    [<InlineData(true, false)>]
    [<InlineData(true, true)>]
    let ``canceled finalizers produce canceled close tasks regardless of slot count`` (two: bool, synchronous: bool) =
        let scope = new Scope()
        use source = new CancellationTokenSource()
        source.Cancel()
        if two then scope.AddFinalizer(fun _ -> doneTask ())
        if synchronous then
            scope.AddFinalizer(fun _ -> raise (OperationCanceledException(source.Token)))
        else
            scope.AddFinalizer(fun _ -> Task.FromCanceled(source.Token))
        let closed = scope.Close(CancellationToken.None)
        Assert.ThrowsAny<OperationCanceledException>(fun () -> closed.GetAwaiter().GetResult()) |> ignore
        Assert.Equal(TaskStatus.Canceled, closed.Status)

    [<Fact>]
    let ``registration racing close is either included before completion or rejected`` () =
        for _ in 1 .. 2000 do
            let scope = new Scope()
            use start = new Barrier(2)
            let mutable runs = 0
            let register = Task.Run(fun () ->
                start.SignalAndWait()
                scope.TryRegister(fun _ -> Interlocked.Increment(&runs) |> ignore; doneTask ()))
            start.SignalAndWait()
            let closed = scope.Close(CancellationToken.None)
            closed.GetAwaiter().GetResult()
            match register.GetAwaiter().GetResult() with
            | ValueSome _ -> Assert.Equal(1, Volatile.Read(&runs))
            | ValueNone -> Assert.Equal(0, Volatile.Read(&runs))
            Assert.Equal(0, scope.RegisteredCount)

    [<Fact>]
    let ``unregister racing close runs at most once`` () =
        for _ in 1 .. 2000 do
            let scope = new Scope()
            let mutable runs = 0
            let key = scope.Register(fun _ -> Interlocked.Increment(&runs) |> ignore; doneTask ())
            use start = new Barrier(2)
            let unregister = Task.Run(fun () -> start.SignalAndWait(); scope.Unregister key)
            start.SignalAndWait()
            scope.Close(CancellationToken.None).GetAwaiter().GetResult()
            unregister.GetAwaiter().GetResult()
            Assert.InRange(Volatile.Read(&runs), 0, 1)
            Assert.Equal(0, scope.RegisteredCount)

    [<Fact>]
    let ``single slot and dictionary transitions retain LIFO order`` () =
        for _ in 1 .. 2000 do
            let scope = new Scope()
            let order = ResizeArray<int>()
            let add n = scope.Register(fun _ -> order.Add n; doneTask ())
            add 1 |> ignore
            let second = add 2
            scope.Unregister second
            let third = add 3
            scope.Close(CancellationToken.None).GetAwaiter().GetResult()
            Assert.Equal<int>([| 3; 1 |], order)
            Assert.Equal(0, scope.RegisteredCount)

    [<Fact>]
    let ``branch first attachment racing parent close and empty close never leaks`` () =
        for _ in 1 .. 2000 do
            let parent = new Scope()
            let child = Scope.Branch parent
            let mutable runs = 0
            use start = new Barrier(2)
            let register = Task.Run(fun () ->
                start.SignalAndWait()
                child.TryRegister(fun _ -> Interlocked.Increment(&runs) |> ignore; doneTask ()))
            start.SignalAndWait()
            child.CloseIfEmpty()
            parent.Close(CancellationToken.None).GetAwaiter().GetResult()
            match register.GetAwaiter().GetResult() with
            | ValueSome _ -> Assert.Equal(1, Volatile.Read(&runs))
            | ValueNone -> Assert.Equal(0, Volatile.Read(&runs))
            Assert.Equal(0, parent.RegisteredCount)
            child.Close(CancellationToken.None).GetAwaiter().GetResult()

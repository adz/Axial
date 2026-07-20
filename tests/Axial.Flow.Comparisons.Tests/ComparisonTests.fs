namespace Axial.Flow.Comparisons.Tests

open System
open System.Collections.Concurrent
open System.IO
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Axial.Flow.Comparisons
open Swensen.Unquote
open Xunit

/// Every test here guards a claimed guarantee from one comparison scenario: remove the
/// guarantee (finalizer, selective retry, loser interruption, atomic commit) and the
/// corresponding test fails.
module CheckoutTests =
    open CheckoutCompensation

    type private Fakes(chargeOutcome: Result<PaymentId, PaymentError>) =
        member val Released = ConcurrentBag<ReservationId>()

        member this.Inventory =
            { new IInventory with
                member _.Reserve(sku, _) = Task.FromResult(Ok(ReservationId $"res-{sku}"))
                member _.Release reservation =
                    this.Released.Add reservation
                    Task.CompletedTask }

        member _.Payments =
            { new IPayments with
                member _.Charge _ = Task.FromResult chargeOutcome }

        member _.Shipping =
            { new IShipping with
                member _.CreateShipment reservation = Task.FromResult(Ok(ShipmentId "ship-1")) }

    [<Fact>]
    let ``flow checkout releases the reservation when the charge fails`` () =
        let fakes = Fakes(Error(CardDeclined "insufficient funds"))
        let env: WithFlow.CheckoutEnv = { Inventory = fakes.Inventory; Payments = fakes.Payments; Shipping = fakes.Shipping }

        let exit = Flow.runSync env (WithFlow.checkout "sku-1" 10m)

        test <@ exit = Exit.Failure(Cause.Fail(Payment(CardDeclined "insufficient funds"))) @>
        test <@ List.ofSeq fakes.Released = [ ReservationId "res-sku-1" ] @>

    [<Fact>]
    let ``flow checkout releases the reservation on a defect inside fulfilment`` () =
        let fakes = Fakes(Ok(PaymentId "pay-1"))

        let throwingShipping =
            { new IShipping with
                member _.CreateShipment _ = raise (InvalidOperationException "carrier SDK bug") }

        let env: WithFlow.CheckoutEnv = { Inventory = fakes.Inventory; Payments = fakes.Payments; Shipping = throwingShipping }

        let exit = Flow.runSync env (WithFlow.checkout "sku-1" 10m)

        test <@ (match exit with Exit.Failure(Cause.Die _) -> true | _ -> false) @>
        test <@ fakes.Released.Count = 1 @>

    [<Fact>]
    let ``ordinary buggy checkout leaks the reservation on the payment path`` () =
        let fakes = Fakes(Error(CardDeclined "insufficient funds"))

        let run () =
            (Ordinary.checkoutBuggy fakes.Inventory fakes.Payments fakes.Shipping "sku-1" 10m).GetAwaiter().GetResult()

        Assert.Throws<Ordinary.CheckoutFailed>(fun () -> run () |> ignore) |> ignore
        // The reservation was never released — the bug the compiler could not see.
        test <@ fakes.Released.Count = 0 @>

    [<Fact>]
    let ``ordinary correct checkout releases on the payment path`` () =
        let fakes = Fakes(Error(CardDeclined "insufficient funds"))

        let run () =
            (Ordinary.checkout fakes.Inventory fakes.Payments fakes.Shipping "sku-1" 10m).GetAwaiter().GetResult()

        Assert.Throws<Ordinary.CheckoutFailed>(fun () -> run () |> ignore) |> ignore
        test <@ fakes.Released.Count = 1 @>

module RetryTests =
    open RetryBudget
    open Axial.Flow.HttpClient

    /// An IHttp whose first `failures` sends fail with a transport error.
    type private FlakyHttp(failures: int, body: string) =
        let attempts = ref 0
        member _.Attempts = attempts.Value

        interface IHttp with
            member _.Send(_, _) =
                async {
                    let n = Interlocked.Increment attempts

                    if n <= failures then
                        return Error(HttpError.ConnectionFailed("GET https://rates.example/gbp-usd", "socket reset"))
                    else
                        return Ok(Response.create DateTimeOffset.UtcNow 200 body)
                }

    type private HttpEnv =
        { Http: IHttp }

        interface IHas<IHttp> with
            member this.Service = this.Http

    [<Fact>]
    let ``transient transport failures are retried within the budget`` () =
        let http = FlakyHttp(2, "1.2345")
        let exit = Flow.runSync { Http = http } (WithFlow.fetchRate "https://rates.example/gbp-usd" "GBP/USD")

        test <@ exit = Exit.Success { Pair = "GBP/USD"; Value = 1.2345m } @>
        test <@ http.Attempts = 3 @>

    [<Fact>]
    let ``the budget is finite: a fourth transient failure surfaces as Transport`` () =
        let http = FlakyHttp(99, "1.2345")
        let exit = Flow.runSync { Http = http } (WithFlow.fetchRate "https://rates.example/gbp-usd" "GBP/USD")

        test <@ (match exit with Exit.Failure(Cause.Fail(Transport _)) -> true | _ -> false) @>
        test <@ http.Attempts = 3 @>

    [<Fact>]
    let ``a malformed successful response is never retried`` () =
        let http = FlakyHttp(0, "not-a-rate")
        let exit = Flow.runSync { Http = http } (WithFlow.fetchRate "https://rates.example/gbp-usd" "GBP/USD")

        test <@ exit = Exit.Failure(Cause.Fail(Malformed "not-a-rate")) @>
        test <@ http.Attempts = 1 @>

    [<Fact>]
    let ``a hung request becomes TimedOut instead of hanging the caller`` () =
        let hangingHttp =
            { new IHttp with
                member _.Send(_, cancellationToken) =
                    async {
                        do! Async.Sleep 30_000
                        return Error(HttpError.ConnectionFailed("GET https://rates.example/gbp-usd", "unreachable"))
                    } }

        // Shrink the wall-clock cost: the two-second policy timeout is the guarantee under test,
        // so run it for real but assert it fires (the request would otherwise take 30 s).
        let exit = Flow.runSync { Http = hangingHttp } (WithFlow.fetchRate "https://rates.example/gbp-usd" "GBP/USD")

        test <@ exit = Exit.Failure(Cause.Fail TimedOut) @>

    [<Fact>]
    let ``ordinary version retries transients but not malformed bodies`` () =
        let attempts = ref 0

        let send (_: CancellationToken) =
            task {
                let n = Interlocked.Increment attempts
                if n < 3 then return raise (TransientTransportException "socket reset") else return "not-a-rate"
            }

        let result =
            (Ordinary.fetchRate send "GBP/USD" 3 (TimeSpan.FromMilliseconds 1.0) (TimeSpan.FromSeconds 2.0) CancellationToken.None)
                .GetAwaiter()
                .GetResult()

        test <@ result = Error(Malformed "not-a-rate") @>
        test <@ attempts.Value = 3 @>

module DashboardTests =
    open DashboardFanOut

    let private account () = { Id = "a-1"; Name = "Ada" }

    let private accounts () =
        { new IAccounts with
            member _.Load(_, _) = Task.FromResult(Ok(account ())) }

    let private orders (result: Result<Order list, string>) =
        { new IOrders with
            member _.Recent(_, _) = Task.FromResult result }

    [<Fact>]
    let ``recommendation failure falls back to an empty list`` () =
        let env: WithFlow.DashboardEnv =
            { Accounts = accounts ()
              Orders = orders (Ok [ { Id = "o-1"; Total = 9m } ])
              Recommendations =
                { new IRecommendations with
                    member _.For(_, _) = Task.FromResult(Error "model offline") } }

        let exit = Flow.runSync env (WithFlow.loadPage "a-1")

        test
            <@ exit = Exit.Success
                          { Account = account ()
                            Orders = [ { Id = "o-1"; Total = 9m } ]
                            Recommendations = [] } @>

    [<Fact>]
    let ``a mandatory failure interrupts the slow sibling`` () =
        task {
            let siblingCancelled = new TaskCompletionSource<bool>()

            let slowRecommendations =
                { new IRecommendations with
                    member _.For(_, cancellationToken) =
                        task {
                            use _ = cancellationToken.Register(fun () -> siblingCancelled.TrySetResult true |> ignore)
                            do! Task.Delay(30_000, cancellationToken)
                            return Ok []
                        } }

            let env: WithFlow.DashboardEnv =
                { Accounts = accounts ()
                  Orders = orders (Error "orders store down")
                  Recommendations = slowRecommendations }

            let exit = Flow.runSync env (WithFlow.loadPage "a-1")

            test <@ (match exit with Exit.Failure cause -> (string cause).Contains "OrdersUnavailable" | _ -> false) @>

            let! cancelled = siblingCancelled.Task.WaitAsync(TimeSpan.FromSeconds 30.0)
            test <@ cancelled @>
        }

module WorkspaceTests =
    open ScopedWorkspace
    open Axial.Flow.FileSystem

    let private env () : WithFlow.WorkspaceEnv = { FileSystem = FileSystem.live }

    let private tempEntriesCreatedBy (run: unit -> 'a) =
        let before = Directory.GetDirectories(Path.GetTempPath()) |> Set.ofArray
        run () |> ignore
        let after = Directory.GetDirectories(Path.GetTempPath()) |> Set.ofArray
        Set.difference after before

    [<Fact>]
    let ``the workspace is removed when the gate fails`` () =
        let leaked = tempEntriesCreatedBy (fun () -> Flow.runSync (env ()) (WithFlow.importBatch []))
        test <@ Set.isEmpty leaked @>

    [<Fact>]
    let ``the workspace is removed on success too`` () =
        let leaked = tempEntriesCreatedBy (fun () -> Flow.runSync (env ()) (WithFlow.importBatch [ "a"; "b" ]))
        test <@ Set.isEmpty leaked @>

    [<Fact>]
    let ``flow import returns the record count on success`` () =
        test <@ Flow.runSync (env ()) (WithFlow.importBatch [ "a"; "b" ]) = Exit.Success 2 @>

    [<Fact>]
    let ``the ordinary leaky version leaves the directory behind`` () =
        let leaked =
            tempEntriesCreatedBy (fun () ->
                try
                    Ordinary.importBatchLeaky([]).GetAwaiter().GetResult() |> ignore
                with _ ->
                    ())

        test <@ Set.count leaked = 1 @>
        // Clean up after proving the leak.
        leaked |> Set.iter (fun path -> Directory.Delete(path, recursive = true))

module WiringTests =
    open ReportWiring
    open Axial.Flow.Console
    open Axial.Flow.FileSystem
    open Axial.Flow.PlatformService

    /// A capture-only console; everything except output raises, keeping the double honest.
    type private CaptureConsole() =
        let output = System.Text.StringBuilder()
        let unsupported () : 'a = failwith "not used by this test"
        member _.Output = output.ToString()

        interface IConsole with
            member _.In = unsupported ()
            member _.Out = unsupported ()
            member _.Error = unsupported ()
            member _.InputEncoding with get () = unsupported () and set _ = unsupported ()
            member _.OutputEncoding with get () = unsupported () and set _ = unsupported ()
            member _.IsInputRedirected = unsupported ()
            member _.IsOutputRedirected = unsupported ()
            member _.IsErrorRedirected = unsupported ()
            member _.KeyAvailable = unsupported ()
            member _.Read() = unsupported ()
            member _.ReadLine() = unsupported ()
            member _.ReadKey _ = unsupported ()
            member _.Write value = output.Append value |> ignore
            member _.WriteLine value = output.AppendLine value |> ignore
            member _.WriteError _ = unsupported ()
            member _.WriteErrorLine _ = unsupported ()
            member _.OpenStandardInput() = unsupported ()
            member _.OpenStandardOutput() = unsupported ()
            member _.OpenStandardError() = unsupported ()
            member _.Clear() = unsupported ()
            member _.Beep() = unsupported ()
            member _.ResetColor() = unsupported ()
            member _.ForegroundColor with get () = unsupported () and set _ = unsupported ()
            member _.BackgroundColor with get () = unsupported () and set _ = unsupported ()
            member _.CursorLeft with get () = unsupported () and set _ = unsupported ()
            member _.CursorTop with get () = unsupported () and set _ = unsupported ()
            member _.CursorVisible with get () = unsupported () and set _ = unsupported ()
            member _.SetCursorPosition(_, _) = unsupported ()
            member _.Title with get () = unsupported () and set _ = unsupported ()
            member _.TreatControlCAsInput with get () = unsupported () and set _ = unsupported ()

    [<Fact>]
    let ``a fixed clock and in-memory store make the report deterministic`` () =
        let saved = ConcurrentDictionary<string, string>()

        let store =
            { new IReportStore with
                member _.Save(name, body) =
                    saved[name] <- body
                    Ok() }

        let console = CaptureConsole()
        let sourcePath = Path.GetTempFileName()
        File.WriteAllText(sourcePath, "42 widgets")

        let env: WithFlow.ReportEnv =
            { Clock = Clock.fromValue (DateTimeOffset(2026, 7, 17, 8, 0, 0, TimeSpan.Zero))
              FileSystem = FileSystem.live
              Console = console
              Store = store }

        let exit = Flow.runSync env (WithFlow.writeDailyReport sourcePath)

        test <@ exit = Exit.Success "daily-2026-07-17.txt" @>
        test <@ saved["daily-2026-07-17.txt"] = "42 widgets" @>
        test <@ console.Output.Contains "wrote daily-2026-07-17.txt" @>

    [<Fact>]
    let ``a rejected save surfaces as the typed report error`` () =
        let store =
            { new IReportStore with
                member _.Save(_, _) = Error "quota exceeded" }

        let env: WithFlow.ReportEnv =
            { Clock = Clock.fromValue DateTimeOffset.UnixEpoch
              FileSystem = FileSystem.live
              Console = CaptureConsole()
              Store = store }

        let sourcePath = Path.GetTempFileName()
        let exit = Flow.runSync env (WithFlow.writeDailyReport sourcePath)

        test <@ exit = Exit.Failure(Cause.Fail(StoreRejected "quota exceeded")) @>

module PipelineTests =
    open OutputPipeline

    [<Fact>]
    let ``the pipeline persists parsed records and reports the count`` () =
        let persisted = ConcurrentQueue<string>()

        let persist record =
            persisted.Enqueue record
            Ok()

        let lines: FlowStream<unit, RecordError, string> = FlowStream.fromSeq [ "alpha"; "beta" ]
        let exit = Flow.runSync () (WithFlow.processLines lines persist)

        test <@ exit = Exit.Success 2 @>
        test <@ List.ofSeq persisted = [ "ALPHA"; "BETA" ] @>

    [<Fact>]
    let ``a consumer failure stops the producer from being pulled further`` () =
        let pulled = ref 0

        let lines: FlowStream<unit, RecordError, string> =
            Seq.initInfinite (fun index ->
                Interlocked.Increment pulled |> ignore
                if index = 1 then "#comment" else $"line-{index}")
            |> FlowStream.fromSeq

        let exit = Flow.runSync () (WithFlow.processLines lines (fun _ -> Ok()))

        test <@ exit = Exit.Failure(Cause.Fail(BadRecord "#comment")) @>
        // The stream is cold and pull-based: after the failing element nothing else is produced.
        test <@ pulled.Value <= 3 @>

    [<Fact>]
    let ``process output streams through the same pipeline`` () =
        let persisted = ConcurrentQueue<string>()

        let persist record =
            persisted.Enqueue record
            Ok()

        let specification =
            Axial.Flow.Process.Process.command "/bin/sh" [ "-c"; "printf 'one\\ntwo\\n'" ]
            |> Axial.Flow.Process.Process.framing Axial.Flow.Process.OutputFraming.Lines

        let environment =
            { new IHas<Axial.Flow.Process.IProcess> with
                member _.Service =
                    Axial.Flow.Process.Process.live
                        (Axial.Flow.PlatformService.Clock.live)
                        (Axial.Flow.FileSystem.FileSystem.live)
                        (Axial.Flow.Console.Console.live) }

        let exit = Flow.runSync environment (WithFlow.streamProcessOutput specification persist)

        test <@ exit = Exit.Success 2 @>
        test <@ List.ofSeq persisted = [ "ONE"; "TWO" ] @>

module StmTests =
    open InventoryStm

    [<Fact>]
    let ``two racing reservations for one unit never oversell`` () =
        let program =
            flow {
                let! inventory = WithFlow.createInventory 1 1

                let! first = Flow.fork (WithFlow.reserve inventory)
                let! second = Flow.fork (WithFlow.reserve inventory)

                let! warehouseA = Flow.join first
                let! warehouseB = Flow.join second

                let! (localLeft, regionalLeft, reservations) = WithFlow.snapshot inventory
                return warehouseA, warehouseB, localLeft, regionalLeft, reservations
            }

        match Flow.runSync () program with
        | Exit.Success(warehouseA, warehouseB, localLeft, regionalLeft, reservations) ->
            // One reservation takes Local, the other falls back to Regional — never both Local.
            test <@ List.sort [ warehouseA; warehouseB ] = [ Local; Regional ] @>
            test <@ (localLeft, regionalLeft, reservations) = (0, 0, 2) @>
        | Exit.Failure cause -> failwith $"unexpected failure: %A{cause}"

    [<Fact>]
    let ``a reservation with no stock suspends until replenishment`` () =
        let program =
            flow {
                let! inventory = WithFlow.createInventory 0 0

                let! waiting = Flow.fork (WithFlow.reserve inventory)
                do! Task.Delay 50
                do! WithFlow.replenish Regional 1 inventory

                let! warehouse = Flow.join waiting
                let! (_, regionalLeft, reservations) = WithFlow.snapshot inventory
                return warehouse, regionalLeft, reservations
            }

        test <@ Flow.runSync () program = Exit.Success(Regional, 0, 1) @>

    [<Fact>]
    let ``ordinary lock-based inventory also works but each guarantee is hand-maintained`` () =
        let inventory = Ordinary.Inventory(1, 1)

        let first = inventory.Reserve CancellationToken.None
        let second = inventory.Reserve CancellationToken.None
        Task.WaitAll([| first :> Task; second :> Task |], TimeSpan.FromSeconds 5.0) |> ignore

        let localLeft, regionalLeft, reservations = inventory.Snapshot
        test <@ List.sort [ first.Result; second.Result ] = [ Local; Regional ] @>
        test <@ (localLeft, regionalLeft, reservations) = (0, 0, 2) @>

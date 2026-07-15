namespace Axial.Tests

open System
open System.Threading
open System.Threading.Tasks
open Axial.Flow
open Swensen.Unquote
open Xunit

module FiberDiagnosticsTests =
    let private dumpOf id name parentId annotations status settledAt : FiberDump =
        {
            Id = FiberId id
            Name = name
            ParentId = parentId |> Option.map FiberId
            Annotations = annotations |> Map.ofList
            StartedAt = DateTimeOffset(2026, 7, 16, 10, 0, 0, TimeSpan.Zero)
            SettledAt = settledAt
            Status = status
        }

    [<Fact>]
    let ``forkNamed records the name and fork-site annotations in the dump`` () =
        let result =
            flow {
                let! fiber = Flow.forkNamed "worker" (Flow.succeed 42)
                let! value = Flow.join fiber
                return value, Fiber.dump fiber
            }
            |> Flow.annotate "request_id" "req-9"
            |> Flow.runSync ()

        match result with
        | Exit.Success(value, dump) ->
            test <@ value = 42 @>
            test <@ dump.Name = Some "worker" @>
            test <@ dump.Annotations = Map.ofList [ "request_id", "req-9" ] @>
        | other -> failwith $"unexpected exit {other}"

    [<Fact>]
    let ``a settled fiber's dump carries a settle timestamp at or after its start`` () =
        let result =
            flow {
                let! fiber = Flow.fork (Flow.succeed 1)
                let! _ = Flow.join fiber
                return Fiber.dump fiber
            }
            |> Flow.runSync ()

        match result with
        | Exit.Success dump ->
            test <@ dump.Status = FiberStatus.Succeeded @>

            match dump.SettledAt with
            | Some settledAt -> test <@ settledAt >= dump.StartedAt @>
            | None -> failwith "expected SettledAt on a settled fiber"
        | other -> failwith $"unexpected exit {other}"

    [<Fact>]
    let ``registry tracks a live fiber and forgets it after settle`` () =
        let registry = FiberRegistry()
        use release = new SemaphoreSlim(0)

        let blocked : Flow<unit, string, unit> =
            Flow.fromTask (task { do! release.WaitAsync() })

        let result =
            flow {
                let! fiber = Flow.forkNamed "blocked-worker" blocked
                let snapshot = registry.Snapshot()
                let rendered = registry.Dump()
                release.Release() |> ignore
                let! _ = Flow.join fiber
                return snapshot, rendered
            }
            |> Flow.withFiberRegistry registry
            |> Flow.runSync ()

        match result with
        | Exit.Success(snapshot, rendered) ->
            test <@ snapshot |> List.map (fun dump -> dump.Name) = [ Some "blocked-worker" ] @>
            test <@ snapshot |> List.forall (fun dump -> dump.Status = FiberStatus.Running) @>
            test <@ rendered.Contains "blocked-worker" @>
            test <@ registry.LiveFiberCount = 0 @>
            test <@ registry.Snapshot() = [] @>
        | other -> failwith $"unexpected exit {other}"

    [<Fact>]
    let ``withFiberRegistry composes with an already-installed observer`` () =
        let registry = FiberRegistry()
        let started = ResizeArray<FiberId>()

        let recording =
            { FiberObserver.none with OnStart = fun metadata -> lock started (fun () -> started.Add metadata.Id) }

        let result =
            flow {
                let! fiber = Flow.fork (Flow.succeed ())
                return! Flow.join fiber
            }
            |> Flow.withFiberRegistry registry
            |> Flow.withFiberObserver recording
            |> Flow.runSync ()

        test <@ result = Exit.Success() @>
        test <@ started.Count = 1 @>

    [<Fact>]
    let ``renderTree draws parents, children, and orphans with names, lifetimes, and annotations`` () =
        let now = DateTimeOffset(2026, 7, 16, 10, 0, 12, 500, TimeSpan.Zero)

        let dumps =
            [
                dumpOf 1L (Some "supervisor") None [] FiberStatus.Running None
                dumpOf 2L None (Some 1L) [ "request_id", "req-9" ] FiberStatus.Running None
                dumpOf 3L (Some "poller") (Some 1L) [] FiberStatus.Failed (Some(DateTimeOffset(2026, 7, 16, 10, 0, 3, TimeSpan.Zero)))
                dumpOf 9L None (Some 99L) [] FiberStatus.Running None
            ]

        let rendered = FiberDump.renderTreeAt now dumps

        let expected =
            String.concat
                "\n"
                [
                    "#1 \"supervisor\" Running 12.5s (started 2026-07-16T10:00:00.0000000+00:00)"
                    "├─ #2 Running 12.5s (started 2026-07-16T10:00:00.0000000+00:00) [request_id=req-9]"
                    "└─ #3 \"poller\" Failed 3.0s (started 2026-07-16T10:00:00.0000000+00:00)"
                    "#9 Running 12.5s (started 2026-07-16T10:00:00.0000000+00:00)"
                ]

        test <@ rendered = expected @>

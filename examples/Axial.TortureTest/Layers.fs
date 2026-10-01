/// Layers provisioning an environment from many resources, sequentially and in parallel, with failures and
/// interruption during provisioning and use.
module Axial.TortureTest.Layers

open System
open System.Threading
open Axial
open Axial.Layers
open Axial.TortureTest.Scenario

type private Services =
    { Primary: string
      Secondary: string
      Workers: Pool<int>
      Region: string
      Clock: IClock }
    interface IHasClock with member this.Clock = this.Clock

// <snippet:torture-layers>
let run (round: Round) : Flow<Axial.ClockEnvironment, Never, Check list> =
    flow {
        let! clock = Flow.envWith (fun (env: ClockEnvironment) -> env.Clock)
        let gate = obj ()
        let acquired = ResizeArray<string>()
        let released = ResizeArray<string>()

        // A resource layer that may fail while acquiring, and logs its release.
        let resource (name: string) : Layer<string, string, string> =
            Layer.acquireRelease
                (Layer.envWith id
                 |> Layer.bind (fun region ->
                     if round.Chance 5 then
                         let unavailable = Exit.Failure(Cause.Fail $"{name} unavailable")
#if FABLE_COMPILER
                         Layer.fromAsync (fun _ _ -> async { return unavailable })
#else
                         match round.Next 3 with
                         | 0 -> Layer.fromAsync (fun _ _ -> async { return unavailable })
                         | 1 -> Layer.fromTask (fun _ _ -> Tasks.Task.FromResult unavailable)
                         | _ -> Layer.fromValueTask (fun _ _ -> Tasks.ValueTask<_>(unavailable))
#endif
                     else
                         lock gate (fun () -> acquired.Add name)
                         Layer.succeed $"{name}@{region}"))
                (fun _ -> finalizer (fun () -> lock gate (fun () -> released.Add name)))

        let pool =
            Layer.pool 3 (fun index -> resource $"worker{index}" |> Layer.map (fun _ -> index))

        // Two resources provisioned in sequence and one pool in parallel with them, plus a plain finalizer.
        let services : Layer<string, string, Services> =
            Layer.map3
                (fun (primary, secondary) workers region -> { Primary = primary; Secondary = secondary; Workers = workers; Region = region; Clock = clock })
                (Layer.zip (resource "primary") (resource "secondary"))
                (Layer.zipPar pool (Layer.widenError (Layer.succeed () : Layer<string, Never, unit>)) |> Layer.map fst)
                (Layer.merge (Layer.envWith id) (Layer.addFinalizer (finalizer ignore)) |> Layer.map fst)
            |> Layer.mapError id

        let applied : Layer<string, string, int> =
            Layer.apply (Layer.succeed (fun (text: string) -> text.Length)) (Layer.map2 (+) (Layer.succeed "ab") (Layer.succeed "c"))

        // The flow that uses the services may fail or be interrupted; every provisioned resource must still be released.
        let work : Flow<Services, string, string * int> =
            flow {
                let! services = Flow.env
                let! region = Flow.envWith _.Region
                let first = services.Workers.Next()
                let second = services.Workers.Next()
                do! Flow.sleep (TimeSpan.FromMilliseconds(float (round.Next 3)))
                if round.Chance 10 then return! Flow.fail "work failed"
                return $"{services.Primary},{services.Secondary},{region}", first + second + services.Workers.Count + Seq.length services.Workers.Instances
            }

        let! runner = work |> Layer.provide services |> Flow.localEnv (fun (_: ClockEnvironment) -> "eu") |> Flow.fork
        if round.Chance 30 then do! Flow.sleep (TimeSpan.FromMilliseconds(float (round.Next 3)))
        let! outcome = if round.Chance 30 then Fiber.interrupt runner else Fiber.await runner
        let! length = Flow.ok () |> Flow.map (fun () -> 0) |> Layer.provide (applied |> Layer.map id) |> Flow.localEnv (fun (_: ClockEnvironment) -> "eu") |> exitOf

        let acquiredNames, releasedNames = lock gate (fun () -> List.ofSeq acquired, List.ofSeq released)

        let outcomeValid =
            match outcome with
            | Exit.Success(names, total) -> names = "primary@eu,secondary@eu,eu" && total = 0 + 1 + 3 + 3
            | Exit.Failure cause -> isInterrupted outcome || not (Cause.failures cause).IsEmpty

        return
            [ check "every provisioned resource was released exactly once" (List.sort acquiredNames = List.sort releasedNames)
              check "the provisioned environment was built from its layers" outcomeValid
              check "a layer provided to a flow ran it" (length = Exit.Success 0) ]
    }
// </snippet:torture-layers>

let scenario : Scenario =
    { Name = "layers"
      Title = "Layers: provisioning resources in sequence and in parallel, with failures and interruption"
      Run = run }

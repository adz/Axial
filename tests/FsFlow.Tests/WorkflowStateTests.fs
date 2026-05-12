namespace FsFlow.Tests

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open FsFlow
open FsFlow.Tests.TestSupport
open Swensen.Unquote
open Xunit

module WorkflowStateTests =
    [<Fact>]
    let ``Ref: atomic updates`` () =
        let workflow =
            flow {
                let! r = Ref.make 10
                do! r |> Ref.update (fun v -> v + 1)
                let! v1 = r |> Ref.get
                let! v2 = r |> Ref.modify (fun v -> v * 2, "result")
                let! v3 = r |> Ref.get
                return v1, v2, v3
            }

        test <@ Flow.runSync () workflow = Exit.Success (11, "result", 22) @>

    [<Fact>]
    let ``STM: atomic transactional updates`` () =
        let workflow =
            flow {
                let! r1 = TRef.make 10 |> STM.atomically
                let! r2 = TRef.make 20 |> STM.atomically
                
                let tx = 
                    stm {
                        let! v1 = TRef.get r1
                        let! v2 = TRef.get r2
                        do! TRef.set (v1 + 5) r1
                        do! TRef.set (v2 - 5) r2
                    }
                do! tx |> STM.atomically
                
                let! v1 = TRef.get r1 |> STM.atomically
                let! v2 = TRef.get r2 |> STM.atomically
                return v1, v2
            }

        test <@ Flow.runSync () workflow = Exit.Success (15, 15) @>

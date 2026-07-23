namespace Axial.Examples

open System
open System.Threading.Tasks
open Axial.Flow
open Axial.ErrorHandling
open Microsoft.Extensions.DependencyInjection

// --- DOMAIN TYPES ---
type Order = { Id: Guid; Amount: decimal }
type OrderError = | SaveFailed of string

type IOrderRepo =
    abstract member Save : Order -> unit

// --- LEVEL 1: DIRECT RECORD ACCESS ---
module RecordExample =
    type AppEnv = { Repo: IOrderRepo }

    let saveOrder order = flow {
        let! repo = Flow.read _.Repo
        repo.Save order
    }

// --- LEVEL 2: NOMINAL SERVICES (HONEST) ---
module NominalExample =
    type IHasOrderRepo = inherit IHas<IOrderRepo>

    let saveOrder order : Flow<#IHasOrderRepo, OrderError, unit> = flow {
        let! repo = Service<IOrderRepo>.get()
        repo.Save order
    }

// --- LEVEL 3: DEPENDENCY INJECTION (PRAGMATIC) ---
module DIExample =
    let saveOrder order : Flow<IServiceProvider, OrderError, unit> = flow {
        let! repo = Service<IOrderRepo>.resolve()
        repo.Save order
    }

// --- COMBINATION: THE HONEST BRIDGE ---
module HonestBridgeExample =
    // Core logic is Honest (Level 2)
    let processOrder order = NominalExample.saveOrder order

    // Host Edge implements the bridge
    type AppEnv(sp: IServiceProvider) =
        interface NominalExample.IHasOrderRepo with
            member _.Service = sp.GetRequiredService<IOrderRepo>()

    let run (sp: IServiceProvider) order =
        let env = AppEnv(sp)
        (processOrder order).RunSynchronously(env)

// --- MOCKS FOR RUNNING ---
type MockRepo() =
    interface IOrderRepo with
        member _.Save o = printfn "Order %O saved." o.Id

type Env2 =
    { Repo: IOrderRepo }
    interface NominalExample.IHasOrderRepo with
        member x.Service = x.Repo

module ServiceExamples =
    let run () =
        let mockRepo = MockRepo() :> IOrderRepo
        let order = { Id = Guid.NewGuid(); Amount = 100m }

        // Level 1
        let env1 = { RecordExample.Repo = mockRepo }
        (RecordExample.saveOrder order).RunSynchronously(env1) |> ignore

        // Level 2
        let env2 = { Repo = mockRepo }
        (NominalExample.saveOrder order).RunSynchronously(env2) |> ignore

        // Level 3
        let services = ServiceCollection()
        services.AddSingleton<IOrderRepo>(mockRepo) |> ignore
        let sp = services.BuildServiceProvider()
        (DIExample.saveOrder order).RunSynchronously(sp :> IServiceProvider) |> ignore

        // Bridge
        HonestBridgeExample.run sp order |> ignore

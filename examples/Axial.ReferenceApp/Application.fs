namespace Axial.ReferenceApp

open Axial

open System
open System.IO
open System.Text.Json
open Axial.ErrorHandling
open Axial.Flow
open Axial.Flow.FileSystem
open Axial.Flow.PlatformService
open Axial.Schema
open Axial.Schema.Json
open Axial.Refined

[<RequireQualifiedAccess>]
type AppError =
    | InvalidInput of SchemaErrors
    | InvalidValue of RefinementError
    | InvalidContract of ContractError
    | ProductionRejected of ProductionAdmissionError
    | Domain of DomainError
    | NotFound of WorkspaceId
    | Storage of string

type IWorkspaceStore =
    abstract Load: WorkspaceId -> Flow<AppEnv, AppError, Workspace>
    abstract Save: Workspace -> Flow<AppEnv, AppError, unit>
    abstract List: unit -> Flow<AppEnv, AppError, Workspace list>
    abstract Delete: WorkspaceId -> Flow<AppEnv, AppError, unit>

and AppEnv =
    { Store: IWorkspaceStore
      Runtime: BaseRuntime
      FileSystem: IFileSystem }

    interface IHas<IClock> with
        member this.Service = this.Runtime.Clock

    interface IHas<ILog> with
        member this.Service = this.Runtime.Log

    interface IHas<IRandom> with
        member this.Service = this.Runtime.Random

    interface IHas<IGuid> with
        member this.Service = this.Runtime.Guid

    interface IHas<IEnvironmentVariables> with
        member this.Service = this.Runtime.EnvironmentVariables

    interface IHas<IFileSystem> with
        member this.Service = this.FileSystem

[<RequireQualifiedAccess>]
module FileWorkspaceStore =
    let private codec = Json.compile Contracts.workspaceV2

    let create directory : IWorkspaceStore =
        let path id = Path.Combine(directory, $"{WorkspaceId.value id:N}.json")
        let storage flow = flow |> Flow.mapError (FileSystemError.describe >> AppError.Storage)

        let loadFile file =
            flow {
                let! json = FileSystem.readAllText file |> storage

                try
                    use document = JsonDocument.Parse json

                    return!
                        Contract.parse Contracts.workspace (Data.ofJsonDocument document)
                        |> Result.mapError AppError.InvalidContract
                        |> Result.bind (Contracts.toDomain >> Result.mapError AppError.Domain)
                        |> Flow.fromResult
                with error ->
                    return! Flow.fail (AppError.Storage error.Message)
            }

        { new IWorkspaceStore with
            member _.Load id =
                flow {
                    let file = path id
                    let! exists = FileSystem.fileExists file |> storage

                    return!
                        Result.requireTrue (AppError.NotFound id) exists
                        |> Flow.fromResult
                        |> Flow.bind (fun () -> loadFile file)
                }

            member _.Save workspace =
                flow {
                    do! FileSystem.createDirectory directory |> storage
                    let file = path workspace.Id
                    let temp = file + ".tmp"
                    let json = Contracts.fromDomain workspace |> Json.serialize codec
                    do! FileSystem.writeAllText temp json |> storage
                    do! FileSystem.moveFile temp file true |> storage
                }

            member _.List() =
                flow {
                    do! FileSystem.createDirectory directory |> storage
                    let! files = FileSystem.enumerateFiles directory "*.json" SearchOption.TopDirectoryOnly |> storage
                    return! files |> Flow.traverse loadFile
                }

            member _.Delete id =
                flow {
                    let file = path id
                    let! exists = FileSystem.fileExists file |> storage
                    do! Result.requireTrue (AppError.NotFound id) exists |> Flow.fromResult
                    do! FileSystem.deleteFile file |> storage
                } }

[<RequireQualifiedAccess>]
module Application =
    let private invalidValue result = result |> Result.mapError AppError.InvalidValue

    let private store operation : Flow<AppEnv, AppError, 'value> =
        Flow.read (fun env -> operation env.Store)
        |> Flow.bind id

    let private update id change =
        flow {
            let! workspace = store (fun repository -> repository.Load id)
            let! updated = change workspace |> Result.mapError AppError.Domain
            do! store (fun repository -> repository.Save updated)
            return updated
        }

    let createWorkspace name : Flow<AppEnv, AppError, Workspace> =
        flow {
            do! Log.info "Creating workspace"
            let! generatedId = Guid.newGuid
            let! name = WorkspaceName.create name |> invalidValue
            let workspace = Workspace.create (WorkspaceId.create generatedId) name
            do! store (fun repository -> repository.Save workspace)
            return workspace
        }

    let addMember workspaceId name =
        flow {
            do! Log.info $"Adding member to workspace {WorkspaceId.value workspaceId}"
            let! generatedId = Guid.newGuid
            let! name = PersonName.create name |> invalidValue
            let member' = { Id = MemberId.create generatedId; Name = name }
            return! update workspaceId (Workspace.addMember member')
        }

    let addWorkItem workspaceId title =
        flow {
            do! Log.info $"Adding work item to workspace {WorkspaceId.value workspaceId}"
            let! generatedId = Guid.newGuid
            let! title = WorkItemTitle.create title |> invalidValue
            let item = { Id = WorkItemId.create generatedId; Title = title; Assignee = None; State = WorkItemState.Todo }
            return! update workspaceId (Workspace.addWorkItem item)
        }

    let assign workspaceId itemId memberId =
        flow {
            do! Log.info $"Assigning item {WorkItemId.value itemId} in workspace {WorkspaceId.value workspaceId}"
            return! update workspaceId (Workspace.assign itemId memberId)
        }

    let complete workspaceId itemId =
        flow {
            do! Log.info $"Completing item {WorkItemId.value itemId} in workspace {WorkspaceId.value workspaceId}"
            return! update workspaceId (Workspace.complete itemId)
        }
    let rename workspaceId name =
        flow {
            let! name = WorkspaceName.create name |> invalidValue
            return! update workspaceId (Workspace.rename name >> Ok)
        }
    let delete workspaceId = store (fun repository -> repository.Delete workspaceId)
    let get workspaceId = store (fun repository -> repository.Load workspaceId)
    let list () = store (fun repository -> repository.List())

    /// Admission from any boundary: schema proof and production policy are consumed
    /// here; the rest of the application receives the invariant-preserving domain value.
    let admitProduction value =
        value
        |> Contracts.admitProduction
        |> Result.mapError AppError.ProductionRejected
        |> Result.bind (Contracts.toDomain >> Result.mapError AppError.Domain)

    let importWorkspace value =
        flow {
            let! workspace = admitProduction value |> Flow.fromResult
            do! store (fun repository -> repository.Save workspace)
            return workspace
        }

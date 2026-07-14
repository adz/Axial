namespace Axial.ReferenceApp

open System
open System.IO
open System.Text.Json
open Axial.Flow
open Axial.Schema
open Axial.Codec
open Axial.Refined
open Axial.Validation

[<RequireQualifiedAccess>]
type AppError =
    | InvalidInput of Diagnostics<SchemaError>
    | InvalidValue of RefinementError
    | InvalidContract of ContractError
    | Domain of DomainError
    | NotFound of WorkspaceId
    | Storage of string

type IWorkspaceStore =
    abstract Load: WorkspaceId -> Result<Workspace, AppError>
    abstract Save: Workspace -> Result<unit, AppError>
    abstract List: unit -> Result<Workspace list, AppError>
    abstract Delete: WorkspaceId -> Result<unit, AppError>

type AppEnv =
    { Store: IWorkspaceStore
      NewGuid: unit -> Guid }

[<RequireQualifiedAccess>]
module FileWorkspaceStore =
    let private codec = Json.compile Contracts.workspaceV2

    let create directory : IWorkspaceStore =
        Directory.CreateDirectory(directory) |> ignore

        let path id = Path.Combine(directory, $"{WorkspaceId.value id:N}.json")

        let loadFile file =
            try
                use document = JsonDocument.Parse(File.ReadAllText file)
                match Contract.parse Contracts.workspace (RawInput.ofJsonDocument document) with
                | Ok current -> Ok(Contracts.toDomain current)
                | Error error -> Error(AppError.InvalidContract error)
            with error -> Error(AppError.Storage error.Message)

        { new IWorkspaceStore with
            member _.Load id =
                let file = path id
                if File.Exists file then loadFile file else Error(AppError.NotFound id)

            member _.Save workspace =
                try
                    let file = path workspace.Id
                    let temp = file + ".tmp"
                    Contracts.fromDomain workspace |> Json.serialize codec |> fun json -> File.WriteAllText(temp, json)
                    File.Move(temp, file, true)
                    Ok ()
                with error -> Error(AppError.Storage error.Message)

            member _.List() =
                Directory.EnumerateFiles(directory, "*.json")
                |> Seq.map loadFile
                |> Seq.fold (fun state next ->
                    match state, next with
                    | Ok values, Ok value -> Ok(value :: values)
                    | Error error, _ | _, Error error -> Error error) (Ok [])
                |> Result.map List.rev

            member _.Delete id =
                try
                    let file = path id
                    if File.Exists file then File.Delete file; Ok () else Error(AppError.NotFound id)
                with error -> Error(AppError.Storage error.Message) }

[<RequireQualifiedAccess>]
module Application =
    let private invalidValue result = result |> Result.mapError AppError.InvalidValue

    let private store operation : Flow<AppEnv, AppError, 'value> =
        Flow.read (fun env -> operation env.Store)
        |> Flow.bind Flow.fromResult

    let private update id change =
        flow {
            let! workspace = store (fun repository -> repository.Load id)
            let! updated = change workspace |> Result.mapError AppError.Domain
            do! store (fun repository -> repository.Save updated)
            return updated
        }

    let createWorkspace name : Flow<AppEnv, AppError, Workspace> =
        flow {
            let! env = Flow.env
            let! name = WorkspaceName.create name |> invalidValue
            let workspace = Workspace.create (WorkspaceId.create (env.NewGuid())) name
            do! store (fun repository -> repository.Save workspace)
            return workspace
        }

    let addMember workspaceId name =
        flow {
            let! env = Flow.env
            let! name = PersonName.create name |> invalidValue
            let member' = { Id = MemberId.create (env.NewGuid()); Name = name }
            return! update workspaceId (Workspace.addMember member')
        }

    let addWorkItem workspaceId title =
        flow {
            let! env = Flow.env
            let! title = WorkItemTitle.create title |> invalidValue
            let item = { Id = WorkItemId.create (env.NewGuid()); Title = title; Assignee = None; State = WorkItemState.Todo }
            return! update workspaceId (Workspace.addWorkItem item)
        }

    let assign workspaceId itemId memberId = update workspaceId (Workspace.assign itemId memberId)
    let complete workspaceId itemId = update workspaceId (Workspace.complete itemId)
    let rename workspaceId name =
        flow {
            let! name = WorkspaceName.create name |> invalidValue
            return! update workspaceId (fun workspace -> Ok { workspace with Name = name })
        }
    let delete workspaceId = store (fun repository -> repository.Delete workspaceId)
    let get workspaceId = store (fun repository -> repository.Load workspaceId)
    let list () = store (fun repository -> repository.List())

    /// Admission from any boundary: schema proof and contextual proof are consumed
    /// here; the rest of the application receives the invariant-preserving domain value.
    let admitProduction raw =
        let parsed = Schema.parse Contracts.workspaceV2 raw
        match parsed.Result with
        | Error diagnostics -> Error(AppError.InvalidInput diagnostics)
        | Ok value ->
            Ok value
            |> Result.bind (ContextRules.apply Contracts.productionRules)
            |> Result.map Contracts.toDomain
            |> Result.mapError AppError.InvalidInput

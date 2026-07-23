namespace Axial.ReferenceApp

open System
open Axial.ErrorHandling
open Axial.Refined
open Axial.Schema

// Wire records can represent untrusted drafts and old persisted versions.
type WorkspaceV1 = { version: int; id: Guid; name: string }
type MemberV2 = { id: Guid; name: PersonName }
type WorkItemV2 = { id: Guid; title: WorkItemTitle; assignee: Guid option; state: string }
type WorkspaceV2 =
    { version: int
      id: Guid
      name: WorkspaceName
      members: MemberV2 list
      items: WorkItemV2 list }

type NameInput = { name: string }
type TitleInput = { title: string }
type AssignmentInput = { memberId: Guid }

[<RequireQualifiedAccess>]
type ProductionAdmissionError =
    | DemoWorkspaceName

[<RequireQualifiedAccess>]
module ProductionAdmissionError =
    let describe = function
        | ProductionAdmissionError.DemoWorkspaceName -> "Production workspace names cannot end in -demo."

[<RequireQualifiedAccess>]
module Contracts =
    open Axial.Schema.Syntax

    let private requiredText create inspect maximum : Schema<'value> =
        Schema.text
        |> Schema.constrainAll [ Constraint.required; Constraint.maxLength maximum ]
        |> Schema.refine (Refinement.define create inspect)

    let workspaceName = requiredText WorkspaceName.create WorkspaceName.value 80
    let personName = requiredText PersonName.create PersonName.value 80
    let workItemTitle = requiredText WorkItemTitle.create WorkItemTitle.value 160
    let workspaceId = Schema.guid |> Schema.convert WorkspaceId.create WorkspaceId.value
    let memberId = Schema.guid |> Schema.convert MemberId.create MemberId.value
    let workItemId = Schema.guid |> Schema.convert WorkItemId.create WorkItemId.value

    let nameInput =
        schema<NameInput> {
            field "name" (fun (value: NameInput) -> value.name) {
                constrain (minLength 1)
            }
            construct (fun name -> { name = name })
        }

    let titleInput =
        schema<TitleInput> {
            field "title" (fun (value: TitleInput) -> value.title) {
                constrain (minLength 1)
            }
            construct (fun title -> { title = title })
        }

    let assignmentInput =
        schema<AssignmentInput> {
            field "memberId" (fun (value: AssignmentInput) -> value.memberId)
            construct (fun memberId -> { memberId = memberId })
        }

    let workspaceV1 =
        schema<WorkspaceV1> {
            field "version" (fun (value: WorkspaceV1) -> value.version)
            field "id" (fun (value: WorkspaceV1) -> value.id)
            field "name" (fun (value: WorkspaceV1) -> value.name) {
                constrain (minLength 1)
                constrain (maxLength 80)
            }
            construct (fun version id name -> { version = version; id = id; name = name })
        }

    let memberV2 =
        schema<MemberV2> {
            field "id" (fun (value: MemberV2) -> value.id)
            field "name" (fun (value: MemberV2) -> value.name) {
                withSchema personName
            }
            construct (fun id name -> { id = id; name = name })
        }

    let workItemV2 =
        schema<WorkItemV2> {
            field "id" (fun (value: WorkItemV2) -> value.id)
            field "title" (fun (value: WorkItemV2) -> value.title) {
                withSchema workItemTitle
            }
            field "assignee" (fun (value: WorkItemV2) -> value.assignee) {
                withSchema (Schema.option Schema.guid)
            }
            field "state" (fun (value: WorkItemV2) -> value.state) {
                constrain (oneOf [ "todo"; "done" ])
            }
            construct (fun id title assignee state ->
                { id = id; title = title; assignee = assignee; state = state })
        }

    let workspaceV2 =
        schema<WorkspaceV2> {
            field "version" (fun (value: WorkspaceV2) -> value.version)
            field "id" (fun (value: WorkspaceV2) -> value.id)
            field "name" (fun (value: WorkspaceV2) -> value.name) {
                withSchema workspaceName
            }
            field "members" (fun (value: WorkspaceV2) -> value.members) {
                withSchema (Schema.listWith memberV2)
            }
            field "items" (fun (value: WorkspaceV2) -> value.items) {
                withSchema (Schema.listWith workItemV2)
            }
            construct (fun version id name members items ->
                { version = version; id = id; name = name; members = members; items = items })
        }

    let private migrateV1 (value: WorkspaceV1) =
        WorkspaceName.create value.name
        |> Result.map (fun name -> { version = 2; id = value.id; name = name; members = []; items = [] })
        |> Result.mapError (fun error -> MigrationError.MigrationFailed(string error))

    let workspace =
        Contract.create "workspace" 2 workspaceV2
        |> Contract.supersedes 1 workspaceV1 migrateV1
        |> Contract.build (VersionSource.Field "version")

    let toDomain wire =
        let members = wire.members |> List.map (fun member' -> { Id = MemberId.create member'.id; Name = member'.name })
        let items =
            wire.items
            |> List.map (fun item ->
                { Id = WorkItemId.create item.id
                  Title = item.title
                  Assignee = item.assignee |> Option.map MemberId.create
                  State = if item.state = "done" then WorkItemState.Done else WorkItemState.Todo })

        Workspace.restore (WorkspaceId.create wire.id) wire.name members items

    let fromDomain workspace =
        { version = 2
          id = WorkspaceId.value workspace.Id
          name = workspace.Name
          members = workspace.Members |> List.map (fun member' -> { id = MemberId.value member'.Id; name = member'.Name })
          items =
              workspace.Items
              |> List.map (fun item ->
                  { id = WorkItemId.value item.Id
                    title = item.Title
                    assignee = item.Assignee |> Option.map MemberId.value
                    state = if item.State = WorkItemState.Done then "done" else "todo" }) }

    let admitProduction (value: WorkspaceV2) : Result<WorkspaceV2, ProductionAdmissionError> =
        value
        |> Result.failIf (fun value ->
            (WorkspaceName.value value.name).EndsWith("-demo", StringComparison.OrdinalIgnoreCase))
        |> Result.orError ProductionAdmissionError.DemoWorkspaceName

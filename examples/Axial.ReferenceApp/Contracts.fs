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
        SchemaCE.schema<NameInput> {
            SchemaCE.field "name" (fun (value: NameInput) -> value.name) {
                constrain (minLength 1)
            }
            SchemaCE.construct (fun name -> { name = name })
        }

    let titleInput =
        SchemaCE.schema<TitleInput> {
            SchemaCE.field "title" (fun (value: TitleInput) -> value.title) {
                constrain (minLength 1)
            }
            SchemaCE.construct (fun title -> { title = title })
        }

    let assignmentInput =
        SchemaCE.schema<AssignmentInput> {
            SchemaCE.field "memberId" (fun (value: AssignmentInput) -> value.memberId)
            SchemaCE.construct (fun memberId -> { memberId = memberId })
        }

    let workspaceV1 =
        SchemaCE.schema<WorkspaceV1> {
            SchemaCE.field "version" (fun (value: WorkspaceV1) -> value.version)
            SchemaCE.field "id" (fun (value: WorkspaceV1) -> value.id)
            SchemaCE.field "name" (fun (value: WorkspaceV1) -> value.name) {
                constrain (minLength 1)
                constrain (maxLength 80)
            }
            SchemaCE.construct (fun version id name -> { version = version; id = id; name = name })
        }

    let memberV2 =
        SchemaCE.schema<MemberV2> {
            SchemaCE.field "id" (fun (value: MemberV2) -> value.id)
            SchemaCE.field "name" (fun (value: MemberV2) -> value.name) {
                withSchema personName
            }
            SchemaCE.construct (fun id name -> { id = id; name = name })
        }

    let workItemV2 =
        SchemaCE.schema<WorkItemV2> {
            SchemaCE.field "id" (fun (value: WorkItemV2) -> value.id)
            SchemaCE.field "title" (fun (value: WorkItemV2) -> value.title) {
                withSchema workItemTitle
            }
            SchemaCE.field "assignee" (fun (value: WorkItemV2) -> value.assignee) {
                withSchema (Schema.option Schema.guid)
            }
            SchemaCE.field "state" (fun (value: WorkItemV2) -> value.state) {
                constrain (oneOf [ "todo"; "done" ])
            }
            SchemaCE.construct (fun id title assignee state ->
                { id = id; title = title; assignee = assignee; state = state })
        }

    let workspaceV2 =
        SchemaCE.schema<WorkspaceV2> {
            SchemaCE.field "version" (fun (value: WorkspaceV2) -> value.version)
            SchemaCE.field "id" (fun (value: WorkspaceV2) -> value.id)
            SchemaCE.field "name" (fun (value: WorkspaceV2) -> value.name) {
                withSchema workspaceName
            }
            SchemaCE.field "members" (fun (value: WorkspaceV2) -> value.members) {
                withSchema (Schema.listWith memberV2)
            }
            SchemaCE.field "items" (fun (value: WorkspaceV2) -> value.items) {
                withSchema (Schema.listWith workItemV2)
            }
            SchemaCE.construct (fun version id name members items ->
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

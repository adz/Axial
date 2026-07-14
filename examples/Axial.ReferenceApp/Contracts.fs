namespace Axial.ReferenceApp

open System
open Axial.Schema
open Axial.Validation

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

[<RequireQualifiedAccess>]
module Contracts =
    let private requiredText refine inspect maximum : Schema<'value> =
        Schema.text
        |> Schema.constrainAll [ Constraint.required; Constraint.maxLength maximum ]
        |> Schema.refine refine SchemaError.ofRefinementError inspect

    let workspaceName = requiredText WorkspaceName.create WorkspaceName.value 80
    let personName = requiredText PersonName.create PersonName.value 80
    let workItemTitle = requiredText WorkItemTitle.create WorkItemTitle.value 160

    let workspaceV1 =
        Schema.recordFor<WorkspaceV1, _> (fun version id name -> { version = version; id = id; name = name })
        |> Schema.field "version" _.version Schema.int
        |> Schema.field "id" _.id Schema.guid
        |> Schema.field "name" _.name (Schema.text |> Schema.constrainAll [ Constraint.required; Constraint.maxLength 80 ])
        |> Schema.build

    let memberV2 =
        Schema.recordFor<MemberV2, _> (fun id name -> { id = id; name = name })
        |> Schema.field "id" _.id Schema.guid
        |> Schema.field "name" _.name personName
        |> Schema.build

    let workItemV2 =
        Schema.recordFor<WorkItemV2, _> (fun id title assignee state ->
            { id = id; title = title; assignee = assignee; state = state })
        |> Schema.field "id" _.id Schema.guid
        |> Schema.field "title" _.title workItemTitle
        |> Schema.field "assignee" _.assignee (Schema.option Schema.guid)
        |> Schema.field "state" _.state (Schema.text |> Schema.constrain (Constraint.oneOf [ "todo"; "done" ]))
        |> Schema.build

    let workspaceV2 =
        Schema.recordFor<WorkspaceV2, _> (fun version id name members items ->
            { version = version; id = id; name = name; members = members; items = items })
        |> Schema.field "version" _.version Schema.int
        |> Schema.field "id" _.id Schema.guid
        |> Schema.field "name" _.name workspaceName
        |> Schema.field "members" _.members (Schema.list (memberV2))
        |> Schema.field "items" _.items (Schema.list (workItemV2))
        |> Schema.build

    let private migrateV1 (value: WorkspaceV1) =
        WorkspaceName.create value.name
        |> Result.map (fun name -> { version = 2; id = value.id; name = name; members = []; items = [] })
        |> Result.mapError (fun error -> MigrationError.MigrationFailed(string error))

    let workspace =
        Contract.create "workspace" 2 workspaceV2
        |> Contract.supersedes 1 workspaceV1 migrateV1
        |> Contract.build (VersionSource.Field "version")

    let toDomain wire =
        { Id = WorkspaceId.create wire.id
          Name = wire.name
          Members = wire.members |> List.map (fun member' -> { Id = MemberId.create member'.id; Name = member'.name })
          Items =
              wire.items
              |> List.map (fun item ->
                  { Id = WorkItemId.create item.id
                    Title = item.title
                    Assignee = item.assignee |> Option.map MemberId.create
                    State = if item.state = "done" then WorkItemState.Done else WorkItemState.Todo }) }

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

    let productionRules : (WorkspaceV2 -> Result<unit, Diagnostics<SchemaError>>) list =
        [ fun value ->
              if (WorkspaceName.value value.name).EndsWith("-demo", StringComparison.OrdinalIgnoreCase) then
                  ContextRules.failCustom "workspace.demo-name" "Production workspace names cannot end in -demo."
              else Ok ()
          fun value ->
              let memberIds = value.members |> List.map _.id |> Set.ofList
              match value.items |> List.tryFind (fun item -> item.assignee |> Option.exists (memberIds.Contains >> not)) with
              | Some _ -> ContextRules.failCustom "workspace.assignee.unknown" "Every assignee must be a workspace member."
              | None -> Ok () ]

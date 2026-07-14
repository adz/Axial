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
    open Axial.Schema.DSL

    let private requiredText create inspect maximum : Schema<'value> =
        text
        |> constrainAll [ required; maxLength maximum ]
        |> refine create SchemaError.ofRefinementError inspect

    let workspaceName = requiredText WorkspaceName.create WorkspaceName.value 80
    let personName = requiredText PersonName.create PersonName.value 80
    let workItemTitle = requiredText WorkItemTitle.create WorkItemTitle.value 160

    let workspaceV1 =
        recordFor<WorkspaceV1, _> (fun version id name -> { version = version; id = id; name = name })
        |> field "version" _.version int
        |> field "id" _.id guid
        |> field "name" _.name (text |> constrainAll [ required; maxLength 80 ])
        |> build

    let memberV2 =
        recordFor<MemberV2, _> (fun id name -> { id = id; name = name })
        |> field "id" _.id guid
        |> field "name" _.name personName
        |> build

    let workItemV2 =
        recordFor<WorkItemV2, _> (fun id title assignee state ->
            { id = id; title = title; assignee = assignee; state = state })
        |> field "id" _.id guid
        |> field "title" _.title workItemTitle
        |> field "assignee" _.assignee (option guid)
        |> field "state" _.state (text |> constrain (oneOf [ "todo"; "done" ]))
        |> build

    let workspaceV2 =
        recordFor<WorkspaceV2, _> (fun version id name members items ->
            { version = version; id = id; name = name; members = members; items = items })
        |> field "version" _.version int
        |> field "id" _.id guid
        |> field "name" _.name workspaceName
        |> field "members" _.members (list memberV2)
        |> field "items" _.items (list workItemV2)
        |> build

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

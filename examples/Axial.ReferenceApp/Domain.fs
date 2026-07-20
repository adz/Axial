namespace Axial.ReferenceApp

open System
open Axial.ErrorHandling
open Axial.Refined

/// Domain values use private representations so successful construction is the durable proof.
type WorkspaceId = private WorkspaceId of Guid
type MemberId = private MemberId of Guid
type WorkItemId = private WorkItemId of Guid
type WorkspaceName = private WorkspaceName of NonBlankString
type PersonName = private PersonName of NonBlankString
type WorkItemTitle = private WorkItemTitle of NonBlankString

[<RequireQualifiedAccess>]
module WorkspaceId =
    let create value = WorkspaceId value
    let value (WorkspaceId value) = value

[<RequireQualifiedAccess>]
module MemberId =
    let create value = MemberId value
    let value (MemberId value) = value

[<RequireQualifiedAccess>]
module WorkItemId =
    let create value = WorkItemId value
    let value (WorkItemId value) = value

module private RequiredText =
    let create label wrap value =
        value
        |> Refine.nonBlankString
        |> Result.map wrap
        |> Result.mapError (function
            | RefinementError.CheckFailed(_, failures) -> RefinementError.CheckFailed(label, failures)
            | error -> error)

[<RequireQualifiedAccess>]
module WorkspaceName =
    let create = RequiredText.create "WorkspaceName" WorkspaceName
    let value (WorkspaceName value) = value.Value

[<RequireQualifiedAccess>]
module PersonName =
    let create = RequiredText.create "PersonName" PersonName
    let value (PersonName value) = value.Value

[<RequireQualifiedAccess>]
module WorkItemTitle =
    let create = RequiredText.create "WorkItemTitle" WorkItemTitle
    let value (WorkItemTitle value) = value.Value

type Member =
    { Id: MemberId
      Name: PersonName }

[<RequireQualifiedAccess>]
type WorkItemState =
    | Todo
    | Done

type WorkItem =
    { Id: WorkItemId
      Title: WorkItemTitle
      Assignee: MemberId option
      State: WorkItemState }

type Workspace =
    { Id: WorkspaceId
      Name: WorkspaceName
      Members: Member list
      Items: WorkItem list }

[<RequireQualifiedAccess>]
type DomainError =
    | MemberNotFound of MemberId
    | WorkItemNotFound of WorkItemId
    | WorkItemAlreadyDone of WorkItemId
    | DuplicateMember of PersonName
    | AssigneeNotMember of MemberId

[<RequireQualifiedAccess>]
module Workspace =
    let create (id: WorkspaceId) (name: WorkspaceName) : Workspace =
        { Id = id; Name = name; Members = []; Items = [] }

    let restore (id: WorkspaceId) (name: WorkspaceName) (members: Member list) (items: WorkItem list) =
        let memberIds = members |> List.map _.Id |> Set.ofList

        items
        |> List.choose _.Assignee
        |> Collection.traverseResult (fun assignee ->
            memberIds
            |> Check.Seq.contains assignee
            |> Result.orError (DomainError.AssigneeNotMember assignee))
        |> Result.map (fun _ -> { Id = id; Name = name; Members = members; Items = items })

    let rename (name: WorkspaceName) (workspace: Workspace) = { workspace with Name = name }

    let addMember (member': Member) (workspace: Workspace) =
        workspace
        |> Result.failIf (fun workspace -> workspace.Members |> List.exists (fun item -> item.Name = member'.Name))
        |> Result.orError (DomainError.DuplicateMember member'.Name)
        |> Result.map (fun workspace -> { workspace with Members = workspace.Members @ [ member' ] })

    let addWorkItem (item: WorkItem) (workspace: Workspace) =
        Ok { workspace with Items = workspace.Items @ [ item ] }

    let assign itemId memberId workspace =
        workspace.Members
        |> Result.okIf (List.exists (fun member' -> member'.Id = memberId))
        |> Result.orError (DomainError.MemberNotFound memberId)
        |> Result.bind (fun _ ->
            workspace.Items
            |> List.tryFindIndex (fun item -> item.Id = itemId)
            |> Result.someOr (DomainError.WorkItemNotFound itemId))
        |> Result.map (fun index ->
            let item = workspace.Items[index]
            { workspace with Items = workspace.Items |> List.updateAt index { item with Assignee = Some memberId } })

    let complete itemId workspace =
        workspace.Items
        |> List.tryFindIndex (fun item -> item.Id = itemId)
        |> Result.someOr (DomainError.WorkItemNotFound itemId)
        |> Result.bind (fun index ->
            index
            |> Result.failIf (fun index -> workspace.Items[index].State = WorkItemState.Done)
            |> Result.orError (DomainError.WorkItemAlreadyDone itemId))
        |> Result.map (fun index ->
            let item = workspace.Items[index]
            { workspace with Items = workspace.Items |> List.updateAt index { item with State = WorkItemState.Done } })

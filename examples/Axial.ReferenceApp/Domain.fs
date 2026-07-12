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

[<RequireQualifiedAccess>]
module Workspace =
    let create (id: WorkspaceId) (name: WorkspaceName) : Workspace =
        { Id = id; Name = name; Members = []; Items = [] }

    let addMember (member': Member) (workspace: Workspace) =
        if workspace.Members |> List.exists (fun item -> item.Name = member'.Name) then
            Error(DomainError.DuplicateMember member'.Name)
        else
            Ok { workspace with Members = workspace.Members @ [ member' ] }

    let addWorkItem (item: WorkItem) (workspace: Workspace) =
        Ok { workspace with Items = workspace.Items @ [ item ] }

    let assign itemId memberId workspace =
        if workspace.Members |> List.exists (fun member' -> member'.Id = memberId) |> not then
            Error(DomainError.MemberNotFound memberId)
        else
            match workspace.Items |> List.tryFindIndex (fun item -> item.Id = itemId) with
            | None -> Error(DomainError.WorkItemNotFound itemId)
            | Some index ->
                let item = workspace.Items[index]
                Ok { workspace with Items = workspace.Items |> List.updateAt index { item with Assignee = Some memberId } }

    let complete itemId workspace =
        match workspace.Items |> List.tryFindIndex (fun item -> item.Id = itemId) with
        | None -> Error(DomainError.WorkItemNotFound itemId)
        | Some index when workspace.Items[index].State = WorkItemState.Done -> Error(DomainError.WorkItemAlreadyDone itemId)
        | Some index ->
            let item = workspace.Items[index]
            Ok { workspace with Items = workspace.Items |> List.updateAt index { item with State = WorkItemState.Done } }

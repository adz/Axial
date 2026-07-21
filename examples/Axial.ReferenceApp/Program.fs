module Axial.ReferenceApp.Program

open Axial

open System
open System.Net
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Axial.ErrorHandling
open Axial.Flow
open Axial.Flow.FileSystem
open Axial.Refined
open Axial.Schema
open Axial.Schema.Http
open Axial.Schema.Http.AspNetCore
open Axial.Schema.Json
open Axial.Flow.PlatformService
open Axial.Validation
open Axial.ReferenceApp

let private renderError = function
    | AppError.InvalidInput diagnostics ->
        diagnostics
        |> Diagnostics.flatten
        |> List.map (fun item -> $"{item.Path}: {SchemaError.render item.Error}")
        |> String.concat "; "
    | AppError.InvalidValue error -> RefinementError.describe error
    | AppError.ProductionRejected error -> ProductionAdmissionError.describe error
    | error -> string error

let private run (env: AppEnv) (flow: Flow<AppEnv, AppError, 'value>) =
    match flow.RunSynchronously(env) with
    | Exit.Success value -> Ok value
    | Exit.Failure(Cause.Fail error) -> Error(renderError error)
    | Exit.Failure cause -> Error(Cause.prettyPrint renderError cause)

let private id (text: string) create =
    System.Guid.TryParse text
    |> Result.fromTry
    |> Result.orError $"Invalid id: {text}"
    |> Result.map create

[<RequireQualifiedAccess>]
module Boundary =
    let workspaceCodec = Json.compile Contracts.workspaceV2
    let workspaceListSchema = Schema.listWith Contracts.workspaceV2
    let workspaceListCodec = Json.compile workspaceListSchema

    let private workspaceResponse status description spec =
        spec |> Endpoint.returnsJson status description Contracts.workspaceV2

    let openApiDocument =
        let problem spec = spec |> Endpoint.returnsProblemDetails
        let workspaceMutation summary input path =
            Endpoint.post path
            |> Endpoint.summary summary
            |> Endpoint.accepts input
            |> workspaceResponse 200 "The updated workspace."
            |> problem

        OpenApi.document
            (OpenApi.info "Axial reference application" "1.0.0")
            [ Endpoint.get "/api/workspaces"
              |> Endpoint.summary "List workspaces"
              |> Endpoint.returnsJson 200 "All persisted workspaces." workspaceListSchema
              Endpoint.get "/api/workspaces/{workspaceId}"
              |> Endpoint.summary "Get a workspace"
              |> workspaceResponse 200 "The requested workspace."
              |> problem
              Endpoint.post "/api/workspaces"
              |> Endpoint.summary "Import a workspace"
              |> Endpoint.accepts Contracts.workspaceV2
              |> workspaceResponse 201 "The admitted workspace."
              |> problem
              workspaceMutation "Add a member" Contracts.nameInput "/api/workspaces/{workspaceId}/members"
              workspaceMutation "Add a work item" Contracts.titleInput "/api/workspaces/{workspaceId}/items"
              Endpoint.post "/api/workspaces/{workspaceId}/items/{itemId}/complete"
              |> Endpoint.summary "Complete a work item"
              |> workspaceResponse 200 "The updated workspace."
              |> problem
              Endpoint.post "/api/workspaces/{workspaceId}/items/{itemId}/assign/{memberId}"
              |> Endpoint.summary "Assign a work item"
              |> workspaceResponse 200 "The updated workspace."
              |> problem
              Endpoint.delete "/api/workspaces/{workspaceId}"
              |> Endpoint.summary "Delete a workspace"
              |> Endpoint.returns 204 "The workspace was deleted."
              |> problem ]

[<RequireQualifiedAccess>]
module FormPage =
    let private encode (text: string) = WebUtility.HtmlEncode text

    let private attributes (field: FieldDescription) =
        let metadata =
            (field.Constraints |> List.map _.Metadata)
            @ (field.Schema.Constraints |> List.map _.Metadata)

        let required =
            if metadata |> List.contains ConstraintMetadata.Required then " required" else ""

        let maxLength =
            metadata
            |> List.tryPick (function
                | ConstraintMetadata.MaxLength maximum -> Some $" maxlength=\"{maximum}\""
                | _ -> None)
            |> Option.defaultValue ""

        required + maxLength

    let renderNewWorkspace (parsed: RetainedParseResult<NameInput, SchemaError> option) =
        let input = parsed |> Option.map _.Input |> Option.defaultValue (Data.Object [])
        let description = Inspect.model Contracts.nameInput

        let fields =
            description.Fields
            |> List.map (fun field ->
                let value = Data.redisplayPath field.Name input
                let errors =
                    parsed
                    |> Option.map (fun result -> result.ErrorsFor(field.Name: string))
                    |> Option.defaultValue []
                    |> List.map (SchemaError.render >> encode >> fun error -> $"<p class=\"error\">{error}</p>")
                    |> String.concat ""

                $"<label for=\"{field.Name}\">{encode field.Name}</label><input id=\"{field.Name}\" name=\"{field.Name}\" value=\"{encode value}\"{attributes field}>{errors}")
            |> String.concat "\n"

        $"""<!doctype html><html><head><title>New workspace</title><style>
body {{ font-family: system-ui, sans-serif; max-width: 32rem; margin: 2rem auto; }}
label {{ display: block; margin-top: 1rem; }} input {{ width: 100%%; padding: .4rem; }}
.error {{ color: #b00020; margin: .2rem 0 0; }}
</style></head><body><h1>New workspace</h1><form method="post">{fields}<button type="submit">Create</button></form></body></html>"""

let private summary = Contracts.fromDomain

let buildWebApp (env: AppEnv) (args: string array) =
    let builder = WebApplication.CreateBuilder(args)
    let app = builder.Build()

    let mapApplicationError error : IResult =
        match error with
        | AppError.NotFound _ -> Results.NotFound(renderError error)
        | AppError.Storage _ -> Results.Problem(renderError error, statusCode = 500)
        | _ -> Results.BadRequest(renderError error)

    let endpoint = flowEndpoint (fun _ -> env) mapApplicationError

    let listWorkspaces =
        flow {
            let! workspaces = EndpointFlow.run (fun () -> Application.list ()) ()
            return Response.json 200 Boundary.workspaceListCodec (workspaces |> List.map Contracts.fromDomain)
        }

    let getWorkspace =
        flow {
            let! workspaceId = Request.route "workspaceId" Contracts.workspaceId
            let! workspace = EndpointFlow.run Application.get workspaceId
            return Response.json 200 Boundary.workspaceCodec (Contracts.fromDomain workspace)
        }

    let createWorkspace
        (request: Schema<WorkspaceV2> -> Flow<HttpEndpointEnv<AppEnv>, EndpointError<AppError>, WorkspaceV2>) =
        flow {
            let! input = request Contracts.workspaceV2
            let! workspace = EndpointFlow.run Application.importWorkspace input
            return Response.json 201 Boundary.workspaceCodec (Contracts.fromDomain workspace)
        }

    let addMember
        (request: Schema<NameInput> -> Flow<HttpEndpointEnv<AppEnv>, EndpointError<AppError>, NameInput>)
        (response: WorkspaceId -> Workspace -> IResult) =
        flow {
            let! workspaceId = Request.route "workspaceId" Contracts.workspaceId
            let! input = request Contracts.nameInput
            let! workspace = EndpointFlow.run (fun name -> Application.addMember workspaceId name) input.name
            return response workspaceId workspace
        }

    let addItem
        (request: Schema<TitleInput> -> Flow<HttpEndpointEnv<AppEnv>, EndpointError<AppError>, TitleInput>)
        (response: WorkspaceId -> Workspace -> IResult) =
        flow {
            let! workspaceId = Request.route "workspaceId" Contracts.workspaceId
            let! input = request Contracts.titleInput
            let! workspace = EndpointFlow.run (fun title -> Application.addWorkItem workspaceId title) input.title
            return response workspaceId workspace
        }

    let completeItem (response: WorkspaceId -> Workspace -> IResult) =
        flow {
            let! workspaceId = Request.route "workspaceId" Contracts.workspaceId
            let! itemId = Request.route "itemId" Contracts.workItemId
            let! workspace = EndpointFlow.run (fun itemId -> Application.complete workspaceId itemId) itemId
            return response workspaceId workspace
        }

    let assignItemFromRoute =
        flow {
            let! workspaceId = Request.route "workspaceId" Contracts.workspaceId
            let! itemId = Request.route "itemId" Contracts.workItemId
            let! memberId = Request.route "memberId" Contracts.memberId
            let! workspace = EndpointFlow.run (fun memberId -> Application.assign workspaceId itemId memberId) memberId
            return Response.json 200 Boundary.workspaceCodec (Contracts.fromDomain workspace)
        }

    let assignItemFromForm =
        flow {
            let! workspaceId = Request.route "workspaceId" Contracts.workspaceId
            let! itemId = Request.route "itemId" Contracts.workItemId
            let! input = Request.form Contracts.assignmentInput
            let memberId = MemberId.create input.memberId
            let! _ = EndpointFlow.run (fun memberId -> Application.assign workspaceId itemId memberId) memberId
            return Response.native (Results.Redirect($"/workspaces/{WorkspaceId.value workspaceId}"))
        }

    let deleteWorkspace =
        flow {
            let! workspaceId = Request.route "workspaceId" Contracts.workspaceId
            do! EndpointFlow.run Application.delete workspaceId
            return Response.empty 204
        }

    let workspacePage =
        flow {
            let! workspaceId = Request.route "workspaceId" Contracts.workspaceId
            let! workspace = EndpointFlow.run Application.get workspaceId
            let wid = WorkspaceId.value workspace.Id
            let items =
                workspace.Items
                |> List.map (fun item ->
                    let iid = WorkItemId.value item.Id
                    $"""<li>{WebUtility.HtmlEncode(WorkItemTitle.value item.Title)} ({item.State}) <form method="post" action="/workspaces/{wid}/items/{iid}/complete" style="display:inline"><button>Complete</button></form><form method="post" action="/workspaces/{wid}/items/{iid}/assign" style="display:inline"><input name="memberId" placeholder="Member id"><button>Assign</button></form></li>""")
                |> String.concat ""
            let html =
                $"""<!doctype html><html><body><h1>{WebUtility.HtmlEncode(WorkspaceName.value workspace.Name)}</h1><ul>{items}</ul><form method="post" action="/workspaces/{wid}/members"><input name="name" placeholder="Member name"><button>Add member</button></form><form method="post" action="/workspaces/{wid}/items"><input name="title" placeholder="Work item"><button>Add item</button></form></body></html>"""
            return Response.text 200 html
        }

    let apiResponse _ workspace = Response.json 200 Boundary.workspaceCodec (Contracts.fromDomain workspace)
    let htmlResponse workspaceId _ = Response.native (Results.Redirect($"/workspaces/{WorkspaceId.value workspaceId}"))

    app.MapGet("/api/workspaces", endpoint listWorkspaces) |> ignore
    app.MapGet("/api/workspaces/{workspaceId}", endpoint getWorkspace) |> ignore
    app.MapPost("/api/workspaces", endpoint (createWorkspace Request.json)) |> ignore
    app.MapPost("/api/workspaces/{workspaceId}/members", endpoint (addMember Request.json apiResponse)) |> ignore
    app.MapPost("/api/workspaces/{workspaceId}/items", endpoint (addItem Request.json apiResponse)) |> ignore
    app.MapPost("/api/workspaces/{workspaceId}/items/{itemId}/complete", endpoint (completeItem apiResponse)) |> ignore
    app.MapPost("/api/workspaces/{workspaceId}/items/{itemId}/assign/{memberId}", endpoint assignItemFromRoute) |> ignore
    app.MapDelete("/api/workspaces/{workspaceId}", endpoint deleteWorkspace) |> ignore
    app.MapGet("/openapi.json", Func<IResult>(fun () -> SchemaResult.openApi Boundary.openApiDocument)) |> ignore

    app.MapGet("/workspaces/new", Func<IResult>(fun () -> Results.Text(FormPage.renderNewWorkspace None, "text/html"))) |> ignore

    app.MapPost("/workspaces/new", Func<HttpRequest, Task<IResult>>(fun request -> task {
        let! parsed = SchemaRequest.form Contracts.nameInput request

        match parsed.Result with
        | Error _ -> return Results.Text(FormPage.renderNewWorkspace (Some parsed), "text/html", statusCode = 400)
        | Ok input ->
            match run env (Application.createWorkspace input.name) with
            | Ok workspace -> return Results.Redirect($"/workspaces/{WorkspaceId.value workspace.Id}")
            | Error error -> return Results.BadRequest(error) })) |> ignore

    app.MapGet("/workspaces/{workspaceId}", endpoint workspacePage) |> ignore

    app.MapPost("/workspaces/{workspaceId}/members", endpoint (addMember Request.form htmlResponse)) |> ignore
    app.MapPost("/workspaces/{workspaceId}/items", endpoint (addItem Request.form htmlResponse)) |> ignore
    app.MapPost("/workspaces/{workspaceId}/items/{itemId}/complete", endpoint (completeItem htmlResponse)) |> ignore
    app.MapPost("/workspaces/{workspaceId}/items/{itemId}/assign", endpoint assignItemFromForm) |> ignore
    app

let private usage () =
    printfn "Axial reference app"
    printfn "  web [--urls URL]"
    printfn "  create-workspace NAME"
    printfn "  add-member WORKSPACE_ID NAME"
    printfn "  add-item WORKSPACE_ID TITLE"
    printfn "  assign WORKSPACE_ID ITEM_ID MEMBER_ID"
    printfn "  complete WORKSPACE_ID ITEM_ID"
    printfn "  rename WORKSPACE_ID NAME"
    printfn "  delete WORKSPACE_ID"
    printfn "  list"

[<EntryPoint>]
let main args =
    let runtime = BaseRuntime.liveValue
    let data = runtime.EnvironmentVariables.TryGet "AXIAL_REFERENCE_DATA" |> Option.defaultValue ".axial-reference-data"
    let env = { Store = FileWorkspaceStore.create data; Runtime = runtime; FileSystem = FileSystem.live }
    let execute flow =
        match run env flow with
        | Ok workspace -> printfn "%s" (Json.serialize Boundary.workspaceCodec (summary workspace)); 0
        | Error error -> eprintfn "%s" error; 1

    match args |> Array.toList with
    | "web" :: rest -> buildWebApp env (List.toArray rest) |> fun app -> app.Run(); 0
    | [ "create-workspace"; name ] -> execute (Application.createWorkspace name)
    | [ "add-member"; workspace; name ] ->
        match id workspace WorkspaceId.create with Ok workspaceId -> execute (Application.addMember workspaceId name) | Error error -> eprintfn "%s" error; 1
    | [ "add-item"; workspace; title ] ->
        match id workspace WorkspaceId.create with Ok workspaceId -> execute (Application.addWorkItem workspaceId title) | Error error -> eprintfn "%s" error; 1
    | [ "assign"; workspace; item; member' ] ->
        match id workspace WorkspaceId.create, id item WorkItemId.create, id member' MemberId.create with
        | Ok workspaceId, Ok itemId, Ok memberId -> execute (Application.assign workspaceId itemId memberId)
        | _ -> eprintfn "Invalid id."; 1
    | [ "complete"; workspace; item ] ->
        match id workspace WorkspaceId.create, id item WorkItemId.create with
        | Ok workspaceId, Ok itemId -> execute (Application.complete workspaceId itemId)
        | _ -> eprintfn "Invalid id."; 1
    | [ "rename"; workspace; name ] ->
        match id workspace WorkspaceId.create with Ok workspaceId -> execute (Application.rename workspaceId name) | Error error -> eprintfn "%s" error; 1
    | [ "delete"; workspace ] ->
        match id workspace WorkspaceId.create with
        | Error error -> eprintfn "%s" error; 1
        | Ok workspaceId -> match run env (Application.delete workspaceId) with Ok () -> 0 | Error error -> eprintfn "%s" error; 1
    | [ "list" ] ->
        match run env (Application.list ()) with
        | Ok values -> printfn "%s" (Json.serialize Boundary.workspaceListCodec (values |> List.map summary)); 0
        | Error error -> eprintfn "%s" error; 1
    | _ -> usage (); 0

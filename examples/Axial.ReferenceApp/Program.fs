module Axial.ReferenceApp.Program

open Axial

open System
open System.Net
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open OpenTelemetry.Logs
open OpenTelemetry.Metrics
open OpenTelemetry.Resources
open OpenTelemetry.Trace
open Axial.ErrorHandling
open Axial.Flow
open Axial.Flow.FileSystem
open Axial.Refined
open Axial.Schema
open Axial.Schema.Http
open Axial.Schema.Http.AspNetCore
open Axial.Schema.Json
open Axial.Flow.PlatformService
open Axial.Flow.Hosting
open Axial.Flow.Telemetry
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

    let renderHome () =
        """<!doctype html><html><head><title>Axial reference application</title><style>
body { font-family: system-ui, sans-serif; max-width: 44rem; margin: 3rem auto; padding: 0 1rem; }
li { margin: .75rem 0; } code { background: #eee; padding: .15rem .3rem; }
</style></head><body><h1>Axial reference application</h1>
<p>This runnable application demonstrates Axial schemas, refined values, typed failures, Flow, and observability.</p>
<ul>
<li><a href="/workspaces/new">Create a workspace</a></li>
<li><a href="/api/workspaces">List workspaces as JSON</a></li>
<li><a href="/openapi.json">OpenAPI document</a></li>
<li><a href="/observability/demo">Run the observability demo</a></li>
<li><a href="http://localhost:18888">Open the Aspire dashboard</a></li>
</ul>
<p>The observability demo creates structured logs, a traced Flow, named fiber spans, a fiber-dump event, and fiber metrics.</p>
</body></html>"""

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

let buildWebApp (baseEnvironment: AppEnv) (args: string array) =
    let builder = WebApplication.CreateBuilder(args)

    builder.Services
        .AddOpenTelemetry()
        .ConfigureResource(fun resource -> resource.AddService("axial-reference-app") |> ignore)
        .WithTracing(fun tracing ->
            tracing
                .AddSource("Axial.Flow")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter()
            |> ignore)
        .WithMetrics(fun metrics ->
            metrics
                .AddMeter("Axial.Flow")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter()
            |> ignore)
    |> ignore

    builder.Logging.AddOpenTelemetry(fun logging ->
        logging.IncludeFormattedMessage <- true
        logging.IncludeScopes <- true
        logging.ParseStateValues <- true
        logging.AddOtlpExporter() |> ignore)
    |> ignore

    let app = builder.Build()
    let loggerFactory = app.Services.GetRequiredService<ILoggerFactory>()
    let logger = loggerFactory.CreateLogger("Axial.ReferenceApp")
    let runtime =
        { baseEnvironment.Runtime with
            Log = MicrosoftLogging.fromFactory "Axial.ReferenceApp.Flow" loggerFactory }

    let env = { baseEnvironment with Runtime = runtime }
    let registry = FiberRegistry()

    let mapApplicationError error : IResult =
        match error with
        | AppError.NotFound _ -> Results.NotFound(renderError error)
        | AppError.Storage _ -> Results.Problem(renderError error, statusCode = 500)
        | _ -> Results.BadRequest(renderError error)

    let endpoint name application =
        application
        |> Activity.traceWith (fun error -> string error) name
        |> Flow.withFiberRegistry registry
        |> FiberTelemetry.observeWithSpans
        |> FiberMetrics.observe
        |> flowEndpoint (fun _ -> env) mapApplicationError

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
            let membersById = workspace.Members |> List.map (fun member' -> member'.Id, member') |> Map.ofList
            let members =
                workspace.Members
                |> List.map (fun member' ->
                    $"<li>{WebUtility.HtmlEncode(PersonName.value member'.Name)} <code>{MemberId.value member'.Id}</code></li>")
                |> String.concat ""

            let memberOptions =
                workspace.Members
                |> List.map (fun member' ->
                    $"<option value=\"{MemberId.value member'.Id}\">{WebUtility.HtmlEncode(PersonName.value member'.Name)}</option>")
                |> String.concat ""

            let items =
                workspace.Items
                |> List.map (fun item ->
                    let iid = WorkItemId.value item.Id
                    let assignee =
                        item.Assignee
                        |> Option.bind (fun memberId -> membersById |> Map.tryFind memberId)
                        |> Option.map (fun member' -> $" — assigned to {WebUtility.HtmlEncode(PersonName.value member'.Name)}")
                        |> Option.defaultValue ""
                    let complete =
                        match item.State with
                        | WorkItemState.Todo -> $"<form method=\"post\" action=\"/workspaces/{wid}/items/{iid}/complete\" style=\"display:inline\"><button>Complete</button></form>"
                        | WorkItemState.Done -> ""
                    let assign =
                        if List.isEmpty workspace.Members then
                            "<span>Add a member before assigning this item.</span>"
                        else
                            $"<form method=\"post\" action=\"/workspaces/{wid}/items/{iid}/assign\" style=\"display:inline\"><select name=\"memberId\">{memberOptions}</select><button>Assign</button></form>"
                    $"<li>{WebUtility.HtmlEncode(WorkItemTitle.value item.Title)} ({item.State}){assignee} {complete}{assign}</li>")
                |> String.concat ""
            let html =
                $"""<!doctype html><html><head><title>{WebUtility.HtmlEncode(WorkspaceName.value workspace.Name)}</title><style>
body {{ font-family: system-ui, sans-serif; max-width: 52rem; margin: 2rem auto; padding: 0 1rem; }}
li {{ margin: .75rem 0; }} form {{ margin: .5rem 0; }} li form {{ display: inline; margin-left: .5rem; }}
input, select, button {{ padding: .35rem; }} code {{ background: #eee; padding: .15rem .3rem; }}
</style></head><body><p><a href="/">Home</a></p><h1>{WebUtility.HtmlEncode(WorkspaceName.value workspace.Name)}</h1>
<h2>Members</h2><ul>{members}</ul><form method="post" action="/workspaces/{wid}/members"><input name="name" placeholder="Member name" required><button>Add member</button></form>
<h2>Work items</h2><ul>{items}</ul><form method="post" action="/workspaces/{wid}/items"><input name="title" placeholder="Work item" required><button>Add item</button></form></body></html>"""
            return Response.native (Results.Content(html, "text/html"))
        }

    let apiResponse _ workspace = Response.json 200 Boundary.workspaceCodec (Contracts.fromDomain workspace)
    let htmlResponse workspaceId _ = Response.native (Results.Redirect($"/workspaces/{WorkspaceId.value workspaceId}"))

    app.MapGet("/", Func<IResult>(fun () -> Results.Text(FormPage.renderHome (), "text/html"))) |> ignore
    app.MapGet("/favicon.ico", Func<IResult>(fun () -> Results.NoContent())) |> ignore
    app.MapGet("/api/workspaces", endpoint "workspaces.list" listWorkspaces) |> ignore
    app.MapGet("/api/workspaces/{workspaceId}", endpoint "workspaces.get" getWorkspace) |> ignore
    app.MapPost("/api/workspaces", endpoint "workspaces.import" (createWorkspace Request.json)) |> ignore
    app.MapPost("/api/workspaces/{workspaceId}/members", endpoint "workspaces.members.add" (addMember Request.json apiResponse)) |> ignore
    app.MapPost("/api/workspaces/{workspaceId}/items", endpoint "workspaces.items.add" (addItem Request.json apiResponse)) |> ignore
    app.MapPost("/api/workspaces/{workspaceId}/items/{itemId}/complete", endpoint "workspaces.items.complete" (completeItem apiResponse)) |> ignore
    app.MapPost("/api/workspaces/{workspaceId}/items/{itemId}/assign/{memberId}", endpoint "workspaces.items.assign" assignItemFromRoute) |> ignore
    app.MapDelete("/api/workspaces/{workspaceId}", endpoint "workspaces.delete" deleteWorkspace) |> ignore
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

    app.MapGet("/workspaces/{workspaceId}", endpoint "workspaces.page" workspacePage) |> ignore

    app.MapPost("/workspaces/{workspaceId}/members", endpoint "workspaces.members.add-form" (addMember Request.form htmlResponse)) |> ignore
    app.MapPost("/workspaces/{workspaceId}/items", endpoint "workspaces.items.add-form" (addItem Request.form htmlResponse)) |> ignore
    app.MapPost("/workspaces/{workspaceId}/items/{itemId}/complete", endpoint "workspaces.items.complete-form" (completeItem htmlResponse)) |> ignore
    app.MapPost("/workspaces/{workspaceId}/items/{itemId}/assign", endpoint "workspaces.items.assign-form" assignItemFromForm) |> ignore

    app.MapGet("/observability/demo", Func<IResult>(fun () ->
        let demonstration =
            flow {
                do! Log.info "Starting the observability demonstration"
                let! first = Flow.forkNamed "demo-fast" (Flow.Runtime.sleep(TimeSpan.FromMilliseconds 150.0))
                let! second = Flow.forkNamed "demo-slow" (Flow.Runtime.sleep(TimeSpan.FromMilliseconds 350.0))
                do FiberDumpTelemetry.record registry
                do! Flow.join first
                do! Flow.join second
                do! Log.info "Finished the observability demonstration"
            }
            |> Flow.annotate "demo.kind" "concurrent-work"
            |> Activity.traceWith renderError "observability.demo"
            |> Flow.withFiberRegistry registry
            |> FiberTelemetry.observeWithSpans
            |> FiberMetrics.observe

        logger.LogInformation(
            "Running observability demo {DemoKind} with {ExpectedFibers} fibers",
            "concurrent-work",
            2)

        match run env demonstration with
        | Ok () -> Results.Ok({| message = "Demo complete. Inspect traces, structured logs, and metrics in the Aspire dashboard." |})
        | Error error -> Results.Problem(error)))
    |> ignore
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

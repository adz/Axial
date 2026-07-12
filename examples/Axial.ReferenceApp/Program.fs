module Axial.ReferenceApp.Program

open System
open System.Net
open System.Text.Json
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Axial.Flow
open Axial.Schema
open Axial.ReferenceApp

let private renderError = function
    | AppError.InvalidInput diagnostics ->
        diagnostics
        |> Axial.Validation.Diagnostics.flatten
        |> List.map (fun item -> $"{item.Path}: {SchemaError.render item.Error}")
        |> String.concat "; "
    | error -> string error

let private run (env: AppEnv) (flow: Flow<AppEnv, AppError, 'value>) =
    match flow.RunSynchronously(env) with
    | Exit.Success value -> Ok value
    | Exit.Failure(Cause.Fail error) -> Error(renderError error)
    | Exit.Failure cause -> Error(Cause.prettyPrint renderError cause)

let private id (text: string) create =
    match Guid.TryParse text with
    | true, value -> Ok(create value)
    | _ -> Error $"Invalid id: {text}"

let private summary workspace =
    {| id = WorkspaceId.value workspace.Id
       name = WorkspaceName.value workspace.Name
       members = workspace.Members |> List.map (fun member' -> {| id = MemberId.value member'.Id; name = PersonName.value member'.Name |})
       items = workspace.Items |> List.map (fun item -> {| id = WorkItemId.value item.Id; title = WorkItemTitle.value item.Title; assignee = item.Assignee |> Option.map MemberId.value; state = string item.State |}) |}

let private formRaw (form: IFormCollection) =
    form
    |> Seq.collect (fun field -> field.Value |> Seq.map (fun value -> field.Key.Replace(".", ":"), value))
    |> RawInput.ofConfiguration

let buildWebApp (env: AppEnv) (args: string array) =
    let builder = WebApplication.CreateBuilder(args)
    let app = builder.Build()

    app.MapGet("/api/workspaces", Func<IResult>(fun () ->
        match run env (Application.list ()) with
        | Ok values -> Results.Json(values |> List.map summary)
        | Error error -> Results.Problem(error, statusCode = 500))) |> ignore

    app.MapGet("/api/workspaces/{workspaceId}", Func<string, IResult>(fun workspaceId ->
        match id workspaceId WorkspaceId.create with
        | Error error -> Results.BadRequest(error)
        | Ok workspaceId ->
            match run env (Application.get workspaceId) with
            | Ok workspace -> Results.Json(summary workspace)
            | Error error -> Results.NotFound(error))) |> ignore

    app.MapPost("/api/workspaces", Func<HttpRequest, Threading.Tasks.Task<IResult>>(fun request -> task {
        use! document = JsonDocument.ParseAsync(request.Body)
        let raw = RawInput.ofJsonDocument document
        match Application.admitProduction raw with
        | Error error -> return Results.BadRequest(renderError error)
        | Ok workspace ->
            let save =
                Flow.read (fun env -> env.Store.Save workspace)
                |> Flow.bind (function Ok () -> Flow.ok () | Error error -> Flow.fail error)
            match run env save with
            | Ok () -> return Results.Json(summary workspace, statusCode = 201)
            | Error error -> return Results.Problem(error, statusCode = 500) })) |> ignore

    app.MapPost("/api/workspaces/{workspaceId}/members", Func<string, HttpRequest, Threading.Tasks.Task<IResult>>(fun workspaceId request -> task {
        use! document = JsonDocument.ParseAsync(request.Body)
        match id workspaceId WorkspaceId.create with
        | Error error -> return Results.BadRequest(error)
        | Ok workspaceId ->
            let name = document.RootElement.GetProperty("name").GetString()
            match run env (Application.addMember workspaceId name) with
            | Ok workspace -> return Results.Json(summary workspace)
            | Error error -> return Results.BadRequest(error) })) |> ignore

    app.MapPost("/api/workspaces/{workspaceId}/items", Func<string, HttpRequest, Threading.Tasks.Task<IResult>>(fun workspaceId request -> task {
        use! document = JsonDocument.ParseAsync(request.Body)
        match id workspaceId WorkspaceId.create with
        | Error error -> return Results.BadRequest(error)
        | Ok workspaceId ->
            let title = document.RootElement.GetProperty("title").GetString()
            match run env (Application.addWorkItem workspaceId title) with
            | Ok workspace -> return Results.Json(summary workspace)
            | Error error -> return Results.BadRequest(error) })) |> ignore

    app.MapPost("/api/workspaces/{workspaceId}/items/{itemId}/complete", Func<string, string, IResult>(fun workspaceId itemId ->
        match id workspaceId WorkspaceId.create, id itemId WorkItemId.create with
        | Ok workspaceId, Ok itemId ->
            match run env (Application.complete workspaceId itemId) with Ok workspace -> Results.Json(summary workspace) | Error error -> Results.BadRequest(error)
        | _ -> Results.BadRequest("Invalid id."))) |> ignore

    app.MapPost("/api/workspaces/{workspaceId}/items/{itemId}/assign/{memberId}", Func<string, string, string, IResult>(fun workspaceId itemId memberId ->
        match id workspaceId WorkspaceId.create, id itemId WorkItemId.create, id memberId MemberId.create with
        | Ok workspaceId, Ok itemId, Ok memberId ->
            match run env (Application.assign workspaceId itemId memberId) with Ok workspace -> Results.Json(summary workspace) | Error error -> Results.BadRequest(error)
        | _ -> Results.BadRequest("Invalid id."))) |> ignore

    app.MapDelete("/api/workspaces/{workspaceId}", Func<string, IResult>(fun workspaceId ->
        match id workspaceId WorkspaceId.create with
        | Ok workspaceId -> match run env (Application.delete workspaceId) with Ok () -> Results.NoContent() | Error error -> Results.NotFound(error)
        | Error error -> Results.BadRequest(error))) |> ignore

    app.MapGet("/workspaces/new", Func<IResult>(fun () ->
        Results.Text("""<!doctype html><html><body><h1>New workspace</h1><form method="post"><input type="hidden" name="version" value="2"><label>Id <input name="id"></label><label>Name <input name="name"></label><button>Create</button></form></body></html>""", "text/html"))) |> ignore

    app.MapPost("/workspaces/new", Func<HttpRequest, Threading.Tasks.Task<IResult>>(fun request -> task {
        let! form = request.ReadFormAsync()
        match formRaw form |> Application.admitProduction with
        | Error error -> return Results.Text($"<p>{WebUtility.HtmlEncode(renderError error)}</p>", "text/html", statusCode = 400)
        | Ok workspace ->
            match env.Store.Save workspace with
            | Ok () -> return Results.Redirect("/api/workspaces")
            | Error error -> return Results.Text(WebUtility.HtmlEncode(renderError error), statusCode = 500) })) |> ignore

    app.MapGet("/workspaces/{workspaceId}", Func<string, IResult>(fun workspaceId ->
        match id workspaceId WorkspaceId.create with
        | Error error -> Results.BadRequest(error)
        | Ok workspaceId ->
            match run env (Application.get workspaceId) with
            | Error error -> Results.NotFound(error)
            | Ok workspace ->
                let wid = WorkspaceId.value workspace.Id
                let items = workspace.Items |> List.map (fun item ->
                    let iid = WorkItemId.value item.Id
                    $"""<li>{WebUtility.HtmlEncode(WorkItemTitle.value item.Title)} ({item.State}) <form method="post" action="/workspaces/{wid}/items/{iid}/complete" style="display:inline"><button>Complete</button></form><form method="post" action="/workspaces/{wid}/items/{iid}/assign" style="display:inline"><input name="memberId" placeholder="Member id"><button>Assign</button></form></li>""") |> String.concat ""
                Results.Text($"""<!doctype html><html><body><h1>{WebUtility.HtmlEncode(WorkspaceName.value workspace.Name)}</h1><ul>{items}</ul><form method="post" action="/workspaces/{wid}/members"><input name="name" placeholder="Member name"><button>Add member</button></form><form method="post" action="/workspaces/{wid}/items"><input name="title" placeholder="Work item"><button>Add item</button></form></body></html>""", "text/html"))) |> ignore

    app.MapPost("/workspaces/{workspaceId}/members", Func<string, HttpRequest, Threading.Tasks.Task<IResult>>(fun workspaceId request -> task {
        let! form = request.ReadFormAsync()
        match id workspaceId WorkspaceId.create with
        | Ok workspaceId -> match run env (Application.addMember workspaceId (string form["name"])) with Ok _ -> return Results.Redirect($"/workspaces/{workspaceId}") | Error error -> return Results.BadRequest(error)
        | Error error -> return Results.BadRequest(error) })) |> ignore

    app.MapPost("/workspaces/{workspaceId}/items", Func<string, HttpRequest, Threading.Tasks.Task<IResult>>(fun workspaceId request -> task {
        let! form = request.ReadFormAsync()
        match id workspaceId WorkspaceId.create with
        | Ok workspaceId -> match run env (Application.addWorkItem workspaceId (string form["title"])) with Ok _ -> return Results.Redirect($"/workspaces/{workspaceId}") | Error error -> return Results.BadRequest(error)
        | Error error -> return Results.BadRequest(error) })) |> ignore

    app.MapPost("/workspaces/{workspaceId}/items/{itemId}/complete", Func<string, string, IResult>(fun workspaceId itemId ->
        match id workspaceId WorkspaceId.create, id itemId WorkItemId.create with
        | Ok workspaceId, Ok itemId -> match run env (Application.complete workspaceId itemId) with Ok _ -> Results.Redirect($"/workspaces/{workspaceId}") | Error error -> Results.BadRequest(error)
        | _ -> Results.BadRequest("Invalid id."))) |> ignore

    app.MapPost("/workspaces/{workspaceId}/items/{itemId}/assign", Func<string, string, HttpRequest, Threading.Tasks.Task<IResult>>(fun workspaceId itemId request -> task {
        let! form = request.ReadFormAsync()
        match id workspaceId WorkspaceId.create, id itemId WorkItemId.create, id (string form["memberId"]) MemberId.create with
        | Ok workspaceId, Ok itemId, Ok memberId -> match run env (Application.assign workspaceId itemId memberId) with Ok _ -> return Results.Redirect($"/workspaces/{workspaceId}") | Error error -> return Results.BadRequest(error)
        | _ -> return Results.BadRequest("Invalid id.") })) |> ignore
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
    let data = Environment.GetEnvironmentVariable("AXIAL_REFERENCE_DATA") |> Option.ofObj |> Option.defaultValue ".axial-reference-data"
    let env = { Store = FileWorkspaceStore.create data; NewGuid = Guid.NewGuid }
    let execute flow =
        match run env flow with
        | Ok workspace -> printfn "%s" (JsonSerializer.Serialize(summary workspace)); 0
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
        | Ok values -> printfn "%s" (JsonSerializer.Serialize(values |> List.map summary)); 0
        | Error error -> eprintfn "%s" error; 1
    | _ -> usage (); 0

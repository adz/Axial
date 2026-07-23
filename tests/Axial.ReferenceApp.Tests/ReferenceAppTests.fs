namespace Axial.ReferenceApp.Tests

open Axial

open System
open System.IO
open System.Net.Http
open System.Text
open Xunit
open Swensen.Unquote
open Axial.Flow
open Axial.Flow.FileSystem
open Axial.Flow.PlatformService
open Axial.Refined
open Axial.Schema
open Axial.ReferenceApp

module ReferenceAppTests =
    let private tempDirectory () = Path.Combine(Path.GetTempPath(), "axial-reference-" + Guid.NewGuid().ToString("N"))
    let private scalar value = Data.Text(string value)
    let private object' fields = Data.objectOfMap (Map.ofList fields)

    [<Fact>]
    let ``direct smart-constructor failures are structured admission errors`` () =
        let unexpectedStore =
            { new IWorkspaceStore with
                member _.Load _ = failwith "Invalid input must fail before storage."
                member _.Save _ = failwith "Invalid input must fail before storage."
                member _.List() = failwith "Invalid input must fail before storage."
                member _.Delete _ = failwith "Invalid input must fail before storage." }
        let env = { Store = unexpectedStore; Runtime = BaseRuntime.liveValue; FileSystem = FileSystem.live }
        let workspaceId = WorkspaceId.create Guid.Empty
        let expectInvalidValue (workflow: Flow<AppEnv, AppError, 'value>) =
            match workflow.RunSynchronously(env) with
            | Exit.Failure(Cause.Fail(AppError.InvalidValue(RefinementError.CheckFailed(_, _)))) -> ()
            | result -> failwithf "Expected a structured invalid-value error, got %A" result

        expectInvalidValue (Application.createWorkspace " ")
        expectInvalidValue (Application.addMember workspaceId " ")
        expectInvalidValue (Application.addWorkItem workspaceId " ")
        expectInvalidValue (Application.rename workspaceId " ")

    [<Fact>]
    let ``schema constructs refined fields and reports boundary paths`` () =
        let valid =
            Schema.parseRetainingInput Contracts.workspaceV2
                (object' [ "version", scalar 2; "id", scalar(Guid.NewGuid()); "name", scalar "Delivery"; "members", Data.List []; "items", Data.List [] ])
        test <@ valid.Value.name |> WorkspaceName.value = "Delivery" @>

        let invalid =
            Schema.parseRetainingInput Contracts.workspaceV2
                (object' [ "version", scalar 2; "id", scalar(Guid.NewGuid()); "name", scalar " "; "members", Data.List []; "items", Data.List [] ])
        test <@ invalid.Errors |> List.map _.Path = [ Path.key "name" ] @>

    [<Fact>]
    let ``v1 files migrate and subsequent saves write only v2`` () =
        let directory = tempDirectory ()
        let id = Guid.NewGuid()
        Directory.CreateDirectory directory |> ignore
        File.WriteAllText(Path.Combine(directory, $"{id:N}.json"), $"""{{"version":1,"id":"{id}","name":"Legacy"}}""")
        let store = FileWorkspaceStore.create directory

        let env = { Store = store; Runtime = BaseRuntime.liveValue; FileSystem = FileSystem.live }
        let run (flow: Flow<AppEnv, AppError, 'value>) =
            match flow.RunSynchronously(env) with Exit.Success value -> value | exit -> failwithf "%A" exit
        let loaded = run (store.Load(WorkspaceId.create id))
        test <@ WorkspaceName.value loaded.Name = "Legacy" @>
        run (store.Save loaded)
        let rewritten = File.ReadAllText(Path.Combine(directory, $"{id:N}.json"))
        test <@ rewritten.Contains("\"version\":2") @>
        test <@ rewritten.Contains("\"members\":[]") @>

    [<Fact>]
    let ``production admission is separate from intrinsic schema validity`` () =
        let raw = object' [ "version", scalar 2; "id", scalar(Guid.NewGuid()); "name", scalar "sales-demo"; "members", Data.List []; "items", Data.List [] ]
        test <@ (Schema.parse Contracts.workspaceV2 raw |> Result.isOk) @>
        let parsed = Schema.parse Contracts.workspaceV2 raw |> Result.defaultWith (failwithf "%A")
        match Application.admitProduction parsed with
        | Error(AppError.ProductionRejected ProductionAdmissionError.DemoWorkspaceName) -> ()
        | result -> failwithf "Expected production rejection, got %A" result

    [<Fact>]
    let ``domain admission rejects an assignee absent from workspace members`` () =
        let missing = Guid.NewGuid()
        let wire =
            { version = 2
              id = Guid.NewGuid()
              name = WorkspaceName.create "Delivery" |> Result.defaultWith (failwithf "%A")
              members = []
              items =
                [ { id = Guid.NewGuid()
                    title = WorkItemTitle.create "Ship" |> Result.defaultWith (failwithf "%A")
                    assignee = Some missing
                    state = "todo" } ] }

        test <@ Contracts.toDomain wire = Error(DomainError.AssigneeNotMember(MemberId.create missing)) @>

    [<Fact>]
    let ``Flow use cases create update assign complete and persist`` () =
        let directory = tempDirectory ()
        let mutable next = 0
        let guid () = next <- next + 1; Guid.Parse($"00000000-0000-0000-0000-{next:D12}")
        let guidService =
            { new IGuid with
                member _.NewGuid() = guid () }
        let runtime = { BaseRuntime.liveValue with Guid = guidService }
        let env = { Store = FileWorkspaceStore.create directory; Runtime = runtime; FileSystem = FileSystem.live }
        let run (flow: Flow<AppEnv, AppError, 'value>) =
            match flow.RunSynchronously(env) with Exit.Success value -> value | exit -> failwithf "%A" exit

        let workspace = run (Application.createWorkspace "Delivery")
        let withMember = run (Application.addMember workspace.Id "Ada")
        let withItem = run (Application.addWorkItem workspace.Id "Ship version two")
        let assigned = run (Application.assign workspace.Id withItem.Items.Head.Id withMember.Members.Head.Id)
        let completed = run (Application.complete workspace.Id assigned.Items.Head.Id)
        let reloaded = run (Application.get workspace.Id)
        test <@ completed.Items.Head.State = WorkItemState.Done @>
        test <@ completed.Items.Head.Assignee = Some completed.Members.Head.Id @>
        test <@ reloaded.Items.Head.State = WorkItemState.Done @>

    [<Fact>]
    let ``JSON API accepts a schema-valid workspace and lists it`` () = task {
        let directory = tempDirectory ()
        let env = { Store = FileWorkspaceStore.create directory; Runtime = BaseRuntime.liveValue; FileSystem = FileSystem.live }
        let app = Axial.ReferenceApp.Program.buildWebApp env [| "--urls"; "http://127.0.0.1:0" |]
        do! app.StartAsync()
        use client = new HttpClient(BaseAddress = Uri(Seq.head app.Urls))
        let! home = client.GetStringAsync("/")
        let id = Guid.NewGuid()
        use content = new StringContent($"""{{"version":2,"id":"{id}","name":"Web","members":[],"items":[]}}""", Encoding.UTF8, "application/json")
        let! created = client.PostAsync("/api/workspaces", content)
        let! listedResponse = client.GetAsync("/api/workspaces")
        let! listed = listedResponse.Content.ReadAsStringAsync()
        let! openApiResponse = client.GetAsync("/openapi.json")
        let! openApi = openApiResponse.Content.ReadAsStringAsync()
        let! form = client.GetStringAsync("/workspaces/new")
        use invalidForm = new StringContent("name=", Encoding.UTF8, "application/x-www-form-urlencoded")
        let! invalid = client.PostAsync("/workspaces/new", invalidForm)
        let! invalidHtml = invalid.Content.ReadAsStringAsync()
        let! workspacePage = client.GetAsync($"/workspaces/{id}")
        let! workspaceHtml = workspacePage.Content.ReadAsStringAsync()
        do! app.StopAsync()
        test <@ home.Contains("Axial reference application") @>
        test <@ home.Contains("/observability/demo") @>
        test <@ created.StatusCode = Net.HttpStatusCode.Created @>
        Assert.True(listedResponse.StatusCode = Net.HttpStatusCode.OK, listed)
        test <@ listed.Contains("Web") @>
        Assert.True(openApiResponse.StatusCode = Net.HttpStatusCode.OK, openApi)
        test <@ openApi.Contains("\"/api/workspaces\"") @>
        test <@ openApi.Contains("\"maxLength\":80") @>
        test <@ form.Contains("name=\"name\"") @>
        test <@ invalid.StatusCode = Net.HttpStatusCode.BadRequest @>
        test <@ invalidHtml.Contains("class=\"error\"") @>
        test <@ workspacePage.Content.Headers.ContentType.MediaType = "text/html" @>
        test <@ workspaceHtml.Contains("Web") @>
    }

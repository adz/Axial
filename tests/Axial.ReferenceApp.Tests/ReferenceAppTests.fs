namespace Axial.ReferenceApp.Tests

open System
open System.IO
open System.Net.Http
open System.Text
open Xunit
open Swensen.Unquote
open Axial.Flow
open Axial.Refined
open Axial.Schema
open Axial.ReferenceApp

module ReferenceAppTests =
    let private tempDirectory () = Path.Combine(Path.GetTempPath(), "axial-reference-" + Guid.NewGuid().ToString("N"))
    let private scalar value = RawInput.Scalar(string value)
    let private object' fields = RawInput.Object(Map.ofList fields)

    [<Fact>]
    let ``direct smart-constructor failures are structured admission errors`` () =
        let unexpectedStore =
            { new IWorkspaceStore with
                member _.Load _ = failwith "Invalid input must fail before storage."
                member _.Save _ = failwith "Invalid input must fail before storage."
                member _.List() = failwith "Invalid input must fail before storage."
                member _.Delete _ = failwith "Invalid input must fail before storage." }
        let env = { Store = unexpectedStore; NewGuid = Guid.NewGuid }
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
            Schema.parse Contracts.workspaceV2
                (object' [ "version", scalar 2; "id", scalar(Guid.NewGuid()); "name", scalar "Delivery"; "members", RawInput.Many []; "items", RawInput.Many [] ])
        test <@ valid.Value.name |> WorkspaceName.value = "Delivery" @>

        let invalid =
            Schema.parse Contracts.workspaceV2
                (object' [ "version", scalar 2; "id", scalar(Guid.NewGuid()); "name", scalar " "; "members", RawInput.Many []; "items", RawInput.Many [] ])
        test <@ invalid.Errors |> List.map _.Path = [ [ Axial.Validation.PathSegment.Name "name" ] ] @>

    [<Fact>]
    let ``v1 files migrate and subsequent saves write only v2`` () =
        let directory = tempDirectory ()
        let id = Guid.NewGuid()
        Directory.CreateDirectory directory |> ignore
        File.WriteAllText(Path.Combine(directory, $"{id:N}.json"), $"""{{"version":1,"id":"{id}","name":"Legacy"}}""")
        let store = FileWorkspaceStore.create directory

        let loaded = store.Load(WorkspaceId.create id) |> Result.defaultWith (failwithf "%A")
        test <@ WorkspaceName.value loaded.Name = "Legacy" @>
        test <@ store.Save loaded = Ok () @>
        let rewritten = File.ReadAllText(Path.Combine(directory, $"{id:N}.json"))
        test <@ rewritten.Contains("\"version\":2") @>
        test <@ rewritten.Contains("\"members\":[]") @>

    [<Fact>]
    let ``contextual production rules are separate from intrinsic schema validity`` () =
        let raw = object' [ "version", scalar 2; "id", scalar(Guid.NewGuid()); "name", scalar "sales-demo"; "members", RawInput.Many []; "items", RawInput.Many [] ]
        test <@ (Schema.parse Contracts.workspaceV2 raw).IsValid @>
        match Application.admitProduction raw with
        | Error(AppError.InvalidInput diagnostics) ->
            test <@ diagnostics |> Axial.Validation.Diagnostics.flatten |> List.map (SchemaError.render << _.Error) = [ "Production workspace names cannot end in -demo." ] @>
        | result -> failwithf "Expected contextual rejection, got %A" result

    [<Fact>]
    let ``Flow use cases create update assign complete and persist`` () =
        let directory = tempDirectory ()
        let mutable next = 0
        let guid () = next <- next + 1; Guid.Parse($"00000000-0000-0000-0000-{next:D12}")
        let env = { Store = FileWorkspaceStore.create directory; NewGuid = guid }
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
        let env = { Store = FileWorkspaceStore.create directory; NewGuid = Guid.NewGuid }
        let app = Axial.ReferenceApp.Program.buildWebApp env [| "--urls"; "http://127.0.0.1:0" |]
        do! app.StartAsync()
        use client = new HttpClient(BaseAddress = Uri(Seq.head app.Urls))
        let id = Guid.NewGuid()
        use content = new StringContent($"""{{"version":2,"id":"{id}","name":"Web","members":[],"items":[]}}""", Encoding.UTF8, "application/json")
        let! created = client.PostAsync("/api/workspaces", content)
        let! listed = client.GetStringAsync("/api/workspaces")
        do! app.StopAsync()
        test <@ created.StatusCode = Net.HttpStatusCode.Created @>
        test <@ listed.Contains("Web") @>
    }

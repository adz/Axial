namespace FsFlow.Tests

open System
open System.Reflection
open FsFlow
open Swensen.Unquote
open Xunit

module ApiShapeTests =
    let private publicInstanceMethodNames (targetType: Type) =
        targetType.GetMethods(BindingFlags.Instance ||| BindingFlags.Public)
        |> Array.filter (fun methodInfo -> not methodInfo.IsSpecialName)
        |> Array.map _.Name
        |> Set.ofArray

    let private publicStaticMemberNames (targetType: Type) =
        let methods =
            targetType.GetMethods(BindingFlags.Static ||| BindingFlags.Public)
            |> Array.filter (fun methodInfo -> not methodInfo.IsSpecialName)
            |> Array.map _.Name

        let properties =
            targetType.GetProperties(BindingFlags.Static ||| BindingFlags.Public)
            |> Array.map _.Name

        Array.append methods properties |> Set.ofArray

    let private moduleType (assemblyMarker: Type) (fullName: string) =
        let assembly = assemblyMarker.Assembly

        match assembly.GetType(fullName, false), assembly.GetType(fullName + "Module", false) with
        | null, null -> failwithf "Could not find module type %s in %s." fullName assembly.FullName
        | found, _ when not (isNull found) -> found
        | _, found -> found

    let private assertContainsAll expected actual =
        let missing = expected |> List.filter (fun name -> not (Set.contains name actual))
        test <@ missing = [] @>

    [<Fact>]
    let ``core Flow module keeps expected public shape`` () =
        moduleType typeof<Flow<unit, unit, unit>> "FsFlow.Flow"
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "run"
              "runFull"
              "runWithToken"
              "toAsync"
              "toTask"
              "ok"
              "fail"
              "fromResult"
              "fromOption"
              "env"
              "read"
              "map"
              "bind"
              "zipPar"
              "race"
              "traverse"
              "sequence" ]

    [<Fact>]
    let ``flow builder keeps expected computation expression shape`` () =
        typeof<FlowBuilder>
        |> publicInstanceMethodNames
        |> assertContainsAll
            [ "Return"
              "ReturnFrom"
              "Bind"
              "Delay"
              "Run"
              "Combine"
              "TryWith"
              "TryFinally"
              "Using"
              "While"
              "For" ]

        let argumentTypeNames = TestSupport.flowBuilderBindAndReturnFromArgumentNames () |> Set.ofArray
        assertContainsAll [ "ColdTask`1"; "Task`1"; "ValueTask`1"; "FSharpAsync`1"; "Flow`3" ] argumentTypeNames

    [<Fact>]
    let ``validation modules and builders keep expected public shape`` () =
        moduleType typeof<Validation<int, string>> "FsFlow.Validation"
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "ok"
              "fail"
              "fromResult"
              "toResult"
              "map"
              "bind"
              "map2"
              "map3"
              "collect"
              "sequence"
              "at"
              "traverseIndexed" ]

        typeof<ValidateBuilder>
        |> publicInstanceMethodNames
        |> assertContainsAll [ "Return"; "ReturnFrom"; "Bind"; "MergeSources"; "Run"; "at" ]

    [<Fact>]
    let ``schedule stream and STM modules keep expected public shape`` () =
        moduleType typeof<Schedule<unit, unit, unit>> "FsFlow.Schedule"
        |> publicStaticMemberNames
        |> assertContainsAll [ "recurs"; "spaced"; "exponential"; "jittered" ]

        moduleType typeof<FlowStream<unit, unit, unit>> "FsFlow.FlowStream"
        |> publicStaticMemberNames
        |> assertContainsAll [ "fromSeq"; "runForEach"; "map" ]

        moduleType typeof<STM<int>> "FsFlow.STM"
        |> publicStaticMemberNames
        |> assertContainsAll [ "retry"; "orElse"; "atomically" ]

        moduleType typeof<TRef<int>> "FsFlow.TRef"
        |> publicStaticMemberNames
        |> assertContainsAll [ "make"; "get"; "set"; "update" ]

    [<Fact>]
    let ``service modules keep expected public shape`` () =
        moduleType typeof<FsFlow.Services.Console.IConsole> "FsFlow.Services.Console.Console"
        |> publicStaticMemberNames
        |> assertContainsAll [ "readLine"; "writeLine"; "layer"; "live" ]

        moduleType typeof<FsFlow.Services.FileSystem.IFileSystem> "FsFlow.Services.FileSystem.FileSystem"
        |> publicStaticMemberNames
        |> assertContainsAll [ "readAllText"; "writeAllText"; "exists"; "layer"; "live" ]

        moduleType typeof<FsFlow.Services.Http.IHttp> "FsFlow.Services.Http.Http"
        |> publicStaticMemberNames
        |> assertContainsAll [ "getString"; "layer"; "live" ]

        moduleType typeof<FsFlow.Services.Process.IProcess> "FsFlow.Services.Process.Process"
        |> publicStaticMemberNames
        |> assertContainsAll [ "execute"; "layer"; "live" ]

        moduleType typeof<FsFlow.Services.Core.EnvironmentVariableError> "FsFlow.Services.Core.Clock"
        |> publicStaticMemberNames
        |> assertContainsAll [ "now"; "layer"; "live"; "fromValue" ]

        moduleType typeof<FsFlow.Services.Core.EnvironmentVariableError> "FsFlow.Services.Core.EnvironmentVariable"
        |> publicStaticMemberNames
        |> assertContainsAll [ "get"; "tryGet"; "getInt"; "getGuid"; "getBool" ]

    [<Fact>]
    let ``service and layer surfaces keep expected public shape`` () =
        typeof<Service<int>>
        |> publicStaticMemberNames
        |> assertContainsAll [ "get"; "resolve" ]

        moduleType typeof<Layer<unit, unit, int>> "FsFlow.Layer"
        |> publicStaticMemberNames
        |> assertContainsAll [ "effect"; "succeed"; "read"; "map"; "bind"; "zip" ]

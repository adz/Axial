namespace Axial.Tests

open System
open System.Reflection
open System.Threading.Tasks
open Axial.Flow
open Axial.ErrorHandling
open Axial.Refined
open Axial.Schema
open Axial.Validation
open Axial.Flow.Hosting
open Axial.Flow.Telemetry
open Microsoft.FSharp.Reflection
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

    let private publicStaticMethods (targetType: Type) =
        targetType.GetMethods(BindingFlags.Static ||| BindingFlags.Public)
        |> Array.filter (fun methodInfo -> not methodInfo.IsSpecialName)

    let private moduleType (assemblyMarker: Type) (fullName: string) =
        let assembly = assemblyMarker.Assembly

        match assembly.GetType(fullName, false), assembly.GetType(fullName + "Module", false) with
        | null, null -> failwithf "Could not find module type %s in %s." fullName assembly.FullName
        | found, _ when not (isNull found) -> found
        | _, found -> found

    let private moduleTypeFromAssembly (assemblyName: string) (fullName: string) =
        let assembly = Assembly.Load assemblyName

        match assembly.GetType(fullName, false), assembly.GetType(fullName + "Module", false) with
        | null, null -> failwithf "Could not find module type %s in %s." fullName assembly.FullName
        | found, _ when not (isNull found) -> found
        | _, found -> found

    let private assertModuleAbsentFromAssembly (assemblyName: string) (fullName: string) =
        let assembly = Assembly.Load assemblyName
        let found = assembly.GetType(fullName, false)
        let foundModule = assembly.GetType(fullName + "Module", false)

        test <@ isNull found @>
        test <@ isNull foundModule @>

    let private referencedAssemblyNames (assembly: Assembly) =
        assembly.GetReferencedAssemblies()
        |> Array.map _.Name
        |> Set.ofArray

    let private assertContainsAll expected actual =
        let missing = expected |> List.filter (fun name -> not (Set.contains name actual))
        test <@ List.isEmpty missing @>

    let private assertContainsNone forbidden actual =
        let present = forbidden |> List.filter (fun name -> Set.contains name actual)
        test <@ List.isEmpty present @>

    let private publicUnionCaseNames (targetType: Type) =
        FSharpType.GetUnionCases(targetType, BindingFlags.Public)
        |> Array.map _.Name
        |> Set.ofArray

    let private returnsCheckResultShape (returnType: Type) =
        let checkResultType = typedefof<Result<_, _>>
        let checkFunctionType = typedefof<FSharpFunc<_, _>>

        let rec loop (returnType: Type) =
            if returnType.IsGenericType && returnType.GetGenericTypeDefinition() = checkResultType then
                let arguments = returnType.GetGenericArguments()
                arguments[0] = typeof<unit> && arguments[1] = typeof<CheckFailure list>
            elif returnType.IsGenericType && returnType.GetGenericTypeDefinition() = checkFunctionType then
                returnType.GetGenericArguments()[1] |> loop
            else
                false

        loop returnType

    let private assertMethodsReturnCheckResult methodNames (targetType: Type) =
        let methods = targetType |> publicStaticMethods

        let missing =
            methodNames
            |> List.filter (fun name -> methods |> Array.exists (fun methodInfo -> methodInfo.Name = name) |> not)

        let wrongReturnType =
            methodNames
            |> List.choose (fun name ->
                methods
                |> Array.tryFind (fun methodInfo -> methodInfo.Name = name)
                |> Option.bind (fun methodInfo ->
                    if returnsCheckResultShape methodInfo.ReturnType then
                        None
                    else
                        Some(name, methodInfo.ReturnType.FullName)))

        test <@ List.isEmpty missing @>
        test <@ List.isEmpty wrongReturnType @>

    [<Fact>]
    let ``core Flow module keeps expected public shape`` () =
        moduleType typeof<Flow<unit, unit, unit>> "Axial.Flow.Flow"
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "ok"
              "fail"
              "fromResult"
              "fromOption"
              "fromAsync"
              "attemptAsync"
              "fromTask"
              "attemptTask"
              "fromValueTask"
              "attemptValueTask"
              "acquireRelease"
              "acquireReleaseWith"
              "addFinalizer"
              "addDisposable"
              "addAsyncDisposable"
              "env"
              "read"
              "map"
              "bind"
              "zipPar"
              "race"
              "traverse"
              "sequence" ]

        typeof<Flow<unit, unit, unit>>
        |> publicInstanceMethodNames
        |> assertContainsAll [ "ToAsync"; "ToTask"; "ToValueTask"; "RunSynchronously" ]

    [<Fact>]
    let ``flow type aliases compile to canonical flow shapes`` () =
        let valueFlow : Flow<unit, Never, int> = Flow.succeed 1
        let typedFlow : Flow<unit, string, int> = Flow.fail "missing"
        let envFlow : Flow<string, Never, int> = Flow.read _.Length
        let exnFlow : Flow<unit, exn, int> = Flow.fail (InvalidOperationException "recoverable")
        let exnEnvFlow : Flow<string, exn, int> = Flow.read _.Length

        let valueAlias : Flow<int> = valueFlow
        let typedAlias : Flow<string, int> = typedFlow
        let envAlias : EnvFlow<string, int> = envFlow
        let exnAlias : ExnFlow<int> = exnFlow
        let exnEnvAlias : ExnEnvFlow<string, int> = exnEnvFlow

        test <@ valueAlias.RunSynchronously(()) = Exit.Success 1 @>
        test <@ typedAlias.RunSynchronously(()) = Exit.Failure (Cause.Fail "missing") @>
        test <@ envAlias.RunSynchronously("abc") = Exit.Success 3 @>
        match exnAlias.RunSynchronously(()) with
        | Exit.Failure (Cause.Fail (:? InvalidOperationException)) -> ()
        | other -> failwithf "Expected typed exception failure, got %A" other

        test <@ exnEnvAlias.RunSynchronously("abcd") = Exit.Success 4 @>

    [<Fact>]
    let ``runtime outcome types keep expected public shape`` () =
        moduleType typeof<Cause<unit>> "Axial.Flow.Cause"
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "map"
              "thenCause"
              "both"
              "traced"
              "failures"
              "defects"
              "isInterrupted"
              "prettyPrint" ]

        moduleType typeof<Exit<unit, unit>> "Axial.Flow.Exit"
        |> publicStaticMemberNames
        |> assertContainsAll [ "map"; "bind"; "mapError"; "mapBoth"; "fromResult"; "toResult" ]

        moduleType typeof<Fiber<unit, unit>> "Axial.Flow.Fiber"
        |> publicStaticMemberNames
        |> assertContainsAll [ "dump" ]

        typeof<Scope>
        |> publicInstanceMethodNames
        |> assertContainsAll [ "AddFinalizer"; "AddDisposable"; "AddAsyncDisposable"; "AddChild"; "Close" ]

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
        moduleType typeof<Validation<int, string>> "Axial.Validation.Validation"
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

        typeof<ResultBuilder>
        |> publicInstanceMethodNames
        |> assertContainsAll [ "Return"; "ReturnFrom"; "Bind"; "Delay"; "Run"; "Combine"; "TryWith"; "TryFinally"; "Using"; "While"; "For" ]

        typeof<RefineBuilder>
        |> publicInstanceMethodNames
        |> assertContainsAll [ "Return"; "ReturnFrom"; "Bind"; "Delay"; "Run"; "Combine" ]

    [<Fact>]
    let ``schema type starts as independent leaf package`` () =
        let schemaType = typedefof<Schema<_>>
        let schemaAssembly = schemaType.Assembly
        let references = referencedAssemblyNames schemaAssembly
        let publicConstructors =
            schemaType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)

        test <@ schemaType.IsGenericTypeDefinition @>
        test <@ schemaType.GetGenericArguments().Length = 1 @>
        test <@ publicConstructors.Length = 0 @>
        test <@ schemaAssembly.GetName().Name = "Axial.Schema" @>
        references
        |> assertContainsNone [ "Axial.Flow"; "Axial.ErrorHandling"; "Axial.Refined"; "Axial.Validation" ]

    [<Fact>]
    let ``check take binderror diagnostics and ref helpers keep expected public shape`` () =
        typeof<CheckFailure>
        |> publicUnionCaseNames
        |> assertContainsAll [ "Missing"; "Blank"; "InvalidFormat"; "Length"; "Range"; "Count"; "Equality"; "CustomCode" ]

        let checkModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.Check"

        let checkMembers =
            checkModule
            |> publicStaticMemberNames

        checkMembers
        |> assertContainsAll
            [ "all"
              "any"
              "not"
              "mapFailure"
              "isSome"
              "isNone"
              "isValueSome"
              "isValueNone"
              "hasValue"
              "hasNoValue"
              "notNull"
              "isNull"
              "isOk"
              "isError"
              "notEmpty"
              "isEmpty"
              "notNullOrEmpty"
              "nullOrEmpty"
              "notEmptyString"
              "emptyString"
              "notBlank"
              "blank"
              "hasMinLength"
              "hasMaxLength"
              "hasExactLength"
              "matchesRegex"
              "isEmail"
              "isNumeric"
              "isAlphaNumeric"
              "equalTo"
              "notEqualTo"
              "contains"
              "hasCount"
              "isSingle"
              "atMostOne"
              "atLeastOne"
              "moreThanOne"
              "hasDuplicates"
              "hasNoDuplicates"
              "greaterThan"
              "lessThan"
              "atLeast"
              "atMost"
              "between"
              "positive"
              "nonNegative"
              "negative"
              "nonPositive"
              "negate" ]

        checkModule
        |> assertMethodsReturnCheckResult [ "all"; "any"; "not"; "mapFailure" ]

        let checkStringModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.CheckModule+String"

        checkStringModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "present"; "minLength"; "maxLength"; "lengthBetween"; "exactLength"; "email"; "matches"; "oneOf" ]

        checkStringModule
        |> assertMethodsReturnCheckResult [ "present"; "minLength"; "maxLength"; "lengthBetween"; "exactLength"; "email"; "matches"; "oneOf" ]

        let checkNumberModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.CheckModule+Number"

        checkNumberModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "between"; "greaterThan"; "lessThan"; "atLeast"; "atMost" ]

        checkNumberModule
        |> assertMethodsReturnCheckResult [ "between"; "greaterThan"; "lessThan"; "atLeast"; "atMost" ]

        let checkCollectionModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.CheckModule+Collection"

        checkCollectionModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "notEmpty"; "minCount"; "maxCount"; "countBetween"; "distinct" ]

        checkCollectionModule
        |> assertMethodsReturnCheckResult [ "notEmpty"; "minCount"; "maxCount"; "countBetween"; "distinct" ]

        let checkOptionModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.CheckModule+Option"

        checkOptionModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "some"; "none" ]

        checkOptionModule
        |> assertMethodsReturnCheckResult [ "some"; "none" ]

        let checkValueOptionModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.CheckModule+ValueOption"

        checkValueOptionModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "some"; "none" ]

        checkValueOptionModule
        |> assertMethodsReturnCheckResult [ "some"; "none" ]

        let checkNullableModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.CheckModule+Nullable"

        checkNullableModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "hasValue"; "hasNoValue" ]

        checkNullableModule
        |> assertMethodsReturnCheckResult [ "hasValue"; "hasNoValue" ]

        let checkResultModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.CheckModule+Result"

        checkResultModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "ok"; "error" ]

        checkResultModule
        |> assertMethodsReturnCheckResult [ "ok"; "error" ]

        checkMembers
        |> assertContainsNone
            [ "isTrue"
              "isFalse"
              "fromPredicate"
              "fromTry"
              "fromChoice"
              "both"
              "either"
              "whenTrue"
              "whenFalse"
              "whenNotBlank"
              "takeSome"
              "orError" ]

        let resultMembers =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.Result"
            |> publicStaticMemberNames

        resultMembers
        |> assertContainsAll
            [ "ok"
              "error"
              "map"
              "bind"
              "mapError"
              "require"
              "guard"
              "checkOr"
              "keepIf"
              "withError"
              "fromTry"
              "fromChoice"
              "toOption"
              "toValueOption"
              "defaultValue"
              "someOr"
              "noneOr"
              "valueSomeOr"
              "valueNoneOr"
              "nullableOr"
              "notNullOr"
              "okOr"
              "errorOr"
              "headOr"
              "notBlank"
              "length"
              "minLength"
              "maxLength"
              "exactLength"
              "range"
              "greaterThan"
              "lessThan"
              "atLeast"
              "atMost"
              "single"
              "atMostOne"
              "atLeastOne"
              "moreThanOne" ]

        let parseMembers =
            moduleTypeFromAssembly "Axial.Refined" "Axial.Refined.Parse"
            |> publicStaticMemberNames

        test <@ typeof<ParseError>.Assembly.GetName().Name = "Axial.Refined" @>
        assertModuleAbsentFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.Parse"
        assertModuleAbsentFromAssembly "Axial.Validation" "Axial.Validation.Parse"

        parseMembers
        |> assertContainsAll
            [ "int"
              "long"
              "decimal"
              "float"
              "bool"
              "guid"
              "dateTime"
              "dateTimeOffset"
              "dateOnly"
              "timeOnly"
              "enum"
              "intOption"
              "boolOption"
              "decimalOption"
              "guidOption"
              "intOrDefault"
              "boolOrDefault"
              "decimalOrDefault" ]

        let refineMembers =
            moduleTypeFromAssembly "Axial.Refined" "Axial.Refined.Refine"
            |> publicStaticMemberNames

        refineMembers
        |> assertContainsAll [ "nonBlankString"; "positiveInt"; "nonEmptyList" ]

        moduleType typeof<Flow<unit, unit, unit>> "Axial.Flow.Bind"
        |> publicStaticMemberNames
        |> assertContainsAll [ "error"; "mapError" ]

        let bindErrorWithErrorSources =
            typeof<BindErrorWithError>.GetMethods(BindingFlags.Static ||| BindingFlags.Public)
            |> Array.filter (fun methodInfo -> methodInfo.Name = "WithError")
            |> Array.choose (fun methodInfo ->
                let parameters = methodInfo.GetParameters()

                if parameters.Length = 0 then
                    None
                else
                    let tupleFields = FSharpType.GetTupleElements parameters[0].ParameterType
                    tupleFields |> Array.tryHead)
            |> Array.map _.FullName
            |> Set.ofArray

        bindErrorWithErrorSources
        |> assertContainsNone [ typeof<bool>.FullName; typeof<Async<bool>>.FullName; typeof<Task<bool>>.FullName; typeof<ValueTask<bool>>.FullName ]

        moduleType typeof<Diagnostics<string>> "Axial.Validation.Diagnostics"
        |> publicStaticMemberNames
        |> assertContainsAll [ "empty"; "singleton"; "merge"; "toString"; "flatten" ]

        moduleType typeof<Ref<int>> "Axial.Flow.Ref"
        |> publicStaticMemberNames
        |> assertContainsAll [ "make"; "get"; "set"; "update"; "modify" ]

    [<Fact>]
    let ``schedule stream and STM modules keep expected public shape`` () =
        moduleType typeof<Schedule<unit, unit, unit>> "Axial.Flow.Schedule"
        |> publicStaticMemberNames
        |> assertContainsAll [ "recurs"; "spaced"; "exponential"; "jittered"; "retry"; "repeat" ]

        moduleType typeof<FlowStream<unit, unit, unit>> "Axial.Flow.FlowStream"
        |> publicStaticMemberNames
        |> assertContainsAll [ "fromSeq"; "runForEach"; "map" ]

        moduleType typeof<STM<int>> "Axial.Flow.STM"
        |> publicStaticMemberNames
        |> assertContainsAll [ "retry"; "orElse"; "atomically" ]

        moduleType typeof<TRef<int>> "Axial.Flow.TRef"
        |> publicStaticMemberNames
        |> assertContainsAll [ "make"; "get"; "set"; "update" ]

    [<Fact>]
    let ``concurrency modules keep expected public shape`` () =
        moduleType typeof<Deferred<string, int>> "Axial.Flow.Deferred"
        |> publicStaticMemberNames
        |> assertContainsAll [ "make"; "await"; "complete"; "succeed"; "fail"; "die"; "interrupt" ]

        moduleType typeof<FlowSemaphore> "Axial.Flow.Semaphore"
        |> publicStaticMemberNames
        |> assertContainsAll [ "make"; "create"; "withPermit" ]

    [<Fact>]
    let ``hosting and telemetry modules keep expected public shape`` () =
        moduleType typeof<LiveClock> "Axial.Flow.Hosting.Hosting"
        |> publicStaticMemberNames
        |> assertContainsAll [ "createBaseRuntime" ]

        moduleType typeof<LiveClock> "Axial.Flow.Hosting.Startup"
        |> publicStaticMemberNames
        |> assertContainsAll [ "validateEnvironment" ]

        moduleTypeFromAssembly "Axial.Flow.Telemetry" "Axial.Flow.Telemetry.Activity"
        |> publicStaticMemberNames
        |> assertContainsAll [ "source"; "trace" ]

    [<Fact>]
    let ``service modules keep expected public shape`` () =
        moduleType typeof<Axial.Flow.Console.IConsole> "Axial.Flow.Console.Console"
        |> publicStaticMemberNames
        |> assertContainsAll [ "readLine"; "writeLine"; "layer"; "live" ]

        moduleType typeof<Axial.Flow.FileSystem.IFileSystem> "Axial.Flow.FileSystem.FileSystem"
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "readAllText"
              "readAllTextWithEncoding"
              "readAllTextAsync"
              "readAllLines"
              "readAllLinesWithEncoding"
              "readAllLinesAsync"
              "readAllBytes"
              "readAllBytesAsync"
              "writeAllText"
              "writeAllTextWithEncoding"
              "writeAllTextAsync"
              "writeAllLines"
              "writeAllLinesWithEncoding"
              "writeAllLinesAsync"
              "writeAllBytes"
              "writeAllBytesAsync"
              "appendAllText"
              "appendAllTextWithEncoding"
              "appendAllTextAsync"
              "appendAllLines"
              "appendAllLinesWithEncoding"
              "fileExists"
              "exists"
              "deleteFile"
              "copyFile"
              "moveFile"
              "openFile"
              "openFileWithAccess"
              "openFileWithShare"
              "openRead"
              "openText"
              "openWrite"
              "createFile"
              "createText"
              "appendText"
              "getFileAttributes"
              "setFileAttributes"
              "getFileCreationTime"
              "getFileCreationTimeUtc"
              "setFileCreationTime"
              "setFileCreationTimeUtc"
              "getFileLastAccessTime"
              "getFileLastAccessTimeUtc"
              "setFileLastAccessTime"
              "setFileLastAccessTimeUtc"
              "getFileLastWriteTime"
              "getFileLastWriteTimeUtc"
              "setFileLastWriteTime"
              "setFileLastWriteTimeUtc"
              "directoryExists"
              "createDirectory"
              "deleteDirectory"
              "moveDirectory"
              "enumerateFiles"
              "getFiles"
              "enumerateDirectories"
              "getDirectories"
              "enumerateFileSystemEntries"
              "getFileSystemEntries"
              "getLogicalDrives"
              "getDirectoryRoot"
              "getParent"
              "getCurrentDirectory"
              "setCurrentDirectory"
              "getDirectoryCreationTime"
              "getDirectoryCreationTimeUtc"
              "setDirectoryCreationTime"
              "setDirectoryCreationTimeUtc"
              "getDirectoryLastAccessTime"
              "getDirectoryLastAccessTimeUtc"
              "setDirectoryLastAccessTime"
              "setDirectoryLastAccessTimeUtc"
              "getDirectoryLastWriteTime"
              "getDirectoryLastWriteTimeUtc"
              "setDirectoryLastWriteTime"
              "setDirectoryLastWriteTimeUtc"
              "combine"
              "changeExtension"
              "getDirectoryName"
              "getInvalidFileNameChars"
              "getInvalidPathChars"
              "getExtension"
              "getFileName"
              "getFileNameWithoutExtension"
              "getFullPath"
              "getPathRoot"
              "getRelativePath"
              "getTempPath"
              "getTempFileName"
              "getRandomFileName"
              "hasExtension"
              "endsInDirectorySeparator"
              "trimEndingDirectorySeparator"
              "isPathFullyQualified"
              "isPathRooted"
              "layer"
              "live" ]

        moduleType typeof<Axial.Flow.FileSystem.IFileSystem> "Axial.Flow.FileSystem.FileSystemErrorModule"
        |> publicStaticMemberNames
        |> assertContainsAll [ "fromException"; "describe" ]

        moduleType typeof<Axial.Flow.Http.IHttp> "Axial.Flow.Http.Http"
        |> publicStaticMemberNames
        |> assertContainsAll [ "getString"; "layer"; "live" ]

        moduleType typeof<Axial.Flow.Process.IProcess> "Axial.Flow.Process.Process"
        |> publicStaticMemberNames
        |> assertContainsAll [ "execute"; "layer"; "live" ]

        moduleType typeof<Axial.Flow.PlatformService.EnvironmentVariableError> "Axial.Flow.PlatformService.Clock"
        |> publicStaticMemberNames
        |> assertContainsAll [ "now"; "utcDateTime"; "unixTimeSeconds"; "unixTimeMilliseconds"; "layer"; "live"; "fromValue" ]

        moduleType typeof<Axial.Flow.PlatformService.EnvironmentVariableError> "Axial.Flow.PlatformService.Log"
        |> publicStaticMemberNames
        |> assertContainsAll [ "log"; "trace"; "debug"; "info"; "warning"; "error"; "critical"; "layer"; "live"; "fromSink" ]

        moduleType typeof<Axial.Flow.PlatformService.EnvironmentVariableError> "Axial.Flow.PlatformService.Random"
        |> publicStaticMemberNames
        |> assertContainsAll [ "next"; "nextMax"; "nextInt"; "nextDouble"; "nextBytes"; "bytes"; "layer"; "live"; "fromValue"; "fromFixed" ]

        moduleType typeof<Axial.Flow.PlatformService.EnvironmentVariableError> "Axial.Flow.PlatformService.EnvironmentVariable"
        |> publicStaticMemberNames
        |> assertContainsAll [ "get"; "tryGet"; "getInt"; "getInt64"; "getDouble"; "getDecimal"; "getGuid"; "getUri"; "getTimeSpan"; "getBool" ]

    [<Fact>]
    let ``service and layer surfaces keep expected public shape`` () =
        typeof<Service<int>>
        |> publicStaticMemberNames
        |> assertContainsAll [ "get"; "resolve" ]

        moduleType typeof<Layer<unit, unit, int>> "Axial.Flow.Layer"
        |> publicStaticMemberNames
        |> assertContainsAll [ "fromAsync"; "fromTask"; "fromValueTask"; "succeed"; "read"; "addFinalizer"; "acquireRelease"; "map"; "mapError"; "bind"; "zip"; "zipPar"; "merge"; "map2"; "map3"; "apply" ]

        typeof<LayerBuilder>
        |> publicInstanceMethodNames
        |> assertContainsAll [ "Return"; "ReturnFrom"; "Bind"; "BindReturn"; "Delay"; "Run"; "Combine"; "MergeSources"; "MergeSources3" ]

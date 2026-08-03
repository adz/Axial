namespace Axial.Tests

open Axial.Parse

open Axial

open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Threading.Tasks
open Axial.Flow
open Axial.Result
open Axial.Constraint
open Axial.Refined
open Axial.Schema
open Axial.Schema.Syntax
open Axial.Constraint.ConstraintDSL
open Swensen.Unquote
open Axial.Flow.Hosting
open Axial.Flow.Telemetry
open Microsoft.FSharp.Reflection
open Xunit

module ApiShapeTests =
    type private Customer =
        { Name: string
          Age: int }

    type private CustomerProfile =
        { Name: string
          Age: int
          Active: bool }

    type private PrimitiveProfile =
        { Name: string
          Age: int
          Balance: decimal
          Active: bool
          BirthDate: DateOnly
          LastSeen: DateTimeOffset
          Id: Guid }

    let private flowBuilderBindAndReturnFromArgumentNames () =
        typeof<FlowBuilder>.GetMethods()
        |> Array.filter (fun methodInfo ->
            methodInfo.IsPublic
            && not methodInfo.IsSpecialName
            && (methodInfo.Name = "Bind" || methodInfo.Name = "ReturnFrom"))
        |> Array.collect (fun methodInfo -> methodInfo.GetParameters())
        |> Array.map (fun parameterInfo -> parameterInfo.ParameterType.Name)
        |> Array.distinct
        |> Array.sort

    let private runFsiScript (scriptContents: string) =
        let scriptPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.fsx")
        File.WriteAllText(scriptPath, scriptContents)

        try
            use childProcess =
                new Process(
                    StartInfo =
                        ProcessStartInfo(
                            FileName = "dotnet",
                            Arguments = $"fsi \"{scriptPath}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false
                        )
                )

            childProcess.Start() |> ignore

            let standardOutput = childProcess.StandardOutput.ReadToEndAsync()
            let standardError = childProcess.StandardError.ReadToEndAsync()
            childProcess.WaitForExit()
            Task.WhenAll(standardOutput, standardError).Wait()

            childProcess.ExitCode, standardOutput.Result + standardError.Result
        finally
            File.Delete scriptPath

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

    let private assertTypeAbsentFromAssembly (assemblyName: string) (fullName: string) =
        let assembly = Assembly.Load assemblyName
        let found = assembly.GetType(fullName, false)

        test <@ isNull found @>

    let private assertTypePresentInAssembly (assemblyName: string) (fullName: string) =
        let assembly = Assembly.Load assemblyName
        let found = assembly.GetType(fullName, true)

        test <@ not (isNull found) @>
        found

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

    let private schemaField<'model, 'value> externalName order getter : Field<'model, 'value> =
        let definition: FieldDefinition<'model, 'value> =
            { ExternalName = ExternalFieldName.create externalName
              Order = FieldOrder.create order
              Getter = getter
              ValueSchema = Schema.text.ValueDefinition
              Rules = [] }

        Field definition

    let private schemaFieldDescriptor<'model, 'value> (field: Field<'model, 'value>) : FieldDescriptor<'model> =
        FieldDescriptorOps.fromField field

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
                arguments[1] = typeof<Violation>
            elif returnType.IsGenericType && returnType.GetGenericTypeDefinition() = checkFunctionType then
                returnType.GetGenericArguments()[1] |> loop
            else
                false

        loop returnType

    let private returnsBoolShape (returnType: Type) =
        let checkFunctionType = typedefof<FSharpFunc<_, _>>

        let rec loop (returnType: Type) =
            if returnType = typeof<bool> then
                true
            elif returnType.IsGenericType && returnType.GetGenericTypeDefinition() = checkFunctionType then
                returnType.GetGenericArguments()[1] |> loop
            else
                false

        loop returnType

    /// There is no public Check type: a constraint is a sealed value, not a function alias, so a rule can carry
    /// its description alongside the closures that execute it.
    let private assertConstraintValueShape<'value> () =
        let constraintType = typeof<Constraint<'value>>

        test <@ constraintType.IsGenericType @>
        test <@ constraintType.GetGenericTypeDefinition() = typedefof<Constraint<_>> @>
        test <@ constraintType.IsSealed @>
        test <@ constraintType.GetGenericTypeDefinition() <> typedefof<FSharpFunc<_, _>> @>
        let publicConstructorCount =
            constraintType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance).Length

        test <@ publicConstructorCount = 0 @>

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

    let private assertMethodsReturnBool methodNames (targetType: Type) =
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
                    if returnsBoolShape methodInfo.ReturnType then
                        None
                    else
                        Some(name, methodInfo.ReturnType.FullName)))

        test <@ List.isEmpty missing @>
        test <@ List.isEmpty wrongReturnType @>

    let private assertNoMethodsReturnBool (targetType: Type) =
        let boolMethodNames =
            targetType
            |> publicStaticMethods
            |> Array.choose (fun methodInfo ->
                if returnsBoolShape methodInfo.ReturnType then Some methodInfo.Name else None)

        test <@ Array.isEmpty boolMethodNames @>

    [<Fact>]
    let ``the localization surface keeps its documented public shape`` () =
        moduleType typeof<Renderer> "Axial.Constraint.RendererModule"
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "english"
              "ofLookup"
              "ofResourceManager"
              "ofResourceManagerWithCultures"
              "ofCurrentCulture"
              "context"
              "attribute"
              "unscoped"
              "withValues"
              "attributeName"
              "fullMessage" ]

        moduleType typeof<Renderer> "Axial.Constraint.MessageDescriptorModule"
        |> publicStaticMemberNames
        |> assertContainsAll [ "key"; "arguments"; "segments" ]

        moduleType typeof<Renderer> "Axial.Constraint.MessageFormatSpecModule"
        |> publicStaticMemberNames
        |> assertContainsAll [ "descriptor"; "fallback"; "pluralArgument" ]

    [<Fact>]
    let ``the documented localization pipelines compile in their end-user form`` () =
        // These are the call sites the guides teach. The assertion is that they compile and type-check as
        // written; the behaviour they produce is covered by the Constraint and Schema localization tests.
        let renderer = Renderer.ofLookup (fun _ -> None)
        let signup = renderer |> Renderer.context "signup"

        let violation: Violation = Atomic(Expected(PresenceAtom Present, None))

        let standalone =
            violation |> Violation.fullMessage (signup |> Renderer.attribute "name")

        let predicate = violation |> Violation.message signup

        let isbn =
            Constraint.customLocalized "books.isbn.invalid" "must be a valid ISBN" (fun (value: string) ->
                value.Length = 13)

        let isbnWith =
            Constraint.customLocalizedWith
                "books.isbn.invalid"
                "must be a valid ISBN"
                (Map.ofList [ "expectedLength", ConstraintValue.Integer 13L ])
                (fun (value: string) -> value.Length = 13)

        let spec =
            MessageDescriptor.Advanced.ofSegments [ "billing"; "cardExpired" ] Map.empty
            |> MessageFormatSpec.Advanced.create "card has expired" None

        let advanced =
            Renderer.Advanced.ofResolver (fun request -> Some(MessageResolution.Rendered request.BaseKey))
            |> Renderer.Advanced.withValueFormatting (fun request -> ConstraintValue.render request.Value)
            |> Renderer.Advanced.attributePath [ "address"; "postcode" ]

        test <@ standalone = "Name must be present" @>
        test <@ predicate = "must be present" @>
        test <@ Constraint.test isbn "1234567890123" @>
        test <@ Constraint.test isbnWith "1234567890123" @>
        test <@ Renderer.english |> Renderer.Advanced.format spec = "card has expired" @>
        test <@ advanced |> Renderer.Advanced.format spec = "address.postcode.billing.cardExpired" @>
        test <@ Renderer.Advanced.attributeCandidates advanced |> List.isEmpty |> not @>
        test <@ Renderer.Advanced.messageRequests advanced spec |> List.length = 3 @>
        test <@ Renderer.Advanced.lookupCandidates advanced spec |> List.length = 3 @>

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

        let argumentTypeNames = flowBuilderBindAndReturnFromArgumentNames () |> Set.ofArray
        assertContainsAll [ "ColdTask`1"; "Task`1"; "ValueTask`1"; "FSharpAsync`1"; "Flow`3" ] argumentTypeNames

    [<Fact>]
    let ``result builder remains while refinement builder is absent`` () =
        typeof<ResultBuilder>
        |> publicInstanceMethodNames
        |> assertContainsAll [ "Return"; "ReturnFrom"; "Bind"; "Delay"; "Run"; "Combine"; "TryWith"; "TryFinally"; "Using"; "While"; "For" ]

        assertTypeAbsentFromAssembly "Axial.Refined" "Axial.Refined.RefineBuilder"

    [<Fact>]
    let ``schema inspection and input interpreter modules expose the expected surface`` () =
        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.Inspect"
        |> publicStaticMemberNames
        |> assertContainsAll [ "model"; "schema"; "field" ]

        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.Schema"
        |> publicStaticMemberNames
        |> assertContainsAll [ "parse"; "parseWith"; "constructorErrorAt"; "check"; "refine" ]

        assertTypeAbsentFromAssembly "Axial.Schema" "Axial.Schema.ValueSchema`1"

        let dataMembers =
            moduleTypeFromAssembly "Axial.Data" "Axial.DataModule"
            |> publicStaticMemberNames

        dataMembers
        |> assertContainsAll
            [ "ofMap"
              "ofNameValues"
              "ofNameValueCollection"
              "ofCliArgs"
              "ofJsonElement"
              "ofJsonDocument"
              "ofConfiguration"
              "assoc"
              "optionalAssoc"
              "data"
              "number"
              "tryPatch"
              "applyEdit"
              "patch"
              "set"
              "replace"
              "remove"
              "append"
              "prepend"
              "insert"
              "rename"
              "update"
              "diff"
              "compare"
              "tryMatch"
              "render"
              "renderIndented"
              "tryText"
              "tryBool"
              "tryNumberToken"
              "tryList"
              "tryObject"
              "redisplay"
              "redisplayAt"
              "redisplayPath" ]

        let dataEditMembers =
            moduleTypeFromAssembly "Axial.Data" "Axial.DataEditModule"
            |> publicStaticMemberNames

        dataEditMembers
        |> assertContainsAll [ "set"; "replace"; "remove"; "append"; "prepend"; "insert"; "rename"; "update" ]

        test <@ not (dataMembers.Contains "put") @>
        test <@ not (dataEditMembers.Contains "put") @>

        test <@ typeof<DataEdit>.Assembly.GetName().Name = "Axial.Data" @>
        test <@ typeof<DataPattern>.Assembly.GetName().Name = "Axial.Data" @>
        test <@ typeof<DataDifference>.Assembly.GetName().Name = "Axial.Data" @>
        test <@ typeof<DataMismatch>.Assembly.GetName().Name = "Axial.Data" @>

        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.RetainedParseResult"
        |> publicStaticMemberNames
        |> assertContainsAll [ "create"; "renderErrors" ]

        test <@ typeof<SchemaErrors>.Assembly.GetName().Name = "Axial.Schema" @>
        test <@ typeof<SchemaIssue>.Assembly.GetName().Name = "Axial.Schema" @>
        test <@ typeof<Axial.Schema.Path>.Assembly.GetName().Name = "Axial.Schema" @>

        Assembly.Load("Axial.Schema").GetType("Axial.Schema.SchemaErrorsModule", true)
        |> publicStaticMemberNames
        |> assertContainsAll [ "toList"; "count"; "isEmpty"; "toString" ]

        Assembly.Load("Axial.Schema").GetType("Axial.Schema.PathModule", true)
        |> publicStaticMemberNames
        |> assertContainsAll [ "root"; "key"; "index"; "append"; "format"; "fold" ]

    [<Fact>]
    let ``removed validation surface is absent`` () =
        let assemblies =
            [ Assembly.Load "Axial.Result"
              Assembly.Load "Axial.Constraint"
              Assembly.Load "Axial.Refined"
              Assembly.Load "Axial.Schema"
              Assembly.Load "Axial" ]

        let removedTypes =
            [ "Axial.Validation.Validation`2"
              "Axial.Validation.Diagnostics`1"
              "Axial.Validation.PathSegment"
              "Axial.Validation.ValidateBuilder" ]

        for assembly in assemblies do
            for fullName in removedTypes do
                test <@ isNull (assembly.GetType(fullName, false)) @>

    [<Fact>]
    let ``codec compiles json codecs from schemas without extra package coupling`` () =
        moduleTypeFromAssembly "Axial.Schema.Json" "Axial.Schema.Json.Json"
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "compile"; "serialize"; "serializeBytes"; "parseData"; "deserialize"; "deserializeBytes"; "tryDeserialize" ]

        // JSON Schema generation lives in its own package; the namespace stays Axial.Schema so
        // callers only add a package reference, not a new open.
        moduleTypeFromAssembly "Axial.Schema.JsonSchema" "Axial.Schema.JsonSchema"
        |> publicStaticMemberNames
        |> assertContainsAll [ "generate"; "generateValue" ]

        referencedAssemblyNames (Assembly.Load "Axial.Schema.Json")
        |> assertContainsNone [ "Axial.Flow" ]

    [<Fact>]
    let ``leaf packages stay independent of each other`` () =
        // Flow, Result, and Constraint are independent leaves. Refined depends only on Constraint; Schema
        // depends directly on Constraint and Refined, never on Result.
        let leafPackages = [ "Axial.Flow"; "Axial.Result"; "Axial.Constraint"; "Axial.Schema" ]

        let allowedReferences =
            [ "Axial.Schema", "Axial.Constraint"
              "Axial.Schema", "Axial.Refined" ]

        for package in leafPackages do
            let forbidden =
                leafPackages
                |> List.filter (fun other ->
                    other <> package && not (List.contains (package, other) allowedReferences))

            let references = referencedAssemblyNames (Assembly.Load package)

            references |> assertContainsNone forbidden
            references |> assertContainsNone [ "Axial" ]

        // Schema owns no Result dependency: it consumes Constraint and Refined directly.
        referencedAssemblyNames (Assembly.Load "Axial.Schema")
        |> assertContainsNone [ "Axial.Result" ]

        // Refined depends on Constraint, never on Result.
        referencedAssemblyNames (Assembly.Load "Axial.Refined")
        |> assertContainsAll [ "Axial.Constraint" ]

        referencedAssemblyNames (Assembly.Load "Axial.Refined")
        |> assertContainsNone [ "Axial.Result" ]

    [<Fact>]
    let ``policy lives in flow without schema or error handling dependencies`` () =
        let flowAssembly = Assembly.Load "Axial.Flow"

        test <@ flowAssembly.GetName().Name = "Axial.Flow" @>

        referencedAssemblyNames flowAssembly
        |> assertContainsNone
            [ "Axial.Schema"
              "Axial.Result"
              "Axial.Constraint"
              "Axial.Diagnostics"
              "Axial.Refined"
              "Axial.ErrorHandling" ]

        moduleTypeFromAssembly "Axial.Flow" "Axial.Flow.PolicyModule"
        |> publicStaticMemberNames
        |> assertContainsAll [ "lift"; "withError"; "context"; "pass"; "compose"; "optional" ]

    [<Fact>]
    let ``error handling meta-package installs three focused packages and exposes no API`` () =
        let metaAssembly = Assembly.Load "Axial.ErrorHandling"

        test <@ metaAssembly.GetName().Name = "Axial.ErrorHandling" @>
        test <@ metaAssembly.GetExportedTypes() |> Array.isEmpty @>
        test <@ isNull (metaAssembly.GetType("Axial.ErrorHandling", false)) @>

    [<Fact>]
    let ``schema validation interpreters live alongside schema in the consolidated schema package`` () =
        let schemaAssembly = Assembly.Load "Axial.Schema"
        let schemaReferences = referencedAssemblyNames schemaAssembly

        test <@ schemaAssembly.GetName().Name = "Axial.Schema" @>

        schemaReferences
        |> assertContainsAll [ "Axial.Constraint"; "Axial.Refined" ]

        schemaReferences |> assertContainsNone [ "Axial.Diagnostics"; "Axial.Result" ]

        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.SchemaValidation"
        |> publicStaticMemberNames
        |> assertContainsAll [ "packageName" ]

        // There is exactly one value-rule vocabulary. Schema publishes no constraint catalogue of its own:
        // the field-block syntax carries only collection adapters, and supply, which is Schema's concern.
        let syntaxMembers =
            moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.SyntaxModule"
            |> publicStaticMemberNames
            |> Set.filter (fun name -> not (name.Contains "$"))

        test <@ syntaxMembers = set [ "constrainItems"; "constrainValues" ] @>

        let schemaAssemblyTypeNames =
            schemaAssembly.GetTypes()
            |> Array.filter _.IsPublic
            |> Array.map _.FullName
            |> Set.ofArray

        // The duplicate facades this design removed must not come back under any name.
        test <@ not (schemaAssemblyTypeNames |> Set.contains "Axial.Schema.Constraint") @>
        test <@ not (schemaAssemblyTypeNames |> Set.contains "Axial.Schema.SchemaConstraint`1") @>
        test <@ not (schemaAssemblyTypeNames |> Set.contains "Axial.Schema.ConstraintDescriptor") @>
        test <@ not (schemaAssemblyTypeNames |> Set.contains "Axial.Schema.ConstraintCheck") @>

        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.SchemaCheck"
        |> publicStaticMemberNames
        |> assertContainsAll [ "fromUnderlying"; "complete"; "text"; "ordered" ]

    [<Fact>]
    let ``schema types stay out of the flow package`` () =
        let schemaType = typedefof<Schema<_>>
        let valueSchemaType = typedefof<Schema<_>>
        let fieldType = typedefof<Field<_, _>>
        let primitiveValueKindType = typeof<PrimitiveValueKind>
        let externalFieldNameType = typeof<ExternalFieldName>
        let fieldOrderType = typeof<FieldOrder>
        let schemaModule = moduleType schemaType "Axial.Schema.Schema"
        let fieldModule = moduleType fieldType "Axial.Schema.Field"
        let schemaAssembly = schemaType.Assembly
        let references = referencedAssemblyNames schemaAssembly
        let publicConstructors =
            schemaType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)
        let publicValueConstructors =
            valueSchemaType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)
        let publicFieldConstructors =
            fieldType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)
        let publicExternalFieldNameConstructors =
            externalFieldNameType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)
        let fieldDefinitionType =
            schemaAssembly.GetType("Axial.Schema.FieldDefinition`2", true)
        let fieldCreateMethods =
            publicStaticMethods fieldModule
            |> Array.filter (fun methodInfo -> methodInfo.Name = "create")
        let fieldTypeDefinition = typedefof<Field<_, _>>
        let fieldCreateMethod = fieldCreateMethods |> Array.tryExactlyOne
        let fieldCreateParameterCount =
            fieldCreateMethod
            |> Option.map (fun methodInfo -> methodInfo.GetParameters().Length)
            |> Option.defaultValue -1
        let fieldCreateReturnType =
            fieldCreateMethod
            |> Option.map (fun methodInfo -> methodInfo.ReturnType.GetGenericTypeDefinition())
            |> Option.defaultValue typeof<obj>
        let externalNameProperty =
            fieldDefinitionType.GetProperty(
                "ExternalName",
                BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance
            )
        let getterProperty =
            fieldDefinitionType.GetProperty(
                "Getter",
                BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance
            )
        let orderProperty =
            fieldDefinitionType.GetProperty(
                "Order",
                BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance
            )

        test <@ schemaType.IsGenericTypeDefinition @>
        test <@ schemaType.GetGenericArguments().Length = 1 @>
        test <@ publicConstructors.Length = 0 @>
        test <@ valueSchemaType.IsGenericTypeDefinition @>
        test <@ valueSchemaType.GetGenericArguments().Length = 1 @>
        test <@ publicValueConstructors.Length = 0 @>
        test <@ fieldType.IsGenericTypeDefinition @>
        test <@ fieldType.GetGenericArguments().Length = 2 @>
        test <@ publicFieldConstructors.Length = 0 @>
        test <@ publicExternalFieldNameConstructors.Length = 0 @>
        let schemaMembers = schemaModule |> publicStaticMemberNames
        schemaMembers
        |> assertContainsAll
            [ "text"
              "int"
              "decimal"
              "bool"
              "date"
              "dateTime"
              "guid" ]
        test <@ [ "define"; "record"; "recordFor"; "field"; "fieldWith"; "build"; "buildResult"; "buildResultWith" ]
                 |> List.forall (fun removed -> not (schemaMembers |> Set.contains removed)) @>
        fieldModule
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "create"; "externalName"; "order"; "getValue"; "constraints"; "supply"; "withConstraint"; "withConstraints" ]
        test <@ fieldCreateMethods.Length = 1 @>
        test <@ fieldCreateParameterCount = 3 @>
        test <@ fieldCreateReturnType = fieldTypeDefinition @>
        schemaModule
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "text"
              "int"
              "decimal"
              "bool"
              "date"
              "dateTime"
              "guid"
              "primitiveKind"
              "convert"
              "refine"
              "isRefined"
              "underlyingPrimitiveKind"
              "inspectUnderlying"
              "rawConstraints"
              "constraints"
              "allConstraints"
              "constrain"
              "constrainAll"
              "mustSupply"
              "mayOmit"
              "supply" ]
        primitiveValueKindType
        |> publicUnionCaseNames
        |> assertContainsAll [ "Text"; "Int"; "Decimal"; "Bool"; "Date"; "DateTime"; "Guid" ]

        typeof<Supply> |> publicUnionCaseNames |> assertContainsAll [ "Supplied"; "Omittable" ]

        // The constraint read model belongs to Axial.Constraint, not to Schema: one declaration, one vocabulary.
        let atomCases = typeof<ConstraintAtom> |> publicUnionCaseNames

        atomCases
        |> assertContainsAll
            [ "PresenceAtom"; "CardinalityAtom"; "RelationAtom"; "MembershipAtom"; "UniquenessAtom"; "FormatAtom"; "NumberAtom" ]

        test <@ typeof<ConstraintAtom>.Assembly = typeof<Violation>.Assembly @>
        test <@ typeof<ConstraintDescription>.Assembly = typeof<Violation>.Assembly @>

        test <@ valueSchemaType.Assembly = schemaAssembly @>
        test <@ fieldType.Assembly = schemaAssembly @>
        test <@ primitiveValueKindType.Assembly = schemaAssembly @>
        test <@ externalFieldNameType.Assembly = schemaAssembly @>
        test <@ fieldOrderType.Assembly = schemaAssembly @>
        test <@ externalNameProperty.PropertyType = externalFieldNameType @>
        test <@ orderProperty.PropertyType = fieldOrderType @>
        test <@ getterProperty.PropertyType.GetGenericTypeDefinition() = typedefof<FSharpFunc<_, _>> @>
        test <@ schemaAssembly.GetName().Name = "Axial.Schema" @>
        // Schema uses Result and Refined directly and stays independent of Flow.
        references
        |> assertContainsNone [ "Axial.Flow" ]

    [<Fact>]
    let ``primitive value schemas carry typed intrinsic metadata`` () =
        let valueSchemas =
            [ Schema.primitiveKind Schema.text
              Schema.primitiveKind Schema.int
              Schema.primitiveKind Schema.decimal
              Schema.primitiveKind Schema.bool
              Schema.primitiveKind Schema.date
              Schema.primitiveKind Schema.dateTime
              Schema.primitiveKind Schema.guid ]

        test <@
            valueSchemas =
                [ PrimitiveValueKind.Text
                  PrimitiveValueKind.Int
                  PrimitiveValueKind.Decimal
                  PrimitiveValueKind.Bool
                  PrimitiveValueKind.Date
                  PrimitiveValueKind.DateTime
                  PrimitiveValueKind.Guid ]
        @>
        test <@ Schema.text.ValueDefinition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Text @>
        test <@ Schema.int.ValueDefinition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Int @>
        test <@ Schema.decimal.ValueDefinition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Decimal @>
        test <@ Schema.bool.ValueDefinition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Bool @>
        test <@ Schema.date.ValueDefinition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Date @>
        test <@ Schema.dateTime.ValueDefinition.Shape = PrimitiveValueDefinition PrimitiveValueKind.DateTime @>
        test <@ Schema.guid.ValueDefinition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Guid @>
        test <@ Schema.constraints Schema.text = [] @>
        raises<ArgumentNullException> <@ Schema.primitiveKind Unchecked.defaultof<Schema<string>> |> ignore @>

    [<Fact>]
    let ``refined value schemas accept one bidirectional refinement descriptor`` () =
        let valueModule = moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.Schema"

        let refinedOverloads =
            publicStaticMethods valueModule
            |> Array.filter (fun methodInfo -> methodInfo.Name.Equals("refine", StringComparison.OrdinalIgnoreCase))

        test <@ refinedOverloads.Length = 1 @>

        let refined = refinedOverloads[0]
        let parameters = refined.GetParameters()
        let parameterNames = parameters |> Array.map _.Name

        test <@ parameterNames = [| "refinement"; "schema" |] @>

        let refinementIsDescriptor =
            parameters[0].ParameterType.GetGenericTypeDefinition() = typedefof<Refinement<_, _>>

        let rawIsValueSchema = parameters[1].ParameterType.GetGenericTypeDefinition() = typedefof<Schema<_>>
        let returnsValueSchema = refined.ReturnType.GetGenericTypeDefinition() = typedefof<Schema<_>>
        let rawMatchesDescriptor =
            parameters[1].ParameterType.GetGenericArguments()[0] = parameters[0].ParameterType.GetGenericArguments()[0]

        test <@ refinementIsDescriptor @>
        test <@ rawIsValueSchema @>
        test <@ returnsValueSchema @>
        test <@ rawMatchesDescriptor @>

    [<Fact>]
    let ``schema attaches the universal constraint and keeps supply separate`` () =
        let maxLength: Constraint<string> = Constraint.maxLength 20
        let text = Schema.text |> Schema.constrain maxLength |> Schema.mustSupply

        let field =
            schemaField "name" 0 (fun (model: Customer) -> model.Name)
            |> Field.withConstraint maxLength

        let descriptor = field |> schemaFieldDescriptor

        // The very same value the caller checked directly is what Schema retains and inspection returns.
        test <@ Schema.constraints text = [ Constraint.inspect maxLength ] @>
        test <@ Schema.supply text = Some Supply.Supplied @>
        test <@ Field.constraints field = [ Constraint.inspect maxLength ] @>
        test <@ Field.supply field = None @>
        test <@ SchemaRule.descriptions descriptor.Rules = [ Constraint.inspect maxLength ] @>
        test <@ Schema.constraints Schema.text = [] @>

        raises<ArgumentNullException> <@ Schema.constrain null Schema.text |> ignore @>
        raises<ArgumentNullException> <@ Field.constraints Unchecked.defaultof<Field<Customer, string>> |> ignore @>

    [<Fact>]
    let ``the constraint catalogue exposes one vocabulary with atom-level identity`` () =
        let text: Constraint<string> = Constraint.present
        let size: Constraint<string> = Constraint.lengthBetween 2 20
        let counts: Constraint<int list> = Constraint.lengthBetween 1 5

        let keys =
            [ text; size; Constraint.email; Constraint.trimmed; Constraint.pattern "^[a-z]+$" ]
            |> List.map (Constraint.inspect >> ConstraintDescription.atoms >> List.map ConstraintAtom.key)
            |> List.concat

        test <@
            keys =
                [ "constraint.presence.present"
                  "constraint.cardinality.between"
                  "constraint.format.email"
                  "constraint.format.trimmed"
                  "constraint.format.pattern" ]
        @>

        // Shape-neutral atoms: text and collection sizes are the same expectation, resolved by the schema shape.
        let textSize = Constraint.inspect size
        let listSize = Constraint.inspect (Constraint.lengthBetween 2 20: Constraint<int list>)
        let countSize = Constraint.inspect counts
        let countAsText = Constraint.inspect (Constraint.lengthBetween 1 5: Constraint<string>)

        test <@ textSize = listSize @>
        test <@ countSize = countAsText @>

        Assert.Throws<ArgumentOutOfRangeException>(fun () -> (Constraint.minLength -1: Constraint<string>) |> ignore) |> ignore
        Assert.Throws<ArgumentOutOfRangeException>(fun () -> (Constraint.length -1: Constraint<int list>) |> ignore) |> ignore
        Assert.Throws<ArgumentException>(fun () -> (Constraint.lengthBetween 5 2: Constraint<string>) |> ignore) |> ignore
        raises<ArgumentException> <@ Constraint.between 10 1 |> ignore @>
        raises<ArgumentException> <@ Constraint.pattern "" |> ignore @>
        raises<ArgumentNullException> <@ Constraint.oneOf null |> ignore @>

    [<Fact>]
    let ``one inspection tree drives diagnostics, export, UI, and documentation`` () =
        // PRD 3: one declaration, many interpreters. Each projection below reads the same atoms, so a rule can
        // never mean one thing at runtime and another in a generated document.
        let constraints =
            (Schema.text
             |> Schema.constrainAll
                 [ Constraint.present
                   Constraint.maxLength 20
                   Constraint.email
                   Constraint.pattern "^[^@]+@example.com$"
                   Constraint.oneOf [ "ada@example.com"; "grace@example.com" ] ]
             |> Schema.constraints)
            @ (Schema.``int`` |> Schema.constrain (Constraint.between 1 10) |> Schema.constraints)
            @ (Schema.listWith Schema.``int``
               |> Schema.constrainAll [ Constraint.lengthBetween 1 3; Constraint.distinct ]
               |> Schema.constraints)

        let atoms = constraints |> List.collect ConstraintDescription.atoms

        let render project = atoms |> List.choose project

        let diagnostics =
            render (function
                | PresenceAtom Present -> Some "presence"
                | CardinalityAtom(Cardinality.Maximum maximum) -> Some $"maxLength {maximum}"
                | FormatAtom Email -> Some "email"
                | FormatAtom(Pattern pattern) -> Some $"pattern {pattern}"
                | MembershipAtom(OneOf choices) -> Some $"oneOf {choices.Length}"
                | RelationAtom(Within(minimum, maximum)) ->
                    Some $"within {ConstraintValue.render minimum}-{ConstraintValue.render maximum}"
                | CardinalityAtom(Cardinality.Between(minimum, maximum)) -> Some $"between {minimum}-{maximum}"
                | UniquenessAtom -> Some "uniqueness"
                | _ -> None)

        test <@
            diagnostics =
                [ "presence"
                  "maxLength 20"
                  "email"
                  "pattern ^[^@]+@example.com$"
                  "oneOf 2"
                  "within 1-10"
                  "between 1-3"
                  "uniqueness" ]
        @>

        // Every atom has a stable message key and a default English phrase, derived from one catalogue.
        test <@ atoms |> List.map ConstraintAtom.key |> List.forall (fun key -> key.StartsWith "constraint.") @>
        test <@ atoms |> List.forall (ConstraintAtom.render >> String.IsNullOrWhiteSpace >> not) @>

        // The failure a constraint reports carries the same atom its description carries, so identity never has
        // to be reconstructed from a string.
        let failure = "" |> Constraint.check (Constraint.maxLength 20: Constraint<string>) 
        test <@ failure = Ok() @>

        let rejected = String.replicate 21 "a" |> Constraint.check (Constraint.maxLength 20: Constraint<string>)

        test <@
            rejected =
                Error(Atomic(Expected(CardinalityAtom(Cardinality.Maximum 20), Some(ConstraintValue.Integer 21L))))
        @>

    [<Fact>]
    let ``schema fields inspect existing trusted models through typed getters`` () =
        let nameField = Field.create "name" (fun (model: Customer) -> model.Name) Schema.text
        let ageField = Field.create "age" (fun (model: Customer) -> model.Age) Schema.int
        let customer = { Name = "Ada"; Age = 37 }
        let missingField = Unchecked.defaultof<Field<Customer, string>>

        test <@ Field.externalName nameField |> ExternalFieldName.value = "name" @>
        test <@ Field.order nameField |> FieldOrder.value = 0 @>
        test <@ Field.getValue nameField customer = "Ada" @>
        test <@ Field.getValue ageField customer = 37 @>
        raises<ArgumentNullException> <@ Field.getValue missingField customer |> ignore @>
        raises<ArgumentNullException> <@ Field.order missingField |> ignore @>

    [<Fact>]
    let ``schema fields reject invalid construction arguments`` () =
        Assert.Throws<ArgumentNullException>(fun () ->
            field null (fun (value: Customer) -> value.Name) |> ignore)
        |> ignore

        Assert.Throws<ArgumentException>(fun () ->
            schema<Customer> {
                field " " (fun (value: Customer) -> value.Name)
                field "age" (fun (value: Customer) -> value.Age)
                construct (fun name age -> { Name = name; Age = age })
            }
            |> ignore)
        |> ignore

        Assert.Throws<ArgumentNullException>(fun () ->
            field "name" Unchecked.defaultof<Customer -> string> |> ignore)
        |> ignore

        Assert.Throws<ArgumentNullException>(fun () ->
            schema<Customer> {
                field "name" (fun (value: Customer) -> value.Name) {
                    withSchema Unchecked.defaultof<Schema<string>>
                }
                field "age" (fun (value: Customer) -> value.Age)
                construct (fun name age -> { Name = name; Age = age })
            }
            |> ignore)
        |> ignore

    [<Fact>]
    let ``schema shape builds explicit ordered model schema with value schema constraints`` () =
        let requiredText = Schema.text |> Schema.constrain Constraint.present

        let schema =
            schema<Customer> {
                field "name" (fun (value: Customer) -> value.Name) {
                    withSchema (requiredText |> Schema.constrainAll [ Constraint.present ])
                }
                field "age" (fun (value: Customer) -> value.Age)
                construct (fun name age -> { Name = name; Age = age })
            }

        let constructed =
            match schema.Definition with
            | ModelDefinition model ->
                let values =
                    model.Fields
                    |> List.map (fun field -> field.Getter { Name = "Ada"; Age = 37 })

                test <@ model.Constructor.ArgumentCount = 2 @>
                test <@ model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) = [ "name"; "age" ] @>
                test <@ model.Fields |> List.map (fun field -> FieldOrder.value field.Order) = [ 0; 1 ] @>
                test <@ model.Fields[0].ValueSchema.Rules |> SchemaRule.descriptions |> List.collect ConstraintDescription.atoms = [ PresenceAtom Present; PresenceAtom Present ] @>
                test <@ model.Fields[0].Rules = [] @>
                ConstructorApplication.apply model.Constructor (values |> List.toArray)
            | _ -> failwith "Expected public schema API to create a model definition."

        test <@ constructed = { Name = "Ada"; Age = 37 } @>

    [<Fact>]
    let ``schema shape builds explicit ordered three field model schema through inferred primitive fields`` () =
        let create name age active = { Name = name; Age = age; Active = active }
        let schema =
            schema<CustomerProfile> {
                field "name" (fun (value: CustomerProfile) -> value.Name)
                field "age" (fun (value: CustomerProfile) -> value.Age)
                field "active" (fun (value: CustomerProfile) -> value.Active)
                construct create
            }

        match schema.Definition with
        | ModelDefinition model ->
            let source = { Name = "Ada"; Age = 37; Active = true }
            let values = model.Fields |> List.map (fun field -> field.Getter source)

            test <@ model.Constructor.ArgumentCount = 3 @>
            test <@ model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) = [ "name"; "age"; "active" ] @>
            test <@ model.Fields |> List.map (fun field -> FieldOrder.value field.Order) = [ 0; 1; 2 ] @>
            test <@ values = [ box "Ada"; box 37; box true ] @>
            test <@ ConstructorApplication.apply model.Constructor (values |> List.toArray) = source @>
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    [<Fact>]
    let ``schema recordFor anchors model type for primitive shorthand getters`` () =
        let create name age active = { Name = name; Age = age; Active = active }
        let schema =
            schema<CustomerProfile> {
                field "name" (fun (value: CustomerProfile) -> value.Name)
                field "age" (fun (value: CustomerProfile) -> value.Age)
                field "active" (fun (value: CustomerProfile) -> value.Active)
                construct create
            }

        match schema.Definition with
        | ModelDefinition model ->
            let source = { Name = "Ada"; Age = 37; Active = true }
            let values = model.Fields |> List.map (fun field -> field.Getter source)

            test <@ model.Constructor.ArgumentCount = 3 @>
            test <@ model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) = [ "name"; "age"; "active" ] @>
            test <@ values = [ box "Ada"; box 37; box true ] @>
            test <@ ConstructorApplication.apply model.Constructor (values |> List.toArray) = source @>
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    [<Fact>]
    let ``schema primitive shorthand fields cover the intended end user pipeline vocabulary`` () =
        let create name age balance active birthDate lastSeen id =
            { Name = name
              Age = age
              Balance = balance
              Active = active
              BirthDate = birthDate
              LastSeen = lastSeen
              Id = id }

        let schema =
            schema<PrimitiveProfile> {
                field "name" (fun (value: PrimitiveProfile) -> value.Name)
                field "age" (fun (value: PrimitiveProfile) -> value.Age)
                field "balance" (fun (value: PrimitiveProfile) -> value.Balance)
                field "active" (fun (value: PrimitiveProfile) -> value.Active)
                field "birthDate" (fun (value: PrimitiveProfile) -> value.BirthDate)
                field "lastSeen" (fun (value: PrimitiveProfile) -> value.LastSeen)
                field "id" (fun (value: PrimitiveProfile) -> value.Id)
                construct create
            }

        match schema.Definition with
        | ModelDefinition model ->
            test <@
                model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) =
                    [ "name"; "age"; "balance"; "active"; "birthDate"; "lastSeen"; "id" ]
            @>
            test <@
                model.Fields |> List.map (fun field -> field.ValueSchema.Shape) =
                    [ PrimitiveValueDefinition PrimitiveValueKind.Text
                      PrimitiveValueDefinition PrimitiveValueKind.Int
                      PrimitiveValueDefinition PrimitiveValueKind.Decimal
                      PrimitiveValueDefinition PrimitiveValueKind.Bool
                      PrimitiveValueDefinition PrimitiveValueKind.Date
                      PrimitiveValueDefinition PrimitiveValueKind.DateTime
                      PrimitiveValueDefinition PrimitiveValueKind.Guid ]
            @>
        | PendingDefinition -> failwith "Expected public schema API to create a model definition."

    [<Fact>]
    let ``schema definitions carry trusted constructor application`` () =
        let application = ConstructorApplication.create2 (fun name age -> { Name = name; Age = age })
        let fields =
            [ schemaField "age" 1 (fun (customer: Customer) -> customer.Age) |> schemaFieldDescriptor
              schemaField "name" 0 (fun (customer: Customer) -> customer.Name) |> schemaFieldDescriptor ]

        let definition = ModelSchemaDefinition.create application fields
        let schema = Schema<Customer>(ModelDefinition definition)

        let constructed =
            match schema.Definition with
            | ModelDefinition model ->
                let constructor = model.Constructor
                test <@ constructor.ArgumentCount = 2 @>
                test <@ model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) = [ "name"; "age" ] @>
                test <@ model.Fields |> List.map (fun field -> FieldOrder.value field.Order) = [ 0; 1 ] @>
                ConstructorApplication.apply constructor [| box "Ada"; box 37 |]
            | PendingDefinition -> failwith "Expected schema definition to carry a constructor application."

        test <@ constructed = { Name = "Ada"; Age = 37 } @>
        raises<ArgumentException> <@ ConstructorApplication.apply application [| box "Ada" |] |> ignore @>
        raises<ArgumentNullException> <@ ConstructorApplication.apply application null |> ignore @>

    [<Fact>]
    let ``model schema definitions sort fields by explicit field order`` () =
        let application = ConstructorApplication.create3 (fun name age active -> { Name = name; Age = age; Active = active })
        let active = schemaField "active" 2 (fun (model: CustomerProfile) -> model.Active) |> schemaFieldDescriptor
        let age = schemaField "age" 1 (fun (model: CustomerProfile) -> model.Age) |> schemaFieldDescriptor
        let name = schemaField "name" 0 (fun (model: CustomerProfile) -> model.Name) |> schemaFieldDescriptor

        let definition = ModelSchemaDefinition.create application [ active; name; age ]
        let values =
            definition.Fields
            |> List.map (fun field -> field.Getter { Name = "Ada"; Age = 37; Active = true })

        test <@ definition.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) = [ "name"; "age"; "active" ] @>
        test <@ definition.Fields |> List.map (fun field -> FieldOrder.value field.Order) = [ 0; 1; 2 ] @>
        test <@ values = [ box "Ada"; box 37; box true ] @>
        test <@ ConstructorApplication.apply definition.Constructor (values |> List.toArray) = { Name = "Ada"; Age = 37; Active = true } @>

    [<Fact>]
    let ``model schema definitions reject ambiguous field order`` () =
        let application = ConstructorApplication.create2 (fun name age -> { Name = name; Age = age })
        let duplicateZero =
            [ schemaField "name" 0 (fun (customer: Customer) -> customer.Name) |> schemaFieldDescriptor
              schemaField "age" 0 (fun (customer: Customer) -> customer.Age) |> schemaFieldDescriptor ]
        let gap =
            [ schemaField "name" 0 (fun (customer: Customer) -> customer.Name) |> schemaFieldDescriptor
              schemaField "age" 2 (fun (customer: Customer) -> customer.Age) |> schemaFieldDescriptor ]
        let tooFew =
            [ schemaField "name" 0 (fun (customer: Customer) -> customer.Name) |> schemaFieldDescriptor ]

        raises<ArgumentException> <@ ModelSchemaDefinition.create application duplicateZero |> ignore @>
        raises<ArgumentException> <@ ModelSchemaDefinition.create application gap |> ignore @>
        raises<ArgumentException> <@ ModelSchemaDefinition.create application tooFew |> ignore @>

    [<Fact>]
    let ``constructor applications support zero one and three trusted arguments`` () =
        let constant = ConstructorApplication.create0 (fun () -> { Name = "System"; Age = 0 })
        let named = ConstructorApplication.create1 (fun name -> { Name = name; Age = 0 })
        let combined = ConstructorApplication.create3 (fun first last age -> { Name = first + " " + last; Age = age })

        test <@ ConstructorApplication.apply constant [||] = { Name = "System"; Age = 0 } @>
        test <@ ConstructorApplication.apply named [| box "Ada" |] = { Name = "Ada"; Age = 0 } @>
        test <@ ConstructorApplication.apply combined [| box "Ada"; box "Lovelace"; box 37 |] = { Name = "Ada Lovelace"; Age = 37 } @>

    [<Fact>]
    let ``external field names preserve exact boundary names and reject unusable names`` () =
        let name = ExternalFieldName.create " customer_id "

        test <@ name.Value = " customer_id " @>
        test <@ ExternalFieldName.value name = " customer_id " @>
        test <@ string name = " customer_id " @>
        raises<ArgumentNullException> <@ ExternalFieldName.create null |> ignore @>
        raises<ArgumentException> <@ ExternalFieldName.create "" |> ignore @>
        raises<ArgumentException> <@ ExternalFieldName.create "   " |> ignore @>
        raises<ArgumentNullException> <@ ExternalFieldName.value null |> ignore @>

    [<Fact>]
    let ``field order preserves zero based positions and rejects negative positions`` () =
        let first = FieldOrder.create 0
        let second = FieldOrder.create 1

        test <@ FieldOrder.value first = 0 @>
        test <@ FieldOrder.value second = 1 @>
        test <@ string second = "1" @>
        raises<ArgumentException> <@ FieldOrder.create -1 |> ignore @>

    [<Fact>]
    let ``the constraint package publishes one value-rule vocabulary and no Check surface`` () =
        assertConstraintValueShape<string> ()
        assertConstraintValueShape<int> ()

        let constraintAssembly = typeof<Violation>.Assembly
        test <@ constraintAssembly.GetName().Name = "Axial.Constraint" @>

        let publicTypeNames =
            constraintAssembly.GetTypes()
            |> Array.filter _.IsPublic
            |> Array.map _.FullName
            |> Set.ofArray

        // Removed outright, with no compatibility alias: a second nearly identical catalogue is the problem this
        // design exists to remove.
        [ "Axial.Constraint.Check"
          "Axial.Constraint.CheckModule"
          "Axial.Constraint.CheckFailure"
          "Axial.Constraint.CheckFailureResources"
          "Axial.Constraint.CheckLengthExpectation"
          "Axial.Constraint.CheckRangeExpectation"
          "Axial.Constraint.CheckDSL"
          "Axial.Constraint.Predicate"
          "Axial.Constraint.PredicateModule"
          "Axial.Constraint.PredicateExtensions"
          "Axial.Constraint.ConstraintMetadata"
          "Axial.Constraint.ConstraintArgument"
          "Axial.Constraint.ConstraintDetails" ]
        |> List.iter (fun removed -> test <@ not (publicTypeNames |> Set.contains removed) @>)

        test <@ not (AppDomain.CurrentDomain.GetAssemblies() |> Array.exists (fun assembly -> assembly.GetName().Name = "Axial.Check")) @>

        let constraintMembers =
            moduleTypeFromAssembly "Axial.Constraint" "Axial.Constraint.Constraint"
            |> publicStaticMemberNames

        constraintMembers
        |> assertContainsAll
            [ // execution
              "test"
              "check"
              "guard"
              "inspect"
              // composition
              "all"
              "any"
              "optional"
              "notWith"
              "custom"
              "customWith"
              "contramap"
              "describe"
              // catalogue
              "present"
              "blank"
              "length"
              "minLength"
              "maxLength"
              "lengthBetween"
              "email"
              "trimmed"
              "numeric"
              "alphanumeric"
              "pattern"
              "oneOf"
              "contains"
              "distinct"
              "equalTo"
              "notEqualTo"
              "greaterThan"
              "lessThan"
              "atLeast"
              "atMost"
              "between"
              "multipleOf"
              "finite"
              "finite32" ]

        // `not` is opaque-only and `validate` belongs to the larger process, not to a constraint.
        constraintMembers |> assertContainsNone [ "not"; "validate"; "define"; "code"; "metadata"; "arguments"; "fromCheck" ]

        // `Violation` names the union; the module carries the compiled suffix.
        let violationMembers =
            moduleTypeFromAssembly "Axial.Constraint" "Axial.Constraint.ViolationModule"
            |> publicStaticMemberNames

        violationMembers
        |> assertContainsAll
            [ "tryExpectation"; "tryActual"; "tryDescription"; "children"; "flatten"; "render"; "toMessageTree"; "conjoin"; "alternatives" ]

        // A string identity never appears on a violation; keys exist only as a rendering projection.
        violationMembers |> assertContainsNone [ "code"; "kind"; "fold"; "describe"; "generic" ]

        typeof<Violation> |> publicUnionCaseNames |> (=) (set [ "Atomic"; "All"; "Any" ]) |> (fun equal -> test <@ equal @>)
        typeof<AtomicViolation> |> publicUnionCaseNames |> (=) (set [ "Expected"; "Described"; "UnsupportedOperand" ]) |> (fun equal -> test <@ equal @>)

        // A violation is plain data: no closure and no description tree is reachable from it.
        let forbiddenViolationTypeNames = [ "Axial.Constraint.Constraint`1"; "Axial.Constraint.ConstraintDescription" ]

        let reachableFieldTypes =
            [ typeof<Violation>; typeof<AtomicViolation> ]
            |> List.collect (fun unionType ->
                FSharpType.GetUnionCases(unionType, BindingFlags.Public)
                |> Array.collect (fun caseInfo -> caseInfo.GetFields() |> Array.map _.PropertyType)
                |> List.ofArray)

        let forbidden =
            reachableFieldTypes
            |> List.filter (fun fieldType ->
                let fullName = fieldType.FullName

                (not (isNull fullName) && List.contains fullName forbiddenViolationTypeNames)
                || (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() = typedefof<FSharpFunc<_, _>>))

        test <@ List.isEmpty forbidden @>

        // The DSL is optional vocabulary over the same values, with the documented collision omissions.
        let dslMembers =
            moduleTypeFromAssembly "Axial.Constraint" "Axial.Constraint.ConstraintDSL"
            |> publicStaticMemberNames
            |> Set.filter (fun name -> not (name.Contains "$"))

        dslMembers
        |> assertContainsAll [ "present"; "blank"; "optional"; "minLength"; "notWith"; "test"; "guard"; "orError"; "mapError" ]

        dslMembers |> assertContainsNone [ "check"; "all"; "any"; "length"; "between"; "contains"; "distinct" ]

        // String, Option, ValueOption, Nullable, Result, and sequence predicates are exposed as extension
        // members directly on those types (see PredicateExtensions), not as PredicateModule submodules.

        let resultMembers =
            moduleTypeFromAssembly "Axial.Result" "Axial.Result.Result"
            |> publicStaticMemberNames

        resultMembers
        |> assertContainsAll
            [ "ok"
              "error"
              "map"
              "bind"
              "mapError"
              "orElse"
              "orElseWith"
              "requireTrue"
              "okIf"
              "failIf"
              "orError"
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
              "headOr" ]

        let parseMembers =
            moduleTypeFromAssembly "Axial.Parse" "Axial.Parse.Parse"
            |> publicStaticMemberNames

        test <@ typeof<ParseError>.Assembly.GetName().Name = "Axial.Parse" @>
        test <@ typeof<Refinement<string, NonBlankString>>.Assembly.GetName().Name = "Axial.Refined" @>
        assertModuleAbsentFromAssembly "Axial.Result" "Axial.Result.Parse"
        assertModuleAbsentFromAssembly "Axial.Refined" "Axial.Refined.Parse"
        test <@ not (referencedAssemblyNames (Assembly.Load "Axial.Refined") |> Set.contains "Axial.Parse") @>

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
              "optional"
              "optionalOr"
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
        |> assertContainsAll [ "nonBlankString"; "nonEmptyList"; "unitInterval"; "finiteFloat"; "interval" ]
        refineMembers |> assertContainsNone [ "from"; "withCheck"; "withChecks" ]

        // Concepts that carry no invariant past the boundary are constraints, not types.
        refineMembers
        |> assertContainsNone
            [ "trimmedString"; "slug"; "boundedString"; "boundedList"; "boundedArray"
              "negativeInt"; "nonPositiveInt"; "dateTimeOffsetRange"; "dateOnlyRange"
              "exactlyOne"; "atMostOne"
              // Numeric ranges are constraints, not types.
              "positiveInt"; "nonNegativeInt"; "nonZeroInt"; "positiveDecimal" ]

        moduleTypeFromAssembly "Axial.Refined" "Axial.Refined.Refinement"
        |> publicStaticMemberNames
        // One constraint, one constructor: several rules compose with `Constraint.all` before the refinement is
        // defined, and an arbitrary predicate is `Constraint.custom`.
        |> assertContainsAll [ "define"; "create"; "underlying"; "constraint'" ]

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

        moduleType typeof<Ref<int>> "Axial.Flow.Ref"
        |> publicStaticMemberNames
        |> assertContainsAll [ "make"; "get"; "set"; "update"; "modify" ]

    [<Fact>]
    let ``schedule stream and STM modules keep expected public shape`` () =
        moduleType typeof<Schedule<unit, unit, unit>> "Axial.Flow.Schedule"
        |> publicStaticMemberNames
        |> assertContainsAll [ "recurs"; "spaced"; "exponential"; "jittered"; "jitteredWith"; "retry"; "repeat" ]

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
        moduleType typeof<FlowHostedService<unit, string>> "Axial.Flow.Hosting.Hosting"
        |> publicStaticMemberNames
        |> assertContainsAll [ "addApp"; "addAppWith" ]

        moduleType typeof<FlowHostedService<unit, string>> "Axial.Flow.Hosting.MicrosoftLogging"
        |> publicStaticMemberNames
        |> assertContainsAll [ "create"; "fromFactory"; "layer" ]

        moduleType typeof<FlowHostedService<unit, string>> "Axial.Flow.Hosting.DotNetApp"
        |> publicStaticMemberNames
        |> assertContainsAll [ "run"; "exitCode" ]

        moduleType typeof<AppHandle<string, unit>> "Axial.Flow.App"
        |> publicStaticMemberNames
        |> assertContainsAll [ "start"; "startWithCancellation"; "run"; "runWithCancellation" ]

        moduleTypeFromAssembly "Axial.Flow.Telemetry" "Axial.Flow.Telemetry.Activity"
        |> publicStaticMemberNames
        |> assertContainsAll [ "source"; "trace" ]

    [<Fact>]
    let ``service modules keep expected public shape`` () =
        moduleType typeof<Axial.Flow.Console.IConsole> "Axial.Flow.Console.Console"
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "read"; "readLine"; "readKey"; "write"; "writeLine"; "writeError"; "writeErrorLine"
              "openStandardInput"; "openStandardOutput"; "openStandardError"; "inputEncoding"; "outputEncoding"
              "isInputRedirected"; "isOutputRedirected"; "isErrorRedirected"; "clear"; "beep"; "resetColor"
              "foregroundColor"; "backgroundColor"; "cursorPosition"; "setCursorPosition"; "title"; "layer"; "live" ]

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

        moduleType typeof<Axial.Flow.HttpClient.IHttp> "Axial.Flow.HttpClient.Http"
        |> publicStaticMemberNames
        |> assertContainsAll [ "getString"; "layer"; "live" ]

        moduleType typeof<Axial.Flow.Process.IProcess> "Axial.Flow.Process.Process"
        |> publicStaticMemberNames
        |> assertContainsAll [ "run"; "capture"; "stream"; "timeout"; "layer"; "live" ]

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

    [<Fact>]
    let ``option and valueoption implicit binding requires unit workflow errors`` () =
        let flowAssemblyPath = typeof<FlowBuilder>.Assembly.Location
        let resultAssemblyPath = typeof<ResultBuilder>.Assembly.Location

        let flowProbe =
            $"""
#r @"{flowAssemblyPath}"
#r @"{resultAssemblyPath}"
open Axial.Flow
open Axial.Result

let probe : Flow<unit, string, int> =
    flow {{
        let! value = Some 42
        return value
    }}
"""

        let asyncProbe =
            $"""
#r @"{flowAssemblyPath}"
#r @"{resultAssemblyPath}"
open Axial.Flow
open Axial.Result

let probe : Flow<unit, string, int> =
    flow {{
        let! value = ValueSome 42
        return value
    }}
"""

        let flowExitCode, flowOutput = runFsiScript flowProbe
        let asyncExitCode, asyncOutput = runFsiScript asyncProbe

        test <@ flowExitCode <> 0 @>
        test <@ flowOutput.Contains("Flow<unit,unit,int>") @>
        test <@ asyncExitCode <> 0 @>
        test <@ asyncOutput.Contains("Flow<unit,unit,int>") @>

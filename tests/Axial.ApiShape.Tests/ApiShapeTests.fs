namespace Axial.Tests

open System
open System.Diagnostics
open System.IO
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
              ValueSchema = Value.text.Definition
              Constraints = [] }

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
                arguments[0] = typeof<unit> && arguments[1] = typeof<CheckFailure list>
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

    let private assertCheckAliasShape<'value> () =
        let checkType = typeof<Check<'value>>

        test <@ checkType.IsGenericType @>
        test <@ checkType.GetGenericTypeDefinition() = typedefof<FSharpFunc<_, _>> @>

        let arguments = checkType.GetGenericArguments()
        test <@ arguments[0] = typeof<'value> @>
        test <@ arguments[1] = typeof<Result<unit, CheckFailure list>> @>

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
    let ``validation modules and builders keep expected public shape`` () =
        moduleType typeof<Validation<int, string>> "Axial.Validation.Validation"
        |> publicStaticMemberNames
        |> fun validationMembers ->
            validationMembers
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

            test <@ validationMembers |> Set.contains "ofResult" |> not @>

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
    let ``schema inspection and input interpreter modules expose the expected surface`` () =
        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.Inspect"
        |> publicStaticMemberNames
        |> assertContainsAll [ "model"; "value"; "field" ]

        moduleTypeFromAssembly "Axial.Validation.Schema" "Axial.Validation.Schema.Input"
        |> publicStaticMemberNames
        |> assertContainsAll [ "parse"; "parseWith"; "constructorErrorAt" ]

        moduleTypeFromAssembly "Axial.Validation.Schema" "Axial.Validation.Schema.RawInputModule"
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "ofMap"
              "ofNameValues"
              "ofNameValueCollection"
              "ofCliArgs"
              "ofJsonLikeValue"
              "ofJsonElement"
              "ofJsonDocument"
              "ofConfiguration"
              "redisplay"
              "redisplayAt"
              "redisplayPath" ]

        moduleTypeFromAssembly "Axial.Validation.Schema" "Axial.Validation.Schema.ParsedInput"
        |> publicStaticMemberNames
        |> assertContainsAll [ "mapErrors" ]

    [<Fact>]
    let ``codec compiles json codecs from schemas without extra package coupling`` () =
        moduleTypeFromAssembly "Axial.Codec" "Axial.Codec.Json"
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "compile"; "serialize"; "serializeBytes"; "deserialize"; "deserializeBytes"; "tryDeserialize" ]

        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.JsonSchema"
        |> publicStaticMemberNames
        |> assertContainsAll [ "generate"; "generateValue" ]

        referencedAssemblyNames (Assembly.Load "Axial.Codec")
        |> assertContainsNone
            [ "Axial.Flow"; "Axial.ErrorHandling"; "Axial.Refined"; "Axial.Validation"; "Axial.Validation.Schema" ]

    [<Fact>]
    let ``leaf packages stay independent of each other`` () =
        let leafPackages =
            [ "Axial.Flow"; "Axial.ErrorHandling"; "Axial.Refined"; "Axial.Schema"; "Axial.Validation" ]

        // Axial.Refined deliberately exposes Check in its signatures, so it may reference Axial.ErrorHandling.
        let allowedReferences = [ "Axial.Refined", "Axial.ErrorHandling" ]

        for package in leafPackages do
            let forbidden =
                leafPackages
                |> List.filter (fun other ->
                    other <> package && not (List.contains (package, other) allowedReferences))

            let references = referencedAssemblyNames (Assembly.Load package)

            references |> assertContainsNone forbidden
            references |> assertContainsNone [ "Axial"; "Axial.Validation.Schema" ]

    [<Fact>]
    let ``policy lives in flow without schema refined or validation dependencies`` () =
        let flowAssembly = Assembly.Load "Axial.Flow"

        test <@ flowAssembly.GetName().Name = "Axial.Flow" @>

        referencedAssemblyNames flowAssembly
        |> assertContainsNone [ "Axial.Schema"; "Axial.Refined"; "Axial.Validation"; "Axial.Validation.Schema" ]

        moduleTypeFromAssembly "Axial.Flow" "Axial.Flow.PolicyModule"
        |> publicStaticMemberNames
        |> assertContainsAll [ "lift"; "withError"; "context"; "pass"; "compose"; "optional" ]

    [<Fact>]
    let ``schema validation interpreters stay out of core validation`` () =
        let validationAssembly = typeof<Validation<int, string>>.Assembly
        let validationReferences = referencedAssemblyNames validationAssembly
        let schemaValidationAssembly = Assembly.Load "Axial.Validation.Schema"
        let schemaValidationReferences = referencedAssemblyNames schemaValidationAssembly

        test <@ validationAssembly.GetName().Name = "Axial.Validation" @>

        validationReferences
        |> assertContainsNone [ "Axial.Schema"; "Axial.Validation.Schema"; "Axial.ErrorHandling"; "Axial.Refined"; "Axial.Flow" ]

        assertModuleAbsentFromAssembly "Axial.Validation" "Axial.Validation.SchemaValidation"
        assertModuleAbsentFromAssembly "Axial.Validation" "Axial.Validation.SchemaConstraintCheck"
        assertModuleAbsentFromAssembly "Axial.Validation" "Axial.Validation.ValueSchemaCheck"
        assertModuleAbsentFromAssembly "Axial.Validation" "Axial.Validation.Input"

        test <@ schemaValidationAssembly.GetName().Name = "Axial.Validation.Schema" @>

        schemaValidationReferences
        |> assertContainsAll [ "Axial.Schema"; "Axial.Validation"; "Axial.ErrorHandling"; "Axial.Refined" ]

        moduleTypeFromAssembly "Axial.Validation.Schema" "Axial.Validation.Schema.SchemaValidation"
        |> publicStaticMemberNames
        |> assertContainsAll [ "packageName" ]

        moduleTypeFromAssembly "Axial.Validation.Schema" "Axial.Validation.Schema.SchemaConstraintCheck"
        |> publicStaticMemberNames
        |> assertContainsAll [ "tryText"; "text"; "tryOrdered"; "ordered"; "trySequence"; "sequence" ]

        moduleTypeFromAssembly "Axial.Validation.Schema" "Axial.Validation.Schema.ValueSchemaCheck"
        |> publicStaticMemberNames
        |> assertContainsAll [ "fromUnderlying"; "text"; "ordered" ]

        moduleTypeFromAssembly "Axial.Validation.Schema" "Axial.Validation.Schema.Rules"
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "empty"
              "fail"
              "custom"
              "failCustom"
              "failAt"
              "failCustomAt"
              "create"
              "ofSeq"
              "ofList"
              "at"
              "name"
              "key"
              "index"
              "append"
              "concat" ]

    [<Fact>]
    let ``schema contextual rules are reserved for validation schema package`` () =
        let forbiddenCoreRuleModules =
            [ "Rules"
              "SchemaRules"
              "ContextualRules" ]

        let forbiddenCoreRuleTypes =
            [ "RuleSet`2"
              "RuleFailure"
              "RuleBuilder`2" ]

        for moduleName in forbiddenCoreRuleModules do
            assertModuleAbsentFromAssembly "Axial.Schema" $"Axial.Schema.{moduleName}"
            assertModuleAbsentFromAssembly "Axial.Validation" $"Axial.Validation.{moduleName}"

        for typeName in forbiddenCoreRuleTypes do
            assertTypeAbsentFromAssembly "Axial.Schema" $"Axial.Schema.{typeName}"
            assertTypeAbsentFromAssembly "Axial.Validation" $"Axial.Validation.{typeName}"

        let ruleSetType =
            assertTypePresentInAssembly "Axial.Validation.Schema" "Axial.Validation.Schema.RuleSet`2"

        let publicRuleSetConstructors =
            ruleSetType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)

        test <@ ruleSetType.IsGenericTypeDefinition @>
        test <@ ruleSetType.GetGenericArguments().Length = 2 @>
        test <@ publicRuleSetConstructors.Length = 0 @>

    [<Fact>]
    let ``schema types start as independent leaf package`` () =
        let schemaType = typedefof<Schema<_>>
        let valueSchemaType = typedefof<ValueSchema<_>>
        let fieldType = typedefof<Field<_, _>>
        let primitiveValueKindType = typeof<PrimitiveValueKind>
        let schemaConstraintMetadataType = typeof<SchemaConstraintMetadata>
        let schemaConstraintType = typeof<SchemaConstraint>
        let externalFieldNameType = typeof<ExternalFieldName>
        let fieldOrderType = typeof<FieldOrder>
        let schemaModule = moduleType schemaType "Axial.Schema.Schema"
        let fieldModule = moduleType fieldType "Axial.Schema.Field"
        let valueModule = moduleType valueSchemaType "Axial.Schema.Value"
        let schemaConstraintModule = moduleType schemaConstraintType "Axial.Schema.SchemaConstraintModule"
        let schemaAssembly = schemaType.Assembly
        let references = referencedAssemblyNames schemaAssembly
        let publicConstructors =
            schemaType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)
        let publicValueConstructors =
            valueSchemaType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)
        let publicFieldConstructors =
            fieldType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)
        let publicSchemaConstraintConstructors =
            schemaConstraintType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)
        let publicExternalFieldNameConstructors =
            externalFieldNameType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)
        let fieldDefinitionType =
            schemaAssembly.GetType("Axial.Schema.FieldDefinition`2", true)
        let schemaFieldMethods =
            publicStaticMethods schemaModule
            |> Array.filter (fun methodInfo -> methodInfo.Name = "field")
        let fieldCreateMethods =
            publicStaticMethods fieldModule
            |> Array.filter (fun methodInfo -> methodInfo.Name = "create")
        let schemaBuilderType = typedefof<SchemaBuilder<_, _, _, _>>
        let fieldTypeDefinition = typedefof<Field<_, _>>
        let schemaFieldMethod = schemaFieldMethods |> Array.tryExactlyOne
        let fieldCreateMethod = fieldCreateMethods |> Array.tryExactlyOne
        let schemaFieldParameterCount =
            schemaFieldMethod
            |> Option.map (fun methodInfo -> methodInfo.GetParameters().Length)
            |> Option.defaultValue -1
        let schemaFieldBuilderParameterType =
            schemaFieldMethod
            |> Option.bind (fun methodInfo ->
                let parameters = methodInfo.GetParameters()

                if parameters.Length = 4 then
                    Some(parameters[3].ParameterType.GetGenericTypeDefinition())
                else
                    None)
            |> Option.defaultValue typeof<obj>
        let schemaFieldReturnType =
            schemaFieldMethod
            |> Option.map (fun methodInfo -> methodInfo.ReturnType.GetGenericTypeDefinition())
            |> Option.defaultValue typeof<obj>
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
        test <@ publicSchemaConstraintConstructors.Length = 0 @>
        test <@ publicExternalFieldNameConstructors.Length = 0 @>
        schemaModule
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "record"
              "recordFor"
              "field"
              "fieldWith"
              "text"
              "int"
              "decimal"
              "bool"
              "date"
              "dateTime"
              "guid"
              "build"
              "buildResult"
              "buildResultWith" ]
        fieldModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "create"; "externalName"; "order"; "getValue"; "constraints"; "withConstraint"; "withConstraints" ]
        test <@ schemaFieldMethods.Length = 1 @>
        test <@ schemaFieldParameterCount = 4 @>
        test <@ schemaFieldBuilderParameterType = schemaBuilderType @>
        test <@ schemaFieldReturnType = schemaBuilderType @>
        test <@ fieldCreateMethods.Length = 1 @>
        test <@ fieldCreateParameterCount = 3 @>
        test <@ fieldCreateReturnType = fieldTypeDefinition @>
        valueModule
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
              "refined"
              "isRefined"
              "underlyingPrimitiveKind"
              "inspectUnderlying"
              "rawConstraints"
              "constraints"
              "allConstraints"
              "withConstraint"
              "withConstraints" ]
        schemaConstraintModule
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "create"
              "createWithArguments"
              "required"
              "optional"
              "minLength"
              "maxLength"
              "lengthBetween"
              "email"
              "trimmed"
              "pattern"
              "oneOf"
              "notEqualTo"
              "between"
              "greaterThan"
              "lessThan"
              "atLeast"
              "atMost"
              "count"
              "minCount"
              "maxCount"
              "countBetween"
              "distinct"
              "code"
              "metadata"
              "arguments"
              "tryFindArgument" ]
        primitiveValueKindType
        |> publicUnionCaseNames
        |> assertContainsAll [ "Text"; "Int"; "Decimal"; "Bool"; "Date"; "DateTime"; "Guid" ]
        schemaConstraintMetadataType
        |> publicUnionCaseNames
        |> assertContainsAll
            [ "Required"
              "Optional"
              "MinLength"
              "MaxLength"
              "LengthBetween"
              "Email"
              "Pattern"
              "OneOf"
              "Between"
              "GreaterThan"
              "LessThan"
              "AtLeast"
              "AtMost"
              "Count"
              "MinCount"
              "MaxCount"
              "CountBetween"
              "Distinct"
              "Custom" ]
        test <@ valueSchemaType.Assembly = schemaAssembly @>
        test <@ fieldType.Assembly = schemaAssembly @>
        test <@ primitiveValueKindType.Assembly = schemaAssembly @>
        test <@ schemaConstraintMetadataType.Assembly = schemaAssembly @>
        test <@ schemaConstraintType.Assembly = schemaAssembly @>
        test <@ externalFieldNameType.Assembly = schemaAssembly @>
        test <@ fieldOrderType.Assembly = schemaAssembly @>
        test <@ externalNameProperty.PropertyType = externalFieldNameType @>
        test <@ orderProperty.PropertyType = fieldOrderType @>
        test <@ getterProperty.PropertyType.GetGenericTypeDefinition() = typedefof<FSharpFunc<_, _>> @>
        test <@ schemaAssembly.GetName().Name = "Axial.Schema" @>
        references
        |> assertContainsNone [ "Axial.Flow"; "Axial.ErrorHandling"; "Axial.Refined"; "Axial.Validation" ]

    [<Fact>]
    let ``primitive value schemas carry typed intrinsic metadata`` () =
        let valueSchemas =
            [ Value.primitiveKind Value.text
              Value.primitiveKind Value.int
              Value.primitiveKind Value.decimal
              Value.primitiveKind Value.bool
              Value.primitiveKind Value.date
              Value.primitiveKind Value.dateTime
              Value.primitiveKind Value.guid ]

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
        test <@ Value.text.Definition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Text @>
        test <@ Value.int.Definition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Int @>
        test <@ Value.decimal.Definition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Decimal @>
        test <@ Value.bool.Definition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Bool @>
        test <@ Value.date.Definition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Date @>
        test <@ Value.dateTime.Definition.Shape = PrimitiveValueDefinition PrimitiveValueKind.DateTime @>
        test <@ Value.guid.Definition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Guid @>
        test <@ Value.constraints Value.text = [] @>
        raises<ArgumentNullException> <@ Value.primitiveKind Unchecked.defaultof<ValueSchema<string>> |> ignore @>

    [<Fact>]
    let ``refined value schemas require both construction and inspection functions`` () =
        let valueModule = moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.Value"

        let refinedOverloads =
            publicStaticMethods valueModule
            |> Array.filter (fun methodInfo -> methodInfo.Name.Equals("refined", StringComparison.OrdinalIgnoreCase))

        test <@ refinedOverloads.Length = 1 @>

        let refined = refinedOverloads[0]
        let parameters = refined.GetParameters()
        let parameterNames = parameters |> Array.map _.Name

        test <@ parameterNames = [| "construct"; "inspect"; "raw" |] @>

        let constructArguments = parameters[0].ParameterType.GetGenericArguments()
        let inspectArguments = parameters[1].ParameterType.GetGenericArguments()
        let constructIsFunction = parameters[0].ParameterType.GetGenericTypeDefinition() = typedefof<FSharpFunc<_, _>>
        let inspectIsFunction = parameters[1].ParameterType.GetGenericTypeDefinition() = typedefof<FSharpFunc<_, _>>
        let rawIsValueSchema = parameters[2].ParameterType.GetGenericTypeDefinition() = typedefof<ValueSchema<_>>
        let returnsValueSchema = refined.ReturnType.GetGenericTypeDefinition() = typedefof<ValueSchema<_>>
        let inspectReversesConstruct = inspectArguments = Array.rev constructArguments
        let rawMatchesConstructInput = parameters[2].ParameterType.GetGenericArguments()[0] = constructArguments[0]
        let returnMatchesConstructOutput = refined.ReturnType.GetGenericArguments()[0] = constructArguments[1]

        test <@ constructIsFunction @>
        test <@ inspectIsFunction @>
        test <@ rawIsValueSchema @>
        test <@ returnsValueSchema @>
        test <@ inspectReversesConstruct @>
        test <@ rawMatchesConstructInput @>
        test <@ returnMatchesConstructOutput @>

    [<Fact>]
    let ``schema constraints are inspectable metadata independent of executable checks`` () =
        let required = SchemaConstraint.required
        let maxLength = SchemaConstraint.maxLength 20
        let text = Value.text |> Value.withConstraints [ required; maxLength ]
        let field =
            schemaField "name" 0 (fun (model: Customer) -> model.Name)
            |> Field.withConstraint required
            |> Field.withConstraint maxLength
        let descriptor = field |> schemaFieldDescriptor

        test <@ SchemaConstraint.code required = "required" @>
        test <@ SchemaConstraint.metadata required = SchemaConstraintMetadata.Required @>
        test <@ SchemaConstraint.metadata maxLength = SchemaConstraintMetadata.MaxLength 20 @>
        test <@ string required = "required" @>
        test <@ SchemaConstraint.arguments required |> Seq.isEmpty @>
        test <@ SchemaConstraint.tryFindArgument "maximum" maxLength = Some(box 20) @>
        test <@ Value.constraints text |> List.map SchemaConstraint.code = [ "required"; "maxLength" ] @>
        test <@ Field.constraints field |> List.map SchemaConstraint.code = [ "required"; "maxLength" ] @>
        test <@ descriptor.Constraints |> List.map SchemaConstraint.code = [ "required"; "maxLength" ] @>
        test <@ descriptor.ValueSchema.Constraints = [] @>
        test <@ text.Definition.Constraints |> List.map SchemaConstraint.code = [ "required"; "maxLength" ] @>
        raises<ArgumentException> <@ SchemaConstraint.create "" |> ignore @>
        raises<ArgumentException> <@ SchemaConstraint.createWithArguments "maxLength" [ "", box 20 ] |> ignore @>
        raises<ArgumentException> <@ SchemaConstraint.createWithArguments "maxLength" [ "maximum", box 20; "maximum", box 30 ] |> ignore @>
        raises<ArgumentNullException> <@ Value.withConstraint null Value.text |> ignore @>
        raises<ArgumentNullException> <@ Field.constraints Unchecked.defaultof<Field<Customer, string>> |> ignore @>

    [<Fact>]
    let ``named schema constraints expose stable codes and structured arguments`` () =
        let codes =
            [ SchemaConstraint.required
              SchemaConstraint.optional
              SchemaConstraint.minLength 2
              SchemaConstraint.maxLength 20
              SchemaConstraint.lengthBetween 2 20
              SchemaConstraint.email
              SchemaConstraint.trimmed
              SchemaConstraint.pattern "^[a-z]+$"
              SchemaConstraint.oneOf [ "draft"; "published" ]
              SchemaConstraint.notEqualTo "archived"
              SchemaConstraint.between 1 10
              SchemaConstraint.greaterThan 0
              SchemaConstraint.lessThan 100
              SchemaConstraint.atLeast 1
              SchemaConstraint.atMost 10
              SchemaConstraint.count 2
              SchemaConstraint.minCount 1
              SchemaConstraint.maxCount 5
              SchemaConstraint.countBetween 1 5
              SchemaConstraint.distinct ]
            |> List.map SchemaConstraint.code

        let length = SchemaConstraint.lengthBetween 2 20
        let pattern = SchemaConstraint.pattern "^[a-z]+$"
        let choices = SchemaConstraint.oneOf [ "draft"; "published" ]
        let range = SchemaConstraint.between 1.5m 3.5m
        let count = SchemaConstraint.countBetween 1 5

        test <@
            codes =
                [ "required"
                  "optional"
                  "minLength"
                  "maxLength"
                  "lengthBetween"
                  "email"
                  "trimmed"
                  "pattern"
                  "oneOf"
                  "notEqualTo"
                  "between"
                  "greaterThan"
                  "lessThan"
                  "atLeast"
                  "atMost"
                  "count"
                  "minCount"
                  "maxCount"
                  "countBetween"
                  "distinct" ]
        @>
        test <@ SchemaConstraint.tryFindArgument "minimum" length = Some(box 2) @>
        test <@ SchemaConstraint.tryFindArgument "maximum" length = Some(box 20) @>
        test <@ SchemaConstraint.tryFindArgument "pattern" pattern = Some(box "^[a-z]+$") @>
        test <@ SchemaConstraint.tryFindArgument "choices" choices |> Option.map unbox<string array> = Some [| "draft"; "published" |] @>
        test <@ SchemaConstraint.tryFindArgument "minimum" range = Some(box 1.5m) @>
        test <@ SchemaConstraint.tryFindArgument "maximum" range = Some(box 3.5m) @>
        test <@ SchemaConstraint.tryFindArgument "minimum" count = Some(box 1) @>
        test <@ SchemaConstraint.tryFindArgument "maximum" count = Some(box 5) @>
        test <@
            SchemaConstraint.metadata (SchemaConstraint.create "tenantOnly") = SchemaConstraintMetadata.Custom "tenantOnly"
        @>
        test <@
            SchemaConstraint.metadata (SchemaConstraint.createWithArguments "tenantOnly" [ "tenant", box "north" ]) =
                SchemaConstraintMetadata.Custom "tenantOnly"
        @>
        raises<ArgumentOutOfRangeException> <@ SchemaConstraint.minLength -1 |> ignore @>
        raises<ArgumentOutOfRangeException> <@ SchemaConstraint.count -1 |> ignore @>
        raises<ArgumentException> <@ SchemaConstraint.lengthBetween 5 2 |> ignore @>
        raises<ArgumentException> <@ SchemaConstraint.countBetween 5 2 |> ignore @>
        raises<ArgumentException> <@ SchemaConstraint.between 10 1 |> ignore @>
        raises<ArgumentException> <@ SchemaConstraint.pattern "" |> ignore @>
        raises<ArgumentNullException> <@ SchemaConstraint.oneOf null |> ignore @>

    [<Fact>]
    let ``schema constraints retain typed metadata for non validation interpreters`` () =
        let constraints =
            [ SchemaConstraint.required
              SchemaConstraint.maxLength 20
              SchemaConstraint.email
              SchemaConstraint.pattern "^[^@]+@example.com$"
              SchemaConstraint.oneOf [ "ada@example.com"; "grace@example.com" ]
              SchemaConstraint.between 1 10
              SchemaConstraint.countBetween 1 3
              SchemaConstraint.distinct ]

        let diagnostics =
            constraints
            |> List.choose (SchemaConstraint.metadata >> function
                | SchemaConstraintMetadata.Required -> Some "SchemaError.Required"
                | SchemaConstraintMetadata.MaxLength maximum -> Some $"SchemaError.InvalidLength maxLength {maximum}"
                | SchemaConstraintMetadata.Email -> Some "SchemaError.InvalidFormat email"
                | SchemaConstraintMetadata.Pattern pattern -> Some $"SchemaError.InvalidFormat {pattern}"
                | SchemaConstraintMetadata.OneOf choices ->
                    Some(sprintf "SchemaError.NotOneOf %s" (String.concat "|" choices))
                | SchemaConstraintMetadata.Between(minimum, maximum) ->
                    Some $"SchemaError.OutOfRange {minimum}-{maximum}"
                | SchemaConstraintMetadata.CountBetween(minimum, maximum) ->
                    Some $"SchemaError.InvalidCount {minimum}-{maximum}"
                | SchemaConstraintMetadata.Distinct -> Some "SchemaError.Duplicate"
                | _ -> None)

        let jsonSchema =
            constraints
            |> List.choose (SchemaConstraint.metadata >> function
                | SchemaConstraintMetadata.Required -> Some "required"
                | SchemaConstraintMetadata.MaxLength maximum -> Some $"maxLength={maximum}"
                | SchemaConstraintMetadata.Email -> Some "format=email"
                | SchemaConstraintMetadata.Pattern pattern -> Some $"pattern={pattern}"
                | SchemaConstraintMetadata.OneOf choices -> Some(sprintf "enum=%s" (String.concat "," choices))
                | SchemaConstraintMetadata.Between(minimum, maximum) ->
                    Some $"minimum={minimum};maximum={maximum}"
                | SchemaConstraintMetadata.CountBetween(minimum, maximum) ->
                    Some $"minItems={minimum};maxItems={maximum}"
                | SchemaConstraintMetadata.Distinct -> Some "uniqueItems=true"
                | _ -> None)

        let ui =
            constraints
            |> List.choose (SchemaConstraint.metadata >> function
                | SchemaConstraintMetadata.Required -> Some "required"
                | SchemaConstraintMetadata.MaxLength maximum -> Some $"maxlength={maximum}"
                | SchemaConstraintMetadata.Email -> Some "input=email"
                | SchemaConstraintMetadata.Pattern pattern -> Some $"pattern={pattern}"
                | SchemaConstraintMetadata.OneOf choices -> Some $"choices={choices.Length}"
                | SchemaConstraintMetadata.Between(minimum, maximum) ->
                    Some $"min={minimum};max={maximum}"
                | SchemaConstraintMetadata.CountBetween(minimum, maximum) ->
                    Some $"min-items={minimum};max-items={maximum}"
                | SchemaConstraintMetadata.Distinct -> Some "unique-items"
                | _ -> None)

        let docs =
            constraints
            |> List.map (SchemaConstraint.metadata >> function
                | SchemaConstraintMetadata.Required -> "Required"
                | SchemaConstraintMetadata.MaxLength maximum -> $"Maximum length {maximum}"
                | SchemaConstraintMetadata.Email -> "Email format"
                | SchemaConstraintMetadata.Pattern pattern -> $"Matches {pattern}"
                | SchemaConstraintMetadata.OneOf choices -> sprintf "One of %s" (String.concat ", " choices)
                | SchemaConstraintMetadata.Between(minimum, maximum) -> $"Between {minimum} and {maximum}"
                | SchemaConstraintMetadata.CountBetween(minimum, maximum) -> $"Between {minimum} and {maximum} items"
                | SchemaConstraintMetadata.Distinct -> "No duplicates"
                | other -> string other)

        test <@
            diagnostics =
                [ "SchemaError.Required"
                  "SchemaError.InvalidLength maxLength 20"
                  "SchemaError.InvalidFormat email"
                  "SchemaError.InvalidFormat ^[^@]+@example.com$"
                  "SchemaError.NotOneOf ada@example.com|grace@example.com"
                  "SchemaError.OutOfRange 1-10"
                  "SchemaError.InvalidCount 1-3"
                  "SchemaError.Duplicate" ]
        @>
        test <@
            jsonSchema =
                [ "required"
                  "maxLength=20"
                  "format=email"
                  "pattern=^[^@]+@example.com$"
                  "enum=ada@example.com,grace@example.com"
                  "minimum=1;maximum=10"
                  "minItems=1;maxItems=3"
                  "uniqueItems=true" ]
        @>
        test <@
            ui =
                [ "required"
                  "maxlength=20"
                  "input=email"
                  "pattern=^[^@]+@example.com$"
                  "choices=2"
                  "min=1;max=10"
                  "min-items=1;max-items=3"
                  "unique-items" ]
        @>
        test <@
            docs =
                [ "Required"
                  "Maximum length 20"
                  "Email format"
                  "Matches ^[^@]+@example.com$"
                  "One of ada@example.com, grace@example.com"
                  "Between 1 and 10"
                  "Between 1 and 3 items"
                  "No duplicates" ]
        @>

    [<Fact>]
    let ``schema fields inspect existing trusted models through typed getters`` () =
        let nameField = Field.create "name" (fun (model: Customer) -> model.Name) Value.text
        let ageField = Field.create "age" (fun (model: Customer) -> model.Age) Value.int
        let customer = { Name = "Ada"; Age = 37 }
        let missingField = Unchecked.defaultof<Field<Customer, string>>

        test <@ Field.externalName nameField |> ExternalFieldName.value = "name" @>
        test <@ Field.order nameField |> FieldOrder.value = 0 @>
        test <@ Field.getValue nameField customer = "Ada" @>
        test <@ Field.getValue ageField customer = 37 @>
        raises<ArgumentNullException> <@ Field.getValue missingField customer |> ignore @>
        raises<ArgumentNullException> <@ Field.order missingField |> ignore @>

    [<Fact>]
    let ``schema field rejects invalid public construction arguments`` () =
        let builder = Schema.recordFor<Customer, _> (fun name -> { Name = name; Age = 0 })
        raises<ArgumentNullException> <@ Schema.field null (fun (model: Customer) -> model.Name) Value.text builder |> ignore @>
        raises<ArgumentException> <@ Schema.field " " (fun (model: Customer) -> model.Name) Value.text builder |> ignore @>
        raises<ArgumentNullException> <@ Schema.field "name" Unchecked.defaultof<Customer -> string> Value.text builder |> ignore @>
        raises<ArgumentNullException> <@ Schema.field "name" (fun (model: Customer) -> model.Name) Unchecked.defaultof<ValueSchema<string>> builder |> ignore @>
        raises<ArgumentNullException> <@ Schema.record Unchecked.defaultof<string -> Customer> |> ignore @>
        raises<ArgumentNullException> <@ Schema.recordFor<Customer, _> Unchecked.defaultof<string -> Customer> |> ignore @>

    [<Fact>]
    let ``schema builder builds explicit ordered model schema with value schema constraints`` () =
        let requiredText = Value.text |> Value.withConstraint SchemaConstraint.required

        let schema =
            Schema.recordFor<Customer, _> (fun name age -> { Name = name; Age = age })
            |> Schema.fieldWith [ SchemaConstraint.required ] "name" _.Name requiredText
            |> Schema.int "age" _.Age
            |> Schema.build

        let constructed =
            match schema.Definition with
            | ModelDefinition model ->
                let values =
                    model.Fields
                    |> List.map (fun field -> field.Getter { Name = "Ada"; Age = 37 })

                test <@ model.Constructor.ArgumentCount = 2 @>
                test <@ model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) = [ "name"; "age" ] @>
                test <@ model.Fields |> List.map (fun field -> FieldOrder.value field.Order) = [ 0; 1 ] @>
                test <@ model.Fields[0].ValueSchema.Constraints |> List.map SchemaConstraint.code = [ "required" ] @>
                test <@ model.Fields[0].Constraints |> List.map SchemaConstraint.code = [ "required" ] @>
                ConstructorApplication.apply model.Constructor (values |> List.toArray)
            | PendingDefinition -> failwith "Expected public schema API to create a model definition."

        test <@ constructed = { Name = "Ada"; Age = 37 } @>

    [<Fact>]
    let ``schema builder builds explicit ordered three field model schema through primitive shorthand fields`` () =
        let create name age active = { Name = name; Age = age; Active = active }
        let schema =
            Schema.recordFor<CustomerProfile, _> create
            |> Schema.text "name" _.Name
            |> Schema.int "age" _.Age
            |> Schema.bool "active" _.Active
            |> Schema.build

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
            Schema.recordFor<CustomerProfile, _> create
            |> Schema.text "name" _.Name
            |> Schema.int "age" _.Age
            |> Schema.bool "active" _.Active
            |> Schema.build

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
            Schema.recordFor<PrimitiveProfile, _> create
            |> Schema.text "name" _.Name
            |> Schema.int "age" _.Age
            |> Schema.decimal "balance" _.Balance
            |> Schema.bool "active" _.Active
            |> Schema.date "birthDate" _.BirthDate
            |> Schema.dateTime "lastSeen" _.LastSeen
            |> Schema.guid "id" _.Id
            |> Schema.build

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
    let ``check take binderror diagnostics and ref helpers keep expected public shape`` () =
        assertCheckAliasShape<string> ()
        assertCheckAliasShape<int> ()

        let checkProgram : Check<string> =
            fun value ->
                if String.IsNullOrWhiteSpace value then Error [ Required ]
                else Ok ()

        let checkFunction : string -> Result<unit, CheckFailure list> = checkProgram
        test <@ checkFunction "Ada" = Ok () @>
        test <@ checkFunction "" = Error [ Required ] @>

        typeof<CheckFailure>
        |> publicUnionCaseNames
        |> assertContainsAll
            [ "Required"
              "InvalidFormat"
              "InvalidLength"
              "OutOfRange"
              "InvalidCount"
              "NotOneOf"
              "Duplicate"
              "Custom" ]

        let forbiddenCheckFailureFieldNames =
            set [ "Path"; "Raw"; "RawInput"; "Input"; "Schema"; "Diagnostic"; "Diagnostics" ]

        let forbiddenCheckFailureTypeNamespaces =
            [ "Axial.Schema"; "Axial.Validation"; "Axial.Refined" ]

        let publicCheckFailureFields =
            FSharpType.GetUnionCases(typeof<CheckFailure>, BindingFlags.Public)
            |> Array.collect (fun caseInfo ->
                caseInfo.GetFields()
                |> Array.map (fun propertyInfo -> caseInfo.Name, propertyInfo.Name, propertyInfo.PropertyType))

        let forbiddenFields =
            publicCheckFailureFields
            |> Array.filter (fun (_, fieldName, fieldType) ->
                Set.contains fieldName forbiddenCheckFailureFieldNames
                || forbiddenCheckFailureTypeNamespaces
                   |> List.exists (fun namespaceName ->
                       let fullName = fieldType.FullName

                       not (isNull fullName) && fullName.StartsWith(namespaceName, StringComparison.Ordinal)))

        test <@ Array.isEmpty forbiddenFields @>

        let checkModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.Check"

        let checkMembers =
            checkModule
            |> publicStaticMemberNames

        let topLevelStructuredCheckNames =
            [ "all"
              "any"
              "not"
              "mapFailure"
              "present"
              "empty"
              "notEmpty"
              "length"
              "minLength"
              "maxLength"
              "lengthBetween"
              "email"
              "matches"
              "oneOf"
              "between"
              "greaterThan"
              "lessThan"
              "atLeast"
              "atMost"
              "positive"
              "nonNegative"
              "negative"
              "nonPositive"
              "count"
              "minCount"
              "maxCount"
              "countBetween"
              "distinct"
              "contains"
              "single"
              "atMostOne"
              "atLeastOne"
              "moreThanOne"
              "equalTo"
              "notEqualTo" ]

        checkMembers
        |> assertContainsAll topLevelStructuredCheckNames

        checkModule
        |> assertMethodsReturnCheckResult topLevelStructuredCheckNames

        checkModule
        |> assertNoMethodsReturnBool

        let checkStringModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.CheckModule+String"

        checkStringModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "present"; "minLength"; "maxLength"; "lengthBetween"; "length"; "exactLength"; "email"; "matches"; "oneOf" ]

        checkStringModule
        |> assertMethodsReturnCheckResult [ "present"; "minLength"; "maxLength"; "lengthBetween"; "length"; "exactLength"; "email"; "matches"; "oneOf" ]

        let checkNumberModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.CheckModule+Number"

        checkNumberModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "between"; "greaterThan"; "lessThan"; "atLeast"; "atMost"; "positive"; "nonNegative"; "negative"; "nonPositive" ]

        checkNumberModule
        |> assertMethodsReturnCheckResult [ "between"; "greaterThan"; "lessThan"; "atLeast"; "atMost"; "positive"; "nonNegative"; "negative"; "nonPositive" ]

        let checkSeqModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.CheckModule+Seq"

        checkSeqModule
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "empty"
              "notEmpty"
              "count"
              "minCount"
              "maxCount"
              "countBetween"
              "noDuplicates"
              "contains"
              "single"
              "atMostOne"
              "atLeastOne"
              "moreThanOne" ]

        checkSeqModule
        |> publicStaticMemberNames
        |> assertContainsNone [ "distinct" ]

        checkSeqModule
        |> assertMethodsReturnCheckResult
            [ "empty"
              "notEmpty"
              "count"
              "minCount"
              "maxCount"
              "countBetween"
              "noDuplicates"
              "contains"
              "single"
              "atMostOne"
              "atLeastOne"
              "moreThanOne" ]

        assertModuleAbsentFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.CheckModule+Collection"

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
        |> assertContainsAll [ "some"; "none"; "present"; "empty"; "notEmpty" ]

        checkValueOptionModule
        |> assertMethodsReturnCheckResult [ "some"; "none"; "present"; "empty"; "notEmpty" ]

        let checkNullableModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.CheckModule+Nullable"

        checkNullableModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "hasValue"; "hasNoValue"; "present"; "empty"; "notEmpty" ]

        checkNullableModule
        |> assertMethodsReturnCheckResult [ "hasValue"; "hasNoValue"; "present"; "empty"; "notEmpty" ]

        let checkResultModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.CheckModule+Result"

        checkResultModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "ok"; "error" ]

        checkResultModule
        |> assertMethodsReturnCheckResult [ "ok"; "error" ]

        let checkNestedTypeNames =
            Assembly.Load "Axial.ErrorHandling"
            |> _.GetTypes()
            |> Array.choose (fun targetType ->
                match targetType.FullName with
                | null -> None
                | fullName when fullName.StartsWith("Axial.ErrorHandling.CheckModule+", StringComparison.Ordinal) ->
                    Some targetType.Name
                | _ -> None)
            |> Set.ofArray

        checkNestedTypeNames
        |> assertContainsAll [ "Present"; "Empty"; "NotEmpty" ]

        checkNestedTypeNames
        |> assertContainsNone
            [ "Distinct"
              "Count"
              "MinCount"
              "MaxCount"
              "CountBetween"
              "Length"
              "MinLength"
              "MaxLength"
              "LengthBetween"
              "Email"
              "Matches"
              "OneOf"
              "Between"
              "GreaterThan"
              "LessThan"
              "AtLeast"
              "AtMost"
              "Ok"
              "Error"
              "EqualTo"
              "NotEqualTo" ]

        checkMembers
        |> assertContainsNone
            [ "isTrue"
              "ok"
              "error"
              "isFalse"
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
              "hasCount"
              "isSingle"
              "hasDuplicates"
              "hasNoDuplicates"
              "negate"
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

        let predicateModule =
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.Predicate"

        predicateModule
        |> publicStaticMemberNames
        |> assertContainsNone [ "distinct" ]

        moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.PredicateModule+Option"
        |> assertMethodsReturnBool [ "isSome"; "isNone"; "present"; "empty"; "notEmpty" ]

        moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.PredicateModule+ValueOption"
        |> assertMethodsReturnBool [ "isSome"; "isNone"; "present"; "empty"; "notEmpty" ]

        moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.PredicateModule+Nullable"
        |> assertMethodsReturnBool [ "hasValue"; "hasNoValue"; "present"; "empty"; "notEmpty" ]

        moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.PredicateModule+Result"
        |> assertMethodsReturnBool [ "isOk"; "isError" ]

        moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.PredicateModule+Reference"
        |> assertMethodsReturnBool [ "isNull"; "notNull" ]

        moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.PredicateModule+String"
        |> assertMethodsReturnBool
            [ "empty"
              "notEmpty"
              "blank"
              "notBlank"
              "minLength"
              "maxLength"
              "lengthBetween"
              "length"
              "matches"
              "email"
              "numeric"
              "alphaNumeric" ]

        moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.PredicateModule+Seq"
        |> assertMethodsReturnBool
            [ "empty"
              "notEmpty"
              "contains"
              "count"
              "minCount"
              "maxCount"
              "countBetween"
              "single"
              "atMostOne"
              "atLeastOne"
              "moreThanOne"
              "duplicates"
              "distinct" ]

        moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.PredicateModule+Number"
        |> assertMethodsReturnBool
            [ "greaterThan"
              "lessThan"
              "atLeast"
              "atMost"
              "between"
              "positive"
              "nonNegative"
              "negative"
              "nonPositive" ]

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

    [<Fact>]
    let ``option and valueoption implicit binding requires unit workflow errors`` () =
        let flowAssemblyPath = typeof<FlowBuilder>.Assembly.Location
        let resultAssemblyPath = typeof<ResultBuilder>.Assembly.Location
        let validationAssemblyPath = typeof<Validation<unit, unit>>.Assembly.Location

        let flowProbe =
            $"""
#r @"{flowAssemblyPath}"
#r @"{resultAssemblyPath}"
#r @"{validationAssemblyPath}"
open Axial.Flow
open Axial.ErrorHandling
open Axial.Validation

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
#r @"{validationAssemblyPath}"
open Axial.Flow
open Axial.ErrorHandling
open Axial.Validation

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

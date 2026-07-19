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
open Axial.Schema.Syntax
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
              ValueSchema = Schema.text.ValueDefinition
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
                arguments[1] = typeof<CheckFailure list>
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
        test <@ arguments[1] = typeof<Result<'value, CheckFailure list>> @>

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
        |> assertContainsAll [ "model"; "schema"; "field" ]

        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.Schema"
        |> publicStaticMemberNames
        |> assertContainsAll [ "parse"; "parseWith"; "constructorErrorAt"; "check"; "refine" ]

        assertTypeAbsentFromAssembly "Axial.Schema" "Axial.Schema.ValueSchema`1"

        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.RawInputModule"
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

        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.RetainedParseResult"
        |> publicStaticMemberNames
        |> assertContainsAll [ "create"; "mapErrors"; "renderErrors" ]

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
        |> assertContainsNone [ "Axial.Flow" ]

    [<Fact>]
    let ``leaf packages stay independent of each other`` () =
        // Axial consolidated to two leaf packages: Axial.ErrorHandling (which absorbed Axial.Validation and
        // Axial.Refined, so it has no internal Axial dependencies) and Axial.Schema (which legitimately depends
        // on Axial.ErrorHandling for Check plumbing and the Refined bridge). Axial.Flow stays independent of both.
        let leafPackages = [ "Axial.Flow"; "Axial.ErrorHandling"; "Axial.Schema" ]

        let allowedReferences = [ "Axial.Schema", "Axial.ErrorHandling" ]

        for package in leafPackages do
            let forbidden =
                leafPackages
                |> List.filter (fun other ->
                    other <> package && not (List.contains (package, other) allowedReferences))

            let references = referencedAssemblyNames (Assembly.Load package)

            references |> assertContainsNone forbidden
            references |> assertContainsNone [ "Axial" ]

    [<Fact>]
    let ``policy lives in flow without schema or error handling dependencies`` () =
        let flowAssembly = Assembly.Load "Axial.Flow"

        test <@ flowAssembly.GetName().Name = "Axial.Flow" @>

        referencedAssemblyNames flowAssembly
        |> assertContainsNone [ "Axial.Schema"; "Axial.ErrorHandling" ]

        moduleTypeFromAssembly "Axial.Flow" "Axial.Flow.PolicyModule"
        |> publicStaticMemberNames
        |> assertContainsAll [ "lift"; "withError"; "context"; "pass"; "compose"; "optional" ]

    [<Fact>]
    let ``schema validation interpreters live alongside schema in the consolidated schema package`` () =
        // Axial.ErrorHandling absorbed Axial.Validation and Axial.Refined. The old cross-package
        // "stays out of core validation" boundary no longer exists as a package boundary, but the
        // schema-specific interpreter modules should still not leak into the (schema-independent)
        // Axial.ErrorHandling assembly.
        let errorHandlingAssembly = typeof<Validation<int, string>>.Assembly
        let schemaAssembly = Assembly.Load "Axial.Schema"
        let schemaReferences = referencedAssemblyNames schemaAssembly

        test <@ errorHandlingAssembly.GetName().Name = "Axial.ErrorHandling" @>

        assertModuleAbsentFromAssembly "Axial.ErrorHandling" "Axial.Schema.SchemaValidation"
        assertModuleAbsentFromAssembly "Axial.ErrorHandling" "Axial.Schema.ConstraintCheck"
        assertModuleAbsentFromAssembly "Axial.ErrorHandling" "Axial.Schema.SchemaCheck"
        assertModuleAbsentFromAssembly "Axial.ErrorHandling" "Axial.Schema.ModelModule"

        test <@ schemaAssembly.GetName().Name = "Axial.Schema" @>

        schemaReferences
        |> assertContainsAll [ "Axial.ErrorHandling" ]

        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.SchemaValidation"
        |> publicStaticMemberNames
        |> assertContainsAll [ "packageName" ]

        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.ConstraintCheck"
        |> publicStaticMemberNames
        |> assertContainsAll [ "tryText"; "text"; "tryOrdered"; "ordered"; "trySequence"; "sequence" ]

        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.SchemaCheck"
        |> publicStaticMemberNames
        |> assertContainsAll [ "fromUnderlying"; "text"; "ordered" ]

        moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.ContextRules"
        |> publicStaticMemberNames
        |> assertContainsAll
            [ "fail"
              "custom"
              "failCustom"
              "failAt"
              "failAtField"
              "at"
              "atField"
              "name"
              "key"
              "index"
              "apply" ]

    [<Fact>]
    let ``schema contextual rules are reserved for schema validation interpreters`` () =
        // Contextual rules are plain functions plus a minimal helper module; there is
        // deliberately no rule-set container type anywhere in the library.
        let forbiddenCoreRuleModules =
            [ "Rules"
              "SchemaRules"
              "ContextRules"
              "ContextualRules" ]

        let forbiddenCoreRuleTypes =
            [ "RuleSet`2"
              "RuleFailure"
              "RuleBuilder`2" ]

        for moduleName in forbiddenCoreRuleModules do
            assertModuleAbsentFromAssembly "Axial.ErrorHandling" $"Axial.ErrorHandling.{moduleName}"
            assertModuleAbsentFromAssembly "Axial.ErrorHandling" $"Axial.Validation.{moduleName}"

        for typeName in forbiddenCoreRuleTypes do
            assertTypeAbsentFromAssembly "Axial.ErrorHandling" $"Axial.ErrorHandling.{typeName}"
            assertTypeAbsentFromAssembly "Axial.ErrorHandling" $"Axial.Validation.{typeName}"

        assertTypeAbsentFromAssembly "Axial.Schema" "Axial.Schema.RuleSet`2"
        assertModuleAbsentFromAssembly "Axial.Schema" "Axial.Schema.Rules"

    [<Fact>]
    let ``schema types stay out of the flow package`` () =
        let schemaType = typedefof<Schema<_>>
        let valueSchemaType = typedefof<Schema<_>>
        let fieldType = typedefof<Field<_, _>>
        let primitiveValueKindType = typeof<PrimitiveValueKind>
        let schemaConstraintMetadataType = typeof<ConstraintMetadata>
        let schemaConstraintType = typeof<Constraint>
        let externalFieldNameType = typeof<ExternalFieldName>
        let fieldOrderType = typeof<FieldOrder>
        let schemaModule = moduleType schemaType "Axial.Schema.Schema"
        let fieldModule = moduleType fieldType "Axial.Schema.Field"
        let schemaConstraintModule = moduleType schemaConstraintType "Axial.Schema.ConstraintModule"
        let schemaAssembly = schemaType.Assembly
        let references = referencedAssemblyNames schemaAssembly
        let publicConstructors =
            schemaType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)
        let publicValueConstructors =
            valueSchemaType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)
        let publicFieldConstructors =
            fieldType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)
        let publicConstraintConstructors =
            schemaConstraintType.GetConstructors(BindingFlags.Public ||| BindingFlags.Instance)
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
        test <@ publicConstraintConstructors.Length = 0 @>
        test <@ publicExternalFieldNameConstructors.Length = 0 @>
        let schemaMembers = schemaModule |> publicStaticMemberNames
        schemaMembers
        |> assertContainsAll
            [ "define"
              "text"
              "int"
              "decimal"
              "bool"
              "date"
              "dateTime"
              "guid" ]
        test <@ [ "record"; "recordFor"; "field"; "build"; "buildResult"; "buildResultWith" ]
                 |> List.forall (fun removed -> not (schemaMembers |> Set.contains removed)) @>
        fieldModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "create"; "externalName"; "order"; "getValue"; "constraints"; "withConstraint"; "withConstraints" ]
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
              "constrainAll" ]
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
              "Contains"
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
        // Axial.Schema legitimately depends on Axial.ErrorHandling (for Check-based constraint
        // lowering and the Axial.Refined bridge) but must stay independent of Axial.Flow.
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
    let ``refined value schemas require fallible construction error lowering and inspection`` () =
        let valueModule = moduleTypeFromAssembly "Axial.Schema" "Axial.Schema.Schema"

        let refinedOverloads =
            publicStaticMethods valueModule
            |> Array.filter (fun methodInfo -> methodInfo.Name.Equals("refine", StringComparison.OrdinalIgnoreCase))

        test <@ refinedOverloads.Length = 1 @>

        let refined = refinedOverloads[0]
        let parameters = refined.GetParameters()
        let parameterNames = parameters |> Array.map _.Name

        test <@ parameterNames = [| "construct"; "mapError"; "inspect"; "schema" |] @>

        let constructArguments = parameters[0].ParameterType.GetGenericArguments()
        let inspectArguments = parameters[2].ParameterType.GetGenericArguments()
        let constructIsFunction = parameters[0].ParameterType.GetGenericTypeDefinition() = typedefof<FSharpFunc<_, _>>
        let inspectIsFunction = parameters[2].ParameterType.GetGenericTypeDefinition() = typedefof<FSharpFunc<_, _>>
        let rawIsValueSchema = parameters[3].ParameterType.GetGenericTypeDefinition() = typedefof<Schema<_>>
        let returnsValueSchema = refined.ReturnType.GetGenericTypeDefinition() = typedefof<Schema<_>>
        let rawMatchesConstructInput = parameters[3].ParameterType.GetGenericArguments()[0] = constructArguments[0]

        test <@ constructIsFunction @>
        test <@ inspectIsFunction @>
        test <@ rawIsValueSchema @>
        test <@ returnsValueSchema @>
        test <@ rawMatchesConstructInput @>

    [<Fact>]
    let ``schema constraints are inspectable metadata independent of executable checks`` () =
        let required = Constraint.required
        let maxLength = Constraint.maxLength 20
        let text = Schema.text |> Schema.constrainAll [ required; maxLength ]
        let field =
            schemaField "name" 0 (fun (model: Customer) -> model.Name)
            |> Field.withConstraint required
            |> Field.withConstraint maxLength
        let descriptor = field |> schemaFieldDescriptor

        test <@ Constraint.code required = "required" @>
        test <@ Constraint.metadata required = ConstraintMetadata.Required @>
        test <@ Constraint.metadata maxLength = ConstraintMetadata.MaxLength 20 @>
        test <@ string required = "required" @>
        test <@ Constraint.arguments required |> Seq.isEmpty @>
        test <@ Constraint.tryFindArgument "maximum" maxLength = Some(box 20) @>
        test <@ Schema.constraints text |> List.map Constraint.code = [ "required"; "maxLength" ] @>
        test <@ Field.constraints field |> List.map Constraint.code = [ "required"; "maxLength" ] @>
        test <@ descriptor.Constraints |> List.map Constraint.code = [ "required"; "maxLength" ] @>
        test <@ descriptor.ValueSchema.Constraints = [] @>
        test <@ text.ValueDefinition.Constraints |> List.map Constraint.code = [ "required"; "maxLength" ] @>
        raises<ArgumentException> <@ Constraint.create "" |> ignore @>
        raises<ArgumentException> <@ Constraint.createWithArguments "maxLength" [ "", box 20 ] |> ignore @>
        raises<ArgumentException> <@ Constraint.createWithArguments "maxLength" [ "maximum", box 20; "maximum", box 30 ] |> ignore @>
        raises<ArgumentNullException> <@ Schema.constrain null Schema.text |> ignore @>
        raises<ArgumentNullException> <@ Field.constraints Unchecked.defaultof<Field<Customer, string>> |> ignore @>

    [<Fact>]
    let ``named schema constraints expose stable codes and structured arguments`` () =
        let codes =
            [ Constraint.required
              Constraint.optional
              Constraint.minLength 2
              Constraint.maxLength 20
              Constraint.lengthBetween 2 20
              Constraint.email
              Constraint.trimmed
              Constraint.pattern "^[a-z]+$"
              Constraint.oneOf [ "draft"; "published" ]
              Constraint.notEqualTo "archived"
              Constraint.between 1 10
              Constraint.greaterThan 0
              Constraint.lessThan 100
              Constraint.atLeast 1
              Constraint.atMost 10
              Constraint.count 2
              Constraint.minCount 1
              Constraint.maxCount 5
              Constraint.countBetween 1 5
              Constraint.distinct ]
            |> List.map Constraint.code

        let length = Constraint.lengthBetween 2 20
        let pattern = Constraint.pattern "^[a-z]+$"
        let choices = Constraint.oneOf [ "draft"; "published" ]
        let range = Constraint.between 1.5m 3.5m
        let count = Constraint.countBetween 1 5

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
        test <@ Constraint.tryFindArgument "minimum" length = Some(box 2) @>
        test <@ Constraint.tryFindArgument "maximum" length = Some(box 20) @>
        test <@ Constraint.tryFindArgument "pattern" pattern = Some(box "^[a-z]+$") @>
        test <@ Constraint.tryFindArgument "choices" choices |> Option.map unbox<string array> = Some [| "draft"; "published" |] @>
        test <@ Constraint.tryFindArgument "minimum" range = Some(box 1.5m) @>
        test <@ Constraint.tryFindArgument "maximum" range = Some(box 3.5m) @>
        test <@ Constraint.tryFindArgument "minimum" count = Some(box 1) @>
        test <@ Constraint.tryFindArgument "maximum" count = Some(box 5) @>
        test <@
            Constraint.metadata (Constraint.create "tenantOnly") = ConstraintMetadata.Custom "tenantOnly"
        @>
        test <@
            Constraint.metadata (Constraint.createWithArguments "tenantOnly" [ "tenant", box "north" ]) =
                ConstraintMetadata.Custom "tenantOnly"
        @>
        raises<ArgumentOutOfRangeException> <@ Constraint.minLength -1 |> ignore @>
        raises<ArgumentOutOfRangeException> <@ Constraint.count -1 |> ignore @>
        raises<ArgumentException> <@ Constraint.lengthBetween 5 2 |> ignore @>
        raises<ArgumentException> <@ Constraint.countBetween 5 2 |> ignore @>
        raises<ArgumentException> <@ Constraint.between 10 1 |> ignore @>
        raises<ArgumentException> <@ Constraint.pattern "" |> ignore @>
        raises<ArgumentNullException> <@ Constraint.oneOf null |> ignore @>

    [<Fact>]
    let ``schema constraints retain typed metadata for non validation interpreters`` () =
        let constraints =
            [ Constraint.required
              Constraint.maxLength 20
              Constraint.email
              Constraint.pattern "^[^@]+@example.com$"
              Constraint.oneOf [ "ada@example.com"; "grace@example.com" ]
              Constraint.between 1 10
              Constraint.countBetween 1 3
              Constraint.distinct ]

        let diagnostics =
            constraints
            |> List.choose (Constraint.metadata >> function
                | ConstraintMetadata.Required -> Some "SchemaError.Required"
                | ConstraintMetadata.MaxLength maximum -> Some $"SchemaError.InvalidLength maxLength {maximum}"
                | ConstraintMetadata.Email -> Some "SchemaError.InvalidFormat email"
                | ConstraintMetadata.Pattern pattern -> Some $"SchemaError.InvalidFormat {pattern}"
                | ConstraintMetadata.OneOf choices ->
                    Some(sprintf "SchemaError.NotOneOf %s" (String.concat "|" choices))
                | ConstraintMetadata.Between(minimum, maximum) ->
                    Some $"SchemaError.OutOfRange {minimum}-{maximum}"
                | ConstraintMetadata.CountBetween(minimum, maximum) ->
                    Some $"SchemaError.InvalidCount {minimum}-{maximum}"
                | ConstraintMetadata.Distinct -> Some "SchemaError.Duplicate"
                | _ -> None)

        let jsonSchema =
            constraints
            |> List.choose (Constraint.metadata >> function
                | ConstraintMetadata.Required -> Some "required"
                | ConstraintMetadata.MaxLength maximum -> Some $"maxLength={maximum}"
                | ConstraintMetadata.Email -> Some "format=email"
                | ConstraintMetadata.Pattern pattern -> Some $"pattern={pattern}"
                | ConstraintMetadata.OneOf choices -> Some(sprintf "enum=%s" (String.concat "," choices))
                | ConstraintMetadata.Between(minimum, maximum) ->
                    Some $"minimum={minimum};maximum={maximum}"
                | ConstraintMetadata.CountBetween(minimum, maximum) ->
                    Some $"minItems={minimum};maxItems={maximum}"
                | ConstraintMetadata.Distinct -> Some "uniqueItems=true"
                | _ -> None)

        let ui =
            constraints
            |> List.choose (Constraint.metadata >> function
                | ConstraintMetadata.Required -> Some "required"
                | ConstraintMetadata.MaxLength maximum -> Some $"maxlength={maximum}"
                | ConstraintMetadata.Email -> Some "input=email"
                | ConstraintMetadata.Pattern pattern -> Some $"pattern={pattern}"
                | ConstraintMetadata.OneOf choices -> Some $"choices={choices.Length}"
                | ConstraintMetadata.Between(minimum, maximum) ->
                    Some $"min={minimum};max={maximum}"
                | ConstraintMetadata.CountBetween(minimum, maximum) ->
                    Some $"min-items={minimum};max-items={maximum}"
                | ConstraintMetadata.Distinct -> Some "unique-items"
                | _ -> None)

        let docs =
            constraints
            |> List.map (Constraint.metadata >> function
                | ConstraintMetadata.Required -> "Required"
                | ConstraintMetadata.MaxLength maximum -> $"Maximum length {maximum}"
                | ConstraintMetadata.Email -> "Email format"
                | ConstraintMetadata.Pattern pattern -> $"Matches {pattern}"
                | ConstraintMetadata.OneOf choices -> sprintf "One of %s" (String.concat ", " choices)
                | ConstraintMetadata.Between(minimum, maximum) -> $"Between {minimum} and {maximum}"
                | ConstraintMetadata.CountBetween(minimum, maximum) -> $"Between {minimum} and {maximum} items"
                | ConstraintMetadata.Distinct -> "No duplicates"
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
        let shape = Schema.define<Customer>
        raises<ArgumentNullException> <@ fieldWith Schema.text null (fun (value: Customer) -> value.Name) shape |> ignore @>
        raises<ArgumentException> <@ fieldWith Schema.text " " (fun (value: Customer) -> value.Name) shape |> ignore @>
        raises<ArgumentNullException> <@ fieldWith Schema.text "name" Unchecked.defaultof<Customer -> string> shape |> ignore @>
        raises<ArgumentNullException>
            <@ fieldWith Unchecked.defaultof<Schema<string>> "name" (fun (value: Customer) -> value.Name) shape |> ignore @>

    [<Fact>]
    let ``schema shape builds explicit ordered model schema with value schema constraints`` () =
        let requiredText = Schema.text |> Schema.constrain Constraint.required

        let schema =
            Schema.define<Customer>
            |> fieldWith (requiredText |> Schema.constrainAll [ Constraint.required ]) "name" _.Name
            |> fieldWith Schema.int "age" _.Age
            |> construct (fun name age -> { Name = name; Age = age })

        let constructed =
            match schema.Definition with
            | ModelDefinition model ->
                let values =
                    model.Fields
                    |> List.map (fun field -> field.Getter { Name = "Ada"; Age = 37 })

                test <@ model.Constructor.ArgumentCount = 2 @>
                test <@ model.Fields |> List.map (fun field -> ExternalFieldName.value field.ExternalName) = [ "name"; "age" ] @>
                test <@ model.Fields |> List.map (fun field -> FieldOrder.value field.Order) = [ 0; 1 ] @>
                test <@ model.Fields[0].ValueSchema.Constraints |> List.map Constraint.code = [ "required"; "required" ] @>
                test <@ model.Fields[0].Constraints = [] @>
                ConstructorApplication.apply model.Constructor (values |> List.toArray)
            | _ -> failwith "Expected public schema API to create a model definition."

        test <@ constructed = { Name = "Ada"; Age = 37 } @>

    [<Fact>]
    let ``schema shape builds explicit ordered three field model schema through inferred primitive fields`` () =
        let create name age active = { Name = name; Age = age; Active = active }
        let schema =
            Schema.define<CustomerProfile>
            |> fieldWith Schema.text "name" _.Name
            |> fieldWith Schema.int "age" _.Age
            |> fieldWith Schema.bool "active" _.Active
            |> construct create

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
            Schema.define<CustomerProfile>
            |> fieldWith Schema.text "name" _.Name
            |> fieldWith Schema.int "age" _.Age
            |> fieldWith Schema.bool "active" _.Active
            |> construct create

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
            Schema.define<PrimitiveProfile>
            |> fieldWith Schema.text "name" _.Name
            |> fieldWith Schema.int "age" _.Age
            |> fieldWith Schema.decimal "balance" _.Balance
            |> fieldWith Schema.bool "active" _.Active
            |> fieldWith Schema.date "birthDate" _.BirthDate
            |> fieldWith Schema.dateTime "lastSeen" _.LastSeen
            |> fieldWith Schema.guid "id" _.Id
            |> construct create

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
                else Ok value

        let checkFunction : string -> Result<string, CheckFailure list> = checkProgram
        test <@ checkFunction "Ada" = Ok "Ada" @>
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

        predicateModule
        |> assertMethodsReturnBool [ "present"; "empty"; "notEmpty" ]

        moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.PredicateModule+Reference"
        |> assertMethodsReturnBool [ "isNull"; "notNull" ]

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

        // String, Option, ValueOption, Nullable, Result, and sequence predicates are exposed as extension
        // members directly on those types (see PredicateExtensions), not as PredicateModule submodules.

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
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.Refined.Parse"
            |> publicStaticMemberNames

        test <@ typeof<ParseError>.Assembly.GetName().Name = "Axial.ErrorHandling" @>
        assertModuleAbsentFromAssembly "Axial.ErrorHandling" "Axial.ErrorHandling.Parse"
        assertModuleAbsentFromAssembly "Axial.Schema" "Axial.Refined.Parse"

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
            moduleTypeFromAssembly "Axial.ErrorHandling" "Axial.Refined.Refine"
            |> publicStaticMemberNames

        refineMembers
        |> assertContainsAll [ "nonBlankString"; "positiveInt"; "nonEmptyList"; "exactlyOne"; "atMostOne" ]

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

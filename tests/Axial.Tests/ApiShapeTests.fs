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
    type private Customer =
        { Name: string
          Age: int }

    type private CustomerProfile =
        { Name: string
          Age: int
          Active: bool }

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
    let ``schema types start as independent leaf package`` () =
        let schemaType = typedefof<Schema<_>>
        let valueSchemaType = typedefof<ValueSchema<_>>
        let fieldType = typedefof<Field<_, _>>
        let primitiveValueKindType = typeof<PrimitiveValueKind>
        let schemaConstraintType = typeof<SchemaConstraint>
        let externalFieldNameType = typeof<ExternalFieldName>
        let fieldOrderType = typeof<FieldOrder>
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
        fieldModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "externalName"; "order"; "getValue"; "constraints"; "withConstraint"; "withConstraints" ]
        valueModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "text"; "int"; "decimal"; "bool"; "date"; "dateTime"; "guid"; "primitiveKind"; "constraints"; "withConstraint"; "withConstraints" ]
        schemaConstraintModule
        |> publicStaticMemberNames
        |> assertContainsAll [ "create"; "createWithArguments"; "code"; "arguments"; "tryFindArgument" ]
        primitiveValueKindType
        |> publicUnionCaseNames
        |> assertContainsAll [ "Text"; "Int"; "Decimal"; "Bool"; "Date"; "DateTime"; "Guid" ]
        test <@ valueSchemaType.Assembly = schemaAssembly @>
        test <@ fieldType.Assembly = schemaAssembly @>
        test <@ primitiveValueKindType.Assembly = schemaAssembly @>
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
              Value.primitiveKind Value.``int``
              Value.primitiveKind Value.``decimal``
              Value.primitiveKind Value.``bool``
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
        test <@ Value.``int``.Definition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Int @>
        test <@ Value.``decimal``.Definition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Decimal @>
        test <@ Value.``bool``.Definition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Bool @>
        test <@ Value.date.Definition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Date @>
        test <@ Value.dateTime.Definition.Shape = PrimitiveValueDefinition PrimitiveValueKind.DateTime @>
        test <@ Value.guid.Definition.Shape = PrimitiveValueDefinition PrimitiveValueKind.Guid @>
        test <@ Value.constraints Value.text = [] @>
        raises<ArgumentNullException> <@ Value.primitiveKind Unchecked.defaultof<ValueSchema<string>> |> ignore @>

    [<Fact>]
    let ``schema constraints are inspectable metadata independent of executable checks`` () =
        let required = SchemaConstraint.create "required"
        let maxLength = SchemaConstraint.createWithArguments "maxLength" [ "maximum", box 20 ]
        let text = Value.text |> Value.withConstraints [ required; maxLength ]
        let field =
            schemaField "name" 0 (fun (model: Customer) -> model.Name)
            |> Field.withConstraint required
            |> Field.withConstraint maxLength
        let descriptor = field |> schemaFieldDescriptor

        test <@ SchemaConstraint.code required = "required" @>
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
    let ``schema fields inspect existing trusted models through typed getters`` () =
        let nameField = schemaField "name" 0 (fun (model: Customer) -> model.Name)
        let ageField = schemaField "age" 1 (fun (model: Customer) -> model.Age)
        let customer = { Name = "Ada"; Age = 37 }
        let missingField = Unchecked.defaultof<Field<Customer, string>>

        test <@ Field.externalName nameField |> ExternalFieldName.value = "name" @>
        test <@ Field.order nameField |> FieldOrder.value = 0 @>
        test <@ Field.getValue nameField customer = "Ada" @>
        test <@ Field.getValue ageField customer = 37 @>
        raises<ArgumentNullException> <@ Field.getValue missingField customer |> ignore @>
        raises<ArgumentNullException> <@ Field.order missingField |> ignore @>

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
                if String.IsNullOrWhiteSpace value then Error [ Blank ]
                else Ok ()

        let checkFunction : string -> Result<unit, CheckFailure list> = checkProgram
        test <@ checkFunction "Ada" = Ok () @>
        test <@ checkFunction "" = Error [ Blank ] @>

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

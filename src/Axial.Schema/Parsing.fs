// The parse interpreter: walks a schema against boundary Data — shape conversion, constraint
// checks, then the record constructor only when every field passed — collecting path-addressed
// diagnostics. Internal; Schema.parse / parseRetainingInput / check in SchemaApi.fs are the doors.
// (Checking existing values reuses this pipeline with getters as the value source.)
namespace Axial.Schema

open Axial.Parse

open Axial.Data

open System
open System.Globalization
open Axial.Constraint
open Axial.Refined
open Axial.Schema

/// <summary>Options that customize how structured data is parsed through a schema.</summary>
type SchemaParseOptions =
    internal
        {
            ConstructorErrorPath: PathComponent list option
        }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
[<RequireQualifiedAccess>]
module internal SchemaParsing =

    let private diagnosticsPath (path: DataPath) : PathComponent list =
        path
        |> List.map (function
            | DataPathSegment.Name name -> KeyComponent name
            | DataPathSegment.Index index -> IndexComponent index)

    /// <summary>The default input parser options.</summary>
    let defaults =
        { ConstructorErrorPath = None }

    /// <summary>
    /// Attaches model constructor errors to the supplied structured data path instead of the current object path.
    /// </summary>
    /// <remarks>
    /// The path is interpreted relative to the model whose constructor failed. For a root model,
    /// <c>Schema.constructorErrorAt "end"</c> attaches the error to <c>end</c>. For a nested model under
    /// <c>range</c>, the same option attaches the error to <c>range.end</c>.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="path" /> is null.</exception>
    /// <exception cref="T:System.FormatException">Thrown when <paramref name="path" /> is not a valid structured data path.</exception>
    let constructorErrorAt (path: string) (options: SchemaParseOptions) =
        if isNull (box options) then
            nullArg (nameof options)

        { options with
            ConstructorErrorPath = path |> DataPath.parse |> diagnosticsPath |> Some }

    let private diagnosticsAt path error =
        SchemaErrors.singleton (Path path) error

    let private errorAt path error =
        Error(diagnosticsAt path error)

    let private errorAtConstructor options path message =
        let errorPath =
            match options.ConstructorErrorPath with
            | Some relativePath -> path @ relativePath
            | None -> path

        errorAt errorPath (SchemaError.ConstructorFailed message)

    let private mergeErrors errors =
        SchemaErrors.collect errors

    let private allRules definition =
        let rec gather valueDefinition =
            match valueDefinition.Shape with
            | RefinedValueDefinition(raw, _) -> gather raw @ valueDefinition.Rules
            | LazyValueDefinition deferred -> gather (deferred.Force()) @ valueDefinition.Rules
            | PrimitiveValueDefinition _
            | NestedValueDefinition _
            | ManyValueDefinition _
            | UnionValueDefinition _
            | UnionInlineValueDefinition _
            | EnumValueDefinition _
            | OptionValueDefinition _
            | MapValueDefinition _ -> valueDefinition.Rules

        gather definition

    let rec private tryDefaultValue (definition: ValueSchemaDefinition) =
        match definition.Default, definition.Shape with
        | Some value, _ -> Some value
        | None, RefinedValueDefinition(raw, _) -> tryDefaultValue raw
        | None, LazyValueDefinition deferred -> tryDefaultValue (deferred.Force())
        | None, _ -> None

    let rec private isOmittableValue (definition: ValueSchemaDefinition) =
        match definition.Shape with
        | OptionValueDefinition _ -> true
        | RefinedValueDefinition(raw, _) -> isOmittableValue raw
        | LazyValueDefinition deferred -> isOmittableValue (deferred.Force())
        | _ -> false

    let private underlyingPrimitiveKind definition =
        let rec kindOf valueDefinition =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition kind -> kind
            | RefinedValueDefinition(raw, _) -> kindOf raw
            | NestedValueDefinition _ -> invalidOp "Nested model value schemas have no underlying primitive kind."
            | ManyValueDefinition _ -> invalidOp "Collection value schemas have no underlying primitive kind."
            | UnionValueDefinition _ -> invalidOp "Union value schemas have no underlying primitive kind."
            | UnionInlineValueDefinition _ -> invalidOp "Union-inline value schemas have no underlying primitive kind."
            | EnumValueDefinition _ -> invalidOp "Enum value schemas have no underlying primitive kind."
            | OptionValueDefinition _ -> invalidOp "Optional value schemas have no underlying primitive kind."
            | MapValueDefinition _ -> invalidOp "Map value schemas have no underlying primitive kind."
            | LazyValueDefinition _ -> invalidOp "Deferred model value schemas have no underlying primitive kind."

        kindOf definition

    let private constructValue path definition primitive =
        let rec construct valueDefinition value =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition _ -> Ok value
            | RefinedValueDefinition(raw, ops) -> construct raw value |> Result.bind ops.Construct
            | NestedValueDefinition _
            | ManyValueDefinition _
            | UnionValueDefinition _
            | UnionInlineValueDefinition _
            | EnumValueDefinition _
            | OptionValueDefinition _
            | MapValueDefinition _ -> Ok value
            | LazyValueDefinition _ -> Ok value

        construct definition primitive
        |> Result.mapError (fun errors ->
            errors |> List.map (diagnosticsAt path) |> mergeErrors)

    /// Every stored constraint carries the typed closure it was built from, so the value's primitive kind no
    /// longer selects which rules apply.
    let private runRules rules value =
        match ErasedCheck.run rules value with
        | Ok() -> Ok value
        | Error violation -> Error [ SchemaError.Violation violation ]

    let private parsePrimitive kind text =
        match kind with
        | PrimitiveValueKind.Text -> Ok(box text)
        | PrimitiveValueKind.Int -> Parse.int text |> Result.map box |> Result.mapError SchemaError.ofParseError
        | PrimitiveValueKind.Int64 -> Parse.long text |> Result.map box |> Result.mapError SchemaError.ofParseError
        | PrimitiveValueKind.Decimal -> Parse.decimal text |> Result.map box |> Result.mapError SchemaError.ofParseError
        | PrimitiveValueKind.Float -> Parse.float text |> Result.map box |> Result.mapError SchemaError.ofParseError
        | PrimitiveValueKind.Bool -> Parse.bool text |> Result.map box |> Result.mapError SchemaError.ofParseError
#if NET8_0_OR_GREATER
        | PrimitiveValueKind.Date ->
            match DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | true, value -> Ok(box value)
            | false, _ ->
                if String.IsNullOrWhiteSpace text then Error SchemaError.Blank else Error(SchemaError.InvalidFormat "date")
#else
        | PrimitiveValueKind.Date -> Error(SchemaError.InvalidFormat "date")
#endif
        | PrimitiveValueKind.DateTime -> Parse.dateTimeOffset text |> Result.map box |> Result.mapError SchemaError.ofParseError
        | PrimitiveValueKind.Guid -> Parse.guid text |> Result.map box |> Result.mapError SchemaError.ofParseError

    let rec private parseValue options valueSchema fieldRules path raw =
        match valueSchema.Shape with
        | LazyValueDefinition deferred ->
            parseValue options (deferred.Force()) (valueSchema.Rules @ fieldRules) path raw
        | OptionValueDefinition optional ->
            // Absence is a legal parse result for optional values: missing (and JSON null, which structured data adapters
            // lower to Missing) becomes None, while present input parses through the payload schema into Some. The
            // constraints attached to the optional layer and the field run against the payload.
            match raw with
            | Data.Null ->
                let rules = valueSchema.Rules @ fieldRules

                match runRules rules optional.NoneValue with
                | Ok checkedValue -> Ok checkedValue
                | Error errors -> errors |> List.map (diagnosticsAt path) |> mergeErrors |> Error
            | Data.Text _
            | Data.Number _
            | Data.Bool _
            | Data.Object _
            | Data.List _ ->
                parseValue options optional.Payload (valueSchema.Rules @ fieldRules) path raw
                |> Result.map optional.WrapSome
        | RefinedValueDefinition(rawSchema, ops) ->
            let rules = valueSchema.Rules @ fieldRules

            parseValue options rawSchema [] path raw
            |> Result.bind (fun rawValue ->
                ops.Construct rawValue
                |> Result.mapError (fun errors -> errors |> List.map (diagnosticsAt path) |> mergeErrors))
            |> Result.bind (fun value ->
                match runRules rules value with
                | Ok checkedValue -> Ok checkedValue
                | Error errors -> errors |> List.map (diagnosticsAt path) |> mergeErrors |> Error)
        | PrimitiveValueDefinition _
        | NestedValueDefinition _
        | ManyValueDefinition _
        | UnionValueDefinition _
        | UnionInlineValueDefinition _
        | EnumValueDefinition _
        | MapValueDefinition _ -> parsePresentValue options valueSchema fieldRules path raw

    and private parsePresentValue options valueSchema fieldRules path raw =
        let rules = allRules valueSchema @ fieldRules

        match raw with
        | Data.Null -> errorAt path SchemaError.Blank
        | Data.Object fields ->
            let fields = Map.ofList fields

            match valueSchema.Shape with
            | NestedValueDefinition(nestedModel, _) -> parseObject options path nestedModel fields
            | UnionValueDefinition union -> parseUnion options path union fields
            | UnionInlineValueDefinition union -> parseUnionInline options path union fields
            | MapValueDefinition collection -> parseMap options path collection rules fields
            | LazyValueDefinition _ -> parseValue options valueSchema fieldRules path (Data.Object(Map.toList fields))
            | RefinedValueDefinition(raw, _) ->
                match raw.Shape with
                | NestedValueDefinition(nestedModel, _) ->
                    parseObject options path nestedModel fields
                    |> Result.bind (constructValue path valueSchema)
                | UnionValueDefinition union ->
                    parseUnion options path union fields
                    |> Result.bind (constructValue path valueSchema)
                | UnionInlineValueDefinition union ->
                    parseUnionInline options path union fields
                    |> Result.bind (constructValue path valueSchema)
                | MapValueDefinition collection ->
                    parseMap options path collection rules fields
                    |> Result.bind (constructValue path valueSchema)
                | LazyValueDefinition _ ->
                    parseValue options raw [] path (Data.Object(Map.toList fields))
                    |> Result.bind (constructValue path valueSchema)
                | OptionValueDefinition _ ->
                    parseValue options raw [] path (Data.Object(Map.toList fields))
                    |> Result.bind (constructValue path valueSchema)
                | PrimitiveValueDefinition _
                | RefinedValueDefinition _
                | EnumValueDefinition _ -> errorAt path SchemaError.ExpectedScalar
                | ManyValueDefinition _ -> errorAt path SchemaError.ExpectedMany
            | ManyValueDefinition _ -> errorAt path SchemaError.ExpectedMany
            | OptionValueDefinition _ -> invalidOp "Optional value schemas are parsed before structured data dispatch."
            | PrimitiveValueDefinition _
            | EnumValueDefinition _ -> errorAt path SchemaError.ExpectedScalar
        | Data.List rawItems ->
            match valueSchema.Shape with
            | NestedValueDefinition _
            | UnionValueDefinition _
            | UnionInlineValueDefinition _
            | MapValueDefinition _ -> errorAt path SchemaError.ExpectedObject
            | ManyValueDefinition collection -> parseMany options path collection rules rawItems
            | LazyValueDefinition _ -> parseValue options valueSchema fieldRules path (Data.List rawItems)
            | RefinedValueDefinition(raw, _) ->
                match raw.Shape with
                | ManyValueDefinition collection ->
                    parseMany options path collection rules rawItems
                    |> Result.bind (constructValue path valueSchema)
                | LazyValueDefinition _ ->
                    parseValue options raw [] path (Data.List rawItems)
                    |> Result.bind (constructValue path valueSchema)
                | OptionValueDefinition _ ->
                    parseValue options raw [] path (Data.List rawItems)
                    |> Result.bind (constructValue path valueSchema)
                | NestedValueDefinition _
                | UnionValueDefinition _
                | UnionInlineValueDefinition _
                | MapValueDefinition _ -> errorAt path SchemaError.ExpectedObject
                | PrimitiveValueDefinition _
                | RefinedValueDefinition _
                | EnumValueDefinition _ -> errorAt path SchemaError.ExpectedScalar
            | OptionValueDefinition _ -> invalidOp "Optional value schemas are parsed before structured data dispatch."
            | PrimitiveValueDefinition _
            | EnumValueDefinition _ -> errorAt path SchemaError.ExpectedScalar
        | Data.Number token -> parsePresentValue options valueSchema fieldRules path (Data.Text token)
        | Data.Bool value ->
            parsePresentValue options valueSchema fieldRules path (Data.Text(if value then "true" else "false"))
        | Data.Text text ->
            match valueSchema.Shape with
            | NestedValueDefinition _
            | UnionValueDefinition _
            | UnionInlineValueDefinition _
            | MapValueDefinition _ -> errorAt path SchemaError.ExpectedObject
            | ManyValueDefinition _ -> errorAt path SchemaError.ExpectedMany
            | LazyValueDefinition _ -> parseValue options valueSchema fieldRules path (Data.Text text)
            | OptionValueDefinition _ -> invalidOp "Optional value schemas are parsed before structured data dispatch."
            | EnumValueDefinition enum -> parseEnum path enum text
            | RefinedValueDefinition(raw, _) ->
                match raw.Shape with
                | NestedValueDefinition _
                | UnionValueDefinition _
                | UnionInlineValueDefinition _
                | MapValueDefinition _ -> errorAt path SchemaError.ExpectedObject
                | ManyValueDefinition _ -> errorAt path SchemaError.ExpectedMany
                | LazyValueDefinition _ ->
                    parseValue options raw [] path (Data.Text text)
                    |> Result.bind (constructValue path valueSchema)
                | OptionValueDefinition _ ->
                    parseValue options raw [] path (Data.Text text)
                    |> Result.bind (constructValue path valueSchema)
                | EnumValueDefinition enum ->
                    parseEnum path enum text |> Result.bind (constructValue path valueSchema)
                | PrimitiveValueDefinition _
                | RefinedValueDefinition _ ->
                    let kind = underlyingPrimitiveKind valueSchema

                    match parsePrimitive kind text with
                    | Error error -> errorAt path error
                    | Ok primitive ->
                        match runRules rules primitive with
                        | Error errors ->
                            errors
                            |> List.map (diagnosticsAt path)
                            |> mergeErrors
                            |> Error
                        | Ok checkedPrimitive -> constructValue path valueSchema checkedPrimitive
            | PrimitiveValueDefinition _ ->
                let kind = underlyingPrimitiveKind valueSchema

                match parsePrimitive kind text with
                | Error error -> errorAt path error
                | Ok primitive ->
                    match runRules rules primitive with
                    | Error errors ->
                        errors
                        |> List.map (diagnosticsAt path)
                        |> mergeErrors
                        |> Error
                    | Ok checkedPrimitive -> constructValue path valueSchema checkedPrimitive

    and private parseUnion options path (union: TaggedUnionValueDefinition) (fields: Map<string, Data>) =
        let discriminatorName = ExternalFieldName.value union.DiscriminatorField
        let payloadName = ExternalFieldName.value union.PayloadField
        let discriminatorPath = path @ [ KeyComponent discriminatorName ]
        let payloadPath = path @ [ KeyComponent payloadName ]

        match fields |> Map.tryFind discriminatorName with
        | None -> errorAt discriminatorPath SchemaError.Omitted
        | Some Data.Null -> errorAt discriminatorPath SchemaError.Blank
        | Some(Data.Text tag) ->
            match union.Cases |> List.tryFind (fun case -> case.Tag = tag) with
            | None ->
                union.Cases
                |> List.map _.Tag
                |> String.concat "|"
                |> SchemaError.UnknownTag
                |> errorAt discriminatorPath
            | Some case ->
                let parsedPayload =
                    match fields |> Map.tryFind payloadName with
                    | Some payloadRaw -> parseValue options case.Payload [] payloadPath payloadRaw
                    | None ->
                        match tryDefaultValue case.Payload with
                        | Some value -> Ok value
                        | None when isOmittableValue case.Payload -> parseValue options case.Payload [] payloadPath Data.Null
                        | None -> errorAt payloadPath SchemaError.Omitted

                parsedPayload |> Result.map case.Construct
        | Some(Data.Object _)
        | Some(Data.List _) -> errorAt discriminatorPath SchemaError.ExpectedScalar
        | Some(Data.Number token) -> parseUnion options path union (fields |> Map.add discriminatorName (Data.Text token))
        | Some(Data.Bool value) ->
            parseUnion options path union (fields |> Map.add discriminatorName (Data.Text(if value then "true" else "false")))

    and private parseUnionInline options path (union: InlineTaggedUnionValueDefinition) (fields: Map<string, Data>) =
        let discriminatorName = ExternalFieldName.value union.DiscriminatorField
        let discriminatorPath = path @ [ KeyComponent discriminatorName ]

        match fields |> Map.tryFind discriminatorName with
        | None -> errorAt discriminatorPath SchemaError.Omitted
        | Some Data.Null -> errorAt discriminatorPath SchemaError.Blank
        | Some(Data.Text tag) ->
            match union.Cases |> List.tryFind (fun case -> case.Tag = tag) with
            | None ->
                union.Cases
                |> List.map _.Tag
                |> String.concat "|"
                |> SchemaError.UnknownTag
                |> errorAt discriminatorPath
            | Some case ->
                match case.Payload.Shape with
                | NestedValueDefinition(nestedModel, _) ->
                    parseObject options path nestedModel fields |> Result.map case.Construct
                | _ -> invalidOp "Union-inline case payloads must be nested model schemas."
        | Some(Data.Object _)
        | Some(Data.List _) -> errorAt discriminatorPath SchemaError.ExpectedScalar
        | Some(Data.Number token) -> parseUnionInline options path union (fields |> Map.add discriminatorName (Data.Text token))
        | Some(Data.Bool value) ->
            parseUnionInline options path union (fields |> Map.add discriminatorName (Data.Text(if value then "true" else "false")))

    and private parseEnum path (enum: TaggedEnumValueDefinition) (text: string) =
        match enum.Cases |> List.tryFind (fun case -> case.Tag = text) with
        | Some case -> Ok case.Value
        | None ->
            enum.Cases
            |> List.map _.Tag
            |> String.concat "|"
            |> SchemaError.UnknownTag
            |> errorAt path

    and private parseMissingValue options path valueSchema rules =
        let effectiveRules = allRules valueSchema @ rules

        if SchemaRule.trySupply effectiveRules = Some Supply.Supplied then
            errorAt path SchemaError.Omitted
        else
            match tryDefaultValue valueSchema with
            | Some value -> Ok value
            | None when isOmittableValue valueSchema -> parseValue options valueSchema rules path Data.Null
            | None -> errorAt path SchemaError.Omitted

    and private parseNestedField options basePath (fields: Map<string, Data>) (field: FieldDescriptor<obj>) =
        let name = ExternalFieldName.value field.ExternalName
        let path = basePath @ [ KeyComponent name ]
        match fields |> Map.tryFind name with
        | Some raw -> parseValue options field.ValueSchema field.Rules path raw
        | None -> parseMissingValue options path field.ValueSchema field.Rules

    and private parseObject options path (model: ModelSchemaDefinition<obj>) (fields: Map<string, Data>) =
        let parsedFields = model.Fields |> List.map (parseNestedField options path fields)
        let errors = parsedFields |> List.choose (function Error diagnostics -> Some diagnostics | Ok _ -> None)

        match errors with
        | [] ->
            parsedFields
            |> List.map (function Ok value -> value | Error _ -> invalidOp "Unexpected parse error.")
            |> List.toArray
            |> ConstructorApplication.tryApply model.Constructor
            |> function
                | Ok model -> Ok model
                | Error message -> errorAtConstructor options path message
        | diagnostics -> Error(mergeErrors diagnostics)

    and private checkMany rules path value =
        match runRules rules value with
        | Ok checkedValue -> Ok checkedValue
        | Error errors ->
            errors
            |> List.map (diagnosticsAt path)
            |> mergeErrors
            |> Error

    and private parseMany options path (collection: CollectionValueDefinition) rules rawItems =
        let parsedItems =
            rawItems
            |> List.mapi (fun index rawItem -> parseValue options collection.Item [] (path @ [ IndexComponent index ]) rawItem)
        let errors = parsedItems |> List.choose (function Error diagnostics -> Some diagnostics | Ok _ -> None)

        match errors with
        | [] ->
            let items =
                parsedItems
                |> List.map (function Ok value -> value | Error _ -> invalidOp "Unexpected parse error.")

            let collectionValue = collection.BoxItems items

            match checkMany rules path collectionValue with
            | Ok checkedValue -> Ok checkedValue
            | Error diagnostics -> Error diagnostics
        | diagnostics -> Error(mergeErrors diagnostics)

    and private parseMap options path (collection: MapValueDefinition) rules (fields: Map<string, Data>) =
        let entries = fields |> Map.toList

        let parsedEntries =
            entries
            |> List.map (fun (key, rawItem) ->
                key, parseValue options collection.Item [] (path @ [ KeyComponent key ]) rawItem)

        let errors =
            parsedEntries |> List.choose (fun (_, result) -> match result with Error diagnostics -> Some diagnostics | Ok _ -> None)

        match errors with
        | [] ->
            let items = parsedEntries |> List.map (fun (_, result) -> match result with Ok value -> value | Error _ -> invalidOp "Unexpected parse error.")
            let collectionValue = List.zip (entries |> List.map fst) items |> collection.BoxEntries

            match checkMany rules path collectionValue with
            | Ok checkedValue -> Ok checkedValue
            | Error diagnostics -> Error diagnostics
        | diagnostics -> Error(mergeErrors diagnostics)

    let private parseRootField options basePath (fields: Map<string, Data>) (field: FieldDescriptor<'model>) =
        let name = ExternalFieldName.value field.ExternalName
        let path = basePath @ [ KeyComponent name ]
        match fields |> Map.tryFind name with
        | Some raw -> parseValue options field.ValueSchema field.Rules path raw
        | None -> parseMissingValue options path field.ValueSchema field.Rules

    /// <summary>Parses structured boundary data through a trusted model schema using custom input parser options.</summary>
    let private parseWithErrors
        (configure: SchemaParseOptions -> SchemaParseOptions)
        (schema: Schema<'model>)
        (input: Data)
        : Result<'model, SchemaErrors> =
        if isNull (box configure) then
            nullArg (nameof configure)

        if isNull (box schema) then
            nullArg (nameof schema)

        let options = configure defaults

        if isNull (box options) then
            nullArg (nameof configure)

        let result =
            match schema.Definition, input with
            | PendingDefinition, _ -> invalidArg (nameof schema) "Expected a built model schema."
            | ValueDefinition value, raw ->
                parseValue options value [] [] raw |> Result.map unbox<'model>
            | ModelDefinition _, Data.Null -> Error(diagnosticsAt [] SchemaError.ExpectedObject)
            | ModelDefinition _, Data.Text _ -> Error(diagnosticsAt [] SchemaError.ExpectedObject)
            | ModelDefinition _, Data.Number _ -> Error(diagnosticsAt [] SchemaError.ExpectedObject)
            | ModelDefinition _, Data.Bool _ -> Error(diagnosticsAt [] SchemaError.ExpectedObject)
            | ModelDefinition _, Data.List _ -> Error(diagnosticsAt [] SchemaError.ExpectedObject)
            | ModelDefinition model, Data.Object fields ->
                let parsedFields = model.Fields |> List.map (parseRootField options [] (Map.ofList fields))
                let errors = parsedFields |> List.choose (function Error diagnostics -> Some diagnostics | Ok _ -> None)

                match errors with
                | [] ->
                    parsedFields
                    |> List.map (function Ok value -> value | Error _ -> invalidOp "Unexpected parse error.")
                    |> List.toArray
                    |> ConstructorApplication.tryApply model.Constructor
                    |> function
                        | Ok model -> Ok model
                        | Error message -> errorAtConstructor options [] message
                | diagnostics -> Error(mergeErrors diagnostics)

        result

    /// <summary>Parses structured boundary data through a trusted model schema using custom input parser options.</summary>
    let parseWith
        (configure: SchemaParseOptions -> SchemaParseOptions)
        (schema: Schema<'model>)
        (input: Data)
        : Result<'model, SchemaErrors> =
        parseWithErrors configure schema input

    /// <summary>Parses structured boundary data through a trusted model schema.</summary>
    let parse (schema: Schema<'model>) (input: Data) : Result<'model, SchemaErrors> =
        parseWith id schema input

    /// <summary>Parses structured boundary data while retaining that input for redisplay and error lookup.</summary>
    let parseRetainingInput (schema: Schema<'model>) (input: Data) : RetainedParseResult<'model> =
        parse schema input |> RetainedParseResult.create input

    /// <summary>
    /// Parses structured boundary data through a trusted model schema using custom input parser options, expressed as a
    /// .NET delegate.
    /// </summary>
    /// <remarks>
    /// A C#-friendly equivalent of <c>parseWith</c>: takes <see cref="T:System.Func`2" /> instead of an F# function
    /// value, so callers do not need to construct an <c>FSharpFunc</c>.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="configure" /> or <paramref name="schema" /> is null.</exception>
    let parseWithOptions
        (configure: System.Func<SchemaParseOptions, SchemaParseOptions>)
        (schema: Schema<'model>)
        (input: Data)
        : Result<'model, SchemaErrors> =
        if isNull (box configure) then
            nullArg (nameof configure)

        parseWith configure.Invoke schema input

    /// <summary>
    /// Checks an existing value whose construction history is uncertain, such as a value produced by a serializer or
    /// database mapper.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs every field's schema constraints through the same executable checks <c>Schema.parse</c> uses, then
    /// re-invokes a record schema's constructor with the checked field values. This includes cross-field constructor
    /// invariants such as a date range's "start must not be after end" rule.
    /// </para>
    /// <para>
    /// This operation returns the original value on success; it does not create a durable proof wrapper. Prefer a
    /// private representation and complete smart constructor when every value in application code must satisfy an
    /// invariant.
    /// </para>
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> is not a built model schema.</exception>
    let check (schema: Schema<'model>) (model: 'model) : Result<'model, SchemaErrors> =
        if isNull (box schema) then
            nullArg (nameof schema)

        let result =
            match schema.Definition with
            | PendingDefinition -> invalidArg (nameof schema) "Expected a built model schema."
            | ValueDefinition _ ->
                ModelFieldCheck.check schema model |> SchemaResult.toResult
            | ModelDefinition modelSchema ->
                match ModelFieldCheck.check schema model |> SchemaResult.toResult with
                | Error diagnostics -> Error diagnostics
                | Ok checkedModel ->
                    let arguments =
                        modelSchema.Fields
                        |> List.map (fun field -> field.Getter checkedModel)
                        |> List.toArray

                    match modelSchema.Constructor.TryApplyTrusted arguments with
                    | Ok _ -> Ok model
                    | Error message -> errorAtConstructor defaults [] message

        result

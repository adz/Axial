namespace Axial.Validation.Schema

open System
open System.Globalization
open Axial.ErrorHandling
open Axial.Refined
open Axial.Schema
open Axial.Validation

/// <summary>Functions for parsing raw input through a schema.</summary>
[<RequireQualifiedAccess>]
module Input =
    /// <summary>Options that customize how raw input is parsed through a schema.</summary>
    type Options =
        internal
            {
                ConstructorErrorPath: Path option
            }

    /// <summary>The default input parser options.</summary>
    let defaults =
        { ConstructorErrorPath = None }

    /// <summary>
    /// Attaches model constructor errors to the supplied raw input path instead of the current object path.
    /// </summary>
    /// <remarks>
    /// The path is interpreted relative to the model whose constructor failed. For a root model,
    /// <c>Input.constructorErrorAt "end"</c> attaches the error to <c>end</c>. For a nested model under
    /// <c>range</c>, the same option attaches the error to <c>range.end</c>.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="path" /> is null.</exception>
    /// <exception cref="T:System.FormatException">Thrown when <paramref name="path" /> is not a valid raw input path.</exception>
    let constructorErrorAt (path: string) (options: Options) =
        if isNull (box options) then
            nullArg (nameof options)

        { options with
            ConstructorErrorPath = path |> InputPath.parse |> InputPath.toDiagnosticsPath |> Some }

    let private diagnosticsAt path error =
        Validation.fail (Diagnostics.singleton error)
        |> Validation.at path
        |> Validation.toResult
        |> function
            | Error diagnostics -> diagnostics
            | Ok _ -> Diagnostics.empty

    let private errorAt path error =
        Error(diagnosticsAt path error)

    let private errorAtConstructor options path message =
        let errorPath =
            match options.ConstructorErrorPath with
            | Some relativePath -> path @ relativePath
            | None -> path

        errorAt errorPath (SchemaError.ConstructorFailed message)

    let private mergeErrors diagnostics =
        diagnostics
        |> List.reduce Diagnostics.merge

    let private allConstraints definition =
        let rec gather valueDefinition =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition _ -> valueDefinition.Constraints
            | RefinedValueDefinition(raw, _) -> gather raw @ valueDefinition.Constraints
            | NestedValueDefinition _ -> valueDefinition.Constraints
            | ManyValueDefinition _ -> valueDefinition.Constraints
            | UnionValueDefinition _ -> valueDefinition.Constraints
            | UnionInlineValueDefinition _ -> valueDefinition.Constraints
            | EnumValueDefinition _ -> valueDefinition.Constraints
            | OptionValueDefinition _ -> valueDefinition.Constraints

        gather definition

    let private hasRequiredConstraint constraints =
        constraints
        |> List.exists (fun constraint' -> SchemaConstraint.code constraint' = "required")

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

        kindOf definition

    let private constructValue definition primitive =
        let rec construct valueDefinition value =
            match valueDefinition.Shape with
            | PrimitiveValueDefinition _ -> value
            | RefinedValueDefinition(raw, ops) -> construct raw value |> ops.Construct
            | NestedValueDefinition _
            | ManyValueDefinition _
            | UnionValueDefinition _
            | UnionInlineValueDefinition _
            | EnumValueDefinition _
            | OptionValueDefinition _ -> value

        construct definition primitive

    let private runCheck constraints check value =
        match check value with
        | Ok () -> Ok value
        | Error failures -> failures |> SchemaCheckFailure.toSchemaErrors constraints |> Error

    let private checkPrimitive kind constraints value =
        match kind with
        | PrimitiveValueKind.Text ->
            value
            |> unbox<string>
            |> runCheck constraints (SchemaConstraintCheck.text constraints)
            |> Result.map box
        | PrimitiveValueKind.Int ->
            value
            |> unbox<int>
            |> runCheck constraints (SchemaConstraintCheck.ordered<int> constraints)
            |> Result.map box
        | PrimitiveValueKind.Decimal ->
            value
            |> unbox<decimal>
            |> runCheck constraints (SchemaConstraintCheck.ordered<decimal> constraints)
            |> Result.map box
        | PrimitiveValueKind.Bool -> Ok value
#if NET8_0_OR_GREATER
        | PrimitiveValueKind.Date ->
            value
            |> unbox<DateOnly>
            |> runCheck constraints (SchemaConstraintCheck.ordered<DateOnly> constraints)
            |> Result.map box
#else
        | PrimitiveValueKind.Date -> Ok value
#endif
        | PrimitiveValueKind.DateTime ->
            value
            |> unbox<DateTimeOffset>
            |> runCheck constraints (SchemaConstraintCheck.ordered<DateTimeOffset> constraints)
            |> Result.map box
        | PrimitiveValueKind.Guid -> Ok value

    let private parsePrimitive kind text =
        match kind with
        | PrimitiveValueKind.Text -> Ok(box text)
        | PrimitiveValueKind.Int -> Parse.int text |> Result.map box |> Result.mapError SchemaError.ofParseError
        | PrimitiveValueKind.Decimal -> Parse.decimal text |> Result.map box |> Result.mapError SchemaError.ofParseError
        | PrimitiveValueKind.Bool -> Parse.bool text |> Result.map box |> Result.mapError SchemaError.ofParseError
#if NET8_0_OR_GREATER
        | PrimitiveValueKind.Date ->
            match DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None) with
            | true, value -> Ok(box value)
            | false, _ ->
                if String.IsNullOrWhiteSpace text then Error SchemaError.Required else Error(SchemaError.InvalidFormat "date")
#else
        | PrimitiveValueKind.Date -> Error(SchemaError.InvalidFormat "date")
#endif
        | PrimitiveValueKind.DateTime -> Parse.dateTimeOffset text |> Result.map box |> Result.mapError SchemaError.ofParseError
        | PrimitiveValueKind.Guid -> Parse.guid text |> Result.map box |> Result.mapError SchemaError.ofParseError

    let rec private parseValue options valueSchema fieldConstraints path raw =
        match valueSchema.Shape with
        | OptionValueDefinition optional ->
            // Absence is a legal parse result for optional values: missing (and JSON null, which raw input adapters
            // lower to Missing) becomes None, while present input parses through the payload schema into Some. The
            // constraints attached to the optional layer and the field run against the payload.
            match raw with
            | RawInput.Missing -> Ok optional.NoneValue
            | RawInput.Scalar _
            | RawInput.Object _
            | RawInput.Many _ ->
                parseValue options optional.Payload (valueSchema.Constraints @ fieldConstraints) path raw
                |> Result.map optional.WrapSome
        | PrimitiveValueDefinition _
        | RefinedValueDefinition _
        | NestedValueDefinition _
        | ManyValueDefinition _
        | UnionValueDefinition _
        | UnionInlineValueDefinition _
        | EnumValueDefinition _ -> parsePresentValue options valueSchema fieldConstraints path raw

    and private parsePresentValue options valueSchema fieldConstraints path raw =
        let constraints = allConstraints valueSchema @ fieldConstraints

        match raw with
        | RawInput.Missing ->
            errorAt path (SchemaCheckFailure.withCustomMessageForCode constraints "required" SchemaError.Required)
        | RawInput.Object fields ->
            match valueSchema.Shape with
            | NestedValueDefinition(nestedModel, _) -> parseObject options path nestedModel fields
            | UnionValueDefinition union -> parseUnion options path union fields
            | UnionInlineValueDefinition union -> parseUnionInline options path union fields
            | RefinedValueDefinition(raw, _) ->
                match raw.Shape with
                | NestedValueDefinition(nestedModel, _) ->
                    parseObject options path nestedModel fields
                    |> Result.map (constructValue valueSchema)
                | UnionValueDefinition union ->
                    parseUnion options path union fields
                    |> Result.map (constructValue valueSchema)
                | UnionInlineValueDefinition union ->
                    parseUnionInline options path union fields
                    |> Result.map (constructValue valueSchema)
                | OptionValueDefinition _ ->
                    parseValue options raw [] path (RawInput.Object fields)
                    |> Result.map (constructValue valueSchema)
                | PrimitiveValueDefinition _
                | RefinedValueDefinition _
                | EnumValueDefinition _ -> errorAt path SchemaError.ExpectedScalar
                | ManyValueDefinition _ -> errorAt path SchemaError.ExpectedMany
            | ManyValueDefinition _ -> errorAt path SchemaError.ExpectedMany
            | OptionValueDefinition _ -> invalidOp "Optional value schemas are parsed before raw input dispatch."
            | PrimitiveValueDefinition _
            | EnumValueDefinition _ -> errorAt path SchemaError.ExpectedScalar
        | RawInput.Many rawItems ->
            match valueSchema.Shape with
            | NestedValueDefinition _
            | UnionValueDefinition _
            | UnionInlineValueDefinition _ -> errorAt path SchemaError.ExpectedObject
            | ManyValueDefinition collection -> parseMany options path collection constraints rawItems
            | RefinedValueDefinition(raw, _) ->
                match raw.Shape with
                | ManyValueDefinition collection ->
                    parseMany options path collection constraints rawItems
                    |> Result.map (constructValue valueSchema)
                | OptionValueDefinition _ ->
                    parseValue options raw [] path (RawInput.Many rawItems)
                    |> Result.map (constructValue valueSchema)
                | NestedValueDefinition _
                | UnionValueDefinition _
                | UnionInlineValueDefinition _ -> errorAt path SchemaError.ExpectedObject
                | PrimitiveValueDefinition _
                | RefinedValueDefinition _
                | EnumValueDefinition _ -> errorAt path SchemaError.ExpectedScalar
            | OptionValueDefinition _ -> invalidOp "Optional value schemas are parsed before raw input dispatch."
            | PrimitiveValueDefinition _
            | EnumValueDefinition _ -> errorAt path SchemaError.ExpectedScalar
        | RawInput.Scalar text when hasRequiredConstraint constraints && String.IsNullOrWhiteSpace text ->
            errorAt path (SchemaCheckFailure.withCustomMessageForCode constraints "required" SchemaError.Required)
        | RawInput.Scalar text ->
            match valueSchema.Shape with
            | NestedValueDefinition _
            | UnionValueDefinition _
            | UnionInlineValueDefinition _ -> errorAt path SchemaError.ExpectedObject
            | ManyValueDefinition _ -> errorAt path SchemaError.ExpectedMany
            | OptionValueDefinition _ -> invalidOp "Optional value schemas are parsed before raw input dispatch."
            | EnumValueDefinition enum -> parseEnum path enum text
            | RefinedValueDefinition(raw, _) ->
                match raw.Shape with
                | NestedValueDefinition _
                | UnionValueDefinition _
                | UnionInlineValueDefinition _ -> errorAt path SchemaError.ExpectedObject
                | ManyValueDefinition _ -> errorAt path SchemaError.ExpectedMany
                | OptionValueDefinition _ ->
                    parseValue options raw [] path (RawInput.Scalar text)
                    |> Result.map (constructValue valueSchema)
                | EnumValueDefinition enum ->
                    parseEnum path enum text |> Result.map (constructValue valueSchema)
                | PrimitiveValueDefinition _
                | RefinedValueDefinition _ ->
                    let kind = underlyingPrimitiveKind valueSchema

                    match parsePrimitive kind text with
                    | Error error -> errorAt path error
                    | Ok primitive ->
                        match checkPrimitive kind constraints primitive with
                        | Error errors ->
                            errors
                            |> List.map (diagnosticsAt path)
                            |> mergeErrors
                            |> Error
                        | Ok checkedPrimitive -> Ok(constructValue valueSchema checkedPrimitive)
            | PrimitiveValueDefinition _ ->
                let kind = underlyingPrimitiveKind valueSchema

                match parsePrimitive kind text with
                | Error error -> errorAt path error
                | Ok primitive ->
                    match checkPrimitive kind constraints primitive with
                    | Error errors ->
                        errors
                        |> List.map (diagnosticsAt path)
                        |> mergeErrors
                        |> Error
                    | Ok checkedPrimitive -> Ok(constructValue valueSchema checkedPrimitive)

    and private parseUnion options path (union: TaggedUnionValueDefinition) (fields: Map<string, RawInput>) =
        let discriminatorName = ExternalFieldName.value union.DiscriminatorField
        let payloadName = ExternalFieldName.value union.PayloadField
        let discriminatorPath = path @ [ PathSegment.Name discriminatorName ]
        let payloadPath = path @ [ PathSegment.Name payloadName ]

        match fields |> Map.tryFind discriminatorName |> Option.defaultValue RawInput.Missing with
        | RawInput.Missing -> errorAt discriminatorPath SchemaError.Required
        | RawInput.Scalar tag ->
            match union.Cases |> List.tryFind (fun case -> case.Tag = tag) with
            | None ->
                union.Cases
                |> List.map _.Tag
                |> String.concat "|"
                |> SchemaError.NotOneOf
                |> errorAt discriminatorPath
            | Some case ->
                let payloadRaw = fields |> Map.tryFind payloadName |> Option.defaultValue RawInput.Missing

                parseValue options case.Payload [] payloadPath payloadRaw
                |> Result.map case.Construct
        | RawInput.Object _
        | RawInput.Many _ -> errorAt discriminatorPath SchemaError.ExpectedScalar

    and private parseUnionInline options path (union: InlineTaggedUnionValueDefinition) (fields: Map<string, RawInput>) =
        let discriminatorName = ExternalFieldName.value union.DiscriminatorField
        let discriminatorPath = path @ [ PathSegment.Name discriminatorName ]

        match fields |> Map.tryFind discriminatorName |> Option.defaultValue RawInput.Missing with
        | RawInput.Missing -> errorAt discriminatorPath SchemaError.Required
        | RawInput.Scalar tag ->
            match union.Cases |> List.tryFind (fun case -> case.Tag = tag) with
            | None ->
                union.Cases
                |> List.map _.Tag
                |> String.concat "|"
                |> SchemaError.NotOneOf
                |> errorAt discriminatorPath
            | Some case ->
                match case.Payload.Shape with
                | NestedValueDefinition(nestedModel, _) ->
                    parseObject options path nestedModel fields |> Result.map case.Construct
                | _ -> invalidOp "Union-inline case payloads must be nested model schemas."
        | RawInput.Object _
        | RawInput.Many _ -> errorAt discriminatorPath SchemaError.ExpectedScalar

    and private parseEnum path (enum: TaggedEnumValueDefinition) (text: string) =
        match enum.Cases |> List.tryFind (fun case -> case.Tag = text) with
        | Some case -> Ok case.Value
        | None ->
            enum.Cases
            |> List.map _.Tag
            |> String.concat "|"
            |> SchemaError.NotOneOf
            |> errorAt path

    and private parseNestedField options basePath (fields: Map<string, RawInput>) (field: FieldDescriptor<obj>) =
        let name = ExternalFieldName.value field.ExternalName
        let path = basePath @ [ PathSegment.Name name ]
        let raw = fields |> Map.tryFind name |> Option.defaultValue RawInput.Missing
        parseValue options field.ValueSchema field.Constraints path raw

    and private parseObject options path (model: ModelSchemaDefinition<obj>) (fields: Map<string, RawInput>) =
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

    and private checkMany constraints path items =
        match items |> runCheck constraints (SchemaConstraintCheck.sequence<obj> constraints) with
        | Ok checkedItems -> Ok checkedItems
        | Error errors ->
            errors
            |> List.map (diagnosticsAt path)
            |> mergeErrors
            |> Error

    and private parseMany options path (collection: CollectionValueDefinition) constraints rawItems =
        let parsedItems =
            rawItems
            |> List.mapi (fun index rawItem -> parseValue options collection.Item [] (path @ [ PathSegment.Index index ]) rawItem)
        let errors = parsedItems |> List.choose (function Error diagnostics -> Some diagnostics | Ok _ -> None)

        match errors with
        | [] ->
            let items =
                parsedItems
                |> List.map (function Ok value -> value | Error _ -> invalidOp "Unexpected parse error.")

            match checkMany constraints path items with
            | Ok checkedItems -> checkedItems |> collection.BoxItems |> Ok
            | Error diagnostics -> Error diagnostics
        | diagnostics -> Error(mergeErrors diagnostics)

    let private parseRootField options basePath (fields: Map<string, RawInput>) (field: FieldDescriptor<'model>) =
        let name = ExternalFieldName.value field.ExternalName
        let path = basePath @ [ PathSegment.Name name ]
        let raw = fields |> Map.tryFind name |> Option.defaultValue RawInput.Missing
        parseValue options field.ValueSchema field.Constraints path raw

    /// <summary>Parses raw boundary input through a trusted model schema using custom input parser options.</summary>
    let parseWith (configure: Options -> Options) (schema: Schema<'model>) (input: RawInput) : ParsedInput<'model, SchemaError> =
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
            | ModelDefinition _, RawInput.Missing -> Error(diagnosticsAt [] SchemaError.ExpectedObject)
            | ModelDefinition _, RawInput.Scalar _ -> Error(diagnosticsAt [] SchemaError.ExpectedObject)
            | ModelDefinition _, RawInput.Many _ -> Error(diagnosticsAt [] SchemaError.ExpectedObject)
            | ModelDefinition model, RawInput.Object fields ->
                let parsedFields = model.Fields |> List.map (parseRootField options [] fields)
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

        { Input = input; Result = result }

    /// <summary>Parses raw boundary input through a trusted model schema.</summary>
    let parse (schema: Schema<'model>) (input: RawInput) : ParsedInput<'model, SchemaError> =
        parseWith id schema input

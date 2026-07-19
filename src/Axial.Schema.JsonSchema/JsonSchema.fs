namespace Axial.Schema

open System
open System.Globalization
open System.Text

/// <summary>Generates JSON Schema documents from built model schemas.</summary>
/// <remarks>
/// <para>
/// The generator is a pure interpreter over <see cref="T:Axial.Schema.Inspect" /> descriptions: it lowers shapes,
/// declared formats, and portable constraint metadata to JSON Schema keywords without parsing input, running checks,
/// or constructing models. One schema declaration therefore drives parsing, validation, and the published contract.
/// </para>
/// <para>
/// Lowering rules: primitives map to <c>type</c> (with <c>format</c> for dates, date-times, and uuids), refined values
/// lower to their underlying primitive representation, nested models to <c>object</c> with <c>properties</c> and
/// <c>required</c>, collections to <c>array</c> with <c>items</c>, maps to <c>object</c> with
/// <c>additionalProperties</c>, and tagged unions to <c>oneOf</c> with a <c>const</c>-constrained discriminator
/// property per case. Constraint metadata lowers to <c>minLength</c>, <c>maxLength</c>, <c>pattern</c>, <c>enum</c>,
/// <c>minimum</c>/<c>maximum</c> (and exclusive variants), <c>multipleOf</c>, <c>minItems</c>/<c>maxItems</c>, and
/// <c>uniqueItems</c>; constraints without a JSON Schema equivalent, such as <c>trimmed</c>, are skipped.
/// Default-value metadata attached with <c>Schema.withDefault</c> lowers to <c>default</c>.
/// </para>
/// </remarks>
[<RequireQualifiedAccess>]
module JsonSchema =
    let private escape (text: string) =
        let builder = StringBuilder(text.Length + 2)

        text
        |> Seq.iter (fun c ->
            match c with
            | '"' -> builder.Append "\\\"" |> ignore
            | '\\' -> builder.Append "\\\\" |> ignore
            | '\b' -> builder.Append "\\b" |> ignore
            | '\f' -> builder.Append "\\f" |> ignore
            | '\n' -> builder.Append "\\n" |> ignore
            | '\r' -> builder.Append "\\r" |> ignore
            | '\t' -> builder.Append "\\t" |> ignore
            | c when int c < 32 -> builder.AppendFormat("\\u{0:x4}", int c) |> ignore
            | c -> builder.Append c |> ignore)

        builder.ToString()

    let private literal (value: obj) =
        match value with
        | :? string as text -> sprintf "\"%s\"" (escape text)
        | :? bool as flag -> if flag then "true" else "false"
        | other -> Convert.ToString(other, CultureInfo.InvariantCulture)

    /// Collects the constraint metadata visible at a boundary: the layer's own constraints plus every refinement
    /// layer down to the primitive foundation.
    let rec private boundaryConstraints (description: SchemaDescription) : ConstraintMetadata list =
        let own = description.Constraints |> List.map _.Metadata

        match description.Shape with
        | SchemaShape.Refined underlying -> own @ boundaryConstraints underlying
        | _ -> own

    let rec private boundaryFormat (description: SchemaDescription) : SchemaFormat option =
        match description.Format, description.Shape with
        | Some format, _ -> Some format
        | None, SchemaShape.Refined underlying -> boundaryFormat underlying
        | None, _ -> None

    let rec private underlyingShape (description: SchemaDescription) : SchemaShape =
        match description.Shape with
        | SchemaShape.Refined underlying -> underlyingShape underlying
        | shape -> shape

    let private constraintKeywords (constraints: ConstraintMetadata list) =
        constraints
        |> List.collect (fun metadata ->
            match metadata with
            | ConstraintMetadata.MinLength minimum -> [ sprintf "\"minLength\":%d" minimum ]
            | ConstraintMetadata.MaxLength maximum -> [ sprintf "\"maxLength\":%d" maximum ]
            | ConstraintMetadata.LengthBetween(minimum, maximum) ->
                [ sprintf "\"minLength\":%d" minimum; sprintf "\"maxLength\":%d" maximum ]
            | ConstraintMetadata.Email -> [ "\"format\":\"email\"" ]
            | ConstraintMetadata.Trimmed -> []
            | ConstraintMetadata.Pattern pattern -> [ sprintf "\"pattern\":%s" (literal pattern) ]
            | ConstraintMetadata.OneOf choices ->
                [ choices |> List.map literal |> String.concat "," |> sprintf "\"enum\":[%s]" ]
            | ConstraintMetadata.NotEqualTo _ -> []
            | ConstraintMetadata.Between(minimum, maximum) ->
                [ sprintf "\"minimum\":%s" (literal minimum); sprintf "\"maximum\":%s" (literal maximum) ]
            | ConstraintMetadata.GreaterThan minimum -> [ sprintf "\"exclusiveMinimum\":%s" (literal minimum) ]
            | ConstraintMetadata.LessThan maximum -> [ sprintf "\"exclusiveMaximum\":%s" (literal maximum) ]
            | ConstraintMetadata.AtLeast minimum -> [ sprintf "\"minimum\":%s" (literal minimum) ]
            | ConstraintMetadata.AtMost maximum -> [ sprintf "\"maximum\":%s" (literal maximum) ]
            | ConstraintMetadata.Count expected ->
                [ sprintf "\"minItems\":%d" expected; sprintf "\"maxItems\":%d" expected ]
            | ConstraintMetadata.MinCount minimum -> [ sprintf "\"minItems\":%d" minimum ]
            | ConstraintMetadata.MaxCount maximum -> [ sprintf "\"maxItems\":%d" maximum ]
            | ConstraintMetadata.CountBetween(minimum, maximum) ->
                [ sprintf "\"minItems\":%d" minimum; sprintf "\"maxItems\":%d" maximum ]
            | ConstraintMetadata.Distinct -> [ "\"uniqueItems\":true" ]
            | ConstraintMetadata.Contains item ->
                [ sprintf "\"contains\":{\"const\":%s}" (literal item) ]
            | ConstraintMetadata.MultipleOf divisor -> [ sprintf "\"multipleOf\":%s" (literal divisor) ]
            | ConstraintMetadata.Required
            | ConstraintMetadata.Optional
            | ConstraintMetadata.Custom _ -> [])

    let private primitiveKeywords kind =
        match kind with
        | PrimitiveValueKind.Text -> [ "\"type\":\"string\"" ]
        | PrimitiveValueKind.Int -> [ "\"type\":\"integer\"" ]
        | PrimitiveValueKind.Decimal -> [ "\"type\":\"number\"" ]
        | PrimitiveValueKind.Bool -> [ "\"type\":\"boolean\"" ]
        | PrimitiveValueKind.Date -> [ "\"type\":\"string\""; "\"format\":\"date\"" ]
        | PrimitiveValueKind.DateTime -> [ "\"type\":\"string\""; "\"format\":\"date-time\"" ]
        | PrimitiveValueKind.Guid -> [ "\"type\":\"string\""; "\"format\":\"uuid\"" ]

    let rec private boundaryDescription (description: SchemaDescription) : string option =
        match description.Description, description.Shape with
        | Some text, _ -> Some text
        | None, SchemaShape.Refined underlying -> boundaryDescription underlying
        | None, _ -> None

    let rec private boundaryDefault (description: SchemaDescription) : obj option =
        match description.Default, description.Shape with
        | Some value, _ -> Some value
        | None, SchemaShape.Refined underlying -> boundaryDefault underlying
        | None, _ -> None

    let rec private valueKeywords (fieldConstraints: ConstraintMetadata list) (description: SchemaDescription) =
        let constraints = fieldConstraints @ boundaryConstraints description

        let formatKeyword =
            match boundaryFormat description with
            | Some format when constraints |> List.contains ConstraintMetadata.Email |> not ->
                [ sprintf "\"format\":\"%s\"" (escape format.Name) ]
            | _ -> []

        let descriptionKeyword =
            match boundaryDescription description with
            | Some text -> [ sprintf "\"description\":%s" (literal text) ]
            | None -> []

        let defaultKeyword =
            match boundaryDefault description with
            | Some value ->
                let rendered =
                    match underlyingShape description with
                    | SchemaShape.Enum enum ->
                        // The stored default is the typed enum value; the document needs its wire tag.
                        let name = Convert.ToString(value, CultureInfo.InvariantCulture)

                        enum.Cases
                        |> List.tryFind (fun case -> String.Equals(case.Tag, name, StringComparison.OrdinalIgnoreCase))
                        |> Option.map (fun case -> literal (box case.Tag))
                        |> Option.defaultValue (literal (box name))
                    | _ -> literal value

                [ sprintf "\"default\":%s" rendered ]
            | None -> []

        let shapeKeywords =
            match underlyingShape description with
            | SchemaShape.Primitive kind ->
                primitiveKeywords kind @ formatKeyword @ constraintKeywords constraints |> List.distinct
            | SchemaShape.Nested model -> modelKeywords model
            | SchemaShape.Many item ->
                [ "\"type\":\"array\""
                  sprintf "\"items\":{%s}" (valueKeywords [] item |> String.concat ",") ]
                @ constraintKeywords constraints
            | SchemaShape.Union union ->
                let cases =
                    union.Cases
                    |> List.map (fun case ->
                        let payload = valueKeywords [] case.Payload |> String.concat ","

                        sprintf
                            "{\"type\":\"object\",\"properties\":{\"%s\":{\"const\":%s},\"%s\":{%s}},\"required\":[\"%s\",\"%s\"]}"
                            (escape union.DiscriminatorField)
                            (literal case.Tag)
                            (escape union.PayloadField)
                            payload
                            (escape union.DiscriminatorField)
                            (escape union.PayloadField))
                    |> String.concat ","

                [ sprintf "\"oneOf\":[%s]" cases ]
            | SchemaShape.UnionInline union ->
                let cases =
                    union.Cases
                    |> List.map (fun case -> inlineCaseKeywords union.DiscriminatorField case.Tag case.Payload)
                    |> String.concat ","

                [ sprintf "\"oneOf\":[%s]" cases ]
            | SchemaShape.Enum enum ->
                let tags = enum.Cases |> List.map (fun case -> literal case.Tag) |> String.concat ","
                [ "\"type\":\"string\""; sprintf "\"enum\":[%s]" tags ] @ constraintKeywords constraints
            | SchemaShape.Optional payload -> valueKeywords constraints payload
            | SchemaShape.MapOf item ->
                [ "\"type\":\"object\""
                  sprintf "\"additionalProperties\":{%s}" (valueKeywords [] item |> String.concat ",") ]
                @ constraintKeywords constraints
            | SchemaShape.Deferred(reference, _) -> [ sprintf "\"$ref\":\"#/$defs/recursive%d\"" reference ]
            | SchemaShape.Recursive reference -> [ sprintf "\"$ref\":\"#/$defs/recursive%d\"" reference ]
            | SchemaShape.Refined _ -> failwith "underlyingShape never returns a refined shape."

        descriptionKeyword @ defaultKeyword @ shapeKeywords

    /// An optional field stays out of the object's `required` list; every other field is required, matching the
    /// parser, which requires every non-optional field regardless of the `required` constraint.
    and private isOptionalDescription (description: SchemaDescription) =
        match description.Shape with
        | SchemaShape.Optional _ -> true
        | SchemaShape.Refined underlying -> isOptionalDescription underlying
        | SchemaShape.Deferred(_, value) -> isOptionalDescription value
        | SchemaShape.Primitive _
        | SchemaShape.Nested _
        | SchemaShape.Many _
        | SchemaShape.Union _
        | SchemaShape.UnionInline _
        | SchemaShape.Enum _
        | SchemaShape.MapOf _ -> false
        | SchemaShape.Recursive _ -> false

    and private inlineCaseKeywords (discriminatorField: string) (tag: string) (model: ModelDescription) =
        let discriminatorProperty = sprintf "\"%s\":{\"const\":%s}" (escape discriminatorField) (literal tag)

        let payloadProperties =
            model.Fields
            |> List.map (fun field ->
                let constraints = field.Constraints |> List.map _.Metadata
                sprintf "\"%s\":{%s}" (escape field.Name) (valueKeywords constraints field.Schema |> String.concat ","))

        let properties = discriminatorProperty :: payloadProperties |> String.concat ","

        let required =
            escape discriminatorField
            :: (model.Fields
                |> List.filter (fun field -> not (isOptionalDescription field.Schema))
                |> List.map (fun field -> escape field.Name))
            |> List.map (sprintf "\"%s\"")
            |> String.concat ","

        sprintf "{\"type\":\"object\",\"properties\":{%s},\"required\":[%s]}" properties required

    and private modelKeywords (model: ModelDescription) =
        let properties =
            model.Fields
            |> List.map (fun field ->
                let constraints = field.Constraints |> List.map _.Metadata

                sprintf "\"%s\":{%s}" (escape field.Name) (valueKeywords constraints field.Schema |> String.concat ","))
            |> String.concat ","

        let required =
            model.Fields
            |> List.filter (fun field -> not (isOptionalDescription field.Schema))
            |> List.map (fun field -> sprintf "\"%s\"" (escape field.Name))

        (match model.Description with
         | Some text -> [ sprintf "\"title\":%s" (literal text) ]
         | None -> [])
        @ [ "\"type\":\"object\""; sprintf "\"properties\":{%s}" properties ]
        @ (if List.isEmpty required then
               []
           else
               [ sprintf "\"required\":[%s]" (String.concat "," required) ])

    /// <summary>The JSON Schema draft 2020-12 meta-schema URI pinned as every generated document's <c>$schema</c>.</summary>
    [<Literal>]
    let private draft2020_12 = "https://json-schema.org/draft/2020-12/schema"

    let private deferredDefinitions (roots: SchemaDescription list) =
        let found = System.Collections.Generic.Dictionary<int, SchemaDescription>()

        let rec visitValue description =
            match description.Shape with
            | SchemaShape.Deferred(reference, value) ->
                if not (found.ContainsKey reference) then
                    found.Add(reference, value)
                    visitValue value
            | SchemaShape.Refined value
            | SchemaShape.Many value
            | SchemaShape.Optional value
            | SchemaShape.MapOf value -> visitValue value
            | SchemaShape.Nested model -> visitModel model
            | SchemaShape.Union union -> union.Cases |> List.iter (fun case -> visitValue case.Payload)
            | SchemaShape.UnionInline union -> union.Cases |> List.iter (fun case -> visitModel case.Payload)
            | SchemaShape.Primitive _
            | SchemaShape.Enum _
            | SchemaShape.Recursive _ -> ()

        and visitModel model = model.Fields |> List.iter (fun field -> visitValue field.Schema)

        roots |> List.iter visitValue

        found
        |> Seq.sortBy _.Key
        |> Seq.map (fun pair ->
            sprintf "\"recursive%d\":{%s}" pair.Key (valueKeywords [] pair.Value |> String.concat ","))
        |> Seq.toList

    let private definitionsKeyword roots =
        match deferredDefinitions roots with
        | [] -> []
        | definitions -> [ sprintf "\"$defs\":{%s}" (String.concat "," definitions) ]

    /// <summary>Generates a compact JSON Schema document from a built model schema's metadata.</summary>
    /// <param name="schema">The built model schema to lower.</param>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> is not a completed model schema.</exception>
    /// <example>
    /// <code>
    /// let document = JsonSchema.generate customerSchema
    /// // {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{...},"required":[...]}
    /// </code>
    /// </example>
    let generate (schema: Schema<'model>) : string =
        let model = Inspect.model schema
        let roots = model.Fields |> List.map _.Schema
        sprintf
            "{%s}"
            (sprintf "\"$schema\":%s" (literal draft2020_12) :: (modelKeywords model @ definitionsKeyword roots)
             |> String.concat ",")

    /// <summary>Generates a compact JSON Schema document for a standalone value schema.</summary>
    /// <param name="schema">The value schema to lower.</param>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let generateValue (schema: Schema<'value>) : string =
        let value = Inspect.schema schema
        sprintf
            "{%s}"
            (sprintf "\"$schema\":%s" (literal draft2020_12) :: (valueKeywords [] value @ definitionsKeyword [ value ])
             |> String.concat ",")

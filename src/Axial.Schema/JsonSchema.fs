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
/// <c>required</c>, collections to <c>array</c> with <c>items</c>, and tagged unions to <c>oneOf</c> with a
/// <c>const</c>-constrained discriminator property per case. Constraint metadata lowers to <c>minLength</c>,
/// <c>maxLength</c>, <c>pattern</c>, <c>enum</c>, <c>minimum</c>/<c>maximum</c> (and exclusive variants),
/// <c>minItems</c>/<c>maxItems</c>, and <c>uniqueItems</c>; constraints without a JSON Schema equivalent, such as
/// <c>trimmed</c>, are skipped.
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
    let rec private boundaryConstraints (description: ValueDescription) : SchemaConstraintMetadata list =
        let own = description.Constraints |> List.map _.Metadata

        match description.Shape with
        | ValueShape.Refined underlying -> own @ boundaryConstraints underlying
        | _ -> own

    let rec private boundaryFormat (description: ValueDescription) : SchemaFormat option =
        match description.Format, description.Shape with
        | Some format, _ -> Some format
        | None, ValueShape.Refined underlying -> boundaryFormat underlying
        | None, _ -> None

    let rec private underlyingShape (description: ValueDescription) : ValueShape =
        match description.Shape with
        | ValueShape.Refined underlying -> underlyingShape underlying
        | shape -> shape

    let private constraintKeywords (constraints: SchemaConstraintMetadata list) =
        constraints
        |> List.collect (fun metadata ->
            match metadata with
            | SchemaConstraintMetadata.MinLength minimum -> [ sprintf "\"minLength\":%d" minimum ]
            | SchemaConstraintMetadata.MaxLength maximum -> [ sprintf "\"maxLength\":%d" maximum ]
            | SchemaConstraintMetadata.LengthBetween(minimum, maximum) ->
                [ sprintf "\"minLength\":%d" minimum; sprintf "\"maxLength\":%d" maximum ]
            | SchemaConstraintMetadata.Email -> [ "\"format\":\"email\"" ]
            | SchemaConstraintMetadata.Trimmed -> []
            | SchemaConstraintMetadata.Pattern pattern -> [ sprintf "\"pattern\":%s" (literal pattern) ]
            | SchemaConstraintMetadata.OneOf choices ->
                [ choices |> List.map literal |> String.concat "," |> sprintf "\"enum\":[%s]" ]
            | SchemaConstraintMetadata.NotEqualTo _ -> []
            | SchemaConstraintMetadata.Between(minimum, maximum) ->
                [ sprintf "\"minimum\":%s" (literal minimum); sprintf "\"maximum\":%s" (literal maximum) ]
            | SchemaConstraintMetadata.GreaterThan minimum -> [ sprintf "\"exclusiveMinimum\":%s" (literal minimum) ]
            | SchemaConstraintMetadata.LessThan maximum -> [ sprintf "\"exclusiveMaximum\":%s" (literal maximum) ]
            | SchemaConstraintMetadata.AtLeast minimum -> [ sprintf "\"minimum\":%s" (literal minimum) ]
            | SchemaConstraintMetadata.AtMost maximum -> [ sprintf "\"maximum\":%s" (literal maximum) ]
            | SchemaConstraintMetadata.Count expected ->
                [ sprintf "\"minItems\":%d" expected; sprintf "\"maxItems\":%d" expected ]
            | SchemaConstraintMetadata.MinCount minimum -> [ sprintf "\"minItems\":%d" minimum ]
            | SchemaConstraintMetadata.MaxCount maximum -> [ sprintf "\"maxItems\":%d" maximum ]
            | SchemaConstraintMetadata.CountBetween(minimum, maximum) ->
                [ sprintf "\"minItems\":%d" minimum; sprintf "\"maxItems\":%d" maximum ]
            | SchemaConstraintMetadata.Distinct -> [ "\"uniqueItems\":true" ]
            | SchemaConstraintMetadata.Required
            | SchemaConstraintMetadata.Optional
            | SchemaConstraintMetadata.Custom _ -> [])

    let private primitiveKeywords kind =
        match kind with
        | PrimitiveValueKind.Text -> [ "\"type\":\"string\"" ]
        | PrimitiveValueKind.Int -> [ "\"type\":\"integer\"" ]
        | PrimitiveValueKind.Decimal -> [ "\"type\":\"number\"" ]
        | PrimitiveValueKind.Bool -> [ "\"type\":\"boolean\"" ]
        | PrimitiveValueKind.Date -> [ "\"type\":\"string\""; "\"format\":\"date\"" ]
        | PrimitiveValueKind.DateTime -> [ "\"type\":\"string\""; "\"format\":\"date-time\"" ]
        | PrimitiveValueKind.Guid -> [ "\"type\":\"string\""; "\"format\":\"uuid\"" ]

    let rec private valueKeywords (fieldConstraints: SchemaConstraintMetadata list) (description: ValueDescription) =
        let constraints = fieldConstraints @ boundaryConstraints description

        let formatKeyword =
            match boundaryFormat description with
            | Some format when constraints |> List.contains SchemaConstraintMetadata.Email |> not ->
                [ sprintf "\"format\":\"%s\"" (escape format.Name) ]
            | _ -> []

        match underlyingShape description with
        | ValueShape.Primitive kind ->
            primitiveKeywords kind @ formatKeyword @ constraintKeywords constraints |> List.distinct
        | ValueShape.Nested model -> modelKeywords model
        | ValueShape.Many item ->
            [ "\"type\":\"array\""
              sprintf "\"items\":{%s}" (valueKeywords [] item |> String.concat ",") ]
            @ constraintKeywords constraints
        | ValueShape.Union union ->
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
        | ValueShape.Refined _ -> failwith "underlyingShape never returns a refined shape."

    and private modelKeywords (model: ModelDescription) =
        let properties =
            model.Fields
            |> List.map (fun field ->
                let constraints = field.Constraints |> List.map _.Metadata

                sprintf "\"%s\":{%s}" (escape field.Name) (valueKeywords constraints field.Value |> String.concat ","))
            |> String.concat ","

        let required =
            model.Fields
            |> List.filter (fun field ->
                (field.Constraints |> List.map _.Metadata) @ boundaryConstraints field.Value
                |> List.contains SchemaConstraintMetadata.Required)
            |> List.map (fun field -> sprintf "\"%s\"" (escape field.Name))

        [ "\"type\":\"object\""; sprintf "\"properties\":{%s}" properties ]
        @ (if List.isEmpty required then
               []
           else
               [ sprintf "\"required\":[%s]" (String.concat "," required) ])

    /// <summary>Generates a compact JSON Schema document from a built model schema's metadata.</summary>
    /// <param name="schema">The built model schema to lower.</param>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException">Thrown when <paramref name="schema" /> was not produced by <c>Schema.build</c>.</exception>
    /// <example>
    /// <code>
    /// let document = JsonSchema.generate customerSchema
    /// // {"type":"object","properties":{...},"required":[...]}
    /// </code>
    /// </example>
    let generate (schema: Schema<'model>) : string =
        sprintf "{%s}" (modelKeywords (Inspect.model schema) |> String.concat ",")

    /// <summary>Generates a compact JSON Schema document for a standalone value schema.</summary>
    /// <param name="schema">The value schema to lower.</param>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let generateValue (schema: ValueSchema<'value>) : string =
        sprintf "{%s}" (valueKeywords [] (Inspect.value schema) |> String.concat ",")

namespace Axial.Schema

open System
open System.Globalization
open System.Text

open Axial.Constraint

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
        | null -> "null"
        | :? string as text -> sprintf "\"%s\"" (escape text)
        | :? char as character -> sprintf "\"%s\"" (escape (string character))
        | :? bool as flag -> if flag then "true" else "false"
        | :? float as number when Double.IsNaN number || Double.IsInfinity number ->
            sprintf "\"%s\"" (Convert.ToString(number, CultureInfo.InvariantCulture))
        | :? float32 as number when Single.IsNaN number || Single.IsInfinity number ->
            sprintf "\"%s\"" (Convert.ToString(number, CultureInfo.InvariantCulture))
        | other ->
            match Type.GetTypeCode(other.GetType()) with
            | TypeCode.SByte
            | TypeCode.Byte
            | TypeCode.Int16
            | TypeCode.UInt16
            | TypeCode.Int32
            | TypeCode.UInt32
            | TypeCode.Int64
            | TypeCode.UInt64
            | TypeCode.Decimal
            | TypeCode.Single
            | TypeCode.Double -> Convert.ToString(other, CultureInfo.InvariantCulture)
            | _ ->
                Convert.ToString(other, CultureInfo.InvariantCulture)
                |> escape
                |> sprintf "\"%s\""

    /// Collects the constraint descriptions visible at a boundary: the layer's own constraints plus every
    /// refinement layer down to the primitive foundation. A refinement's constraint is written against the raw
    /// representation, which is exactly the representation this document describes.
    let rec private boundaryConstraints (description: SchemaDescription) : ConstraintDescription list =
        match description.Shape with
        | SchemaShape.Refined underlying -> description.Constraints @ boundaryConstraints underlying
        | _ -> description.Constraints

    let rec private boundaryFormat (description: SchemaDescription) : SchemaFormat option =
        match description.Format, description.Shape with
        | Some format, _ -> Some format
        | None, SchemaShape.Refined underlying -> boundaryFormat underlying
        | None, _ -> None

    let rec private underlyingShape (description: SchemaDescription) : SchemaShape =
        match description.Shape with
        | SchemaShape.Refined underlying -> underlyingShape underlying
        | shape -> shape

    /// <summary>How faithfully one rule can be expressed in JSON Schema.</summary>
    /// <remarks>
    /// "Degrade honestly" is not a binary. Some atoms have a keyword that is a sound weakening — it never rejects a
    /// value the runtime accepts — which is worth emitting even though the complete rule stays runtime-only. Those
    /// are <c>Weakened</c>: the keyword is published and the whole atom is also retained as runtime metadata.
    /// </remarks>
    type private Fidelity =
        /// The keywords mean exactly what the runtime rule means.
        | Enforced of string list
        /// The keywords are a sound weakening; the complete rule is also retained as runtime metadata.
        | Weakened of string list
        /// No keyword is sound. The rule is retained as runtime metadata only.
        | NotEnforceable

    /// A JSON literal for an operand, or None when the wire encoding would not preserve the typed comparison the
    /// atom asserts. GUIDs, instants, and IEEE floats decode non-injectively, so wire equality is not typed
    /// equality for them and no `const`, `enum`, `contains`, or `uniqueItems` may be emitted from one.
    let private comparableLiteral (value: ConstraintValue) =
        match value with
        | ConstraintValue.Text text -> Some(sprintf "\"%s\"" (escape text))
        | ConstraintValue.Char character -> Some(sprintf "\"%s\"" (escape (string character)))
        | ConstraintValue.Boolean flag -> Some(if flag then "true" else "false")
        | ConstraintValue.Integer number -> Some(string number)
        | ConstraintValue.Decimal number -> Some(number.ToString CultureInfo.InvariantCulture)
        | ConstraintValue.BigInteger _
        | ConstraintValue.Guid _
        | ConstraintValue.TimeSpan _
        | ConstraintValue.Float _
        | ConstraintValue.Float32 _
        | ConstraintValue.DateTime _
        | ConstraintValue.DateTimeOffset _
        | ConstraintValue.Null
        | ConstraintValue.List _ -> None

    /// A JSON literal for an ordering or divisibility operand. IEEE floats are excluded because JSON Schema
    /// compares under mathematical-number semantics while the runtime rule runs IEEE arithmetic after parsing and
    /// rounding: `0.3 % 0.1` is not zero, so the two disagree in both directions.
    let private numericLiteral (value: ConstraintValue) =
        match value with
        | ConstraintValue.Integer number -> Some(string number)
        | ConstraintValue.Decimal number -> Some(number.ToString CultureInfo.InvariantCulture)
        | ConstraintValue.BigInteger number -> Some(string number)
        | _ -> None

    let private atomKeywords shape (atom: ConstraintAtom) : Fidelity =
        let isText =
            match shape with
            | SchemaShape.Primitive PrimitiveValueKind.Text -> true
            | _ -> false

        let sizeKeyword prefix bound =
            match shape with
            | SchemaShape.Primitive PrimitiveValueKind.Text -> Some(sprintf "\"%sLength\":%d" prefix bound)
            | SchemaShape.Many _ -> Some(sprintf "\"%sItems\":%d" prefix bound)
            | SchemaShape.MapOf _ -> Some(sprintf "\"%sProperties\":%d" prefix bound)
            | _ -> None

        let sizeKeywords bounds =
            match bounds |> List.map (fun (prefix, bound) -> sizeKeyword prefix bound) with
            | keywords when keywords |> List.forall Option.isSome -> Enforced(keywords |> List.map Option.get)
            | _ -> NotEnforceable

        let comparison keyword operand =
            match numericLiteral operand with
            | Some literal -> Enforced [ sprintf "\"%s\":%s" keyword literal ]
            | None -> NotEnforceable

        match atom with
        | PresenceAtom Present ->
            match shape with
            // Runtime presence rejects whitespace-only text, which no JSON Schema keyword expresses: .NET
            // whitespace and ECMA-262 `\s` differ in both directions (U+0085 and U+FEFF each fall on the wrong
            // side). `minLength:1` is the sound weakening; the nonblank rule stays runtime-only.
            | SchemaShape.Primitive PrimitiveValueKind.Text -> Weakened [ "\"minLength\":1" ]
            | SchemaShape.Many _ -> Enforced [ "\"minItems\":1" ]
            | SchemaShape.MapOf _ -> Enforced [ "\"minProperties\":1" ]
            | _ -> NotEnforceable
        | PresenceAtom Blank ->
            match shape with
            // Whitespace-only text is blank at runtime, so `maxLength:0` would reject values the library accepts.
            | SchemaShape.Primitive PrimitiveValueKind.Text -> NotEnforceable
            | SchemaShape.Many _ -> Enforced [ "\"maxItems\":0" ]
            | SchemaShape.MapOf _ -> Enforced [ "\"maxProperties\":0" ]
            | _ -> NotEnforceable
        | CardinalityAtom(Exact expected) -> sizeKeywords [ "min", expected; "max", expected ]
        | CardinalityAtom(Cardinality.Minimum minimum) -> sizeKeywords [ "min", minimum ]
        | CardinalityAtom(Cardinality.Maximum maximum) -> sizeKeywords [ "max", maximum ]
        | CardinalityAtom(Cardinality.Between(minimum, maximum)) -> sizeKeywords [ "min", minimum; "max", maximum ]
        | RelationAtom(Compared(Equal, expected)) ->
            match comparableLiteral expected with
            | Some literal -> Enforced [ sprintf "\"const\":%s" literal ]
            | None -> NotEnforceable
        // JSON Schema has no direct inequality keyword, and `not: {const: x}` would need the whole node.
        | RelationAtom(Compared(NotEqual, _)) -> NotEnforceable
        | RelationAtom(Compared(GreaterThan, minimum)) -> comparison "exclusiveMinimum" minimum
        | RelationAtom(Compared(LessThan, maximum)) -> comparison "exclusiveMaximum" maximum
        | RelationAtom(Compared(AtLeast, minimum)) -> comparison "minimum" minimum
        | RelationAtom(Compared(AtMost, maximum)) -> comparison "maximum" maximum
        | RelationAtom(Within(minimum, maximum)) ->
            match numericLiteral minimum, numericLiteral maximum with
            | Some minimum, Some maximum ->
                Enforced [ sprintf "\"minimum\":%s" minimum; sprintf "\"maximum\":%s" maximum ]
            | _ -> NotEnforceable
        | MembershipAtom(OneOf choices) ->
            match choices |> List.map comparableLiteral with
            | literals when not literals.IsEmpty && literals |> List.forall Option.isSome ->
                Enforced [ literals |> List.map Option.get |> String.concat "," |> sprintf "\"enum\":[%s]" ]
            | _ -> NotEnforceable
        | MembershipAtom(Membership.Contains item) ->
            match comparableLiteral item with
            | Some literal -> Enforced [ sprintf "\"contains\":{\"const\":%s}" literal ]
            | None -> NotEnforceable
        // `uniqueItems` compares decoded wire values. That substitutes for typed equality only where decoding is
        // injective, which the item shape decides — two spellings of one GUID or instant are distinct on the wire
        // and equal after parsing.
        | UniquenessAtom ->
            match shape with
            | SchemaShape.Many item ->
                match underlyingShape item with
                | SchemaShape.Primitive PrimitiveValueKind.Text
                | SchemaShape.Primitive PrimitiveValueKind.Bool
                | SchemaShape.Primitive PrimitiveValueKind.Int
                | SchemaShape.Primitive PrimitiveValueKind.Int64
                | SchemaShape.Primitive PrimitiveValueKind.Decimal
                | SchemaShape.Enum _ -> Enforced [ "\"uniqueItems\":true" ]
                | _ -> NotEnforceable
            | _ -> NotEnforceable
        // The complete runtime rule, not an approximation: the compiled IgnoreCase is inert because the pattern
        // contains no letters. The annotation-oriented `format: email` comes from SchemaFormat.email instead, and
        // declaring both emits both.
        | FormatAtom Email when isText -> Enforced [ sprintf "\"pattern\":\"%s\"" (escape Constraint.emailPattern) ]
        | FormatAtom Numeric when isText -> Enforced [ sprintf "\"pattern\":\"%s\"" (escape Constraint.numericPattern) ]
        // Trimmed needs lookaround; Alphanumeric is Char.IsLetterOrDigit, whose Unicode semantics no ECMA-262
        // class reproduces; an authored pattern is the .NET dialect, which is not ECMA-262.
        | FormatAtom Trimmed
        | FormatAtom Alphanumeric
        | FormatAtom(Pattern _)
        | FormatAtom Email
        | FormatAtom Numeric -> NotEnforceable
        | NumberAtom(MultipleOf divisor) ->
            match numericLiteral divisor with
            | Some literal -> Enforced [ sprintf "\"multipleOf\":%s" literal ]
            | None -> NotEnforceable
        // The JSON number grammar already excludes NaN and the infinities.
        | NumberAtom Finite -> Enforced []

    let private runtimeEntry key description =
        match description with
        | Some description -> sprintf "{\"rule\":\"%s\",\"description\":%s}" key (literal (box (description: string)))
        | None -> sprintf "{\"rule\":\"%s\"}" key

    let private opaqueEntry (opaque: OpaqueConstraint) =
        match opaque with
        | OpaqueConstraint.CustomPredicate description ->
            runtimeEntry "constraint.opaque.customPredicate" (Some description)
        | OpaqueConstraint.RuntimeNegation(description, _) ->
            runtimeEntry "constraint.opaque.negation" (Some description)
        | OpaqueConstraint.RuntimeProjection _ ->
            runtimeEntry "constraint.opaque.projection" None
        | OpaqueConstraint.UnsupportedOperand operation ->
            runtimeEntry (UnsupportedOperation.key operation) (Some(UnsupportedOperation.render operation))

    /// Every rule under a node, as readable runtime-metadata entries. Used when a node cannot be enforced at all,
    /// so nothing under it is silently dropped.
    let rec private runtimeEntries (description: ConstraintDescription) : string list =
        match description.Expression with
        | ConstraintExpression.Atom atom -> [ runtimeEntry (ConstraintAtom.key atom) (Some(ConstraintAtom.render atom)) ]
        | ConstraintExpression.All children -> children |> List.collect runtimeEntries
        | ConstraintExpression.Any(first, rest) -> first :: rest |> List.collect runtimeEntries
        | ConstraintExpression.Optional inner -> runtimeEntries inner
        | ConstraintExpression.Opaque opaque -> [ opaqueEntry opaque ]

    /// What one constraint expression contributes at a schema node.
    type private Lowering =
        { /// Keywords that may be published as enforcement.
          Keywords: string list
          /// Rules the target does not enforce, retained so the document never implies more than it checks.
          Runtime: string list }

    let private nothing = { Keywords = []; Runtime = [] }

    let rec private lower shape (description: ConstraintDescription) : Lowering =
        match description.Expression with
        | ConstraintExpression.Atom atom ->
            match atomKeywords shape atom with
            | Enforced keywords -> { Keywords = keywords; Runtime = [] }
            | Weakened keywords ->
                { Keywords = keywords
                  Runtime = [ runtimeEntry (ConstraintAtom.key atom) (Some(ConstraintAtom.render atom)) ] }
            | NotEnforceable ->
                { Keywords = []
                  Runtime = [ runtimeEntry (ConstraintAtom.key atom) (Some(ConstraintAtom.render atom)) ] }
        | ConstraintExpression.All children ->
            // A conjunction may publish whichever children it can enforce: keeping a subset is stricter than
            // nothing and never stricter than the whole rule. The remainder is still retained.
            let lowerings = children |> List.map (lower shape)

            { Keywords = lowerings |> List.collect _.Keywords
              Runtime = lowerings |> List.collect _.Runtime }
        | ConstraintExpression.Any(first, rest) ->
            // A disjunction may not publish a subset: dropping a branch makes the document reject values the
            // library accepts. Either every branch is enforceable, or the whole node is runtime-only.
            let branches = first :: rest
            let lowerings = branches |> List.map (lower shape)

            if lowerings |> List.forall (fun lowering -> List.isEmpty lowering.Runtime) then
                let cases =
                    lowerings
                    |> List.map (fun lowering -> lowering.Keywords |> String.concat "," |> sprintf "{%s}")
                    |> String.concat ","

                { Keywords = [ sprintf "\"anyOf\":[%s]" cases ]; Runtime = [] }
            else
                { Keywords = []; Runtime = branches |> List.collect runtimeEntries }
        | ConstraintExpression.Optional inner ->
            // Absence is decided by the surrounding shape and required-ness, so only the present branch lowers.
            lower shape inner
        | ConstraintExpression.Opaque opaque -> { Keywords = []; Runtime = [ opaqueEntry opaque ] }

    let private constraintKeywords shape (constraints: ConstraintDescription list) =
        let lowering =
            (nothing, constraints |> List.map (lower shape))
            ||> List.fold (fun state next ->
                { Keywords = state.Keywords @ next.Keywords
                  Runtime = state.Runtime @ next.Runtime })

        let runtimeKeyword =
            match lowering.Runtime |> List.distinct with
            | [] -> []
            | entries -> [ entries |> String.concat "," |> sprintf "\"x-axial-runtime-constraints\":[%s]" ]

        lowering.Keywords @ runtimeKeyword

    let private primitiveKeywords kind =
        match kind with
        | PrimitiveValueKind.Text -> [ "\"type\":\"string\"" ]
        | PrimitiveValueKind.Int -> [ "\"type\":\"integer\"" ]
        | PrimitiveValueKind.Int64 -> [ "\"type\":\"integer\"" ]
        | PrimitiveValueKind.Decimal -> [ "\"type\":\"number\"" ]
        | PrimitiveValueKind.Float -> [ "\"type\":\"number\"" ]
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

    let rec private valueKeywords (fieldConstraints: ConstraintDescription list) (description: SchemaDescription) =
        let constraints = fieldConstraints @ boundaryConstraints description

        // Annotation and enforcement are separate concepts and lower separately. `SchemaFormat.email` makes no
        // validation claim and becomes `format`; the `Email` constraint atom is an executable rule and becomes
        // `pattern`. Declaring both emits both — no collision, no precedence rule.
        let formatKeyword =
            match boundaryFormat description with
            | Some format -> [ sprintf "\"format\":\"%s\"" (escape format.Name) ]
            | None -> []

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
                primitiveKeywords kind @ formatKeyword @ constraintKeywords (SchemaShape.Primitive kind) constraints |> List.distinct
            | SchemaShape.Nested model -> modelKeywords model
            | SchemaShape.Many item ->
                [ "\"type\":\"array\""
                  sprintf "\"items\":{%s}" (valueKeywords [] item |> String.concat ",") ]
                @ constraintKeywords (SchemaShape.Many item) constraints
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
                [ "\"type\":\"string\""; sprintf "\"enum\":[%s]" tags ] @ constraintKeywords (SchemaShape.Enum enum) constraints
            | SchemaShape.Optional payload -> valueKeywords constraints payload
            | SchemaShape.MapOf item ->
                [ "\"type\":\"object\""
                  sprintf "\"additionalProperties\":{%s}" (valueKeywords [] item |> String.concat ",") ]
                @ constraintKeywords (SchemaShape.MapOf item) constraints
            | SchemaShape.Deferred(reference, _) -> [ sprintf "\"$ref\":\"#/$defs/recursive%d\"" reference ]
            | SchemaShape.Recursive reference -> [ sprintf "\"$ref\":\"#/$defs/recursive%d\"" reference ]
            | SchemaShape.Refined _ -> failwith "underlyingShape never returns a refined shape."

        descriptionKeyword @ defaultKeyword @ shapeKeywords

    /// Optional and default-supplied fields stay out of the object's `required` list. Other fields are required unless
    /// their supply constraints explicitly make them omittable.
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

    and private fieldIsRequired (field: FieldDescription) =
        let declaresPresence =
            field.Constraints @ boundaryConstraints field.Schema
            |> List.collect ConstraintDescription.atoms
            |> List.contains (PresenceAtom Present)

        let explicitlySupplied =
            field.Supply = Some Supply.Supplied || field.Schema.Supply = Some Supply.Supplied || declaresPresence

        match boundaryDefault field.Schema with
        | Some _ -> false
        | None -> not (isOptionalDescription field.Schema) || explicitlySupplied

    and private inlineCaseKeywords (discriminatorField: string) (tag: string) (model: ModelDescription) =
        let discriminatorProperty = sprintf "\"%s\":{\"const\":%s}" (escape discriminatorField) (literal tag)

        let payloadProperties =
            model.Fields
            |> List.map (fun field ->
                sprintf "\"%s\":{%s}" (escape field.Name) (valueKeywords field.Constraints field.Schema |> String.concat ","))

        let properties = discriminatorProperty :: payloadProperties |> String.concat ","

        let required =
            escape discriminatorField
            :: (model.Fields
                |> List.filter fieldIsRequired
                |> List.map (fun field -> escape field.Name))
            |> List.map (sprintf "\"%s\"")
            |> String.concat ","

        sprintf "{\"type\":\"object\",\"properties\":{%s},\"required\":[%s]}" properties required

    and private modelKeywords (model: ModelDescription) =
        let properties =
            model.Fields
            |> List.map (fun field ->
                let constraints = field.Constraints

                sprintf "\"%s\":{%s}" (escape field.Name) (valueKeywords constraints field.Schema |> String.concat ","))
            |> String.concat ","

        let required =
            model.Fields
            |> List.filter fieldIsRequired
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

    /// <summary>Generates a compact JSON Schema document from any completed schema declaration.</summary>
    /// <param name="schema">The record, primitive, collection, union, or other completed schema to lower.</param>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    /// <example>
    /// <code>
    /// let document = JsonSchema.generate customerSchema
    /// // {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","properties":{...},"required":[...]}
    /// </code>
    /// </example>
    let generate (schema: Schema<'model>) : string =
        let value = Inspect.schema schema
        sprintf
            "{%s}"
            (sprintf "\"$schema\":%s" (literal draft2020_12) :: (valueKeywords [] value @ definitionsKeyword [ value ])
             |> String.concat ",")

    /// <summary>Generates a compact JSON Schema document for a standalone value schema.</summary>
    /// <param name="schema">The value schema to lower.</param>
    /// <exception cref="T:System.ArgumentNullException">Thrown when <paramref name="schema" /> is null.</exception>
    let generateValue (schema: Schema<'value>) : string =
        generate schema

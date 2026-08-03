namespace Axial.Schema.Testing

open Axial

open System
open Axial.Constraint
open Axial.Schema
open FsCheck
open FsCheck.FSharp
open FsCheck.FSharp.GenBuilder

/// <summary>Why a schema cannot be lowered to an automatic test-data generator.</summary>
/// <remarks>
/// Generation fails closed. A generator that produced values without honouring a rule would be worse than no
/// generator, because every property built on it would silently test the wrong population.
/// </remarks>
[<RequireQualifiedAccess>]
type SchemaGenerationError =
    /// <summary>The rule at this path has no sound generation, named by its stable message key.</summary>
    | UnsupportedConstraint of path: string list * rule: string

/// FsCheck generators derived from Schema metadata.
[<RequireQualifiedAccess>]
module SchemaGen =
    let private maximumOr fallback values = match values with [] -> fallback | values -> List.max values
    let private minimumOr fallback values = match values with [] -> fallback | values -> List.min values

    let private unsupported path rule =
        Error(SchemaGenerationError.UnsupportedConstraint(List.rev path, rule))

    let private traverse results =
        let folder state next =
            match state, next with
            | Ok values, Ok value -> Ok(value :: values)
            | Error error, _
            | _, Error error -> Error error
        results |> List.fold folder (Ok []) |> Result.map List.rev

    let private choose items = Gen.elements items

    /// Whether a generator for this shape actually honours this atom.
    ///
    /// Generatability is shape-dependent, not atom-dependent: the text generator honours choices, sizes, and the
    /// email format but ignores ordering; the numeric generators honour bounds and multiples but ignore equality;
    /// the Boolean, date, and identifier generators honour nothing. Deciding on the atom alone let a rule the
    /// generator silently ignored still count as supported, so `equalTo "exact"` produced random strings — the
    /// generator claiming satisfaction it never checked. This answers for the pairing instead.
    /// The wire rendering of a scalar operand, matching how the generators below emit the same shapes. `None`
    /// means the operand has no single wire form this generator can commit to, so the rule stays unsupported.
    let private scalarLiteral (operand: ConstraintValue) =
        match operand with
        | ConstraintValue.Text value -> Some value
        | ConstraintValue.Char value -> Some(string value)
        | ConstraintValue.Boolean value -> Some(string value)
        | ConstraintValue.Integer value -> Some(string value)
        | ConstraintValue.BigInteger value -> Some(string value)
        | ConstraintValue.Decimal value -> Some(value.ToString Globalization.CultureInfo.InvariantCulture)
        | ConstraintValue.Guid value -> Some(string value)
        | ConstraintValue.DateTime value -> Some(string value)
        | ConstraintValue.DateTimeOffset value -> Some(string value)
        // A float literal cannot be committed to a wire form the parser is guaranteed to read back
        // identically, and NaN and the infinities have no JSON spelling at all.
        | ConstraintValue.Float _
        | ConstraintValue.Float32 _
        | ConstraintValue.TimeSpan _
        | ConstraintValue.Null
        | ConstraintValue.List _ -> None

    let rec private underlyingShape (shape: SchemaShape) =
        // A constraint attached above a refinement is written against the raw representation, and the generator
        // recurses to that raw shape, so the pairing must be judged there too.
        match shape with
        | SchemaShape.Refined raw -> underlyingShape raw.Shape
        | SchemaShape.Deferred(_, expanded) -> underlyingShape expanded.Shape
        | shape -> shape

    let private honours (rawShape: SchemaShape) (atom: ConstraintAtom) =
        let shape = underlyingShape rawShape

        let numeric =
            match shape with
            | SchemaShape.Primitive PrimitiveValueKind.Int
            | SchemaShape.Primitive PrimitiveValueKind.Int64
            | SchemaShape.Primitive PrimitiveValueKind.Decimal
            | SchemaShape.Primitive PrimitiveValueKind.Float -> true
            | _ -> false

        let scalar =
            match shape with
            | SchemaShape.Primitive _ -> true
            | _ -> false

        match shape, atom with
        // The satisfying population is exactly one value, so the generator emits the operand itself.
        | _, RelationAtom(Compared(Equal, operand)) -> scalar && (scalarLiteral operand).IsSome
        | SchemaShape.Primitive PrimitiveValueKind.Text, PresenceAtom Present
        | SchemaShape.Primitive PrimitiveValueKind.Text, CardinalityAtom _
        | SchemaShape.Primitive PrimitiveValueKind.Text, MembershipAtom(OneOf _)
        | SchemaShape.Primitive PrimitiveValueKind.Text, FormatAtom Email -> true
        | _, RelationAtom(Compared((GreaterThan | LessThan | AtLeast | AtMost), _))
        | _, RelationAtom(Within _)
        | _, NumberAtom(MultipleOf _) -> numeric
        // Every generator already produces finite numbers, so the rule is satisfied by construction.
        | _, NumberAtom Finite -> numeric
        | (SchemaShape.Many _ | SchemaShape.MapOf _), CardinalityAtom _
        | (SchemaShape.Many _ | SchemaShape.MapOf _), PresenceAtom Present -> true
        | SchemaShape.Optional _, PresenceAtom _ -> true
        | _ -> false

    let private tryGeneratable shape atom =
        if honours shape atom then Ok atom else Error(ConstraintAtom.key atom)

    /// Flattens an expression into the atoms a generator must satisfy, or names the first node it cannot honour.
    /// A disjunction may generate from any soundly supported branch, so the first usable branch is taken.
    let rec private generatableAtoms shape (description: ConstraintDescription) : Result<ConstraintAtom list, string> =
        match description.Expression with
        | ConstraintExpression.Atom atom -> tryGeneratable shape atom |> Result.map List.singleton
        | ConstraintExpression.All children ->
            (Ok [], children)
            ||> List.fold (fun state child ->
                match state, generatableAtoms shape child with
                | Ok atoms, Ok childAtoms -> Ok(atoms @ childAtoms)
                | Error rule, _ -> Error rule
                | _, Error rule -> Error rule)
        | ConstraintExpression.Any(first, rest) ->
            let branches = first :: rest

            match branches |> List.tryPick (fun branch -> generatableAtoms shape branch |> Result.toOption) with
            | Some atoms -> Ok atoms
            | None ->
                branches
                |> List.tryPick (fun branch ->
                    match generatableAtoms shape branch with
                    | Error rule -> Some rule
                    | Ok _ -> None)
                |> Option.defaultValue "constraint.any"
                |> Error
        | ConstraintExpression.Optional inner -> generatableAtoms shape inner
        | ConstraintExpression.Opaque(OpaqueConstraint.CustomPredicate _) -> Error "constraint.opaque.customPredicate"
        | ConstraintExpression.Opaque(OpaqueConstraint.RuntimeNegation _) -> Error "constraint.opaque.negation"
        | ConstraintExpression.Opaque(OpaqueConstraint.RuntimeProjection _) -> Error "constraint.opaque.projection"
        | ConstraintExpression.Opaque(OpaqueConstraint.UnsupportedOperand operation) -> Error(UnsupportedOperation.key operation)

    let private atomsOf path shape (constraints: ConstraintDescription list) =
        (Ok [], constraints)
        ||> List.fold (fun state description ->
            match state, generatableAtoms shape description with
            | Ok atoms, Ok next -> Ok(atoms @ next)
            | Error error, _ -> Error error
            | _, Error rule -> unsupported path rule)

    let private tryInt value =
        match value with
        | ConstraintValue.Integer number -> Some(int number)
        | _ -> None

    let private tryDecimal value =
        match value with
        | ConstraintValue.Integer number -> Some(decimal number)
        | ConstraintValue.Decimal number -> Some number
        | _ -> None

    let private tryText value =
        match value with
        | ConstraintValue.Text text -> Some text
        | _ -> None

    let private sizeBounds atoms =
        let low =
            atoms
            |> List.choose (function
                | CardinalityAtom(Exact n)
                | CardinalityAtom(Cardinality.Minimum n)
                | CardinalityAtom(Cardinality.Between(n, _)) -> Some n
                | _ -> None)

        let high =
            atoms
            |> List.choose (function
                | CardinalityAtom(Exact n)
                | CardinalityAtom(Cardinality.Maximum n)
                | CardinalityAtom(Cardinality.Between(_, n)) -> Some n
                | _ -> None)

        low, high

    let private textGenerator atoms =
        let oneOf = atoms |> List.tryPick (function MembershipAtom(OneOf choices) -> Some(choices |> List.choose tryText) | _ -> None)
        let email = atoms |> List.contains (FormatAtom Email)
        let present = atoms |> List.contains (PresenceAtom Present)
        let lows, highs = sizeBounds atoms
        let minimum = lows |> maximumOr (if present then 1 else 0)
        let maximum = highs |> minimumOr (max minimum 24)

        match oneOf with
        | Some values when not (List.isEmpty values) -> choose values
        | _ when email -> Gen.elements [ "ada@example.com"; "grace@example.org"; "test.user@example.net" ]
        | _ ->
            let maximum = max minimum maximum

            gen {
                let! length = Gen.choose (minimum, maximum)
                let! chars = Gen.listOfLength length (Gen.elements [ 'a' .. 'z' ])
                return String(Array.ofList chars)
            }

    let private intGenerator atoms =
        let lows =
            atoms
            |> List.choose (function
                | RelationAtom(Within(a, _))
                | RelationAtom(Compared(AtLeast, a)) -> tryInt a
                | RelationAtom(Compared(GreaterThan, a)) -> tryInt a |> Option.map ((+) 1)
                | _ -> None)

        let highs =
            atoms
            |> List.choose (function
                | RelationAtom(Within(_, b))
                | RelationAtom(Compared(AtMost, b)) -> tryInt b
                | RelationAtom(Compared(LessThan, b)) -> tryInt b |> Option.map (fun value -> value - 1)
                | _ -> None)

        let low = lows |> maximumOr -1000
        let high = highs |> minimumOr 1000 |> max low
        let multiple = atoms |> List.tryPick (function NumberAtom(MultipleOf value) -> tryInt value | _ -> None)

        match multiple with
        | Some divisor when divisor <> 0 ->
            let first = int (Math.Ceiling(decimal low / decimal divisor))
            let last = int (Math.Floor(decimal high / decimal divisor))
            Gen.choose(first, max first last) |> Gen.map (fun factor -> factor * divisor)
        | _ -> Gen.choose(low, high)

    let private decimalGenerator atoms =
        let lows =
            atoms
            |> List.choose (function
                | RelationAtom(Within(a, _))
                | RelationAtom(Compared(AtLeast, a)) -> tryDecimal a
                | RelationAtom(Compared(GreaterThan, a)) -> tryDecimal a |> Option.map (fun value -> value + 0.01m)
                | _ -> None)

        let highs =
            atoms
            |> List.choose (function
                | RelationAtom(Within(_, b))
                | RelationAtom(Compared(AtMost, b)) -> tryDecimal b
                | RelationAtom(Compared(LessThan, b)) -> tryDecimal b |> Option.map (fun value -> value - 0.01m)
                | _ -> None)

        let low = lows |> maximumOr -1000m
        let high = highs |> minimumOr 1000m |> max low
        let multiple = atoms |> List.tryPick (function NumberAtom(MultipleOf value) -> tryDecimal value | _ -> None)

        match multiple with
        | Some divisor when divisor <> 0m ->
            let first = int (Math.Ceiling(low / divisor))
            let last = int (Math.Floor(high / divisor))
            Gen.choose(first, max first last) |> Gen.map (fun factor -> decimal factor * divisor)
        | _ -> Gen.choose(0, 10000) |> Gen.map (fun part -> low + (high - low) * decimal part / 10000m)

    let private countBounds atoms size =
        let lows, highs = sizeBounds atoms
        let present = if atoms |> List.contains (PresenceAtom Present) then 1 else 0
        let low = lows |> maximumOr present
        let high = highs |> minimumOr (min 4 (max low size))
        low, max low high

    let private buildDefinitions roots =
        let definitions = Collections.Generic.Dictionary<int, SchemaDescription>()
        let rec value description =
            match description.Shape with
            | SchemaShape.Deferred(reference, expanded) -> if definitions.TryAdd(reference, expanded) then value expanded
            | SchemaShape.Refined item | SchemaShape.Many item | SchemaShape.Optional item | SchemaShape.MapOf item -> value item
            | SchemaShape.Nested model -> model.Fields |> List.iter (fun field -> value field.Schema)
            | SchemaShape.Union union -> union.Cases |> List.iter (fun case -> value case.Payload)
            | SchemaShape.UnionInline union -> union.Cases |> List.iter (fun case -> case.Payload.Fields |> List.iter (fun field -> value field.Schema))
            | _ -> ()
        roots |> List.iter value
        definitions

    let private rawGenerator (custom: Map<string, Gen<Data>>) (roots: SchemaDescription list) =
        let definitions = buildDefinitions roots

        let rec value path size fieldConstraints (description: SchemaDescription) : Result<Gen<Data>, SchemaGenerationError> =
            let constraints = fieldConstraints @ description.Constraints
            let customGenerator = custom |> Map.tryFind (path |> List.rev |> String.concat ".")

            match customGenerator, atomsOf path description.Shape constraints with
            | Some generator, _ -> Ok generator
            | None, Error error -> Error error
            | None, Ok atoms ->

            // An equality rule pins the value, so it outranks every other generator for this node.
            match atoms |> List.tryPick (function
                      | RelationAtom(Compared(Equal, operand)) -> scalarLiteral operand
                      | _ -> None) with
            | Some literal -> Ok(Gen.constant (Data.Text literal))
            | None ->

                match description.Shape with
                | SchemaShape.Primitive PrimitiveValueKind.Text -> Ok(textGenerator atoms |> Gen.map Data.Text)
                | SchemaShape.Primitive PrimitiveValueKind.Int -> Ok(intGenerator atoms |> Gen.map (string >> Data.Text))
                | SchemaShape.Primitive PrimitiveValueKind.Int64 -> Ok(intGenerator atoms |> Gen.map (int64 >> string >> Data.Text))
                | SchemaShape.Primitive PrimitiveValueKind.Decimal -> Ok(decimalGenerator atoms |> Gen.map (fun value -> Data.Text(value.ToString(Globalization.CultureInfo.InvariantCulture))))
                // Generated floats stay finite: JSON has no NaN or infinity literal.
                | SchemaShape.Primitive PrimitiveValueKind.Float -> Ok(decimalGenerator atoms |> Gen.map (fun value -> Data.Text((float value).ToString("R", Globalization.CultureInfo.InvariantCulture))))
                | SchemaShape.Primitive PrimitiveValueKind.Bool -> Ok(ArbMap.defaults.ArbFor<bool>().Generator |> Gen.map (string >> Data.Text))
                | SchemaShape.Primitive PrimitiveValueKind.Date -> Ok(Gen.choose(0, 3650) |> Gen.map (fun days -> DateOnly(2020, 1, 1).AddDays days |> string |> Data.Text))
                | SchemaShape.Primitive PrimitiveValueKind.DateTime -> Ok(Gen.choose(0, 100000) |> Gen.map (fun minutes -> DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes minutes |> string |> Data.Text))
                | SchemaShape.Primitive PrimitiveValueKind.Guid -> Ok(ArbMap.defaults.ArbFor<Guid>().Generator |> Gen.map (string >> Data.Text))
                | SchemaShape.Refined raw -> value path size constraints raw
                | SchemaShape.Nested model -> modelValue path size model
                | SchemaShape.Many item ->
                    let low, high = countBounds atoms size
                    if size <= 0 && low = 0 then
                        Ok(Gen.constant (Data.List []))
                    else
                        value ("[]" :: path) (size / 2) [] item
                        |> Result.map (fun itemGen ->
                        gen {
                            let! count = Gen.choose(low, high)
                            let! items = Gen.listOfLength count itemGen
                            return Data.List items
                        })
                | SchemaShape.MapOf item ->
                    let low, high = countBounds atoms size
                    if size <= 0 && low = 0 then
                        Ok(Gen.constant (Data.Object []))
                    else
                        value ("{}" :: path) (size / 2) [] item
                        |> Result.map (fun itemGen ->
                        gen {
                            let! count = Gen.choose(low, high)
                            let! items = Gen.listOfLength count itemGen
                            return items |> List.mapi (fun index item -> string index, item) |> Data.Object
                        })
                | SchemaShape.Optional payload ->
                    value path size constraints payload |> Result.map (fun present -> Gen.frequency [ 1, Gen.constant Data.Null; 3, present ])
                | SchemaShape.Enum enum -> Ok(enum.Cases |> List.map _.Tag |> choose |> Gen.map Data.Text)
                | SchemaShape.Union union ->
                    union.Cases
                    |> List.map (fun case -> value path (size / 2) [] case.Payload |> Result.map (fun payload -> Gen.map (fun raw -> Data.Object [ union.DiscriminatorField, Data.Text case.Tag; union.PayloadField, raw ]) payload))
                    |> traverse |> Result.map Gen.oneof
                | SchemaShape.UnionInline union ->
                    union.Cases
                    |> List.map (fun case -> modelValue path (size / 2) case.Payload |> Result.map (Gen.map (function Data.Object fields -> Data.Object((union.DiscriminatorField, Data.Text case.Tag) :: fields) | _ -> failwith "model generators produce objects")))
                    |> traverse |> Result.map Gen.oneof
                | SchemaShape.Deferred(_, expanded) -> if size <= 0 then modelLeaf path expanded else value path (size / 2) constraints expanded
                | SchemaShape.Recursive reference ->
                    if size <= 0 then modelLeaf path definitions[reference] else value path (size / 2) constraints definitions[reference]

        and modelLeaf path description =
            match description.Shape with
            | SchemaShape.Nested model -> modelValue path 0 model
            | _ -> value path 0 [] description

        and modelValue path size model =
            model.Fields
            |> List.map (fun field -> value (field.Name :: path) size field.Constraints field.Schema |> Result.map (Gen.map (fun raw -> field.Name, raw)))
            |> traverse
            |> Result.map (fun fields -> Gen.sequenceToList fields |> Gen.map Data.Object)

        value

    /// Derives constraint-satisfying structured data, using field-path overrides for unsupported constraints or domain-specific distributions.
    let rawWith (custom: Map<string, Gen<Data>>) (schema: Schema<'model>) : Result<Gen<Data>, SchemaGenerationError> =
        let model = Inspect.model schema
        let roots = model.Fields |> List.map _.Schema
        let generate = rawGenerator custom roots
        let root =
            { Shape = SchemaShape.Nested model
              Format = None
              Constraints = []
              Supply = None
              Description = None
              Default = None }
        generate [] 10 [] root
        |> Result.map (fun _ ->
            Gen.sized (fun size ->
                generate [] size [] root
                |> Result.defaultWith (fun _ -> invalidOp "Schema generation support changed after inspection.")))

    /// Derives a generator of constraint-satisfying structured data for a built schema.
    let raw (schema: Schema<'model>) : Result<Gen<Data>, SchemaGenerationError> =
        rawWith Map.empty schema

    /// Derives models by generating structured data and parsing it through the schema.
    let model (schema: Schema<'model>) : Result<Gen<'model>, SchemaGenerationError> =
        raw schema
        |> Result.map (fun rawGen ->
            rawGen
            |> Gen.map (fun input -> (Schema.parse schema input))
            |> Gen.filter Result.isOk
            |> Gen.map (function
                | Ok value ->
                    match Schema.check schema value with
                    | Ok accepted -> accepted
                    | Error _ -> invalidOp "A successfully parsed model failed an immediate schema check."
                | Error _ -> invalidOp "Filtered parse failure."))

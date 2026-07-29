namespace Axial.Schema.Testing

open Axial

open System
open Axial.Schema
open FsCheck
open FsCheck.FSharp
open FsCheck.FSharp.GenBuilder

/// Why a schema cannot be lowered to an automatic test-data generator.
[<RequireQualifiedAccess>]
type SchemaGenerationError =
    | UnsupportedConstraint of path: string list * code: string

/// FsCheck generators derived from Schema metadata.
[<RequireQualifiedAccess>]
module SchemaGen =
    let private maximumOr fallback values = match values with [] -> fallback | values -> List.max values
    let private minimumOr fallback values = match values with [] -> fallback | values -> List.min values
    let private unsupported path constraint' =
        Error(SchemaGenerationError.UnsupportedConstraint(List.rev path, Constraint.code constraint'))

    let private traverse results =
        let folder state next =
            match state, next with
            | Ok values, Ok value -> Ok(value :: values)
            | Error error, _
            | _, Error error -> Error error
        results |> List.fold folder (Ok []) |> Result.map List.rev

    let private choose items = Gen.elements items

    let private tryValueMetadata constraint' =
        match Constraint.metadata constraint' with
        | Axial.Schema.ConstraintMetadata.ValueConstraint metadata -> Some metadata
        | Axial.Schema.ConstraintMetadata.Supply _ -> None

    let private valueMetadata constraints = constraints |> List.choose tryValueMetadata

    let private isPresent constraint' =
        match Constraint.metadata constraint' with
        | Axial.Schema.ConstraintMetadata.ValueConstraint Axial.Check.ConstraintMetadata.Present -> true
        | _ -> false

    let private textGenerator path constraints =
        let metadata = constraints |> List.choose (fun value -> tryValueMetadata value |> Option.map (fun metadata -> value, metadata))
        match metadata |> List.tryFind (fun (_, item) -> match item with Axial.Check.ConstraintMetadata.Pattern _ | Axial.Check.ConstraintMetadata.Custom _ -> true | _ -> false) with
        | Some(constraint', _) -> unsupported path constraint'
        | None ->
            let oneOf = metadata |> List.tryPick (fun (_, item) -> match item with Axial.Check.ConstraintMetadata.OneOf values -> Some values | _ -> None)
            let email = metadata |> List.exists (fun (_, item) -> item = Axial.Check.ConstraintMetadata.Email)
            let present = constraints |> List.exists isPresent
            let minimum =
                metadata
                |> List.choose (fun (_, item) -> match item with Axial.Check.ConstraintMetadata.Length n | Axial.Check.ConstraintMetadata.MinLength n | Axial.Check.ConstraintMetadata.LengthBetween(n, _) -> Some n | _ -> None)
                |> maximumOr (if present then 1 else 0)
            let maximum = metadata |> List.choose (fun (_, item) -> match item with Axial.Check.ConstraintMetadata.Length n | Axial.Check.ConstraintMetadata.MaxLength n | Axial.Check.ConstraintMetadata.LengthBetween(_, n) -> Some n | _ -> None) |> minimumOr (max minimum 24)
            match oneOf with
            | Some values when not (List.isEmpty values) -> Ok(choose values)
            | _ when email -> Ok(Gen.elements [ "ada@example.com"; "grace@example.org"; "test.user@example.net" ])
            | _ ->
                let maximum = max minimum maximum
                Ok(
                    gen {
                        let! length = Gen.choose (minimum, maximum)
                        let! chars = Gen.listOfLength length (Gen.elements [ 'a'..'z' ])
                        return String(Array.ofList chars)
                    })

    let private intGenerator constraints =
        let metadata = valueMetadata constraints
        let lows = metadata |> List.choose (function Axial.Check.ConstraintMetadata.Between(a, _) | Axial.Check.ConstraintMetadata.AtLeast a -> Some(unbox<int> a) | Axial.Check.ConstraintMetadata.GreaterThan a -> Some(unbox<int> a + 1) | _ -> None)
        let highs = metadata |> List.choose (function Axial.Check.ConstraintMetadata.Between(_, b) | Axial.Check.ConstraintMetadata.AtMost b -> Some(unbox<int> b) | Axial.Check.ConstraintMetadata.LessThan b -> Some(unbox<int> b - 1) | _ -> None)
        let low = lows |> maximumOr -1000
        let high = highs |> minimumOr 1000 |> max low
        let multiple = metadata |> List.tryPick (function Axial.Check.ConstraintMetadata.MultipleOf value -> Some(unbox<int> value) | _ -> None)
        match multiple with
        | Some divisor when divisor <> 0 ->
            let first = int (Math.Ceiling(decimal low / decimal divisor))
            let last = int (Math.Floor(decimal high / decimal divisor))
            Gen.choose(first, max first last) |> Gen.map (fun factor -> factor * divisor)
        | _ -> Gen.choose(low, high)

    let private decimalGenerator constraints =
        let metadata = valueMetadata constraints
        let lows = metadata |> List.choose (function Axial.Check.ConstraintMetadata.Between(a, _) | Axial.Check.ConstraintMetadata.AtLeast a -> Some(unbox<decimal> a) | Axial.Check.ConstraintMetadata.GreaterThan a -> Some(unbox<decimal> a + 0.01m) | _ -> None)
        let highs = metadata |> List.choose (function Axial.Check.ConstraintMetadata.Between(_, b) | Axial.Check.ConstraintMetadata.AtMost b -> Some(unbox<decimal> b) | Axial.Check.ConstraintMetadata.LessThan b -> Some(unbox<decimal> b - 0.01m) | _ -> None)
        let low = lows |> maximumOr -1000m
        let high = highs |> minimumOr 1000m |> max low
        let multiple = metadata |> List.tryPick (function Axial.Check.ConstraintMetadata.MultipleOf value -> Some(unbox<decimal> value) | _ -> None)
        match multiple with
        | Some divisor when divisor <> 0m ->
            let first = int (Math.Ceiling(low / divisor))
            let last = int (Math.Floor(high / divisor))
            Gen.choose(first, max first last) |> Gen.map (fun factor -> decimal factor * divisor)
        | _ -> Gen.choose(0, 10000) |> Gen.map (fun part -> low + (high - low) * decimal part / 10000m)

    let private countBounds constraints size =
        let metadata = valueMetadata constraints
        let low = metadata |> List.choose (function Axial.Check.ConstraintMetadata.Length n | Axial.Check.ConstraintMetadata.MinLength n | Axial.Check.ConstraintMetadata.LengthBetween(n, _) -> Some n | _ -> None) |> maximumOr 0
        let high = metadata |> List.choose (function Axial.Check.ConstraintMetadata.Length n | Axial.Check.ConstraintMetadata.MaxLength n | Axial.Check.ConstraintMetadata.LengthBetween(_, n) -> Some n | _ -> None) |> minimumOr (min 4 (max low size))
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
            let unsupportedConstraint =
                constraints
                |> List.tryFind (fun constraint' ->
                    match tryValueMetadata constraint' with
                    | Some(Axial.Check.ConstraintMetadata.Custom _)
                    | Some(Axial.Check.ConstraintMetadata.NotEqualTo _)
                    | Some(Axial.Check.ConstraintMetadata.Contains _)
                    | Some Axial.Check.ConstraintMetadata.Distinct -> true
                    | _ -> false)

            match customGenerator, unsupportedConstraint with
            | Some generator, _ -> Ok generator
            | None, Some constraint' -> unsupported path constraint'
            | None, None ->
                match description.Shape with
                | SchemaShape.Primitive PrimitiveValueKind.Text -> textGenerator path constraints |> Result.map (Gen.map Data.Text)
                | SchemaShape.Primitive PrimitiveValueKind.Int -> Ok(intGenerator constraints |> Gen.map (string >> Data.Text))
                | SchemaShape.Primitive PrimitiveValueKind.Decimal -> Ok(decimalGenerator constraints |> Gen.map (fun value -> Data.Text(value.ToString(Globalization.CultureInfo.InvariantCulture))))
                | SchemaShape.Primitive PrimitiveValueKind.Bool -> Ok(ArbMap.defaults.ArbFor<bool>().Generator |> Gen.map (string >> Data.Text))
                | SchemaShape.Primitive PrimitiveValueKind.Date -> Ok(Gen.choose(0, 3650) |> Gen.map (fun days -> DateOnly(2020, 1, 1).AddDays days |> string |> Data.Text))
                | SchemaShape.Primitive PrimitiveValueKind.DateTime -> Ok(Gen.choose(0, 100000) |> Gen.map (fun minutes -> DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes minutes |> string |> Data.Text))
                | SchemaShape.Primitive PrimitiveValueKind.Guid -> Ok(ArbMap.defaults.ArbFor<Guid>().Generator |> Gen.map (string >> Data.Text))
                | SchemaShape.Refined raw -> value path size constraints raw
                | SchemaShape.Nested model -> modelValue path size model
                | SchemaShape.Many item ->
                    let low, high = countBounds constraints size
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
                    let low, high = countBounds constraints size
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
        let root = { Shape = SchemaShape.Nested model; Format = None; Constraints = []; Description = None; Default = None }
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

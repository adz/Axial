namespace Axial.Tests

open System
open System.Globalization
open Axial.Schema
open Swensen.Unquote
open Xunit

/// <summary>
/// Prototype non-validation interpreters over the public <c>Inspect</c> API: a JSON Schema emitter, a documentation
/// describer, and a UI metadata producer. They exist to prove that schema metadata alone — shapes, formats, and
/// portable constraints — is sufficient for these consumers, without parsing raw input, running checks, or
/// constructing models.
/// </summary>
module SchemaInterpreterPrototypes =
    /// Collects the constraint metadata visible at a boundary: field-level constraints plus every value-schema layer
    /// down to (and including) the primitive foundation of refined values.
    let rec boundaryConstraints (description: ValueDescription) : SchemaConstraintMetadata list =
        let own = description.Constraints |> List.map _.Metadata

        match description.Shape with
        | ValueShape.Refined underlying -> own @ boundaryConstraints underlying
        | _ -> own

    let rec boundaryFormat (description: ValueDescription) : SchemaFormat option =
        match description.Format, description.Shape with
        | Some format, _ -> Some format
        | None, ValueShape.Refined underlying -> boundaryFormat underlying
        | None, _ -> None

    let rec underlyingShape (description: ValueDescription) : ValueShape =
        match description.Shape with
        | ValueShape.Refined underlying -> underlyingShape underlying
        | shape -> shape

    [<RequireQualifiedAccess>]
    module JsonSchema =
        let private literal (value: obj) =
            match value with
            | :? string as text -> sprintf "\"%s\"" text
            | :? bool as flag -> if flag then "true" else "false"
            | other -> Convert.ToString(other, CultureInfo.InvariantCulture)

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
                | SchemaConstraintMetadata.GreaterThan minimum ->
                    [ sprintf "\"exclusiveMinimum\":%s" (literal minimum) ]
                | SchemaConstraintMetadata.LessThan maximum ->
                    [ sprintf "\"exclusiveMaximum\":%s" (literal maximum) ]
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
                    [ sprintf "\"format\":\"%s\"" format.Name ]
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
                    |> List.map (fun case -> sprintf "{%s}" (valueKeywords [] case.Payload |> String.concat ","))
                    |> String.concat ","

                [ sprintf "\"oneOf\":[%s]" cases ]
            | ValueShape.Refined _ -> failwith "underlyingShape never returns a refined shape."

        and private modelKeywords (model: ModelDescription) =
            let properties =
                model.Fields
                |> List.map (fun field ->
                    let constraints = field.Constraints |> List.map _.Metadata
                    sprintf "\"%s\":{%s}" field.Name (valueKeywords constraints field.Value |> String.concat ","))
                |> String.concat ","

            let required =
                model.Fields
                |> List.filter (fun field ->
                    (field.Constraints |> List.map _.Metadata) @ boundaryConstraints field.Value
                    |> List.contains SchemaConstraintMetadata.Required)
                |> List.map (fun field -> sprintf "\"%s\"" field.Name)

            [ "\"type\":\"object\""; sprintf "\"properties\":{%s}" properties ]
            @ (if List.isEmpty required then [] else [ sprintf "\"required\":[%s]" (String.concat "," required) ])

        /// <summary>Generates a compact JSON Schema document from a built model schema's metadata.</summary>
        let generate<'model> (schema: Schema<'model>) : string =
            sprintf "{%s}" (modelKeywords (Inspect.model schema) |> String.concat ",")

    [<RequireQualifiedAccess>]
    module Docs =
        let rec private valueSummary (description: ValueDescription) =
            match underlyingShape description with
            | ValueShape.Primitive kind -> (sprintf "%A" kind).ToLowerInvariant()
            | ValueShape.Nested _ -> "object"
            | ValueShape.Many _ -> "list"
            | ValueShape.Union _ -> "union"
            | ValueShape.Refined _ -> failwith "underlyingShape never returns a refined shape."

        let private constraintSummary metadata =
            match metadata with
            | SchemaConstraintMetadata.Required -> Some "required"
            | SchemaConstraintMetadata.Optional -> Some "optional"
            | SchemaConstraintMetadata.MinLength minimum -> Some(sprintf "at least %d characters" minimum)
            | SchemaConstraintMetadata.MaxLength maximum -> Some(sprintf "at most %d characters" maximum)
            | SchemaConstraintMetadata.Email -> Some "email format"
            | SchemaConstraintMetadata.MinCount minimum -> Some(sprintf "at least %d items" minimum)
            | SchemaConstraintMetadata.AtLeast minimum ->
                Some(sprintf "at least %s" (Convert.ToString(minimum, CultureInfo.InvariantCulture)))
            | _ -> None

        let rec private fieldLines indent (model: ModelDescription) =
            model.Fields
            |> List.collect (fun field ->
                let notes =
                    (field.Constraints |> List.map _.Metadata) @ boundaryConstraints field.Value
                    |> List.choose constraintSummary

                let noteText = if List.isEmpty notes then "" else sprintf " — %s" (String.concat ", " notes)
                let line = sprintf "%s- %s (%s)%s" indent field.Name (valueSummary field.Value) noteText

                let children =
                    match underlyingShape field.Value with
                    | ValueShape.Nested nested -> fieldLines (indent + "  ") nested
                    | ValueShape.Many item ->
                        match underlyingShape item with
                        | ValueShape.Nested nested -> fieldLines (indent + "  ") nested
                        | _ -> []
                    | ValueShape.Union union ->
                        union.Cases
                        |> List.collect (fun case ->
                            match underlyingShape case.Payload with
                            | ValueShape.Nested nested -> fieldLines (indent + "  ") nested
                            | _ -> [])
                    | _ -> []

                line :: children)

        /// <summary>Describes a built model schema as human-readable documentation lines.</summary>
        let describe<'model> (schema: Schema<'model>) : string list =
            fieldLines "" (Inspect.model schema)

    /// <summary>The UI control suggested for a field, derived only from schema metadata.</summary>
    type UiControl =
        | TextBox
        | EmailBox
        | NumberBox
        | CheckBox
        | DatePicker
        | DateTimePicker
        | IdentifierBox
        | Group of UiField list
        | Repeater of UiControl

    and UiField =
        { Label: string
          Control: UiControl
          IsRequired: bool
          MaxLength: int option }

    [<RequireQualifiedAccess>]
    module UiMetadata =
        let rec private controlFor (description: ValueDescription) =
            let constraints = boundaryConstraints description

            match underlyingShape description with
            | ValueShape.Primitive PrimitiveValueKind.Text ->
                if constraints |> List.contains SchemaConstraintMetadata.Email then EmailBox else TextBox
            | ValueShape.Primitive PrimitiveValueKind.Int
            | ValueShape.Primitive PrimitiveValueKind.Decimal -> NumberBox
            | ValueShape.Primitive PrimitiveValueKind.Bool -> CheckBox
            | ValueShape.Primitive PrimitiveValueKind.Date -> DatePicker
            | ValueShape.Primitive PrimitiveValueKind.DateTime -> DateTimePicker
            | ValueShape.Primitive PrimitiveValueKind.Guid -> IdentifierBox
            | ValueShape.Nested model -> Group(fieldsFor model)
            | ValueShape.Many item -> Repeater(controlFor item)
            | ValueShape.Union _ -> Group []
            | ValueShape.Refined _ -> failwith "underlyingShape never returns a refined shape."

        and private fieldsFor (model: ModelDescription) =
            model.Fields
            |> List.map (fun field ->
                let constraints = (field.Constraints |> List.map _.Metadata) @ boundaryConstraints field.Value

                { Label = field.Name
                  Control = controlFor field.Value
                  IsRequired = constraints |> List.contains SchemaConstraintMetadata.Required
                  MaxLength =
                    constraints
                    |> List.tryPick (fun metadata ->
                        match metadata with
                        | SchemaConstraintMetadata.MaxLength maximum -> Some maximum
                        | SchemaConstraintMetadata.LengthBetween(_, maximum) -> Some maximum
                        | _ -> None) })

        /// <summary>Describes a built model schema as UI field metadata without creating a UI framework.</summary>
        let describe<'model> (schema: Schema<'model>) : UiField list =
            fieldsFor (Inspect.model schema)

module SchemaInterpreterPrototypeTests =
    open SchemaInterpreterPrototypes

    type private Email = private EmailValue of string

    type private Address =
        { Street: string
          City: string }

    type private Signup =
        { Email: Email
          Age: int
          Newsletter: bool
          Address: Address
          Tags: Tag list }

    and private Tag = { Label: string }

    let private counters () = ref 0, ref 0

    let private emailSchemaWith (constructions: int ref) (getterReads: int ref) =
        Value.text
        |> Value.withConstraints [ SchemaConstraint.required; SchemaConstraint.maxLength 254 ]
        |> Value.refined
            (fun raw ->
                constructions.Value <- constructions.Value + 1
                EmailValue raw)
            (fun (EmailValue raw) ->
                getterReads.Value <- getterReads.Value + 1
                raw)
        |> Value.withConstraint SchemaConstraint.email
        |> Value.withFormat SchemaFormat.email

    let private addressSchema () =
        Schema.recordFor<Address, _> (fun street city -> { Street = street; City = city })
        |> Schema.text "street" _.Street
        |> Schema.text "city" _.City
        |> Schema.build

    let private tagSchema () =
        Schema.recordFor<Tag, _> (fun label -> { Label = label })
        |> Schema.text "label" _.Label
        |> Schema.build

    let private signupSchemaWith constructions getterReads =
        Schema.recordFor<Signup, _> (fun email age newsletter address tags ->
            { Email = email
              Age = age
              Newsletter = newsletter
              Address = address
              Tags = tags })
        |> Schema.field "email" _.Email (emailSchemaWith constructions getterReads)
        |> Schema.fieldWith
            [ SchemaConstraint.between 13 120 ]
            "age"
            _.Age
            Value.``int``
        |> Schema.field "newsletter" _.Newsletter Value.``bool``
        |> Schema.fieldWith [ SchemaConstraint.required ] "address" _.Address (Value.nested (addressSchema ()))
        |> Schema.fieldWith
            [ SchemaConstraint.minCount 1; SchemaConstraint.distinct ]
            "tags"
            _.Tags
            (Value.many (tagSchema ()))
        |> Schema.build

    [<Fact>]
    let ``json schema generation lowers constraint metadata without running validation`` () =
        let constructions, getterReads = counters ()

        let generated = JsonSchema.generate (signupSchemaWith constructions getterReads)

        test <@ generated.Contains "\"type\":\"object\"" @>
        test <@ generated.Contains "\"email\":{\"type\":\"string\",\"format\":\"email\",\"maxLength\":254}" @>
        test <@ generated.Contains "\"age\":{\"type\":\"integer\",\"minimum\":13,\"maximum\":120}" @>
        test <@ generated.Contains "\"newsletter\":{\"type\":\"boolean\"}" @>
        test <@ generated.Contains "\"address\":{\"type\":\"object\",\"properties\":{\"street\":{\"type\":\"string\"},\"city\":{\"type\":\"string\"}}}" @>
        test <@ generated.Contains "\"tags\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"label\":{\"type\":\"string\"}}},\"minItems\":1,\"uniqueItems\":true" @>
        test <@ generated.Contains "\"required\":[\"email\",\"address\"]" @>
        test <@ constructions.Value = 0 @>
        test <@ getterReads.Value = 0 @>

    [<Fact>]
    let ``json schema generation lowers pattern one-of and length range metadata`` () =
        let generated =
            Schema.recordFor<Tag, _> (fun label -> { Label = label })
            |> Schema.fieldWith
                [ SchemaConstraint.lengthBetween 2 10
                  SchemaConstraint.pattern "^[a-z]+$"
                  SchemaConstraint.oneOf [ "alpha"; "beta" ] ]
                "label"
                _.Label
                Value.text
            |> Schema.build
            |> JsonSchema.generate

        test <@ generated.Contains "\"minLength\":2" @>
        test <@ generated.Contains "\"maxLength\":10" @>
        test <@ generated.Contains "\"pattern\":\"^[a-z]+$\"" @>
        test <@ generated.Contains "\"enum\":[\"alpha\",\"beta\"]" @>

    [<Fact>]
    let ``docs describe renders field documentation from schema metadata alone`` () =
        let constructions, getterReads = counters ()
        let lines = Docs.describe (signupSchemaWith constructions getterReads)

        test <@ lines |> List.contains "- email (text) — email format, required, at most 254 characters" @>
        test <@ lines |> List.contains "- age (int)" @>
        test <@ lines |> List.contains "- address (object) — required" @>
        test <@ lines |> List.contains "  - street (text)" @>
        test <@ lines |> List.contains "- tags (list) — at least 1 items" @>
        test <@ lines |> List.contains "  - label (text)" @>

    [<Fact>]
    let ``ui metadata describes controls required flags and max lengths without a ui framework`` () =
        let constructions, getterReads = counters ()
        let fields = UiMetadata.describe (signupSchemaWith constructions getterReads)

        let email = fields |> List.find (fun field -> field.Label = "email")
        test <@ email.Control = EmailBox @>
        test <@ email.IsRequired @>
        test <@ email.MaxLength = Some 254 @>

        let newsletter = fields |> List.find (fun field -> field.Label = "newsletter")
        test <@ newsletter.Control = CheckBox @>
        test <@ not newsletter.IsRequired @>

        let address = fields |> List.find (fun field -> field.Label = "address")

        match address.Control with
        | Group nested -> test <@ nested |> List.map _.Label = [ "street"; "city" ] @>
        | other -> failwithf "Expected a group control for the nested address, but got %A." other

        let tags = fields |> List.find (fun field -> field.Label = "tags")

        match tags.Control with
        | Repeater(Group nested) -> test <@ nested |> List.map _.Label = [ "label" ] @>
        | other -> failwithf "Expected a repeater of tag groups, but got %A." other

    [<Fact>]
    let ``inspection walks shapes formats and constraints without constructing models or reading values`` () =
        let constructions, getterReads = counters ()

        let description = Inspect.model (signupSchemaWith constructions getterReads)

        test <@ description.Fields |> List.map _.Name = [ "email"; "age"; "newsletter"; "address"; "tags" ] @>
        test <@ description.Fields |> List.map _.Order = [ 0; 1; 2; 3; 4 ] @>

        let email = description.Fields |> List.find (fun field -> field.Name = "email")
        test <@ email.Value.Format = Some SchemaFormat.email @>

        match email.Value.Shape with
        | ValueShape.Refined underlying ->
            test <@ underlying.Shape = ValueShape.Primitive PrimitiveValueKind.Text @>
            test <@ underlying.Constraints |> List.map _.Metadata |> List.contains SchemaConstraintMetadata.Required @>
        | other -> failwithf "Expected the email field to be refined, but got %A." other

        test <@ constructions.Value = 0 @>
        test <@ getterReads.Value = 0 @>

    [<Fact>]
    let ``value and field inspection work over standalone schemas`` () =
        let constructions, getterReads = counters ()
        let emailSchema = emailSchemaWith constructions getterReads

        let description = Inspect.value emailSchema

        test <@ boundaryConstraints description |> List.contains SchemaConstraintMetadata.Email @>
        test <@ (Inspect.field (Field.create "email" _.Email emailSchema)).Name = "email" @>
        test <@ constructions.Value = 0 && getterReads.Value = 0 @>

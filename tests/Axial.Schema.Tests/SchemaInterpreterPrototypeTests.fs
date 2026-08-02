namespace Axial.Tests

open Axial.Constraint
open System
open System.Globalization
open Axial.Schema
open Xunit
open Axial.Schema.Syntax
open Axial.Constraint.ConstraintDSL
open Swensen.Unquote

/// <summary>
/// Prototype non-validation interpreters over the public <c>Inspect</c> API: a documentation describer and a UI
/// metadata producer. They exist to prove that schema metadata alone — shapes, formats, and portable constraints — is
/// sufficient for these consumers, without parsing structured data, running checks, or constructing models. The JSON Schema
/// emitter that started here has been promoted to the shipped <c>Axial.Schema.JsonSchema</c> module.
/// </summary>
module SchemaInterpreterPrototypes =
    /// Collects the constraint metadata visible at a boundary: field-level constraints plus every value-schema layer
    /// down to (and including) the primitive foundation of refined values.
    let rec boundaryConstraints (description: SchemaDescription) : ConstraintAtom list =
        let own = description.Constraints |> List.collect ConstraintDescription.atoms

        match description.Shape with
        | SchemaShape.Refined underlying -> own @ boundaryConstraints underlying
        | _ -> own

    let rec boundaryFormat (description: SchemaDescription) : SchemaFormat option =
        match description.Format, description.Shape with
        | Some format, _ -> Some format
        | None, SchemaShape.Refined underlying -> boundaryFormat underlying
        | None, _ -> None

    let rec underlyingShape (description: SchemaDescription) : SchemaShape =
        match description.Shape with
        | SchemaShape.Refined underlying -> underlyingShape underlying
        | shape -> shape

    [<RequireQualifiedAccess>]
    module Docs =
        let rec private valueSummary (description: SchemaDescription) =
            match underlyingShape description with
            | SchemaShape.Primitive kind -> (sprintf "%A" kind).ToLowerInvariant()
            | SchemaShape.Nested _ -> "object"
            | SchemaShape.Many _ -> "list"
            | SchemaShape.Union _ -> "union"
            | SchemaShape.Optional payload -> sprintf "optional %s" (valueSummary payload)
            | SchemaShape.Refined _ -> failwith "underlyingShape never returns a refined shape."

        // Atoms are shape-neutral, so the documentation interpreter supplies the noun from the surrounding shape.
        let private constraintSummary shape atom =
            let lengthNoun =
                match shape with
                | SchemaShape.Many _ | SchemaShape.MapOf _ -> "items"
                | _ -> "characters"

            match atom with
            | PresenceAtom Present -> Some "required"
            | CardinalityAtom(Cardinality.Minimum minimum) -> Some(sprintf "at least %d %s" minimum lengthNoun)
            | CardinalityAtom(Cardinality.Maximum maximum) -> Some(sprintf "at most %d %s" maximum lengthNoun)
            | FormatAtom Email -> Some "email format"
            | RelationAtom(Compared(AtLeast, minimum)) -> Some(sprintf "at least %s" (ConstraintValue.render minimum))
            | _ -> None

        let rec private fieldLines indent (model: ModelDescription) =
            model.Fields
            |> List.collect (fun field ->
                let supplyNote =
                    match field.Supply, field.Schema.Supply with
                    | Some Supply.Supplied, _
                    | _, Some Supply.Supplied -> [ "required" ]
                    | Some Supply.Omittable, _
                    | _, Some Supply.Omittable -> [ "optional" ]
                    | None, None -> []

                let notes =
                    supplyNote
                    @ ((field.Constraints |> List.collect ConstraintDescription.atoms) @ boundaryConstraints field.Schema
                       |> List.choose (constraintSummary (underlyingShape field.Schema)))

                let noteText = if List.isEmpty notes then "" else sprintf " — %s" (String.concat ", " notes)
                let line = sprintf "%s- %s (%s)%s" indent field.Name (valueSummary field.Schema) noteText

                let children =
                    match underlyingShape field.Schema with
                    | SchemaShape.Nested nested -> fieldLines (indent + "  ") nested
                    | SchemaShape.Many item ->
                        match underlyingShape item with
                        | SchemaShape.Nested nested -> fieldLines (indent + "  ") nested
                        | _ -> []
                    | SchemaShape.Union union ->
                        union.Cases
                        |> List.collect (fun case ->
                            match underlyingShape case.Payload with
                            | SchemaShape.Nested nested -> fieldLines (indent + "  ") nested
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
        let rec private controlFor (description: SchemaDescription) =
            let constraints = boundaryConstraints description

            match underlyingShape description with
            | SchemaShape.Primitive PrimitiveValueKind.Text ->
                if constraints |> List.contains (FormatAtom Email) then EmailBox else TextBox
            | SchemaShape.Primitive PrimitiveValueKind.Int
            | SchemaShape.Primitive PrimitiveValueKind.Int64
            | SchemaShape.Primitive PrimitiveValueKind.Decimal
            | SchemaShape.Primitive PrimitiveValueKind.Float -> NumberBox
            | SchemaShape.Primitive PrimitiveValueKind.Bool -> CheckBox
            | SchemaShape.Primitive PrimitiveValueKind.Date -> DatePicker
            | SchemaShape.Primitive PrimitiveValueKind.DateTime -> DateTimePicker
            | SchemaShape.Primitive PrimitiveValueKind.Guid -> IdentifierBox
            | SchemaShape.Nested model -> Group(fieldsFor model)
            | SchemaShape.Many item -> Repeater(controlFor item)
            | SchemaShape.Union _ -> Group []
            | SchemaShape.Optional payload -> controlFor payload
            | SchemaShape.Refined _ -> failwith "underlyingShape never returns a refined shape."

        and private fieldsFor (model: ModelDescription) =
            model.Fields
            |> List.map (fun field ->
                let constraints =
                    (field.Constraints |> List.collect ConstraintDescription.atoms) @ boundaryConstraints field.Schema

                { Label = field.Name
                  Control = controlFor field.Schema
                  IsRequired =
                    field.Supply = Some Supply.Supplied
                    || field.Schema.Supply = Some Supply.Supplied
                    || constraints |> List.contains (PresenceAtom Present)
                  MaxLength =
                    constraints
                    |> List.tryPick (function
                        | CardinalityAtom(Cardinality.Maximum maximum)
                        | CardinalityAtom(Cardinality.Between(_, maximum)) -> Some maximum
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
        Schema.text
        |> Schema.constrainAll [ Constraint.present; Constraint.maxLength 254 ]
        |> Schema.constrain Constraint.email
        |> Schema.convert
            (fun raw ->
                constructions.Value <- constructions.Value + 1
                EmailValue raw)
            (fun (EmailValue raw) ->
                getterReads.Value <- getterReads.Value + 1
                raw)
        |> Schema.withFormat SchemaFormat.email

    let private addressSchema () =
        schema<Address> {
            field "street" _.Street
            field "city" _.City
            construct (fun street city -> { Street = street; City = city })
        }

    let private tagSchema () =
        schema<Tag> {
            field "label" _.Label
            construct (fun label -> { Label = label })
        }

    let private signupSchemaWith constructions getterReads =
        schema<Signup> {
            field "email" _.Email {
                withSchema (emailSchemaWith constructions getterReads)
            }
            field "age" _.Age {
                withSchema (Schema.int |> Schema.constrain (Constraint.between 13 120))
            }
            field "newsletter" _.Newsletter
            field "address" _.Address {
                withSchema ((addressSchema ()) |> Schema.mustSupply)
            }
            field "tags" _.Tags {
                withSchema (
                    Schema.listWith (tagSchema ())
                    |> Schema.constrainAll [ Constraint.minLength 1; Constraint.distinct ]
                )
            }
            construct (fun email age newsletter address tags ->
                { Email = email
                  Age = age
                  Newsletter = newsletter
                  Address = address
                  Tags = tags })
        }

    [<Fact>]
    let ``json schema generation lowers constraint metadata without running validation`` () =
        let constructions, getterReads = counters ()

        let generated = JsonSchema.generate (signupSchemaWith constructions getterReads)

        test <@ generated.Contains "\"type\":\"object\"" @>
        // `present` and `email` are both pattern-shaped, and `pattern` is one key per node, so they merge.
        test <@ generated.Contains "\"email\":{\"type\":\"string\",\"format\":\"email\",\"maxLength\":254,\"allOf\":[{\"pattern\":\"\\\\S\"},{\"pattern\":\"^[^@]+@[^@]+$\"}]" @>
        test <@ generated.Contains "\"age\":{\"type\":\"integer\",\"minimum\":13,\"maximum\":120}" @>
        test <@ generated.Contains "\"newsletter\":{\"type\":\"boolean\"}" @>
        test <@ generated.Contains "\"address\":{\"type\":\"object\",\"properties\":{\"street\":{\"type\":\"string\"},\"city\":{\"type\":\"string\"}},\"required\":[\"street\",\"city\"]}" @>
        test <@ generated.Contains "\"tags\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"label\":{\"type\":\"string\"}},\"required\":[\"label\"]},\"minItems\":1,\"x-axial-runtime-constraints\":[{\"rule\":\"constraint.uniqueness\"" @>
        // Every non-optional field is required, matching the parser; only Schema.option fields drop out.
        test <@ generated.Contains "\"required\":[\"email\",\"age\",\"newsletter\",\"address\",\"tags\"]" @>
        test <@ constructions.Value = 0 @>
        test <@ getterReads.Value = 0 @>

    [<Fact>]
    let ``json schema generation lowers pattern one-of and length range metadata`` () =
        let generated =
            schema<Tag> {
                field "label" _.Label {
                    withSchema (
                        Schema.text
                        |> Schema.constrainAll
                            [ Constraint.lengthBetween 2 10
                              Constraint.pattern "^[a-z]+$"
                              Constraint.oneOf [ "alpha"; "beta" ] ]
                    )
                }
                construct (fun label -> { Label = label })
            }
            |> JsonSchema.generate

        test <@ generated.Contains "\"minLength\":2" @>
        test <@ generated.Contains "\"maxLength\":10" @>
        // An authored pattern is the .NET dialect, which is not ECMA-262, so it is retained as runtime metadata
        // rather than published as an enforced keyword.
        test <@ generated.Contains "\"rule\":\"constraint.format.pattern\"" @>
        test <@ generated.Contains "\"enum\":[\"alpha\",\"beta\"]" @>

    [<Fact>]
    let ``docs describe renders field documentation from schema metadata alone`` () =
        let constructions, getterReads = counters ()
        let lines = Docs.describe (signupSchemaWith constructions getterReads)

        test <@ lines |> List.contains "- email (text) — required, at most 254 characters, email format" @>
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
        test <@ email.Schema.Format = Some SchemaFormat.email @>

        match email.Schema.Shape with
        | SchemaShape.Refined underlying ->
            test <@ underlying.Shape = SchemaShape.Primitive PrimitiveValueKind.Text @>
            test <@ underlying.Constraints |> List.collect ConstraintDescription.atoms |> List.contains (PresenceAtom Present) @>
        | other -> failwithf "Expected the email field to be refined, but got %A." other

        test <@ constructions.Value = 0 @>
        test <@ getterReads.Value = 0 @>

    [<Fact>]
    let ``value and field inspection work over standalone schemas`` () =
        let constructions, getterReads = counters ()
        let emailSchema = emailSchemaWith constructions getterReads

        let description = Inspect.schema emailSchema

        test <@ boundaryConstraints description |> List.contains (FormatAtom Email) @>
        test <@ (Inspect.field (Field.create "email" _.Email emailSchema)).Name = "email" @>
        test <@ constructions.Value = 0 && getterReads.Value = 0 @>

namespace Axial.Schema

open System
open System.Collections
open System.Collections.Generic
open Axial.Check

/// Describes boundary presence before a typed value exists.
[<RequireQualifiedAccess>]
type Presence =
    | Required
    | Optional

/// Distinguishes executable value-constraint metadata from Schema boundary-presence metadata.
[<RequireQualifiedAccess>]
type ConstraintMetadata =
    | ValueConstraint of Axial.Check.ConstraintMetadata
    | Presence of Presence

/// Describes a constraint after its typed Check constraint has been attached to a heterogeneous schema.
[<AllowNullLiteral>]
type ConstraintDescriptor internal (
    code: string,
    metadata: ConstraintMetadata,
    arguments: IReadOnlyDictionary<string, obj>,
    check: (obj -> Result<unit, CheckFailure list>) option,
    message: string option
) =
    member _.Code = code
    member _.Metadata = metadata
    member _.Arguments = arguments
    member internal _.Check = check
    member _.Message = message
    override this.ToString() = this.Code


/// A typed Schema constraint annotation. Value constraints retain a complete Axial.Check.Constraint;
/// presence declarations remain Schema boundary metadata.
[<Sealed; AllowNullLiteral>]
type SchemaConstraint<'value> internal (untyped: ConstraintDescriptor) =
    inherit ConstraintDescriptor(untyped.Code, untyped.Metadata, untyped.Arguments, untyped.Check, untyped.Message)
    member internal this.Untyped = this :> ConstraintDescriptor

/// Creates typed Schema constraints and inspects their erased descriptors.
/// <example>
/// <code>
/// let schema = Schema.text |> Schema.constrain (Constraint.maxLength 80)
/// let custom = Axial.Check.Constraint.define "named" [] check |> Constraint.fromCheck
/// </code>
/// </example>
[<RequireQualifiedAccess>]
module Constraint =
    let private ensureText parameterName (value: string) =
        if isNull value then nullArg parameterName
        if String.IsNullOrWhiteSpace value then invalidArg parameterName "Constraint values must not be blank."

    let private dictionary (pairs: (string * obj) seq) =
        let values = Dictionary<string, obj>()
        for name, value in pairs do values.Add(name, value)
        Platform.freezeDictionary values

    /// Attaches a complete Check constraint to Schema, retaining both its executable check and metadata.
    let private eraseCheck (constraint': Axial.Check.Constraint<'value>) =
        if isNull constraint' then nullArg (nameof constraint')
    /// <summary>Returns the stable external code.</summary>
        let code = Axial.Check.Constraint.code constraint'
    /// <summary>Returns the constraint arguments as an immutable dictionary.</summary>
        let arguments = Axial.Check.Constraint.arguments constraint' |> Map.toSeq |> dictionary
        let check value = Axial.Check.Constraint.check constraint' (unbox<'value> value)
        ConstraintDescriptor(code, ConstraintMetadata.ValueConstraint(Axial.Check.Constraint.metadata constraint'), arguments, Some check, None)

    let private presence code metadata =
        ConstraintDescriptor(code, metadata, dictionary [], None, None)

    /// Adapts a complete Check constraint for use by Schema.
    let fromCheck (constraint': Axial.Check.Constraint<'value>) : SchemaConstraint<'value> =
        SchemaConstraint<'value>(eraseCheck constraint')

    /// Requires boundary input to be present. Presence is handled before a typed value exists.
    let required<'value> : SchemaConstraint<'value> = SchemaConstraint<'value>(presence "required" (ConstraintMetadata.Presence Presence.Required))

    /// Marks boundary input as optional. Presence is handled before a typed value exists.
    let optional<'value> : SchemaConstraint<'value> = SchemaConstraint<'value>(presence "optional" (ConstraintMetadata.Presence Presence.Optional))

    /// <summary>Requires text to contain at least the supplied number of characters.</summary>
    let minLength minimum : SchemaConstraint<string> = fromCheck (Axial.Check.Constraint.minLength minimum)
    /// <summary>Requires text to contain at most the supplied number of characters.</summary>
    let maxLength maximum : SchemaConstraint<string> = fromCheck (Axial.Check.Constraint.maxLength maximum)
    /// <summary>Requires text length to lie within the supplied inclusive bounds.</summary>
    let lengthBetween minimum maximum : SchemaConstraint<string> = fromCheck (Axial.Check.Constraint.lengthBetween minimum maximum)
    /// <summary>Requires text to use the supported email format.</summary>
    let email : SchemaConstraint<string> = fromCheck Axial.Check.Constraint.email
    /// <summary>Requires text to have no leading or trailing whitespace.</summary>
    let trimmed : SchemaConstraint<string> = fromCheck Axial.Check.Constraint.trimmed
    /// <summary>Requires text to match the supplied regular expression.</summary>
    let pattern expression : SchemaConstraint<string> = fromCheck (Axial.Check.Constraint.pattern expression)
    /// <summary>Requires text to equal one of the supplied choices.</summary>
    let oneOf choices : SchemaConstraint<string> = fromCheck (Axial.Check.Constraint.oneOf choices)
    /// <summary>Requires a value to equal the supplied operand.</summary>
    let equalTo expected : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.equalTo expected)
    /// <summary>Requires a value to differ from the supplied operand.</summary>
    let notEqualTo unexpected : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.notEqualTo unexpected)
    /// <summary>Requires a value to lie within the supplied inclusive bounds.</summary>
    let between minimum maximum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.between minimum maximum)
    /// <summary>Requires a value to be greater than the supplied operand.</summary>
    let greaterThan minimum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.greaterThan minimum)
    /// <summary>Requires a value to be less than the supplied operand.</summary>
    let lessThan maximum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.lessThan maximum)
    /// <summary>Requires a value to be at least the supplied operand.</summary>
    let atLeast minimum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.atLeast minimum)
    /// <summary>Requires a value to be at most the supplied operand.</summary>
    let atMost maximum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.atMost maximum)
    let private forCollection<'collection when 'collection :> IEnumerable> constraint' : SchemaConstraint<'collection> =
        constraint'
        |> Axial.Check.Constraint.contramap (fun (collection: 'collection) -> collection |> Seq.cast<obj>)
        |> fromCheck

    /// <summary>Requires an enumerable value to contain exactly the supplied number of items.</summary>
    let count<'collection when 'collection :> IEnumerable> expected : SchemaConstraint<'collection> =
        forCollection (Axial.Check.Constraint.count expected)

    /// <summary>Requires an enumerable value to contain at least the supplied number of items.</summary>
    let minCount<'collection when 'collection :> IEnumerable> minimum : SchemaConstraint<'collection> =
        forCollection (Axial.Check.Constraint.minCount minimum)

    /// <summary>Requires an enumerable value to contain at most the supplied number of items.</summary>
    let maxCount<'collection when 'collection :> IEnumerable> maximum : SchemaConstraint<'collection> =
        forCollection (Axial.Check.Constraint.maxCount maximum)

    /// <summary>Requires an enumerable count to lie within the supplied inclusive bounds.</summary>
    let countBetween<'collection when 'collection :> IEnumerable> minimum maximum : SchemaConstraint<'collection> =
        forCollection (Axial.Check.Constraint.countBetween minimum maximum)
    /// <summary>Requires a list to contain no duplicate values.</summary>
    let distinct<'value when 'value: equality> : SchemaConstraint<'value list> = fromCheck (Axial.Check.Constraint.distinct<'value> |> Axial.Check.Constraint.forList)
    /// <summary>Requires a list to contain the supplied value.</summary>
    let contains item : SchemaConstraint<'value list> = fromCheck (Axial.Check.Constraint.contains item |> Axial.Check.Constraint.forList)
    /// <summary>Requires a numeric value to be an exact multiple of the supplied divisor.</summary>
    let inline multipleOf divisor : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.multipleOf divisor)

    /// <summary>Requires a numeric value to be greater than zero.</summary>
    let inline positive<'value when 'value: comparison and 'value: (static member Zero: 'value)> () : SchemaConstraint<'value> =
        greaterThan LanguagePrimitives.GenericZero<'value>
    /// <summary>Requires a numeric value to be at least zero.</summary>
    let inline nonNegative<'value when 'value: comparison and 'value: (static member Zero: 'value)> () : SchemaConstraint<'value> =
        atLeast LanguagePrimitives.GenericZero<'value>
    /// <summary>Requires a numeric value to be less than zero.</summary>
    let inline negative<'value when 'value: comparison and 'value: (static member Zero: 'value)> () : SchemaConstraint<'value> =
        lessThan LanguagePrimitives.GenericZero<'value>
    /// <summary>Requires a numeric value to be at most zero.</summary>
    let inline nonPositive<'value when 'value: comparison and 'value: (static member Zero: 'value)> () : SchemaConstraint<'value> =
        atMost LanguagePrimitives.GenericZero<'value>

    /// <summary>Returns the stable external code.</summary>
    let code (constraint': ConstraintDescriptor) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Code

    /// <summary>Returns the typed metadata case and retained runtime operands.</summary>
    let metadata (constraint': ConstraintDescriptor) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Metadata

    /// <summary>Returns the constraint arguments as an immutable dictionary.</summary>
    let arguments (constraint': ConstraintDescriptor) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Arguments

    /// <summary>Returns a named constraint argument when present.</summary>
    let tryFindArgument name (constraint': ConstraintDescriptor) =
        ensureText (nameof name) name
        if isNull constraint' then nullArg (nameof constraint')
        match constraint'.Arguments.TryGetValue name with
        | true, value -> Some value
        | false, _ -> None

    /// <summary>Returns the custom diagnostic message when one was attached.</summary>
    let message (constraint': ConstraintDescriptor) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Message

    let internal tryCheck<'value> (constraint': ConstraintDescriptor) : Check<'value> option =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Check
        |> Option.map (fun check value -> check (box value))

    /// <summary>Attaches a custom diagnostic message without changing the check or metadata.</summary>
    let withMessage (message: string) (constraint': SchemaConstraint<'value>) : SchemaConstraint<'value> =
        ensureText (nameof message) message
        if isNull constraint' then nullArg (nameof constraint')
        let untyped = constraint'.Untyped
        SchemaConstraint<'value>(ConstraintDescriptor(untyped.Code, untyped.Metadata, untyped.Arguments, untyped.Check, Some message))

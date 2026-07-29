namespace Axial.Schema

open System
open System.Collections.Generic
open Axial.Check

/// Describes boundary supply before a typed value exists.
[<RequireQualifiedAccess>]
type Supply =
    /// Boundary input must be supplied.
    | Supplied
    /// Boundary input may be omitted.
    | Omittable

/// Distinguishes executable value-constraint metadata from Schema boundary-supply metadata.
[<RequireQualifiedAccess>]
type ConstraintMetadata =
    /// Metadata for a complete executable value constraint.
    | ValueConstraint of Axial.Check.ConstraintMetadata
    /// Metadata interpreted before a typed value exists.
    | Supply of Supply

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
/// supply declarations remain Schema boundary metadata.
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
        let code = Axial.Check.Constraint.code constraint'
        let arguments = Axial.Check.Constraint.arguments constraint' |> Map.toSeq |> dictionary
        let check value = Axial.Check.Constraint.check constraint' (unbox<'value> value)
        ConstraintDescriptor(code, ConstraintMetadata.ValueConstraint(Axial.Check.Constraint.metadata constraint'), arguments, Some check, None)

    let private supply code metadata =
        ConstraintDescriptor(code, metadata, dictionary [], None, None)

    /// Adapts a complete Check constraint for use by Schema.
    /// <example>
    /// <code>let schemaConstraint = Constraint.fromCheck checkConstraint</code>
    /// </example>
    let fromCheck (constraint': Axial.Check.Constraint<'value>) : SchemaConstraint<'value> =
        SchemaConstraint<'value>(eraseCheck constraint')

    /// Requires boundary input to be present. Supply is handled before a typed value exists.
    /// <example>
    /// <code>let constraint' : SchemaConstraint&lt;string&gt; = Constraint.supplied</code>
    /// </example>
    let supplied<'value> : SchemaConstraint<'value> = SchemaConstraint<'value>(supply "supplied" (ConstraintMetadata.Supply Supply.Supplied))

    /// <summary>
    /// Marks boundary input as omittable. Only an option-typed field can be omittable: a field of any other type has
    /// nowhere to put an absent input, so the constructor could not be applied. Declaring the field
    /// <c>'value option</c> is what makes it omittable; this constraint states that intent for interpreters.
    /// </summary>
    /// <example>
    /// <code>let constraint' : SchemaConstraint&lt;int option&gt; = Constraint.omittable</code>
    /// </example>
    let omittable<'value> : SchemaConstraint<'value option> = SchemaConstraint<'value option>(supply "omittable" (ConstraintMetadata.Supply Supply.Omittable))

    /// <summary>Requires a value to be inhabited according to its shape.</summary>
    let inline present< ^value when (^value or Axial.Check.Constraint.PresentDispatcher) : (static member Create: ^value -> Axial.Check.Constraint<^value>)> : SchemaConstraint<^value> =
        fromCheck Axial.Check.Constraint.present

    /// <summary>Requires text or a concrete collection to have exactly the supplied length.</summary>
    let inline length< ^value when (^value or Axial.Check.Constraint.LengthDispatcher) : (static member Create: ^value * int -> Axial.Check.Constraint<^value>)> expected : SchemaConstraint<^value> = fromCheck (Axial.Check.Constraint.length expected)

    /// <summary>Requires text or a concrete collection to have at least the supplied length.</summary>
    /// <example>
    /// <code>let constraint' = Constraint.minLength 2</code>
    /// </example>
    let inline minLength< ^value when (^value or Axial.Check.Constraint.MinLengthDispatcher) : (static member Create: ^value * int -> Axial.Check.Constraint<^value>)> minimum : SchemaConstraint<^value> = fromCheck (Axial.Check.Constraint.minLength minimum)
    /// <summary>Requires text or a concrete collection to have at most the supplied length.</summary>
    let inline maxLength< ^value when (^value or Axial.Check.Constraint.MaxLengthDispatcher) : (static member Create: ^value * int -> Axial.Check.Constraint<^value>)> maximum : SchemaConstraint<^value> = fromCheck (Axial.Check.Constraint.maxLength maximum)
    /// <summary>Requires text or concrete collection length to lie within the supplied inclusive bounds.</summary>
    /// <example>
    /// <code>let constraint' = Constraint.lengthBetween 2 80</code>
    /// </example>
    let inline lengthBetween< ^value when (^value or Axial.Check.Constraint.LengthBetweenDispatcher) : (static member Create: ^value * int * int -> Axial.Check.Constraint<^value>)> minimum maximum : SchemaConstraint<^value> = fromCheck (Axial.Check.Constraint.lengthBetween minimum maximum)
    /// <summary>Requires text to use the supported email format.</summary>
    let email : SchemaConstraint<string> = fromCheck Axial.Check.Constraint.email
    /// <summary>Requires text to have no leading or trailing whitespace.</summary>
    /// <example>
    /// <code>let constraint' = Constraint.trimmed</code>
    /// </example>
    let trimmed : SchemaConstraint<string> = fromCheck Axial.Check.Constraint.trimmed
    /// <summary>Requires text to match the supplied regular expression.</summary>
    let pattern expression : SchemaConstraint<string> = fromCheck (Axial.Check.Constraint.pattern expression)
    /// <summary>Requires text to equal one of the supplied choices.</summary>
    /// <example>
    /// <code>let constraint' = Constraint.oneOf [ "red"; "blue" ]</code>
    /// </example>
    let oneOf choices : SchemaConstraint<string> = fromCheck (Axial.Check.Constraint.oneOf choices)
    /// <summary>Requires a value to equal the supplied operand.</summary>
    let equalTo expected : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.equalTo expected)
    /// <summary>Requires a value to differ from the supplied operand.</summary>
    /// <example>
    /// <code>let constraint' = Constraint.notEqualTo 0</code>
    /// </example>
    let notEqualTo unexpected : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.notEqualTo unexpected)
    /// <summary>Requires a value to lie within the supplied inclusive bounds.</summary>
    let between minimum maximum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.between minimum maximum)
    /// <summary>Requires a value to be greater than the supplied operand.</summary>
    /// <example>
    /// <code>let constraint' = Constraint.greaterThan 0</code>
    /// </example>
    let greaterThan minimum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.greaterThan minimum)
    /// <summary>Requires a value to be less than the supplied operand.</summary>
    let lessThan maximum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.lessThan maximum)
    /// <summary>Requires a value to be at least the supplied operand.</summary>
    /// <example>
    /// <code>let constraint' = Constraint.atLeast 1</code>
    /// </example>
    let atLeast minimum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.atLeast minimum)
    /// <summary>Requires a value to be at most the supplied operand.</summary>
    let atMost maximum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.atMost maximum)
    /// <summary>Requires a list to contain no duplicate values.</summary>
    /// <example>
    /// <code>let constraint' : SchemaConstraint&lt;int list&gt; = Constraint.distinct</code>
    /// </example>
    let distinct<'value when 'value: equality> : SchemaConstraint<'value list> = fromCheck (Axial.Check.Constraint.distinct<'value> |> Axial.Check.Constraint.forList)
    /// <summary>Requires a list to contain the supplied value.</summary>
    let contains item : SchemaConstraint<'value list> = fromCheck (Axial.Check.Constraint.contains item |> Axial.Check.Constraint.forList)
    /// <summary>Requires a numeric value to be an exact multiple of the supplied divisor.</summary>
    /// <example>
    /// <code>let constraint' = Constraint.multipleOf 5</code>
    /// </example>
    let inline multipleOf divisor : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.multipleOf divisor)

    /// <summary>Requires a numeric value to be greater than zero.</summary>
    /// <example>
    /// <code>let constraint' = Constraint.positive&lt;int&gt; ()</code>
    /// </example>
    let inline positive<'value when 'value: comparison and 'value: (static member Zero: 'value)> () : SchemaConstraint<'value> =
        greaterThan LanguagePrimitives.GenericZero<'value>
    /// <summary>Requires a numeric value to be at least zero.</summary>
    /// <example>
    /// <code>let constraint' = Constraint.nonNegative&lt;int&gt; ()</code>
    /// </example>
    let inline nonNegative<'value when 'value: comparison and 'value: (static member Zero: 'value)> () : SchemaConstraint<'value> =
        atLeast LanguagePrimitives.GenericZero<'value>
    /// <summary>Requires a numeric value to be less than zero.</summary>
    /// <example>
    /// <code>let constraint' = Constraint.negative&lt;int&gt; ()</code>
    /// </example>
    let inline negative<'value when 'value: comparison and 'value: (static member Zero: 'value)> () : SchemaConstraint<'value> =
        lessThan LanguagePrimitives.GenericZero<'value>
    /// <summary>Requires a numeric value to be at most zero.</summary>
    /// <example>
    /// <code>let constraint' = Constraint.nonPositive&lt;int&gt; ()</code>
    /// </example>
    let inline nonPositive<'value when 'value: comparison and 'value: (static member Zero: 'value)> () : SchemaConstraint<'value> =
        atMost LanguagePrimitives.GenericZero<'value>

    /// <summary>Returns the stable external code.</summary>
    /// <example>
    /// <code>let code = Constraint.code descriptor</code>
    /// </example>
    let code (constraint': ConstraintDescriptor) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Code

    /// <summary>Returns the typed metadata case and retained runtime operands.</summary>
    /// <example>
    /// <code>let metadata = Constraint.metadata descriptor</code>
    /// </example>
    let metadata (constraint': ConstraintDescriptor) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Metadata

    /// <summary>Returns the constraint arguments as an immutable dictionary.</summary>
    /// <example>
    /// <code>let arguments = Constraint.arguments descriptor</code>
    /// </example>
    let arguments (constraint': ConstraintDescriptor) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Arguments

    /// <summary>Returns a named constraint argument when present.</summary>
    /// <example>
    /// <code>let maximum = Constraint.tryFindArgument "maximum" descriptor</code>
    /// </example>
    let tryFindArgument name (constraint': ConstraintDescriptor) =
        ensureText (nameof name) name
        if isNull constraint' then nullArg (nameof constraint')
        match constraint'.Arguments.TryGetValue name with
        | true, value -> Some value
        | false, _ -> None

    /// <summary>Returns the custom diagnostic message when one was attached.</summary>
    /// <example>
    /// <code>let message = Constraint.message descriptor</code>
    /// </example>
    let message (constraint': ConstraintDescriptor) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Message

    let internal tryCheck<'value> (constraint': ConstraintDescriptor) : Check<'value> option =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Check
        |> Option.map (fun check value -> check (box value))

    /// <summary>Attaches a custom diagnostic message without changing the check or metadata.</summary>
    /// <example>
    /// <code>let constraint' = Constraint.maxLength 80 |&gt; Constraint.withMessage "Too long."</code>
    /// </example>
    let withMessage (message: string) (constraint': SchemaConstraint<'value>) : SchemaConstraint<'value> =
        ensureText (nameof message) message
        if isNull constraint' then nullArg (nameof constraint')
        let untyped = constraint'.Untyped
        SchemaConstraint<'value>(ConstraintDescriptor(untyped.Code, untyped.Metadata, untyped.Arguments, untyped.Check, Some message))

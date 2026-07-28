namespace Axial.Schema

open System
open System.Collections
open System.Collections.Generic
open Axial.Check

/// The value-constraint taxonomy owned by Axial.Check and interpreted by Schema.
type ConstraintMetadata = Axial.Check.ConstraintMetadata

/// Describes a constraint after its typed Check constraint has been attached to a heterogeneous schema.
[<AllowNullLiteral>]
type Constraint internal (
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
type SchemaConstraint<'value> internal (untyped: Constraint) =
    inherit Constraint(untyped.Code, untyped.Metadata, untyped.Arguments, untyped.Check, untyped.Message)
    member internal this.Untyped = this :> Constraint

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
        Constraint(code, Axial.Check.Constraint.metadata constraint', arguments, Some check, None)

    let private presence code metadata =
        Constraint(code, metadata, dictionary [], None, None)

    /// Adapts a complete Check constraint for use by Schema.
    let fromCheck (constraint': Axial.Check.Constraint<'value>) : SchemaConstraint<'value> =
        SchemaConstraint<'value>(eraseCheck constraint')

    /// Requires boundary input to be present. Presence is handled before a typed value exists.
    let required<'value> : SchemaConstraint<'value> = SchemaConstraint<'value>(presence "required" ConstraintMetadata.Required)

    /// Marks boundary input as optional. Presence is handled before a typed value exists.
    let optional<'value> : SchemaConstraint<'value> = SchemaConstraint<'value>(presence "optional" ConstraintMetadata.Optional)

    let minLength minimum : SchemaConstraint<string> = fromCheck (Axial.Check.Constraint.minLength minimum)
    let maxLength maximum : SchemaConstraint<string> = fromCheck (Axial.Check.Constraint.maxLength maximum)
    let lengthBetween minimum maximum : SchemaConstraint<string> = fromCheck (Axial.Check.Constraint.lengthBetween minimum maximum)
    let email : SchemaConstraint<string> = fromCheck Axial.Check.Constraint.email
    let trimmed : SchemaConstraint<string> = fromCheck Axial.Check.Constraint.trimmed
    let pattern expression : SchemaConstraint<string> = fromCheck (Axial.Check.Constraint.pattern expression)
    let oneOf choices : SchemaConstraint<string> = fromCheck (Axial.Check.Constraint.oneOf choices)
    let equalTo expected : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.equalTo expected)
    let notEqualTo unexpected : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.notEqualTo unexpected)
    let between minimum maximum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.between minimum maximum)
    let greaterThan minimum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.greaterThan minimum)
    let lessThan maximum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.lessThan maximum)
    let atLeast minimum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.atLeast minimum)
    let atMost maximum : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.atMost maximum)
    let private forCollection<'collection when 'collection :> IEnumerable> constraint' : SchemaConstraint<'collection> =
        constraint'
        |> Axial.Check.Constraint.contramap (fun (collection: 'collection) -> collection |> Seq.cast<obj>)
        |> fromCheck

    let count<'collection when 'collection :> IEnumerable> expected : SchemaConstraint<'collection> =
        forCollection (Axial.Check.Constraint.count expected)

    let minCount<'collection when 'collection :> IEnumerable> minimum : SchemaConstraint<'collection> =
        forCollection (Axial.Check.Constraint.minCount minimum)

    let maxCount<'collection when 'collection :> IEnumerable> maximum : SchemaConstraint<'collection> =
        forCollection (Axial.Check.Constraint.maxCount maximum)

    let countBetween<'collection when 'collection :> IEnumerable> minimum maximum : SchemaConstraint<'collection> =
        forCollection (Axial.Check.Constraint.countBetween minimum maximum)
    let distinct<'value when 'value: equality> : SchemaConstraint<'value list> = fromCheck (Axial.Check.Constraint.distinct<'value> |> Axial.Check.Constraint.forList)
    let contains item : SchemaConstraint<'value list> = fromCheck (Axial.Check.Constraint.contains item |> Axial.Check.Constraint.forList)
    let inline multipleOf divisor : SchemaConstraint<'value> = fromCheck (Axial.Check.Constraint.multipleOf divisor)

    let inline positive<'value when 'value: comparison and 'value: (static member Zero: 'value)> () : SchemaConstraint<'value> =
        greaterThan LanguagePrimitives.GenericZero<'value>
    let inline nonNegative<'value when 'value: comparison and 'value: (static member Zero: 'value)> () : SchemaConstraint<'value> =
        atLeast LanguagePrimitives.GenericZero<'value>
    let inline negative<'value when 'value: comparison and 'value: (static member Zero: 'value)> () : SchemaConstraint<'value> =
        lessThan LanguagePrimitives.GenericZero<'value>
    let inline nonPositive<'value when 'value: comparison and 'value: (static member Zero: 'value)> () : SchemaConstraint<'value> =
        atMost LanguagePrimitives.GenericZero<'value>

    let code (constraint': Constraint) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Code

    let metadata (constraint': Constraint) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Metadata

    let arguments (constraint': Constraint) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Arguments

    let tryFindArgument name (constraint': Constraint) =
        ensureText (nameof name) name
        if isNull constraint' then nullArg (nameof constraint')
        match constraint'.Arguments.TryGetValue name with
        | true, value -> Some value
        | false, _ -> None

    let message (constraint': Constraint) =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Message

    let internal tryCheck<'value> (constraint': Constraint) : Check<'value> option =
        if isNull constraint' then nullArg (nameof constraint')
        constraint'.Check
        |> Option.map (fun check value -> check (box value))

    let withMessage (message: string) (constraint': SchemaConstraint<'value>) : SchemaConstraint<'value> =
        ensureText (nameof message) message
        if isNull constraint' then nullArg (nameof constraint')
        let untyped = constraint'.Untyped
        SchemaConstraint<'value>(Constraint(untyped.Code, untyped.Metadata, untyped.Arguments, untyped.Check, Some message))

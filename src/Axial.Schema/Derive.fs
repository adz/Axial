// Inert attributes read by schemagen from source text at generation time — never by runtime
// reflection, and never touched by the schema library itself at runtime. Their vocabulary mirrors
// the constructor-last authoring surface (Shape.fs) and the `.contract` constraint grammar.
namespace Axial.Schema.Derive

open System

/// <summary>Marks a plain record for schema derivation: <c>schemagen</c> generates its permissive schema.
/// The advice is to put this on wire DTOs — records that carry no invariants of their own. The attributes
/// in this namespace are inert metadata: they are read from source text at generation time, never by
/// runtime reflection, and their vocabulary mirrors the <c>.contract</c> constraint grammar one-to-one.</summary>
[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct)>]
type DeriveSchemaAttribute() =
    inherit Attribute()

    /// The version-chain name when the record's own name does not follow the `XxxVn` convention.
    member val Chain: string = null with get, set

    /// The version within the chain when the record's own name does not follow the `XxxVn` convention.
    member val Version: int = 0 with get, set

/// <summary>Overrides the external name of one record field or one nullary union case. Without it, field
/// names follow the generation run's naming policy (camelCase by default) and case tags are the camelCased
/// case name.</summary>
[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field)>]
type SchemaNameAttribute(name: string) =
    inherit Attribute()
    member _.Name = name

/// <summary>Marks a discriminated union as an internally tagged union in the derived schema. Every case
/// must carry exactly one <c>[&lt;DeriveSchema&gt;]</c> record payload; the discriminator is the given
/// external field name.</summary>
[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct)>]
type DeriveUnionAttribute(discriminator: string) =
    inherit Attribute()
    member _.Discriminator = discriminator

/// <summary>Marks the static member the derived schema calls to assemble the record, instead of a
/// record literal. Put it on one static member of a <c>[&lt;DeriveSchema&gt;]</c> record that takes the
/// fields in declaration order and returns the record type; use it to normalise values on the way
/// in.</summary>
[<AttributeUsage(AttributeTargets.Method)>]
type SchemaConstructorAttribute() =
    inherit Attribute()

/// <summary>Constrains a text field to the given regular expression.</summary>
[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field)>]
type PatternAttribute(pattern: string) =
    inherit Attribute()
    member _.Pattern = pattern

/// <summary>Bounds the natural size of the field's type from below: text length, list count, or map count.</summary>
[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field)>]
type MinAttribute(size: int) =
    inherit Attribute()
    member _.Size = size

/// <summary>Bounds the natural size of the field's type from above: text length, list count, or map count.</summary>
[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field)>]
type MaxAttribute(size: int) =
    inherit Attribute()
    member _.Size = size

/// <summary>Bounds a numeric field's value inclusively from below (<c>&gt;=</c> in the contract grammar).
/// The literal is read from source text, so decimal precision is preserved exactly.</summary>
[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field)>]
type AtLeastAttribute private (value: obj) =
    inherit Attribute()
    member _.Value = value
    new(value: int) = AtLeastAttribute(box value)
    new(value: float) = AtLeastAttribute(box value)

/// <summary>Bounds a numeric field's value exclusively from below (<c>&gt;</c> in the contract grammar).</summary>
[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field)>]
type GreaterThanAttribute private (value: obj) =
    inherit Attribute()
    member _.Value = value
    new(value: int) = GreaterThanAttribute(box value)
    new(value: float) = GreaterThanAttribute(box value)

/// <summary>Bounds a numeric field's value inclusively from above (<c>&lt;=</c> in the contract grammar).</summary>
[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field)>]
type AtMostAttribute private (value: obj) =
    inherit Attribute()
    member _.Value = value
    new(value: int) = AtMostAttribute(box value)
    new(value: float) = AtMostAttribute(box value)

/// <summary>Bounds a numeric field's value exclusively from above (<c>&lt;</c> in the contract grammar).</summary>
[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field)>]
type LessThanAttribute private (value: obj) =
    inherit Attribute()
    member _.Value = value
    new(value: int) = LessThanAttribute(box value)
    new(value: float) = LessThanAttribute(box value)

/// <summary>Constrains a numeric field's value to whole multiples of the given step.</summary>
[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field)>]
type MultipleOfAttribute private (value: obj) =
    inherit Attribute()
    member _.Value = value
    new(value: int) = MultipleOfAttribute(box value)
    new(value: float) = MultipleOfAttribute(box value)

/// <summary>Requires the elements of a list field to be distinct.</summary>
[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field)>]
type DistinctAttribute() =
    inherit Attribute()

/// <summary>Constrains a text field to the email format.</summary>
[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field)>]
type EmailAttribute() =
    inherit Attribute()

/// <summary>Supplies the field's default when the payload omits it. Not valid on optional fields —
/// absence already parses to <c>None</c>.</summary>
[<AttributeUsage(AttributeTargets.Property ||| AttributeTargets.Field)>]
type DefaultAttribute private (value: obj) =
    inherit Attribute()
    member _.Value = value
    new(value: int) = DefaultAttribute(box value)
    new(value: float) = DefaultAttribute(box value)
    new(value: string) = DefaultAttribute(box value)
    new(value: bool) = DefaultAttribute(box value)

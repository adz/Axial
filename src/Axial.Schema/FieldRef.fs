namespace Axial.Schema

open Axial.Validation

/// <summary>A typed, named reference to one field of a schema-described model.</summary>
/// <remarks>
/// <para>
/// A field reference pairs the field's external (wire) name with its typed getter, so code that needs to talk about
/// a field — contextual rules, redisplay, UI binding — can reference it as an ordinary value instead of re-typing
/// the wire name as a string that can silently drift from the schema. Generated schema declarations emit one
/// <c>FieldRef</c> per field; hand-written schemas can declare them alongside the schema.
/// </para>
/// </remarks>
type FieldRef<'model, 'value> =
    {
        /// <summary>The field's external (wire) name, as declared on the schema.</summary>
        Name: string
        /// <summary>Reads the field's value from a model.</summary>
        Get: 'model -> 'value
    }

    /// <summary>The diagnostics path segment for this field.</summary>
    member this.Segment : PathSegment = PathSegment.Name this.Name

    /// <summary>The single-segment diagnostics path for this field.</summary>
    member this.Path : Path = [ PathSegment.Name this.Name ]

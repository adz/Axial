namespace Axial.Schema

// FS0064: the SRTP witness pattern (`(^w or ^s) : ...` with ^w fixed to a concrete witness class)
// intentionally constrains the witness type variable; the warning is noise here.
#nowarn "64"

// The constructor-last authoring surface. An ObjectShape is a structural record shape that does not
// yet know how to construct its model: define + fields = structural shape; shape + constructor =
// schema. Fields accumulate left to right in a phantom type parameter; construct/constructResult
// close the shape by matching the constructor's arguments against that phantom.

/// <summary>Phantom marker: an <see cref="T:Axial.Schema.ObjectShape`2" /> with no fields declared yet.</summary>
[<Sealed; AbstractClass>]
type NoFields = class end

/// <summary>A typed field constraint: a <see cref="T:Axial.Schema.Constraint" /> that only applies to
/// fields whose value type is <typeparamref name="'value" />. Produced by the typed constraint
/// vocabulary in <see cref="T:Axial.Schema.Syntax" />.</summary>
[<Sealed>]
type Constraint<'value> internal (untyped: Constraint) =
    member internal _.Untyped = untyped

/// <summary>
/// An unfinished structural shape for <typeparamref name="'model" />: committed fields plus one current
/// field, but no constructor yet. <typeparamref name="'fields" /> is a phantom type accumulating the
/// declared field value types left to right, e.g. <c>(NoFields * string) * int</c> after a string field
/// and an int field.
/// </summary>
/// <remarks>
/// <c>Schema.define</c> starts a shape, <c>field</c>/<c>fieldWith</c> add typed fields (committing the
/// previous one), <c>constrain</c> attaches a typed constraint to the current field, and
/// <c>construct</c>/<c>constructResult</c> commit the final field and close the shape into a
/// <see cref="T:Axial.Schema.Schema`1" /> by supplying the constructor last.
/// </remarks>
[<Sealed>]
type ObjectShape<'model, 'fields> internal (revFields: obj list) =
    /// Field definitions, most recently declared first. Each entry is a boxed
    /// FieldDefinition<'model, 'value> whose 'value the phantom type records.
    member internal _.RevFields = revFields

[<RequireQualifiedAccess>]
module internal ShapeInternals =
    let add
        (name: string)
        (getter: 'model -> 'value)
        (valueSchema: Schema<'value>)
        (shape: ObjectShape<'model, 'fields>)
        : ObjectShape<'model, 'fields * 'value> =
        if isNull (box getter) then nullArg (nameof getter)
        if isNull (box valueSchema) then nullArg (nameof valueSchema)
        if isNull (box shape) then nullArg (nameof shape)

        let definition =
            { FieldDefinition.ExternalName = ExternalFieldName.create name
              Order = FieldOrder.create 0
              Getter = getter
              ValueSchema = valueSchema.ValueDefinition
              Constraints = [] }

        ObjectShape(box definition :: shape.RevFields)

    let append
        (definition: FieldDefinition<'model, 'field>)
        (closure: ShapeClosure<'model, 'constructor, 'field -> 'next, 'chain>)
        : ShapeClosure<'model, 'constructor, 'next, ShapeFieldsAppend<'model, 'constructor, 'field, 'next, 'chain>> =
        ShapeClosure(closure.Constructor, ShapeFieldsAppend(closure.Fields, definition))

    let arityMismatch (expected: int) (fields: obj list) : 'result =
        invalidOp $"Unreachable: the shape's phantom type promises {expected} field(s) but {List.length fields} were recorded."

/// <summary>
/// Canonical value-schema resolution for the inferred <c>field</c> form. A type participates by exposing
/// <c>static member Schema: T -&gt; Schema&lt;T&gt;</c>; Axial supplies that member for its supported built-in types.
/// When no member matches, the compile error points at this constraint: use <c>fieldWith</c> with an explicit schema.
/// </summary>
[<Sealed; AbstractClass>]
type SchemaDefaults =
    /// <summary>Builds an optional schema from an explicitly resolved item schema.</summary>
    static member OptionWith(item: Schema<'item>) : Schema<'item option> = SchemaCore.option item
    /// <summary>Builds a list schema from an explicitly resolved item schema.</summary>
    static member ListWith(item: Schema<'item>) : Schema<'item list> = SchemaCore.listWith item
    /// <summary>Builds a string-keyed map schema from an explicitly resolved value schema.</summary>
    static member MapWith(item: Schema<'item>) : Schema<Map<string, 'item>> = SchemaCore.mapWith item
    static member Schema(_: string) : Schema<string> = SchemaCore.text
    static member Schema(_: int) : Schema<int> = SchemaCore.``int``
    static member Schema(_: decimal) : Schema<decimal> = SchemaCore.``decimal``
    static member Schema(_: bool) : Schema<bool> = SchemaCore.``bool``
    static member Schema(_: System.DateTimeOffset) : Schema<System.DateTimeOffset> = SchemaCore.dateTime
    static member Schema(_: System.Guid) : Schema<System.Guid> = SchemaCore.guid
#if NET8_0_OR_GREATER
    static member Schema(_: System.DateOnly) : Schema<System.DateOnly> = SchemaCore.date
#endif

    static member inline Resolve() : Schema< ^value> =
        let inline call (witness: ^w, marker: ^v) : Schema< ^v> =
            ((^w or ^v): (static member Schema: ^v -> Schema< ^v>) marker)

        call (Unchecked.defaultof<SchemaDefaults>, Unchecked.defaultof< ^value>)

    static member inline Schema(_: ^item list) : Schema< ^item list> =
        SchemaDefaults.ListWith(SchemaDefaults.Resolve< ^item>())

    static member inline Schema(_: ^item option) : Schema< ^item option> =
        SchemaDefaults.OptionWith(SchemaDefaults.Resolve< ^item>())

    static member inline Schema(_: Map<string, ^item>) : Schema<Map<string, ^item>> =
        SchemaDefaults.MapWith(SchemaDefaults.Resolve< ^item>())

/// <summary>
/// Arity implementations behind <c>construct</c> and <c>constructResult</c>. Deliberately boring: one
/// overload per field count, selected by the shape's phantom type, so a constructor mismatch is reported
/// at the closing call with fully concrete types. To support more fields, add the next arity; nothing
/// else changes.
/// </summary>
[<Sealed; AbstractClass>]
type Constructors =
    static member Construct(f: 'a0 -> 'model, shape: ObjectShape<'model, (NoFields * 'a0)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'model>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> SchemaCore.closeTotal
        | fields -> ShapeInternals.arityMismatch 1 fields

    static member ConstructResult(f: 'a0 -> Result<'model, string>, shape: ObjectShape<'model, (NoFields * 'a0)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> Result<'model, string>>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> SchemaCore.closeResult
        | fields -> ShapeInternals.arityMismatch 1 fields

    static member Construct(f: 'a0 -> 'a1 -> 'model, shape: ObjectShape<'model, ((NoFields * 'a0) * 'a1)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'model>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> SchemaCore.closeTotal
        | fields -> ShapeInternals.arityMismatch 2 fields

    static member ConstructResult(f: 'a0 -> 'a1 -> Result<'model, string>, shape: ObjectShape<'model, ((NoFields * 'a0) * 'a1)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> Result<'model, string>>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> SchemaCore.closeResult
        | fields -> ShapeInternals.arityMismatch 2 fields

    static member Construct(f: 'a0 -> 'a1 -> 'a2 -> 'model, shape: ObjectShape<'model, (((NoFields * 'a0) * 'a1) * 'a2)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'model>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> SchemaCore.closeTotal
        | fields -> ShapeInternals.arityMismatch 3 fields

    static member ConstructResult(f: 'a0 -> 'a1 -> 'a2 -> Result<'model, string>, shape: ObjectShape<'model, (((NoFields * 'a0) * 'a1) * 'a2)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> Result<'model, string>>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> SchemaCore.closeResult
        | fields -> ShapeInternals.arityMismatch 3 fields

    static member Construct(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'model, shape: ObjectShape<'model, ((((NoFields * 'a0) * 'a1) * 'a2) * 'a3)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'model>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> SchemaCore.closeTotal
        | fields -> ShapeInternals.arityMismatch 4 fields

    static member ConstructResult(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> Result<'model, string>, shape: ObjectShape<'model, ((((NoFields * 'a0) * 'a1) * 'a2) * 'a3)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> Result<'model, string>>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> SchemaCore.closeResult
        | fields -> ShapeInternals.arityMismatch 4 fields

    static member Construct(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'model, shape: ObjectShape<'model, (((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'model>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> SchemaCore.closeTotal
        | fields -> ShapeInternals.arityMismatch 5 fields

    static member ConstructResult(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> Result<'model, string>, shape: ObjectShape<'model, (((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> Result<'model, string>>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> SchemaCore.closeResult
        | fields -> ShapeInternals.arityMismatch 5 fields

    static member Construct(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'model, shape: ObjectShape<'model, ((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'model>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> SchemaCore.closeTotal
        | fields -> ShapeInternals.arityMismatch 6 fields

    static member ConstructResult(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> Result<'model, string>, shape: ObjectShape<'model, ((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> Result<'model, string>>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> SchemaCore.closeResult
        | fields -> ShapeInternals.arityMismatch 6 fields

    static member Construct(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'model, shape: ObjectShape<'model, (((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5) * 'a6)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5; f6 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'model>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a6>> f6)
            |> SchemaCore.closeTotal
        | fields -> ShapeInternals.arityMismatch 7 fields

    static member ConstructResult(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> Result<'model, string>, shape: ObjectShape<'model, (((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5) * 'a6)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5; f6 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> Result<'model, string>>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a6>> f6)
            |> SchemaCore.closeResult
        | fields -> ShapeInternals.arityMismatch 7 fields

    static member Construct(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'model, shape: ObjectShape<'model, ((((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5) * 'a6) * 'a7)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5; f6; f7 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'model>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a6>> f6)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a7>> f7)
            |> SchemaCore.closeTotal
        | fields -> ShapeInternals.arityMismatch 8 fields

    static member ConstructResult(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> Result<'model, string>, shape: ObjectShape<'model, ((((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5) * 'a6) * 'a7)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5; f6; f7 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> Result<'model, string>>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a6>> f6)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a7>> f7)
            |> SchemaCore.closeResult
        | fields -> ShapeInternals.arityMismatch 8 fields

    static member Construct(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'model, shape: ObjectShape<'model, (((((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5) * 'a6) * 'a7) * 'a8)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5; f6; f7; f8 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'model>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a6>> f6)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a7>> f7)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a8>> f8)
            |> SchemaCore.closeTotal
        | fields -> ShapeInternals.arityMismatch 9 fields

    static member ConstructResult(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> Result<'model, string>, shape: ObjectShape<'model, (((((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5) * 'a6) * 'a7) * 'a8)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5; f6; f7; f8 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> Result<'model, string>>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a6>> f6)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a7>> f7)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a8>> f8)
            |> SchemaCore.closeResult
        | fields -> ShapeInternals.arityMismatch 9 fields

    static member Construct(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'a9 -> 'model, shape: ObjectShape<'model, ((((((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5) * 'a6) * 'a7) * 'a8) * 'a9)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5; f6; f7; f8; f9 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'a9 -> 'model>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a6>> f6)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a7>> f7)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a8>> f8)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a9>> f9)
            |> SchemaCore.closeTotal
        | fields -> ShapeInternals.arityMismatch 10 fields

    static member ConstructResult(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'a9 -> Result<'model, string>, shape: ObjectShape<'model, ((((((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5) * 'a6) * 'a7) * 'a8) * 'a9)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5; f6; f7; f8; f9 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'a9 -> Result<'model, string>>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a6>> f6)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a7>> f7)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a8>> f8)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a9>> f9)
            |> SchemaCore.closeResult
        | fields -> ShapeInternals.arityMismatch 10 fields

    static member Construct(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'a9 -> 'a10 -> 'model, shape: ObjectShape<'model, (((((((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5) * 'a6) * 'a7) * 'a8) * 'a9) * 'a10)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5; f6; f7; f8; f9; f10 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'a9 -> 'a10 -> 'model>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a6>> f6)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a7>> f7)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a8>> f8)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a9>> f9)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a10>> f10)
            |> SchemaCore.closeTotal
        | fields -> ShapeInternals.arityMismatch 11 fields

    static member ConstructResult(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'a9 -> 'a10 -> Result<'model, string>, shape: ObjectShape<'model, (((((((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5) * 'a6) * 'a7) * 'a8) * 'a9) * 'a10)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5; f6; f7; f8; f9; f10 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'a9 -> 'a10 -> Result<'model, string>>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a6>> f6)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a7>> f7)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a8>> f8)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a9>> f9)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a10>> f10)
            |> SchemaCore.closeResult
        | fields -> ShapeInternals.arityMismatch 11 fields

    static member Construct(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'a9 -> 'a10 -> 'a11 -> 'model, shape: ObjectShape<'model, ((((((((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5) * 'a6) * 'a7) * 'a8) * 'a9) * 'a10) * 'a11)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5; f6; f7; f8; f9; f10; f11 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'a9 -> 'a10 -> 'a11 -> 'model>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a6>> f6)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a7>> f7)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a8>> f8)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a9>> f9)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a10>> f10)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a11>> f11)
            |> SchemaCore.closeTotal
        | fields -> ShapeInternals.arityMismatch 12 fields

    static member ConstructResult(f: 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'a9 -> 'a10 -> 'a11 -> Result<'model, string>, shape: ObjectShape<'model, ((((((((((((NoFields * 'a0) * 'a1) * 'a2) * 'a3) * 'a4) * 'a5) * 'a6) * 'a7) * 'a8) * 'a9) * 'a10) * 'a11)>) : Schema<'model> =
        match List.rev shape.RevFields with
        | [ f0; f1; f2; f3; f4; f5; f6; f7; f8; f9; f10; f11 ] ->
            ShapeClosure(f, ShapeFieldsEmpty<'model, 'a0 -> 'a1 -> 'a2 -> 'a3 -> 'a4 -> 'a5 -> 'a6 -> 'a7 -> 'a8 -> 'a9 -> 'a10 -> 'a11 -> Result<'model, string>>())
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a0>> f0)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a1>> f1)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a2>> f2)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a3>> f3)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a4>> f4)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a5>> f5)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a6>> f6)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a7>> f7)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a8>> f8)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a9>> f9)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a10>> f10)
            |> ShapeInternals.append (unbox<FieldDefinition<'model, 'a11>> f11)
            |> SchemaCore.closeResult
        | fields -> ShapeInternals.arityMismatch 12 fields

    /// <summary>Dispatches <c>construct</c> to the arity overload selected by the shape's phantom type.</summary>
    static member inline ApplyTotal(f: ^f, shape: ^s) : ^r =
        let inline call (witness: ^w, f: ^f, s: ^s) : ^r =
            ((^w or ^s): (static member Construct: ^f * ^s -> ^r) (f, s))

        call (Unchecked.defaultof<Constructors>, f, shape)

    /// <summary>Dispatches <c>constructResult</c> to the arity overload selected by the shape's phantom type.</summary>
    static member inline ApplyResult(f: ^f, shape: ^s) : ^r =
        let inline call (witness: ^w, f: ^f, s: ^s) : ^r =
            ((^w or ^s): (static member ConstructResult: ^f * ^s -> ^r) (f, s))

        call (Unchecked.defaultof<Constructors>, f, shape)

[<RequireQualifiedAccess>]
module internal ShapeOps =
    let define<'model> : ObjectShape<'model, NoFields> = ObjectShape []

    /// Model-level trusted construction: maps a permissive draft schema to a domain schema through an
    /// admission function and a projection, preserving fields, wire names, constraints, and metadata.
    let admit (create: 'draft -> Result<'domain, string>) (project: 'domain -> 'draft) (draft: Schema<'draft>) : Schema<'domain> =
        if isNull (box create) then nullArg (nameof create)
        if isNull (box project) then nullArg (nameof project)
        if isNull (box draft) then nullArg (nameof draft)

        match draft.Definition with
        | ModelDefinition definition ->
            let fields =
                definition.Fields
                |> List.map (fun field ->
                    { FieldDescriptor.ExternalName = field.ExternalName
                      Order = field.Order
                      Getter = fun (domain: 'domain) -> field.Getter(project domain)
                      ValueSchema = field.ValueSchema
                      Constraints = field.Constraints })

            let tryApply arguments =
                definition.Constructor.TryApplyTrusted arguments |> Result.bind create

            let constructor =
                { ConstructorApplication.ArgumentCount = definition.Constructor.ArgumentCount
                  ApplyTrusted =
                    fun arguments ->
                        match tryApply arguments with
                        | Ok domain -> domain
                        | Error message -> invalidOp message
                  TryApplyTrusted = tryApply }

            Schema(
                ModelDefinition
                    { Constructor = constructor
                      Fields = fields
                      Description = definition.Description },
                None
            )
        | ValueDefinition _ ->
            invalidArg (nameof draft) "Schema.admit expects a model schema; refine value schemas with Schema.refine."
        | PendingDefinition -> invalidArg (nameof draft) "Expected a completed schema definition."

/// <summary>
/// The constructor-last schema authoring vocabulary: <c>field</c>, <c>fieldWith</c>, <c>constrain</c>,
/// typed constraints, and the closing <c>construct</c>/<c>constructResult</c>. Open this module locally
/// inside a schema-definition module; start shapes with <c>Schema.define</c>.
/// </summary>
module Syntax =

    /// <summary>Adds a field whose value schema is inferred from the getter's result type. Supported types
    /// are the <see cref="T:Axial.Schema.SchemaDefaults" /> overload set plus any type exposing
    /// <c>static member Schema</c>. For anything else, use <c>fieldWith</c> with an explicit schema.</summary>
    let fieldWith
        (valueSchema: Schema<'value>)
        (name: string)
        (getter: 'model -> 'value)
        (shape: ObjectShape<'model, 'fields>)
        : ObjectShape<'model, 'fields * 'value> =
        ShapeInternals.add name getter valueSchema shape

    /// <summary>Adds a field whose value schema is inferred from the getter's result type. Supported types
    /// are the <see cref="T:Axial.Schema.SchemaDefaults" /> overload set plus any type exposing
    /// <c>static member Schema</c>. For anything else, use <c>fieldWith</c> with an explicit schema.</summary>
    let inline field (name: string) (getter: 'model -> ^value) (shape: ObjectShape<'model, 'fields>) : ObjectShape<'model, 'fields * ^value> =
        fieldWith (SchemaDefaults.Resolve()) name getter shape

    /// <summary>Attaches a typed constraint to the current (most recently declared) field. The constraint's
    /// value type must match the field's value type, so a misplaced constraint fails to compile.</summary>
    let constrain (constraint': Constraint<'value>) (shape: ObjectShape<'model, 'fields * 'value>) : ObjectShape<'model, 'fields * 'value> =
        if isNull (box constraint') then nullArg (nameof constraint')

        match shape.RevFields with
        | current :: committed ->
            let definition = unbox<FieldDefinition<'model, 'value>> current

            ObjectShape(
                box
                    { definition with
                        Constraints = definition.Constraints @ [ constraint'.Untyped ] }
                :: committed
            )
        | [] -> invalidOp "Unreachable: the phantom type guarantees a current field."

    /// <summary>Adds a typed constraint to every item described by a list schema.</summary>
    let constrainItems (constraint': Constraint<'item>) (schema: Schema<'item list>) : Schema<'item list> =
        if isNull (box constraint') then nullArg (nameof constraint')
        SchemaCore.constrainItems constraint'.Untyped schema

    /// <summary>Adds a typed constraint to every value described by a string-keyed map schema.</summary>
    let constrainValues (constraint': Constraint<'item>) (schema: Schema<Map<string, 'item>>) : Schema<Map<string, 'item>> =
        if isNull (box constraint') then nullArg (nameof constraint')
        SchemaCore.constrainValues constraint'.Untyped schema

    /// <summary>Closes a shape with a total constructor. The constructor's curried parameters must match the
    /// declared fields in order and type.</summary>
    let inline construct (f: ^f) (shape: ^s) : ^r = Constructors.ApplyTotal(f, shape)

    /// <summary>Closes a shape with a checked constructor returning <c>Result&lt;'model, string&gt;</c>. The
    /// error becomes a schema diagnostic; interpreters place it with <c>Schema.constructorErrorAt</c>.</summary>
    let inline constructResult (f: ^f) (shape: ^s) : ^r = Constructors.ApplyResult(f, shape)

    // ---- typed constraints ----

    /// <summary>Requires a text field to have at least the supplied length.</summary>
    let minLength minimum : Constraint<string> = Constraint<string>(Constraint.minLength minimum)

    /// <summary>Requires a text field to have at most the supplied length.</summary>
    let maxLength maximum : Constraint<string> = Constraint<string>(Constraint.maxLength maximum)

    /// <summary>Requires a text field's length to fall inside the supplied inclusive bounds.</summary>
    let lengthBetween minimum maximum : Constraint<string> =
        Constraint<string>(Constraint.lengthBetween minimum maximum)

    /// <summary>Requires a text field to match Axial's pragmatic email format.</summary>
    let emailFormat: Constraint<string> = Constraint<string> Constraint.email

    /// <summary>Requires a text field to have no leading or trailing whitespace.</summary>
    let trimmed: Constraint<string> = Constraint<string> Constraint.trimmed

    /// <summary>Requires a text field to match the supplied regular expression pattern.</summary>
    let pattern expression : Constraint<string> = Constraint<string>(Constraint.pattern expression)

    /// <summary>Requires a field to be at least the supplied value (inclusive).</summary>
    let atLeast (minimum: 'value) : Constraint<'value> = Constraint<'value>(Constraint.atLeast minimum)

    /// <summary>Requires a field to be at most the supplied value (inclusive).</summary>
    let atMost (maximum: 'value) : Constraint<'value> = Constraint<'value>(Constraint.atMost maximum)

    /// <summary>Requires a field to be greater than the supplied value (exclusive).</summary>
    let greaterThan (minimum: 'value) : Constraint<'value> = Constraint<'value>(Constraint.greaterThan minimum)

    /// <summary>Requires a field to be less than the supplied value (exclusive).</summary>
    let lessThan (maximum: 'value) : Constraint<'value> = Constraint<'value>(Constraint.lessThan maximum)

    /// <summary>Requires a field to fall inside the supplied inclusive bounds.</summary>
    let between (minimum: 'value) (maximum: 'value) : Constraint<'value> =
        Constraint<'value>(Constraint.between minimum maximum)

    /// <summary>Requires a field to differ from the supplied value.</summary>
    let notEqualTo (unexpected: 'value) : Constraint<'value> = Constraint<'value>(Constraint.notEqualTo unexpected)

    /// <summary>Requires a text field to equal one of the supplied choices.</summary>
    let oneOf (choices: string list) : Constraint<string> = Constraint<string>(Constraint.oneOf choices)

    /// <summary>Requires a list field to have exactly the supplied number of items.</summary>
    let count expected : Constraint<'item list> = Constraint<'item list>(Constraint.count expected)

    /// <summary>Requires a list field to have at least the supplied number of items.</summary>
    let minCount minimum : Constraint<'item list> = Constraint<'item list>(Constraint.minCount minimum)

    /// <summary>Requires a list field to have at most the supplied number of items.</summary>
    let maxCount maximum : Constraint<'item list> = Constraint<'item list>(Constraint.maxCount maximum)

    /// <summary>Requires a list field's item count to fall inside the supplied inclusive bounds.</summary>
    let countBetween minimum maximum : Constraint<'item list> =
        Constraint<'item list>(Constraint.countBetween minimum maximum)

    /// <summary>Requires a list field's items to be distinct.</summary>
    let distinct<'item when 'item: equality> : Constraint<'item list> = Constraint<'item list> Constraint.distinct

    /// <summary>Requires a list field to contain the supplied item.</summary>
    let contains (item: 'item) : Constraint<'item list> = Constraint<'item list>(Constraint.contains item)

    /// <summary>Replaces a typed constraint's user-facing message.</summary>
    let withMessage (message: string) (constraint': Constraint<'value>) : Constraint<'value> =
        Constraint<'value>(Constraint.withMessage message constraint'.Untyped)

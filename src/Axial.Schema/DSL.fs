namespace Axial.Schema

open System

/// <summary>
/// A curated schema-authoring vocabulary designed to be opened inside a schema definition module.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <c>Schema</c>, <c>SchemaConstraint</c>, and <c>Value</c>, this module is not
/// <c>RequireQualifiedAccess</c>: opening it brings the schema definition words into scope bare, so a field line
/// reads <c>text [ required; email ] "email" _.Email</c> instead of
/// <c>Schema.fieldWith [ SchemaConstraint.required; SchemaConstraint.email ] "email" _.Email Value.text</c>.
/// </para>
/// <para>
/// Every field combinator takes the constraint list first and uses one uniform shape; pass <c>[]</c> for an
/// unconstrained field. The primitive combinators <c>int</c>, <c>decimal</c>, and <c>bool</c> shadow the
/// FSharp.Core conversion functions of the same names, so open this module inside the module that defines the
/// schema rather than at file or namespace top level:
/// <code>
/// module SignupSchema =
///     open Axial.Schema.DSL
///
///     let schema =
///         recordFor&lt;Signup, _&gt; (fun email age -> { Email = email; Age = age })
///         |&gt; text [ required; email ] "email" _.Email
///         |&gt; int [ atLeast 13 ] "age" _.Age
///         |&gt; build
/// </code>
/// The shadowed conversion functions stay reachable as <c>Operators.int</c> and friends, and any accidental use of
/// a shadowed name fails to compile rather than misbehaving. Do not open this module in general application code;
/// outside schema definitions keep using the qualified <c>Schema.*</c>, <c>SchemaConstraint.*</c>, and
/// <c>Value.*</c> API, which this module reuses without replacing.
/// </para>
/// </remarks>
module DSL =

    // ---- Constraints (aliases for SchemaConstraint.*) ----

    /// <summary>Requires a value to be present. Alias for <see cref="M:Axial.Schema.SchemaConstraint.required" />.</summary>
    let required = SchemaConstraint.required

    /// <summary>Marks a value as explicitly optional. Alias for <see cref="M:Axial.Schema.SchemaConstraint.optional" />.</summary>
    let optional = SchemaConstraint.optional

    /// <summary>Requires a minimum text length. Alias for <see cref="M:Axial.Schema.SchemaConstraint.minLength" />.</summary>
    let minLength minimum = SchemaConstraint.minLength minimum

    /// <summary>Requires a maximum text length. Alias for <see cref="M:Axial.Schema.SchemaConstraint.maxLength" />.</summary>
    let maxLength maximum = SchemaConstraint.maxLength maximum

    /// <summary>Requires a text length within an inclusive range. Alias for <see cref="M:Axial.Schema.SchemaConstraint.lengthBetween" />.</summary>
    let lengthBetween minimum maximum =
        SchemaConstraint.lengthBetween minimum maximum

    /// <summary>Requires an email-shaped value. Alias for <see cref="M:Axial.Schema.SchemaConstraint.email" />.</summary>
    let email = SchemaConstraint.email

    /// <summary>Declares leading/trailing whitespace trimming. Alias for <see cref="M:Axial.Schema.SchemaConstraint.trimmed" />.</summary>
    let trimmed = SchemaConstraint.trimmed

    /// <summary>Requires a value to match a regular expression pattern. Alias for <see cref="M:Axial.Schema.SchemaConstraint.pattern" />.</summary>
    let pattern expression = SchemaConstraint.pattern expression

    /// <summary>Requires a value to be one of a fixed set of choices. Alias for <see cref="M:Axial.Schema.SchemaConstraint.oneOf" />.</summary>
    let oneOf choices = SchemaConstraint.oneOf choices

    /// <summary>Requires a value to differ from a specific unexpected value. Alias for <see cref="M:Axial.Schema.SchemaConstraint.notEqualTo" />.</summary>
    let notEqualTo unexpected = SchemaConstraint.notEqualTo unexpected

    /// <summary>Requires a numeric value within an inclusive range. Alias for <see cref="M:Axial.Schema.SchemaConstraint.between" />.</summary>
    let between minimum maximum = SchemaConstraint.between minimum maximum

    /// <summary>Requires a numeric value strictly greater than a bound. Alias for <see cref="M:Axial.Schema.SchemaConstraint.greaterThan" />.</summary>
    let greaterThan minimum = SchemaConstraint.greaterThan minimum

    /// <summary>Requires a numeric value strictly less than a bound. Alias for <see cref="M:Axial.Schema.SchemaConstraint.lessThan" />.</summary>
    let lessThan maximum = SchemaConstraint.lessThan maximum

    /// <summary>Requires a numeric value greater than or equal to a bound. Alias for <see cref="M:Axial.Schema.SchemaConstraint.atLeast" />.</summary>
    let atLeast minimum = SchemaConstraint.atLeast minimum

    /// <summary>Requires a numeric value less than or equal to a bound. Alias for <see cref="M:Axial.Schema.SchemaConstraint.atMost" />.</summary>
    let atMost maximum = SchemaConstraint.atMost maximum

    /// <summary>Requires an exact collection item count. Alias for <see cref="M:Axial.Schema.SchemaConstraint.count" />.</summary>
    let count expected = SchemaConstraint.count expected

    /// <summary>Requires a minimum collection item count. Alias for <see cref="M:Axial.Schema.SchemaConstraint.minCount" />.</summary>
    let minCount minimum = SchemaConstraint.minCount minimum

    /// <summary>Requires a maximum collection item count. Alias for <see cref="M:Axial.Schema.SchemaConstraint.maxCount" />.</summary>
    let maxCount maximum = SchemaConstraint.maxCount maximum

    /// <summary>Requires a collection item count within an inclusive range. Alias for <see cref="M:Axial.Schema.SchemaConstraint.countBetween" />.</summary>
    let countBetween minimum maximum =
        SchemaConstraint.countBetween minimum maximum

    /// <summary>Requires collection items to be distinct. Alias for <see cref="M:Axial.Schema.SchemaConstraint.distinct" />.</summary>
    let distinct = SchemaConstraint.distinct

    /// <summary>Attaches an author-supplied message override to a constraint. Alias for <see cref="M:Axial.Schema.SchemaConstraint.withMessage" />.</summary>
    let withMessage message constraint' =
        SchemaConstraint.withMessage message constraint'

    // ---- Builder entry and exit (aliases for Schema.*) ----

    /// <summary>Starts a progressive schema builder anchored to a model type. Alias for <see cref="M:Axial.Schema.Schema.recordFor``2" />.</summary>
    let recordFor<'model, 'constructor>
        (constructor: 'constructor)
        : SchemaBuilder<'model, 'constructor, 'constructor, FieldsEnd<'model, 'constructor>> =
        Schema.recordFor<'model, 'constructor> constructor

    /// <summary>Builds a model schema from a fully applied progressive builder. Alias for <see cref="M:Axial.Schema.Schema.build``4" />.</summary>
    let build (builder: SchemaBuilder<'model, 'constructor, 'model, 'chain>) : Schema<'model> = Schema.build builder

    /// <summary>Builds a model schema from a builder whose constructor returns <c>Result&lt;'model, string&gt;</c>. Alias for <see cref="M:Axial.Schema.Schema.buildResult``4" />.</summary>
    let buildResult
        (builder: SchemaBuilder<'model, 'constructor, Result<'model, string>, 'chain>)
        : Schema<'model> =
        Schema.buildResult builder

    /// <summary>Builds a model schema from a builder whose constructor returns a domain-typed <c>Result</c>. Alias for <see cref="M:Axial.Schema.Schema.buildResultWith``5" />.</summary>
    let buildResultWith
        (errorMessage: 'error -> string)
        (builder: SchemaBuilder<'model, 'constructor, Result<'model, 'error>, 'chain>)
        : Schema<'model> =
        Schema.buildResultWith errorMessage builder

    // ---- Field combinators (constraint list always first; pass [] when unconstrained) ----

    /// <summary>Appends a typed field with an explicit value schema. Constraint-first alias for <see cref="M:Axial.Schema.Schema.fieldWith``5" />.</summary>
    let field
        (constraints: SchemaConstraint list)
        externalName
        (getter: 'model -> 'field)
        (value: ValueSchema<'field>)
        (builder: SchemaBuilder<'model, 'constructor, 'field -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, 'field, 'next, 'chain>> =
        Schema.fieldWith constraints externalName getter value builder

    /// <summary>Appends a nested model field. Constraint-first alias for <see cref="M:Axial.Schema.Schema.nestedWith``5" />.</summary>
    let nested
        (constraints: SchemaConstraint list)
        externalName
        (getter: 'model -> 'nested)
        (nestedSchema: Schema<'nested>)
        (builder: SchemaBuilder<'model, 'constructor, 'nested -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, 'nested, 'next, 'chain>> =
        Schema.nestedWith constraints externalName getter nestedSchema builder

    /// <summary>Appends a collection field. Constraint-first alias for <see cref="M:Axial.Schema.Schema.manyWith``5" />.</summary>
    let many
        (constraints: SchemaConstraint list)
        externalName
        (getter: 'model -> 'item list)
        (itemSchema: Schema<'item>)
        (builder: SchemaBuilder<'model, 'constructor, 'item list -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, 'item list, 'next, 'chain>> =
        Schema.manyWith constraints externalName getter itemSchema builder

    /// <summary>Appends a text field represented as <see cref="T:System.String" />. Pass <c>[]</c> when unconstrained.</summary>
    let text
        (constraints: SchemaConstraint list)
        externalName
        (getter: 'model -> string)
        (builder: SchemaBuilder<'model, 'constructor, string -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, string, 'next, 'chain>> =
        Schema.fieldWith constraints externalName getter Value.text builder

    /// <summary>Appends a 32-bit signed integer field represented as <see cref="T:System.Int32" />. Shadows the core <c>int</c> conversion function inside the opening scope; pass <c>[]</c> when unconstrained.</summary>
    let ``int``
        (constraints: SchemaConstraint list)
        externalName
        (getter: 'model -> int)
        (builder: SchemaBuilder<'model, 'constructor, int -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, int, 'next, 'chain>> =
        Schema.fieldWith constraints externalName getter Value.``int`` builder

    /// <summary>Appends a decimal field represented as <see cref="T:System.Decimal" />. Shadows the core <c>decimal</c> conversion function inside the opening scope; pass <c>[]</c> when unconstrained.</summary>
    let ``decimal``
        (constraints: SchemaConstraint list)
        externalName
        (getter: 'model -> decimal)
        (builder: SchemaBuilder<'model, 'constructor, decimal -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, decimal, 'next, 'chain>> =
        Schema.fieldWith constraints externalName getter Value.``decimal`` builder

    /// <summary>Appends a Boolean field represented as <see cref="T:System.Boolean" />. Shadows the core <c>bool</c> conversion function inside the opening scope; pass <c>[]</c> when unconstrained.</summary>
    let ``bool``
        (constraints: SchemaConstraint list)
        externalName
        (getter: 'model -> bool)
        (builder: SchemaBuilder<'model, 'constructor, bool -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, bool, 'next, 'chain>> =
        Schema.fieldWith constraints externalName getter Value.``bool`` builder

#if NET6_0_OR_GREATER
    /// <summary>Appends a calendar date field represented as <see cref="T:System.DateOnly" />. Pass <c>[]</c> when unconstrained.</summary>
    let date
        (constraints: SchemaConstraint list)
        externalName
        (getter: 'model -> DateOnly)
        (builder: SchemaBuilder<'model, 'constructor, DateOnly -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, DateOnly, 'next, 'chain>> =
        Schema.fieldWith constraints externalName getter Value.date builder
#endif

    /// <summary>Appends an instant-like date and time field represented as <see cref="T:System.DateTimeOffset" />. Pass <c>[]</c> when unconstrained.</summary>
    let dateTime
        (constraints: SchemaConstraint list)
        externalName
        (getter: 'model -> DateTimeOffset)
        (builder: SchemaBuilder<'model, 'constructor, DateTimeOffset -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, DateTimeOffset, 'next, 'chain>> =
        Schema.fieldWith constraints externalName getter Value.dateTime builder

    /// <summary>Appends a globally unique identifier field represented as <see cref="T:System.Guid" />. Pass <c>[]</c> when unconstrained.</summary>
    let guid
        (constraints: SchemaConstraint list)
        externalName
        (getter: 'model -> Guid)
        (builder: SchemaBuilder<'model, 'constructor, Guid -> 'next, 'chain>)
        : SchemaBuilder<'model, 'constructor, 'next, FieldsAppend<'model, 'constructor, Guid, 'next, 'chain>> =
        Schema.fieldWith constraints externalName getter Value.guid builder

namespace Axial.Schema

/// <summary>
/// The universal <see cref="T:Axial.Schema.Schema`1" /> vocabulary without the <c>Schema.</c> and
/// <c>Constraint.</c> qualifiers.
/// </summary>
/// <remarks>
/// Open this module locally inside a schema-definition module. Names such as <c>int</c>, <c>decimal</c>, and
/// <c>bool</c> shadow FSharp.Core conversion functions, so qualified <c>Schema.*</c> calls remain preferable in
/// general application code. Every function delegates to the qualified API; the DSL does not define a second
/// schema grammar.
/// </remarks>
module DSL =

    // Schema values and composition
    let text = Schema.text
    let ``int`` = Schema.``int``
    let ``decimal`` = Schema.``decimal``
    let ``bool`` = Schema.``bool``
#if NET8_0_OR_GREATER
    let date = Schema.date
#endif
    let dateTime = Schema.dateTime
    let guid = Schema.guid

    let list item = Schema.list item
    let option item = Schema.option item
    let map item = Schema.map item
    let defer schema = Schema.defer schema
    let convert construct inspect schema = Schema.convert construct inspect schema
    let refine construct mapError inspect schema = Schema.refine construct mapError inspect schema
    let union discriminator payload cases = Schema.union discriminator payload cases
    let inlineUnion discriminator cases = Schema.inlineUnion discriminator cases
    let enum cases = Schema.enum cases

    let constrain constraint' schema = Schema.constrain constraint' schema
    let constrainAll constraints schema = Schema.constrainAll constraints schema
    let withFormat format schema = Schema.withFormat format schema
    let describe description schema = Schema.describe description schema
    let withDefault value schema = Schema.withDefault value schema

    // Record schemas
    let record constructor = Schema.record constructor

    let recordFor<'model, 'constructor> constructor =
        Schema.recordFor<'model, 'constructor> constructor

    let field name getter schema builder = Schema.field name getter schema builder
    let build builder = Schema.build builder
    let buildResult builder = Schema.buildResult builder
    let buildResultWith render builder = Schema.buildResultWith render builder

    // Interpretation
    let parse schema input = Schema.parse schema input
    let parseWith configure schema input = Schema.parseWith configure schema input
    let check schema value = Schema.check schema value

    // Constraints
    let required = Constraint.required
    let optional = Constraint.optional
    let minLength minimum = Constraint.minLength minimum
    let maxLength maximum = Constraint.maxLength maximum
    let lengthBetween minimum maximum = Constraint.lengthBetween minimum maximum
    let email = Constraint.email
    let trimmed = Constraint.trimmed
    let pattern expression = Constraint.pattern expression
    let oneOf choices = Constraint.oneOf choices
    let notEqualTo unexpected = Constraint.notEqualTo unexpected
    let between minimum maximum = Constraint.between minimum maximum
    let greaterThan minimum = Constraint.greaterThan minimum
    let lessThan maximum = Constraint.lessThan maximum
    let atLeast minimum = Constraint.atLeast minimum
    let atMost maximum = Constraint.atMost maximum

    let inline positive<'value when 'value: (static member Zero: 'value)> () =
        Constraint.positive<'value> ()

    let inline nonNegative<'value when 'value: (static member Zero: 'value)> () =
        Constraint.nonNegative<'value> ()

    let inline negative<'value when 'value: (static member Zero: 'value)> () =
        Constraint.negative<'value> ()

    let inline nonPositive<'value when 'value: (static member Zero: 'value)> () =
        Constraint.nonPositive<'value> ()

    let count expected = Constraint.count expected
    let minCount minimum = Constraint.minCount minimum
    let maxCount maximum = Constraint.maxCount maximum
    let countBetween minimum maximum = Constraint.countBetween minimum maximum
    let distinct = Constraint.distinct
    let contains item = Constraint.contains item
    let withMessage message constraint' = Constraint.withMessage message constraint'

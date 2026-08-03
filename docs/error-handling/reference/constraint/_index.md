---
title: "Constraint"
weight: 70
---

This page shows `Constraint<'value>`: one reusable description of valid values, shared by direct checking, refined-value admission, Schema, and export. `check` runs it, `test` answers the same question as a `bool`, and `guard` keeps the input after success. There is no separate Check type and no second constructor catalogue. Interpreted constructors build one `ConstraintAtom` that drives both execution and description; `custom`, `customWith`, `notWith`, and `contramap` are the opaque escape hatch, which runs normally and is honestly invisible to export and proof.

## Core types

- [`Constraint`](./t-constraint-constraint.md):
 A reusable description of valid values, coupled to the closures that execute it.

- [`Constraint.Violation`](./t-constraint-violation.md): Why a value failed its constraint.
- [`Constraint.AtomicViolation`](./t-constraint-atomicviolation.md): Why one indivisible constraint failed.
- [`Constraint.ConstraintDescription`](./t-constraint-constraintdescription.md):
 What a constraint says, as inspectable data.

- [`Constraint.ConstraintExpression`](./t-constraint-constraintexpression.md): The logical form of a constraint.
- [`Constraint.ConstraintAtom`](./t-constraint-constraintatom.md):
 One interpreted primitive: the complete semantic identity of a built-in constraint.

- [`Constraint.OpaqueConstraint`](./t-constraint-opaqueconstraint.md): Why a constraint is invisible to export and proof.
- [`Constraint.ConstraintValue`](./t-constraint-constraintvalue.md):
 The closed set of operand and actual-value representations a constraint may carry across a description,
 diagnostic, or localization boundary.


## Expectations

- [`Constraint.Presence`](./t-constraint-presence.md): What a presence rule expects of a value&#39;s shape.
- [`Constraint.Cardinality`](./t-constraint-cardinality.md): What a size rule expects of a text length or collection count.
- [`Constraint.RelationOperator`](./t-constraint-relationoperator.md): The comparison a relation asserts between a value and an operand.
- [`Constraint.Relation`](./t-constraint-relation.md): What an ordering or equality rule expects.
- [`Constraint.Membership`](./t-constraint-membership.md): What a membership rule expects.
- [`Constraint.Format`](./t-constraint-format.md): The built-in text formats. Every case names one Axial-owned executable predicate.
- [`Constraint.Number`](./t-constraint-number.md): What a numeric-property rule expects.
- [`Constraint.UnsupportedOperation`](./t-constraint-unsupportedoperation.md): A built-in operation that received an operand outside the portable value set.

## Execution

- [`Constraint.test`](./m-constraint-constraint-test.md): Answers whether a value satisfies a constraint, without building a violation.
- [`Constraint.check`](./m-constraint-constraint-check.md): Runs a constraint, returning why the value failed.
- [`Constraint.guard`](./m-constraint-constraint-guard.md): Runs a constraint and returns the unchanged value after success.
- [`Constraint.inspect`](./m-constraint-constraint-inspect.md): Returns the constraint&#39;s inspectable description.

## Composition

- [`Constraint.all`](./m-constraint-constraint-all.md):
 Requires every constraint to hold, evaluating each in declaration order and accumulating failures. The
 empty list is the satisfied identity.

- [`Constraint.any`](./m-constraint-constraint-any.md):
 Requires at least one alternative to hold, evaluating left to right and stopping at the first success. When
 none succeeds, every rejected branch is reported.

- [`Constraint.optional`](./m-constraint-constraint-optional.md): Lifts a constraint over an optional container: absence passes, presence runs the inner constraint.
- [`Constraint.notWith`](./m-constraint-constraint-notwith.md):
 Negates a constraint. The result is opaque: it runs normally but cannot be exported or proved, and reports
 the supplied prose.

- [`Constraint.custom`](./m-constraint-constraint-custom.md): Runs an arbitrary predicate, reporting the supplied prose when it fails.
- [`Constraint.customLocalized`](./m-constraint-constraint-customlocalized.md):
 Runs an arbitrary predicate, reporting the supplied prose and the author&#39;s own catalogue key when it fails.

- [`Constraint.customLocalizedWith`](./m-constraint-constraint-customlocalizedwith.md):
 Runs an arbitrary predicate, reporting the supplied prose plus a catalogue key and named arguments a
 translation can interpolate.

- [`Constraint.customWith`](./m-constraint-constraint-customwith.md): Runs an arbitrary callback that reports its own violation.
- [`Constraint.contramap`](./m-constraint-constraint-contramap.md): Applies a constraint to a projection of a larger value.
- [`Constraint.describe`](./m-constraint-constraint-describe.md): Attaches documentary prose to a constraint.

## Presence and size

- [`Constraint.present`](./m-constraint-constraint-present.md): Requires a value to be inhabited according to its shape.
- [`Constraint.blank`](./m-constraint-constraint-blank.md): Requires a value to be uninhabited according to its shape; the exact complement of <code>present</code>.
- [`Constraint.length`](./m-constraint-constraint-length.md): Requires text or a collection to have exactly the supplied size.
- [`Constraint.minLength`](./m-constraint-constraint-minlength.md): Requires text or a collection to have at least the supplied size.
- [`Constraint.maxLength`](./m-constraint-constraint-maxlength.md): Requires text or a collection to have at most the supplied size.
- [`Constraint.lengthBetween`](./m-constraint-constraint-lengthbetween.md): Requires a text or collection size inside the supplied inclusive bounds.

## Text formats

- [`Constraint.email`](./m-constraint-constraint-email.md): Requires text to match Axial&#39;s pragmatic email shape, <code>^[^@]+@[^@]+$</code>.
- [`Constraint.trimmed`](./m-constraint-constraint-trimmed.md): Requires text to have no leading or trailing whitespace.
- [`Constraint.numeric`](./m-constraint-constraint-numeric.md): Requires text to be one or more ASCII digits.
- [`Constraint.alphanumeric`](./m-constraint-constraint-alphanumeric.md): Requires text to be one or more letters or digits.
- [`Constraint.pattern`](./m-constraint-constraint-pattern.md): Requires text to match the supplied .NET regular expression.

## Relations and membership

- [`Constraint.equalTo`](./m-constraint-constraint-equalto.md): Requires equality with the supplied value, under F# structural equality.
- [`Constraint.notEqualTo`](./m-constraint-constraint-notequalto.md): Requires inequality with the supplied value, under F# structural equality.
- [`Constraint.greaterThan`](./m-constraint-constraint-greaterthan.md): Requires a value strictly greater than the supplied bound.
- [`Constraint.lessThan`](./m-constraint-constraint-lessthan.md): Requires a value strictly less than the supplied bound.
- [`Constraint.atLeast`](./m-constraint-constraint-atleast.md): Requires a value greater than or equal to the supplied bound.
- [`Constraint.atMost`](./m-constraint-constraint-atmost.md): Requires a value less than or equal to the supplied bound.
- [`Constraint.between`](./m-constraint-constraint-between.md): Requires a value inside the supplied inclusive bounds.
- [`Constraint.oneOf`](./m-constraint-constraint-oneof.md): Requires the value to equal one of the supplied choices.
- [`Constraint.contains`](./m-constraint-constraint-contains.md): Requires a collection to contain the supplied item.
- [`Constraint.distinct`](./m-constraint-constraint-distinct.md): Requires a collection to hold no duplicates. The first repeat is reported as the actual value.

## Numeric properties

- [`Constraint.multipleOf`](./m-constraint-constraint-multipleof.md): Requires an exact multiple of the supplied divisor, under the value type&#39;s own arithmetic.
- [`Constraint.finite`](./m-constraint-constraint-finite.md): Requires a double to be neither infinite nor <code>NaN</code>.
- [`Constraint.finite32`](./m-constraint-constraint-finite32.md): Requires a single-precision float to be neither infinite nor <code>NaN</code>.

## Messages

- [`Constraint.MessageTree`](./t-constraint-messagetree.md): A violation projected for an external localization system, retaining its grouping.
- [`Constraint.MessageLeaf`](./t-constraint-messageleaf.md): One leaf of a projected message tree.
- [`Constraint.MessageDescriptor`](./t-constraint-messagedescriptor.md): A message identity and the operands its template may interpolate.
- [`Constraint.MessageFormatSpec`](./t-constraint-messageformatspec.md):
 A descriptor plus the rendering metadata its owning catalogue holds: the neutral fallback template and the
 optional plural operand.

- [`Constraint.MessageKeyError`](./t-constraint-messagekeyerror.md): Why a relative message key could not be parsed.
- [`Constraint.MessageFormatSpecError`](./t-constraint-messageformatspecerror.md): Why a message format specification was rejected.
- [`Constraint.MessageDescriptor.key`](./m-constraint-messagedescriptor-key.md): The canonical unencoded key, exactly as authored.
- [`Constraint.MessageDescriptor.arguments`](./m-constraint-messagedescriptor-arguments.md): The operands the message interpolates, named for the template.
- [`Constraint.MessageDescriptor.segments`](./m-constraint-messagedescriptor-segments.md): The parsed, unencoded key segments.
- [`Constraint.MessageDescriptor.Advanced.create`](./m-constraint-messagedescriptor-advanced-create.md): Parses a relative key, raising for a malformed programmer-authored key.
- [`Constraint.MessageDescriptor.Advanced.tryCreate`](./m-constraint-messagedescriptor-advanced-trycreate.md): Parses a relative key, returning the parse failure rather than raising.
- [`Constraint.MessageDescriptor.Advanced.ofSegments`](./m-constraint-messagedescriptor-advanced-ofsegments.md): Builds a descriptor from already-parsed segments, skipping the parse.
- [`Constraint.MessageFormatSpec.descriptor`](./m-constraint-messageformatspec-descriptor.md): The message identity and its arguments.
- [`Constraint.MessageFormatSpec.fallback`](./m-constraint-messageformatspec-fallback.md): The owning catalogue's neutral template, used when no resource resolves.
- [`Constraint.MessageFormatSpec.pluralArgument`](./m-constraint-messageformatspec-pluralargument.md): The argument a translator may pluralize on, when the catalogue declares one.
- [`Constraint.MessageFormatSpec.Advanced.create`](./m-constraint-messageformatspec-advanced-create.md): Builds a specification, raising when the plural operand names no argument.
- [`Constraint.MessageFormatSpec.Advanced.tryCreate`](./m-constraint-messageformatspec-advanced-trycreate.md): Builds a specification, returning the validation failure rather than raising.

## Rendering

- [`Constraint.Renderer`](./t-constraint-renderer.md):
 Renders localized messages for one document context and attribute. Immutable: build one at the composition
 root and derive scoped copies with <code>context</code> and <code>attribute</code>.

- [`MessageLookup`](./t-constraint-messagelookup.md): The ordinary resource lookup: an encoded resource key in, a translated template out.
- [`Constraint.MessageRequest`](./t-constraint-messagerequest.md): One contextual level&#39;s request to an advanced resolver.
- [`Constraint.MessageResolution`](./t-constraint-messageresolution.md): What an advanced resolver found for one contextual level.
- [`MessageResolver`](./t-constraint-messageresolver.md): Resolves one contextual level, or declines so Axial continues to a less specific one.
- [`Constraint.ValueFormatRequest`](./t-constraint-valueformatrequest.md): A value to format, with the placeholder's format suffix when it carried one.
- [`Constraint.Renderer.english`](./p-constraint-renderer-english.md): A renderer that uses each catalogue&#39;s neutral English, with no resources at all.
- [`Constraint.Renderer.ofLookup`](./m-constraint-renderer-oflookup.md): A renderer backed by any key-to-template lookup.
- [`Constraint.Renderer.ofResourceManager`](./m-constraint-renderer-ofresourcemanager.md): A renderer backed by a .NET resource manager, using one culture for everything.
- [`Constraint.Renderer.ofResourceManagerWithCultures`](./m-constraint-renderer-ofresourcemanagerwithcultures.md): A renderer that looks messages up in one culture and formats operands in another.
- [`Constraint.Renderer.ofCurrentCulture`](./m-constraint-renderer-ofcurrentculture.md): A renderer that reads the ambient cultures at each render rather than capturing them.
- [`Constraint.Renderer.context`](./m-constraint-renderer-context.md): Appends a document, model, form, or component segment.
- [`Constraint.Renderer.attribute`](./m-constraint-renderer-attribute.md): Replaces the attribute with one segment.
- [`Constraint.Renderer.unscoped`](./m-constraint-renderer-unscoped.md): Clears both the context and the attribute.
- [`Constraint.Renderer.withValues`](./m-constraint-renderer-withvalues.md): Replaces all operand rendering with one callback, ignoring placeholder format suffixes.
- [`Constraint.Renderer.attributeName`](./m-constraint-renderer-attributename.md): The attribute noun this renderer composes into a full message.
- [`Constraint.Renderer.fullMessage`](./m-constraint-renderer-fullmessage.md): Composes the attribute noun once around an already-rendered message.
- [`Constraint.Renderer.Advanced.ofResolver`](./m-constraint-renderer-advanced-ofresolver.md): A renderer backed by a resolver that answers one contextual level at a time.
- [`Constraint.Renderer.Advanced.withValueFormatting`](./m-constraint-renderer-advanced-withvalueformatting.md): Replaces operand formatting with a callback that receives the placeholder&#39;s format suffix.
- [`Constraint.Renderer.Advanced.attributePath`](./m-constraint-renderer-advanced-attributepath.md): Sets the attribute to a complete path, replacing any previous one.
- [`Constraint.Renderer.Advanced.lookupCandidates`](./m-constraint-renderer-advanced-lookupcandidates.md): Every encoded resource key ordinary lookup will try, in order.
- [`Constraint.Renderer.Advanced.messageRequests`](./m-constraint-renderer-advanced-messagerequests.md): One request per contextual level, as an advanced resolver receives them.
- [`Constraint.Renderer.Advanced.attributeCandidates`](./m-constraint-renderer-advanced-attributecandidates.md): Every encoded attribute-noun key, most specific first.
- [`Constraint.Renderer.Advanced.format`](./m-constraint-renderer-advanced-format.md): Renders any catalogue&#39;s entry through the full contextual, plural, and formatting path.

## Catalogue

- [`Constraint.Catalogue.keys`](./p-constraint-catalogue-keys.md): Every message key Axial can produce, including the composition and joining entries.
- [`Constraint.Catalogue.arguments`](./p-constraint-catalogue-arguments.md): The argument names each entry&#39;s template may interpolate.
- [`Constraint.Catalogue.english`](./p-constraint-catalogue-english.md): The neutral English template for each entry, used when no resource resolves.
- [`Constraint.Catalogue.pluralArgument`](./p-constraint-catalogue-pluralargument.md): The argument each entry may be pluralized on, when it declares one.

## Violations

- [`Constraint.Violation.render`](./m-constraint-violation-render.md):
 Renders a violation as an English sentence fragment with no trailing punctuation, keeping conjunction and
 alternative groups distinct.

- [`Constraint.Violation.message`](./m-constraint-violation-message.md): Renders a violation as a localized predicate, with no attribute noun.
- [`Constraint.Violation.fullMessage`](./m-constraint-violation-fullmessage.md): Renders a violation as a complete sentence fragment, with the attribute noun composed once.
- [`Constraint.Violation.renderWith`](./m-constraint-violation-renderwith.md):
 Renders a violation through a caller-supplied lookup, keeping the same grouping and separators
 <code>render</code> uses.

- [`Constraint.Violation.toMessageTree`](./m-constraint-violation-tomessagetree.md):
 Projects a violation for an external localization system, preserving its grouping so a translator renders
 conjunctions and alternatives in their own word order.

- [`Constraint.Violation.children`](./m-constraint-violation-children.md): The immediate children of a group, or an empty list for an atomic violation.
- [`Constraint.Violation.flatten`](./m-constraint-violation-flatten.md): Every leaf of a violation tree, in report order.
- [`Constraint.Violation.tryExpectation`](./m-constraint-violation-tryexpectation.md): The failing constraint's identity, when the violation is a single interpreted leaf.
- [`Constraint.Violation.tryActual`](./m-constraint-violation-tryactual.md): The value that failed, when the violation is a single leaf carrying a portable one.
- [`Constraint.Violation.tryDescription`](./m-constraint-violation-trydescription.md): The author-supplied prose, when the violation is a single opaque leaf.
- [`Constraint.Violation.conjoin`](./m-constraint-violation-conjoin.md):
 Groups failures as a conjunction, returning <code>None</code> for no failures and the single failure unchanged
 for one.

- [`Constraint.Violation.alternatives`](./m-constraint-violation-alternatives.md):
 Groups failures as rejected alternatives, returning <code>None</code> for no failures and the single failure
 unchanged for one.


## Descriptions and values

- [`Constraint.ConstraintDescription.children`](./m-constraint-constraintdescription-children.md): The immediate child descriptions of a node, in authored order.
- [`Constraint.ConstraintDescription.atoms`](./m-constraint-constraintdescription-atoms.md): Every interpreted primitive reachable without crossing an opacity boundary, in authored order.
- [`Constraint.ConstraintDescription.isOpaque`](./m-constraint-constraintdescription-isopaque.md): True when the node itself declines export and proof. Its children may still be inspectable.
- [`Constraint.ConstraintAtom.key`](./m-constraint-constraintatom-key.md): The stable message key for an atom, derived mechanically from its case.
- [`Constraint.ConstraintAtom.render`](./m-constraint-constraintatom-render.md): The default English phrase describing what an atom expected.
- [`Constraint.ConstraintAtom.arguments`](./m-constraint-constraintatom-arguments.md): The expectation operands an atom carries, named for message interpolation.
- [`Constraint.ConstraintValue.tryCreate`](./m-constraint-constraintvalue-trycreate.md):
 Projects a runtime value to its portable representation, or <code>None</code> when the type is outside the closed
 set. This never throws, including for <code>NaN</code>, infinities, and values no numeric case can hold.

- [`Constraint.ConstraintValue.render`](./m-constraint-constraintvalue-render.md): Renders a portable value for a default English message. Not a wire format.

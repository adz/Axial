---
weight: 40
title: Localization
type: docs
description: Translating constraint failures, with the complete key catalogue.
---


Every built-in failure carries a **key** and **named arguments** rather than English:

```text
constraint.cardinality.minimum   { minimum = 3; actual = 2 }
```

`Violation.render` turning that into English is the zero-dependency default, not the only option. Feed the key and
arguments to whatever resource system you already have and you get any language.

## Render through your catalogue

```fsharp
let localize (descriptor: MessageDescriptor) =
    resources.Format(descriptor.Key, descriptor.Arguments)

violation |> Violation.renderWith localize
// "doit être renseigné; longueur entre 2 et 40"
```

`renderWith` keeps the same grouping and separators `render` uses: conjunctions join with `; `, alternatives with
`, or `. That is the whole path for the common case.

When a translator needs to control word order *across* a group — some languages will not accept a conjunction
joined the way English joins one — project the tree instead and walk it yourself:

```fsharp
match Violation.toMessageTree violation with
| MessageTree.Leaf (MessageLeaf.Localized descriptor) -> localize descriptor
| MessageTree.All (first, rest) -> // your own conjunction
| MessageTree.Any (first, rest) -> // your own disjunction
```

## Your own messages

A rule you author with `Constraint.custom` hands Axial an English string and nothing else, so that string is all
Axial can give back. It projects as `MessageLeaf.Verbatim` — untranslatable, by construction.

Supply your own catalogue key and it becomes translatable like any other:

```fsharp
let isbn =
    Constraint.customLocalized
        "must be a valid ISBN"
        { Key = "signup.isbn"; Arguments = Map.empty }
        isValidIsbn
```

The prose is still required and is still what `render` produces. The key only changes what a translator sees.

Axial never invents a key for your text. A key it made up would name a catalogue entry that does not exist, and
the lookup would fail at runtime in whichever language you had not thought about. You have the catalogue; you name
the entry.

## The key catalogue

Every key Axial can produce, with the arguments its message may interpolate. `actual` is added to any expectation
key when the failing value has a portable representation, so treat it as optional in every template.

| Key | Arguments | Default English |
| --- | --- | --- |
| `constraint.presence.present` | — | value must be present |
| `constraint.presence.blank` | — | value must be blank |
| `constraint.cardinality.exact` | `expected` | expected a size of exactly {expected} |
| `constraint.cardinality.minimum` | `minimum` | expected a size of at least {minimum} |
| `constraint.cardinality.maximum` | `maximum` | expected a size of at most {maximum} |
| `constraint.cardinality.between` | `minimum`, `maximum` | expected a size between {minimum} and {maximum} |
| `constraint.relation.equal` | `expected` | expected {expected} |
| `constraint.relation.notEqual` | `expected` | expected a value other than {expected} |
| `constraint.relation.greaterThan` | `expected` | expected a value greater than {expected} |
| `constraint.relation.lessThan` | `expected` | expected a value less than {expected} |
| `constraint.relation.atLeast` | `expected` | expected a value at least {expected} |
| `constraint.relation.atMost` | `expected` | expected a value at most {expected} |
| `constraint.relation.within` | `minimum`, `maximum` | expected a value between {minimum} and {maximum} |
| `constraint.membership.oneOf` | `choices` | expected one of: {choices} |
| `constraint.membership.noneOf` | `choices` | expected none of: {choices} |
| `constraint.membership.contains` | `item` | expected the collection to contain {item} |
| `constraint.membership.notContains` | `item` | expected the collection not to contain {item} |
| `constraint.uniqueness` | — | duplicate values are not allowed |
| `constraint.format.email` | — | expected an email address |
| `constraint.format.trimmed` | — | expected no leading or trailing whitespace |
| `constraint.format.numeric` | — | expected digits only |
| `constraint.format.alphanumeric` | — | expected letters and digits only |
| `constraint.format.pattern` | `pattern` | expected a match for {pattern} |
| `constraint.number.multipleOf` | `divisor` | expected a multiple of {divisor} |
| `constraint.number.finite` | — | expected a finite number |

A built-in whose operand has no portable representation reports the operation rather than approximating the
operand. These keys carry no arguments:

| Key | Default English |
| --- | --- |
| `constraint.unsupportedOperand.relation.equal` | failed an equality rule whose operand has no portable representation |
| `constraint.unsupportedOperand.relation.notEqual` | failed an inequality rule … |
| `constraint.unsupportedOperand.relation.greaterThan` | failed a greater-than rule … |
| `constraint.unsupportedOperand.relation.lessThan` | failed a less-than rule … |
| `constraint.unsupportedOperand.relation.atLeast` | failed an at-least rule … |
| `constraint.unsupportedOperand.relation.atMost` | failed an at-most rule … |
| `constraint.unsupportedOperand.within` | failed a range rule … |
| `constraint.unsupportedOperand.contains` | failed a containment rule … |
| `constraint.unsupportedOperand.multipleOf` | failed a multiple-of rule … |

Keys are derived mechanically from the atom, so this table is the complete catalogue and can be generated into an
ICU or resource template. A test enumerates the atom union against this page, so a new rule cannot ship with its
key undocumented.

## Argument values

Arguments are `ConstraintValue`, the portable value type — text, char, boolean, integer, decimal, big integer,
float, GUID, timespan, date-time, date-time-offset, null, and lists of those. `ConstraintValue.render` gives the
default English rendering of one; a localized template will usually want to format numbers and dates through the
target culture instead.

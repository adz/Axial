---
weight: 20
title: Check
type: docs
notoc: true
description: Reusable, named structural checks that attach to Result.
---

# Check

Some rules apply in more than one place: "this string is present," "this number is at least 18." Writing the
condition inline each time works until the rule changes and one call site is missed, or until you want the same
rule to explain itself the same way everywhere it fails.

`Axial.Check` gives that kind of rule a name and a type. A `Check<'value>` takes a value and returns the same value
on success or a structured [`CheckFailure`]({{< relref "/error-handling/reference/check/t-errorhandling-checkfailure.md" >}})
list on failure — not a loose string, and not a different shape than what went in. Checks compose (`Check.all`, `Check.any`, `Check.not`),
attach to a domain error with `orError`/`mapError`, and return the standard F# `Result`, so they work directly in
`result {}`, [`flow {}`]({{< relref "/flow/" >}}), or your own composition style.

`Predicate` gives the same facts as a plain `bool`, for a local `if` or `match` that doesn't need a `Result`.

Continue to [Using Check](./overview/) for the DSL, composition, and how Check relates to Result and Predicate.

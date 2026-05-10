---
title: "FsFlow.Builders.result"
linkTitle: "result"
---

The fail-fast `result { }` computation expression.

## Remarks

<para>
 Use this builder when the happy path should short-circuit on the first error
 and you want to keep the workflow in `Result` shape all the way through.
 </para>
 <para>
 It works well for parsing, validation, and other boundaries where failure is expected
 to stop the flow immediately instead of accumulating diagnostics.
 </para>
 <para>
 Use `Check.orError` when a pure check needs a domain error, and `Guard.MapError` when
 you need to remap an existing error before entering the CE.
 </para>


## Examples

```fsharp
 let parsedUser =
     result {
         let! age = parseAge input
         let! name = parseName input
         return { Age = age; Name = name }
     }
 ```


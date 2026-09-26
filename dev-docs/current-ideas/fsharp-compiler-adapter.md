# F# compiler adapter and pooled-resource layer

Prompted by FsLiveDocs, the documentation tool that builds this site, adopting Axial. That adoption surfaced two
related gaps that belong in Axial rather than in each consumer.

## 1. Generic pooled-resource combinator (`Axial.Layers`) — shipped in 0.9.0

Done: `Layer.pool : size:int -> (int -> Layer<'input,'error,'resource>) -> Layer<'input,'error,Pool<'resource>>` plus
`Pool<'resource>` (`Next()` round-robin accessor, `Count`, `Instances`) in `src/Axial.Layers/Layer.fs`. Sequential
acquisition, `acquireRelease`-based release in reverse order, same as every other layer combinator. Tested in
`tests/Axial.Tests/WorkflowResourceTests.fs`. See `dev-docs/releases/0.9.0.md`.

A consumer that pools F# checkers can build the pool on this instead of managing its own round-robin index.

## 2. `Axial.FSharpCompiler` adapter

A documentation tool needs a normalized F# semantic model on top of the F# Compiler Service (FCS): resolved XML
docs, renderer-neutral symbol records, accessibility, and signatures. That model is worth offering as its own Axial
adapter, in the same family as `Axial.Console`/`FileSystem`/`Process`/`HttpClient` (an effectful, error-prone,
resource-heavy subsystem wrapped as a typed-env `Flow` service), because:

- The adapter is the place to keep any workaround needed at awkward FCS boundaries, such as reading XML docs. Check
  which typed accessors the current FCS release offers before designing this part.
- A useful model does more than pass FCS through: XML-doc resolution cached by file version, symbols normalized away
  from FCS's own types, and signature matching.
- It builds on the pool combinator above instead of duplicating it.

Sketch:

```fsharp
namespace Axial.FSharpCompiler

type SymbolInfo =
    { Signature: string; Kind: SymbolKind; Documentation: string option
      DisplayName: string; Accessibility: Accessibility; Range: SourceRange }

type ProjectSymbols = { Symbols: SymbolInfo list; Diagnostics: Diagnostic list }

type ICompiler =
    abstract Check : CompilationUnit -> Flow<unit, CompilerError, CheckResult>
    abstract ExtractSymbols : projectPath: string -> Flow<unit, CompilerError, ProjectSymbols>

type IHasCompiler = abstract Compiler : ICompiler
```

Also intended to cover: running compiler warning checks and documentation code-sample checks as named operations on
`ICompiler`, not just diagnostics from a single check call, so tools and agents can query structured API-surface and
check results instead of searching source text.

## Sequencing

Follow-up work. Build it once a consumer is using the existing Axial surface, so the design follows real usage
rather than speculation.

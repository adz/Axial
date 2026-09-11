# F# compiler adapter and pooled-resource layer

Prompted by FsLiveDocs adopting Axial (see `FsLiveDocs/dev-docs/new-ideas/axial-reified-dogfooding.md`). Two related
gaps surfaced there that belong in Axial, not worked around downstream.

## 1. Generic pooled-resource combinator (`Axial.Layers`) — shipped in 0.9.0

Done: `Layer.pool : size:int -> (int -> Layer<'input,'error,'resource>) -> Layer<'input,'error,Pool<'resource>>` plus
`Pool<'resource>` (`Next()` round-robin accessor, `Count`, `Instances`) in `src/Axial.Layers/Layer.fs`. Sequential
acquisition, `acquireRelease`-based release in reverse order, same as every other layer combinator. Tested in
`tests/Axial.Tests/WorkflowResourceTests.fs`. See `dev-docs/releases/0.9.0.md`.

FsLiveDocs' Runner should build its `ICompilerService`'s checker pool on this instead of hand-rolling
`Interlocked.Increment` + array indexing.

## 2. `Axial.FSharpCompiler` adapter

FsLiveDocs' `SemanticExtractor.fs`/`SymbolLister.fs` build a normalized F# semantic model (resolved XML docs,
renderer-neutral symbol records, accessibility, signatures) on top of raw FCS. This is worth extracting as its own
Axial adapter — same family as `Axial.Console`/`FileSystem`/`Process`/`HttpClient` (wrap an effectful, error-prone,
resource-heavy subsystem as a typed-env `Flow` service) — because:

- FCS's own API is genuinely awkward at these boundaries: `FSharpXmlDoc` is unpacked via
  `FSharpValue.GetUnionFields` reflection in FsLiveDocs today because there's no clean typed accessor, with a comment
  admitting it's a defensive workaround for FCS instability.
- FsLiveDocs already derives real value beyond passthrough: XML-doc resolution cached by file version, symbols
  normalized away from FCS's own types, tooltip signature matching.
- Built on the pool combinator above instead of duplicating it.

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

Also intended to cover: running compiler warning checks and FsLiveDocs' own doc-code-fence ("livedocs") checks as
named operations on `ICompiler`, not just diagnostics from a single check call — see the FsLiveDocs doc for the
agent-facing motivation (structured, queryable API-surface + check results instead of an LLM grepping source).

## Sequencing

Follow-up work, not blocking FsLiveDocs' initial Axial/Reified adoption pass. Build once FsLiveDocs is dogfooding the
existing Axial surface, informed by real usage rather than speculative design.

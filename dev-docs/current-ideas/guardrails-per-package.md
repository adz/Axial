# Per-package guardrails

Let each package contribute its own guardrail, and let consumers turn guardrails and individual rules on or off per
project.

## Problem

- Every rule is hardcoded in `src/Axial.Guardrails/EffectCatalog.fs`. The catalog already recommends services the
  consumer may not reference: the `File.*` rule points at `IFileSystem` whether or not Axial.FileSystem is installed.
- Configuration is global: `AxialGuardrailsEnabled`, one `AxialGuardrailsSeverity`, and a code list fixed in
  `build/Axial.Guardrails.targets`. A consumer cannot keep the HTTP rules and drop the console rules.
- A package can only add rules by shipping its own analyzer DLL, which bundles FSharp.Compiler.Service and costs the
  CLI a second type-check of the project.

## Vocabulary

- A **guardrail** is a named group of rules contributed by one package, usually named after it: `Axial.HttpClient`.
- A **rule** is one check inside a guardrail, with a stable id such as `http.new-client`.
- A **category** is the word used in `// axial-allow-effect: <category>` comments. Several rules can share a category.

## Design

### Rules are data

Most effect rules are symbol bans: "this constructor or member; use this service instead". Express them as MSBuild
items so a package can ship them in `buildTransitive/<PackageId>.props`:

```xml
<ItemGroup>
  <AxialGuardrail Include="Axial.HttpClient" />
  <AxialGuardrailRule Include="http.new-client"
      Guardrail="Axial.HttpClient"
      Category="http"
      Match="ctor:System.Net.Http.HttpClient"
      Message="constructs HttpClient directly, which exhausts sockets and cannot be replaced in tests"
      Replacement="Axial.HttpClient IHttp (Http.live)" />
</ItemGroup>
```

`Match` forms mirror `EffectCatalog.SymbolMatch`: `ctor:<type>`, `member:<type>::<name>;<name>`, `any:<type>`.

Optional `Where="insideFlow"` restricts a rule to code inside a `flow { }` builder, reusing the detection in
`RaiseInFlowAnalyzer`. This covers `.GetAwaiter().GetResult()` and `.Result` without custom analyzer code.

Referencing a package turns its guardrail on: a rule that recommends `IHttp` is only actionable once `IHttp` is
available. Third-party packages use the same mechanism.

### Consumers configure with MSBuild item operations

```xml
<AxialGuardrail Remove="Axial.Console" />                              <!-- guardrail off -->
<AxialGuardrail Update="Axial.HttpClient" Severity="warning" />        <!-- per-guardrail severity -->
<AxialGuardrailRule Remove="environment.processor-count" />            <!-- one rule off -->
<AxialGuardrail Include="Axial.FileSystem" />                          <!-- opt in without a reference -->
```

A rule runs only when its guardrail is present in `@(AxialGuardrail)`. `Severity` defaults to
`$(AxialGuardrailsSeverity)`. The existing `AxialGuardrailsEnabled=false` still disables everything.

Test projects stop using the hardcoded `IsTestProject` code list; the targets file removes the effect guardrails for
test projects by default, and a test project can add them back.

Opting into a guardrail without referencing its package only works when Axial.Guardrails itself ships that
guardrail's rules. Axial's own guardrails therefore live in Axial.Guardrails' props, each guarded so it activates
when the matching package is referenced or explicitly included; third-party guardrails live in their own packages.

### The analyzer reads the resolved set

`build/Axial.Guardrails.targets` writes the enabled rules (after filtering by `@(AxialGuardrail)`) to
`$(IntermediateOutputPath)axial-guardrails.rules`, one rule per line, and passes the path to the analyzer through an
environment variable on the `Exec`. The rules file is an input of the up-to-date check so configuration changes rerun
the analyzer. There remains one analyzer DLL and one type-check.

The analyzer emits each finding with its guardrail's severity. Open question: confirm that the fsharp-analyzers CLI
exits non-zero on `Severity.Error` without the `--treat-as-error <codes>` list. If it does not, each guardrail needs
its own diagnostic code.

### Structural rules stay in code

Raise-in-flow, the xUnit fixture rule, discarded cancellation, and reflection formatting need real analysis and stay
in the analyzer DLL. They belong to the `Axial` guardrail and are gated the same way.

`<AxialGuardrailAnalyzers Include="path/to/analyzers" />` adds another analyzer directory to the same CLI run, so a
package that needs custom analysis can ship a DLL without a second type-check.

### Suppressions

`// axial-allow-effect: http` keeps its form. `SuppressionIntegrity` validates categories against the loaded rules
instead of `EffectCatalog.knownCategories`. A suppression whose category belongs to a disabled guardrail is reported as
stale rather than unknown.

## Initial split of the current catalog

| Guardrail | Rules |
| --- | --- |
| `Axial` | `Task.Delay`, `Thread.Sleep`, raise-in-flow, discarded cancellation, reflection formatting, `GetResult`/`.Result` inside `flow { }` |
| `Axial.PlatformService` | clock, random, guid, environment |
| `Axial.Console` | `System.Console` |
| `Axial.FileSystem` | `System.IO.File`, `System.IO.Directory` |
| `Axial.Process` | `Process.Start` |
| `Axial.HttpClient` | `new HttpClient` |

`Environment.ProcessorCount` leaves the environment rule once `Parallelism.ofProcessors` exists.

## Steps

1. Check the CLI's exit code for `Severity.Error` findings.
2. Load the catalog from the generated rules file, with the current rules emitted by Axial.Guardrails' props; no
   behavior change.
3. Add `AxialGuardrail` gating and per-guardrail severity; replace the test-project code list.
4. Split the rules into the guardrails above.
5. Add `Where="insideFlow"`, then the `new HttpClient` and `GetResult` rules.
6. Update `docs/15-notes/03-guardrails.md`.

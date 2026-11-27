---
title: Analyzer packaging
linkTitle: Analyzer packaging
description: The NuGet layout, CLI download, and MSBuild target wiring that makes an analyzer package enable itself.
---

# Analyzer packaging

Package an analyzer so that `dotnet add package` enables it without requiring consumers to edit MSBuild files. This
page is the packaging reference; [Write an effect-boundary analyzer](/guardrails/writing-effect-analyzers.html) shows how to
adapt a rule and verify a diagnostic.

Use [`src/Axial.Guardrails/Axial.Guardrails.fsproj`](https://github.com/adz/Axial/blob/main/src/Axial.Guardrails/Axial.Guardrails.fsproj) and [`src/Axial.Guardrails/build/`](https://github.com/adz/Axial/tree/main/src/Axial.Guardrails/build) from the Axial source tree as references for the following steps.

## Include analyzer dependencies

Set `CopyLocalLockFileAssemblies` to `true`. This setting places the analyzer's transitive dependencies beside its assembly in the build output.

Pack the complete output under `analyzers/` in the NuGet package. Set `IncludeBuildOutput` to `false` so consumer projects do not compile against the analyzer assembly.

## Include the analyzer CLI

The `fsharp-analyzers` CLI is a .NET tool. NuGet does not allow a normal `PackageReference` to a tool package and reports `NU1212` if you add one.

Use `PackageDownload` during your build instead. At pack time, copy the downloaded CLI files into the NuGet package under `cli/`.

## Add automatic build targets

Add `build/{YourPackageId}.props` and `build/{YourPackageId}.targets` to the package. NuGet imports these files automatically into projects that reference the package.

Use the props file to define the enabled state. The targets run every check through one MSBuild task,
`AxialGuardrailsTask`, which ships in the package under `tasks/` and is declared with `<UsingTask>`. After `Build`, its
`Run` action does the analysis and owns its result:

- **Fast** (the default) loads the parse-only visitors and their FCS dependencies in process, in an isolated load
  context. It never launches the CLI.
- **Full** starts the bundled CLI from `cli/` as a child process with the bundled `analyzers/` directory. It reuses
  the compiler arguments CoreCompile already resolved (`--fsc-args-file`) instead of evaluating the project again,
  and runs under a fixed timeout that kills the whole process tree.
- **Framework MSBuild** (Visual Studio) cannot isolate the net8 parser payload, so a Fast request there runs Full.
  Outside checkpoints the targets tell the analyzers explicitly to cap severity at Fast's, so the fallback never
  fails a build that `dotnet build` would only warn about.

Each analyzer records one coverage entry per rule and file. The task combines them into
`axial-guardrails.<TFM>.result.xml` and reports findings itself, as MSBuild errors, warnings or messages by severity,
both on a fresh run and when it replays a cached result. Severity is resolved in the analyzer from the generated rules
file, not with the CLI's `--treat-as-error`, which would also promote acknowledged findings.

The task sets `DOTNET_ROLL_FORWARD=LatestMajor` for the child process only, not for the whole build: the CLI targets
.NET 8 but project loading can use MSBuild from a newer SDK. `Major` still chooses .NET 8 when it is installed, which
cannot load SDK 10's runtime dependencies. Axial applies the child-only setting to both analyzer and policy-tool
launches, including builds started by `axial policy baseline`. For a standalone DLL invocation, use
`dotnet --roll-forward LatestMajor <path-to-FSharp.Analyzers.Cli.dll> ...`.

The task does not trust the CLI's exit code alone. Exit code 0 is success only when coverage is complete. The upstream
CLI's exit code 254 (-2 on Windows) is ambiguous: it accompanies Error diagnostics, but alone it does not prove the
analysis completed. The task accepts 254 only when every rule/file entry ran and at least one Error finding was
recorded. The
build then fails on those findings. Any other nonzero exit, a missing or incomplete coverage entry, or a timeout fails
the build with `AXGINF001` and leaves no reusable stamp.

Do not use the `AfterBuild` target from `FSharp.Analyzers.Build`, because that target sets `IgnoreExitCode="true"` and
cannot fail the build.

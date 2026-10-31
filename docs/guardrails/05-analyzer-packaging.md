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

Use the props file to define the default severity and enabled state. Use the targets file to run the bundled CLI after `Build`, passing the bundled `analyzers/` and `cli/` directories.

Run the CLI with an MSBuild `<Exec>` task. Do not use the `AfterBuild` target from `FSharp.Analyzers.Build`, because that target sets `IgnoreExitCode="true"` and cannot fail the build.

The CLI exits with a nonzero code only when a diagnostic uses `--treat-as-error`. A normal `<Exec>` task therefore preserves warning and error behavior.

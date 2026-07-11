---
weight: 10
title: Commands And Pipelines
description: Construct safely tokenized commands and connect typed process endpoints.
---

# Commands And Pipelines

This page shows how immutable commands and typed endpoints replace shell quoting and redirection.

## Arguments Stay Separate

```fsharp
open Axial.Flow.Process
open Axial.Flow.Process.DSL

let archiveName = "release files.tar.gz"
let inputDirectory = "build output"
let archive = cmd $"tar -czf {archiveName} {inputDirectory}"
```

Each interpolation hole remains one argument. Do not quote interpolated values. Literal whitespace divides arguments,
and literal single or double quotes group fixed text:

```fsharp
cmd $"git commit -m 'fixed message'"
cmd $"git commit -m {message}"
```

Use `cmdText "git status --short"` only for fixed or deliberately assembled command text. Use
`Process.command executable arguments` for a programmatically generated argument collection.

## Connect Endpoints

`=>` means connect the byte output on the left to the typed endpoint on the right:

```fsharp
cmd $"dotnet build --nologo"
=> cmd $"grep warning"
=> cmd $"head -n 20"
|> capture
```

The named two-stage equivalent is:

```fsharp
cmd $"producer" |> pipeTo (cmd $"consumer") |> capture
```

Use explicit endpoints for input and output:

```fsharp
Input.file "records.json.gz"
=> cmd $"gzip -dc"
=> cmd $"jq '.records[]'"
=> Output.file "records.ndjson"
```

The final output endpoint converts the topology to Flow. Other input sources are `Input.text`, `Input.bytes`,
`Input.read`, and, on .NET, `Input.stream`. Other output endpoints include capture, bounded capture, console forwarding,
true handle inheritance, discard, append files, callbacks, streams, text writers, and tee.

The configuration form supports dynamically selected sources:

```fsharp
let source = if compressed then Input.file archive else Input.text fallback

pipe [
    $"decoder"
    $"consumer"
]
|> stdin source
|> capture
```

## Standard Error Routing

Bash `|&` sends both producer channels into the next command:

```bash
compiler |& formatter
```

```fsharp
cmd $"compiler" |> pipeBothTo (cmd $"formatter") |> capture
```

Bash `2>&1` routes final stderr through final stdout targets:

```fsharp
cmd $"tool"
|> mergeStderr
|> stdout (Output.tee [ Output.console; Output.file "combined.log" ])
|> toFlow
```

Ordering is preserved within each channel. Cross-channel ordering is nondeterministic because independent stdout and
stderr writes have no portable total order.

## Concurrent Fan-In

Run independent producers concurrently and feed complete lines to one consumer:

```fsharp
merge [
    cmd $"producer-a"
    cmd $"producer-b"
]
=> cmd $"consumer"
|> capture
```

`merge` serializes complete newline-delimited records from each producer and applies backpressure at the shared stdin.
Use `mergeBytes` only when arbitrary binary chunk interleaving is acceptable. Flow cancellation terminates every
producer and the consumer.

## Invoke A Shell Deliberately

Typed topology is portable across Windows, Linux, and macOS. When shell language is the clearer representation, use an
explicit host:

```fsharp
bash $"git log --author={author} | sort | uniq -c" |> capture
pwsh $"Get-ChildItem -LiteralPath {root} | Sort-Object Name" |> capture
sh $"find {root} -maxdepth 1 | sort" |> capture
```

Interpolation values are passed as shell arguments, not concatenated into program source. `bashText`, `shText`, and
`pwshText` are the visibly unsafe forms for already assembled source. Bash programs enable `pipefail`, but the shell
program is one Flow stage, so inner commands do not receive individual stage transcripts.

Do not use a shell when typed commands express the same work clearly. There is intentionally no generic `shell` that
silently changes language on Windows.

## Encoding, Plans, And Secrets

UTF-8 is the default. `Process.encoding` changes decoding while `CapturedOutput.Bytes` retains exact bytes.

```fsharp
let token = secret deploymentToken
let deploy = cmd $"deploy --token={token}"

printfn "%s" (Process.render deploy)
```

Output:

```text
deploy --token=***
```

`Process.plan` returns the redacted topology, input/output policies, stderr routing, and framing without requesting
`IProcess` or starting a child.

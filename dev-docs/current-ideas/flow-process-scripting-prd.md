# Flow Process And Scripting PRD

## Problem Statement

F# scripts currently have to choose between two unsatisfactory process models.

Bash is concise and makes pipelines, redirection, background work, and exit status immediately available, but command
construction is stringly typed, interpolation is easy to quote incorrectly, failures are weakly structured, and
background processes are easy to orphan. Conventional .NET process APIs provide types but require substantial ceremony
for arguments, standard streams, concurrent draining, cancellation, pipelines, and failure diagnostics.

`Axial.Flow.Process` should make ordinary process work at least as direct as Bash while retaining safely tokenized
arguments, explicit effects, typed failures, structured concurrency, deterministic resource cleanup, bounded streaming,
redacted secrets, and testable service dependencies. It should cover the practical source and target flexibility that
makes CliWrap and ProcessX useful without copying features that do not improve real scripts.

The surrounding scripting experience also needs a complete portable `FlowStream` vocabulary, filesystem symbolic-link
operations, and a one-file shebang entry point. The API must remain compatible with the architectural role of
`Flow<'env, 'error, 'value>` and must not introduce another public effect carrier.

## Solution

Provide a command-line-shaped DSL in which literal whitespace separates arguments and every interpolated value remains
one argument, regardless of its contents. Commands compose into immutable process topologies. Standard input sources and
output targets are explicit values. A small family of terminal operations converts a topology into a `Flow` or
`FlowStream`; the Flow runtime remains the only component that performs effects.

The normal linear pipeline uses an F# collection expression so modern F# supplies implicit yields:

```fsharp
open Axial.Flow.Process.DSL

pipe [
    $"git log --format=%%an"
    $"sort"
    $"uniq -c"
]
|> capture
```

`pipeTo` handles a single connection without introducing a symbolic operator:

```fsharp
cmd $"git log --format=%%an"
|> pipeTo (cmd $"sort")
|> capture
```

For shell-like scripts, one optional visual connector exposes the same topology operations:

```fsharp
cmd $"git log --format=%%an"
=> cmd $"sort"
=> cmd $"uniq -c"
|> capture
```

`source => destination` means connect the typed endpoints; it introduces no separate topology semantics. Command to
command is exactly `source |> pipeTo destination`. The named forms remain available in ordinary application code.

Input and output endpoints use the same connector explicitly:

```fsharp
Input.file "source.txt" => cmd $"gzip" => Output.file "source.txt.gz"
```

The individual forms are:

```fsharp
Input.file "source.txt" => cmd $"wc -l" |> capture
cmd $"git archive HEAD" => Output.file "source.tar"
```

`Input.file path => command` creates a topology whose primary stdin is the file. `topology => Output.file path` routes
final stdout to a truncating file and converts the completed topology to Flow. The endpoint types determine the
connection. Requiring `Input.file` and `Output.file` instead of bare strings keeps file direction explicit and leaves
room for text, bytes, streams, console, capture, and tee endpoints without adding more operators.

The operator is valid F#, associates left-to-right, and binds before `|>`, so a complete compact pipeline remains
unambiguous:

```fsharp
cmd $"git log --format=%%an" => cmd $"sort" => Output.file "authors.txt"
```

The mental model has four phases:

1. `cmd` creates a safely tokenized command. Interpolation holes are arguments, not shell fragments.
2. `pipe`, `pipeTo`, `pipeBothTo`, and `merge` create process topology.
3. Input, output, environment, working-directory, timeout, success-code, and diagnostic combinators configure it.
4. `toFlow`, `capture`, `console`, or `stream` converts the description into a Flow value. The Flow runtime executes it.

`toFlow` is the explicit foundational conversion. `capture`, `console`, and `stream` are lightweight execution-mode
shortcuts. They describe what will happen when the returned Flow is interpreted; they do not eagerly start processes.
Capture is the default output policy, not eager execution: a topology still needs a terminal conversion. This keeps the
effect boundary visible while allowing the common case to end in the single word `capture`.

## Bash Rosetta Stone

### Run And Capture

```bash
output="$(git status --short)"
```

```fsharp
flow {
    let! result = cmd $"git status --short" |> capture
    return result.StdOut
}
```

`capture` waits for every stage to exit, validates configured success codes, and retains complete final stdout and
stderr by default. `captureResult` returns the same structured result without turning an unacceptable exit code into a
Flow failure. Bounded capture is available for untrusted or potentially large output.

### Safe Interpolation

```bash
git show -- "$path"
```

```fsharp
cmd $"git show -- {path}" |> capture
```

Even when `path` contains spaces, quotes, wildcard characters, or semicolons, it remains exactly one argument. No shell
is involved. Fixed literal text may use command-line quoting for grouping. A deliberately assembled fixed command line
uses the visibly less-safe `cmdText` entry point.

### Linear Pipelines

```bash
git log --format=%an | sort | uniq -c | sort -nr
```

```fsharp
pipe [
    $"git log --format=%%an"
    $"sort"
    $"uniq -c"
    $"sort -nr"
]
|> capture
```

Adjacent stages are connected by real bounded byte streams. Only final stdout is an external output target by default;
each stage still reports exit status, timing, its redacted command, and a bounded stderr diagnostic tail.

### Conditional And Generated Stages

```bash
git fetch
if [[ "$include_tags" = true ]]; then git tag; fi
for branch in "${branches[@]}"; do git log "$branch"; done
```

```fsharp
pipe [
    $"git fetch"

    if includeTags then
        $"git tag"

    for branch in branches do
        $"git log {branch}"
]
|> capture
```

Collection-expression syntax supplies implicit yields on supported F# versions. Explicit `yield!` remains available for
splicing an existing command collection.

### Console Output

```bash
dotnet test
```

```fsharp
cmd $"dotnet test" |> console
```

`console` forwards redirected bytes through the host standard streams and still permits observation and structured
failure reporting. It is distinct from true `Inherit`, where a child receives the host handles directly and may behave
as an interactive terminal application. A truly inherited channel cannot also be captured, teed, or observed.

### Stream Live Output

```bash
dotnet test 2>&1 | while IFS= read -r line; do
    printf '[test] %s\n' "$line"
done
```

```fsharp
cmd $"dotnet test"
|> mergeStderr
|> streamLines
|> FlowStream.runForEach (fun event ->
    printfn "[test] %s" event.Text)
```

Streaming is bounded and backpressured. Events identify stage and channel, include timestamps, and end with a completed
process result. Early termination and Flow cancellation terminate the process topology and release all resources.

### Redirect Files

```bash
gzip < input.txt > input.txt.gz 2> errors.log
```

```fsharp
cmd $"gzip"
|> stdin (Input.file "input.txt")
|> stdout (Output.file "input.txt.gz")
|> stderr (Output.file "errors.log")
|> toFlow
```

Input sources are selected before Flow conversion and always feed the first stage:

```fsharp
cmd $"wc -l"
|> stdin (Input.text report)
|> capture
```

They may also participate directly as the left endpoint of `=>`, which is the closest typed equivalent to shell input
redirection:

```fsharp
Input.text report => cmd $"wc -l" |> capture
Input.bytes payload => cmd $"sha256sum" |> capture
Input.file "items.json.gz" => cmd $"gzip -dc" => cmd $"jq '.items[]'" |> capture
```

The named equivalent is `source |> pipeTo command`. `=>` therefore has one consistent meaning: connect the byte output
of the left endpoint to the standard input of the right command. Its valid left endpoints are `InputSource`, `Command`,
and `Pipeline`; its right endpoint is a `Command`. An input source cannot appear after a command, and a topology can have
only one primary stdin source.

The `stdin` combinator remains useful when configuration is assembled dynamically or reads more clearly in a longer
pipeline:

```fsharp
let source = if compressed then Input.file archive else Input.text fallback

pipe [
    $"decoder"
    $"consumer"
]
|> stdin source
|> capture
```

```fsharp
cmd $"sha256sum"
|> stdin (Input.bytes payload)
|> capture
```

```fsharp
pipe [
    $"gzip -dc"
    $"jq '.items[]'"
]
|> stdin (Input.file "items.json.gz")
|> capture
```

For the especially common file-output cases, named terminal shortcuts avoid spelling the target union and conversion:

```fsharp
cmd $"git archive HEAD" |> writeTo "source.tar"
cmd $"build-log" |> appendTo "build.log"
```

`writeTo path` means final stdout to a truncating file followed by `toFlow`; `appendTo path` uses append mode. Both return
the same final `ProcessResult` shape, with stdout capture empty unless it was explicitly teed to capture. Their explicit
configuration equivalents remain available for mixed routing and teeing.

Files, text, bytes, portable Flow streams, and platform stream adapters are first-class input sources. Capture, bounded
capture, files, append files, console forwarding, true inheritance, discard, callbacks, Flow sinks, and platform stream
adapters are first-class output targets.

### Tee Output

```bash
dotnet test 2>&1 | tee test.log
```

```fsharp
cmd $"dotnet test"
|> mergeStderr
|> stdout (Output.tee [ Output.console; Output.file "test.log"; Output.capture ])
|> toFlow
```

Tee applies backpressure to all targets and reports target I/O failures through `ProcessError`.

### Pipe Standard Error Too (`|&`)

```bash
compiler |& formatter
```

```fsharp
cmd $"compiler"
|> pipeBothTo (cmd $"formatter")
|> capture
```

`pipeBothTo` is the typed equivalent of Bash `|&`: the producer's stdout and stderr feed the next stage's stdin. The
merged byte order is explicitly nondeterministic across channels because the operating system does not provide a useful
cross-platform total ordering for independently produced stdout and stderr writes. Backpressure and cancellation still
apply to both pumps.

### Merge Standard Error Into Standard Output (`2>&1`)

```bash
tool > combined.log 2>&1
```

```fsharp
cmd $"tool"
|> mergeStderr
|> stdout (Output.file "combined.log")
|> toFlow
```

`mergeStderr` routes stderr to the topology's stdout route. It is not merely a post-processing concatenation. Both
channels reach the selected stdout targets while the process is running, with nondeterministic cross-channel ordering.
Unlike Bash file-descriptor syntax, routing is not sensitive to combinator order: the final immutable routing plan is
validated before execution.

The distinct Bash command below intentionally has different semantics and remains expressible without positional
redirection rules:

```bash
tool 2>&1 > stdout.log
```

```fsharp
cmd $"tool"
|> stderr Output.console
|> stdout (Output.file "stdout.log")
|> toFlow
```

### Structured Parallelism

```bash
dotnet test tests/Unit.Tests &
unit_pid=$!
dotnet test tests/Integration.Tests &
integration_pid=$!
wait "$unit_pid"
wait "$integration_pid"
```

```fsharp
Flow.zipPar
    (cmd $"dotnet test tests/Unit.Tests" |> capture)
    (cmd $"dotnet test tests/Integration.Tests" |> capture)
```

For a dynamic collection:

```fsharp
projects
|> Seq.map (fun project -> cmd $"dotnet test {project}")
|> captureParallel (maxConcurrency = 4)
```

Parallel execution preserves result order, bounds concurrency, propagates cancellation, and cannot silently orphan
children. `Flow.fork` supports scoped background work when subsequent Flow operations must run before joining it.

### Live Fan-In

```bash
{ producer-a & producer-b & wait; } | consumer
```

```fsharp
merge [
    cmd $"producer-a"
    cmd $"producer-b"
]
|> pipeTo (cmd $"consumer")
|> capture
```

`merge` is an explicit fan-in topology. Line-framed merge is the safe default for textual scripting. Raw byte fan-in
requires the explicit `mergeBytes` operation because chunks from different producers may interleave nondeterministically.

### Secrets And Diagnostics

```bash
deploy --token "$TOKEN"
```

```fsharp
cmd $"deploy --token {secret token}" |> capture
```

The actual token remains one argument. Plans, exceptions, transcripts, and rendered commands display `***` while
retaining useful literal prefixes such as `--token=***`.

### One-File Scripts

```fsharp
#!/usr/bin/env -S dotnet fsi
#r "nuget: Axial.Flow.Process"

open Axial.Flow
open Axial.Flow.Process
open Axial.Flow.Process.DSL

flow {
    let! branch = cmd $"git branch --show-current" |> capture
    do! cmd $"dotnet test --configuration Release" |> console |> Flow.ignore
    return branch.StdOut.Trim()
}
|> Script.run
```

`Script.run` is the actual interpreter boundary for a shebang file. It installs live services, handles cancellation,
prints typed failures, and maps the failed process stage to the host exit code.

### Shell Escape Hatches And Windows

The typed command and topology API is the portable default. It works on Windows, Linux, and macOS whenever the named
executables are installed because no command shell is involved.

For cases where shell syntax is itself valuable, host-specific constructors provide an explicit escape hatch:

```fsharp
bash $"git log --format=%%an | sort | uniq -c | sort -nr" |> capture
```

```fsharp
sh $"find {root} -maxdepth 1 | sort" |> capture
```

```fsharp
pwsh $"Get-ChildItem -LiteralPath {root} | Sort-Object Name" |> capture
```

Interpolated holes are passed as shell arguments and referenced from generated shell source; their values are never
concatenated into the program text. Literal shell syntax remains intentionally active. The visibly unsafe `bashText`,
`shText`, and `pwshText` constructors accept already assembled program text.

There is no generic `shell` constructor that silently selects a different language by operating system. Cross-platform
scripts either use typed topology or branch explicitly:

```fsharp
let listing =
    if OperatingSystem.IsWindows() then
        pwsh $"Get-ChildItem -LiteralPath {root} | Sort-Object Name"
    else
        sh $"find {root} -maxdepth 1 | sort"

listing |> capture
```

`bash` requires Bash, `sh` requires a POSIX-compatible shell, and `pwsh` requires PowerShell 7. Bash execution enables
`pipefail`, but the complete shell program remains one Flow process stage: Flow cannot provide individual inner-command
timings, exit codes, or stderr tails. Windows cancellation uses a Job Object to supervise descendants; Unix uses a
process group. WSL is treated as an explicitly invoked `wsl.exe` process boundary unless a later design adds deeper WSL
lifecycle integration.

## User Stories

1. As an F# script author, I want commands to look like command lines, so that scripts remain as scannable as Bash.
2. As an F# script author, I want interpolated values to remain atomic arguments, so that spaces and shell characters cannot alter command structure.
3. As an F# script author, I want fixed literal quoting to group arguments, so that familiar command examples require little translation.
4. As a security-conscious author, I want interpolated secrets redacted everywhere diagnostic text is produced, so that credentials do not leak into logs.
5. As a library author, I want an argument-list constructor beneath the DSL, so that generated arguments do not require reparsing strings.
6. As a script reader, I want linear process pipelines to read vertically, so that data flow is immediately visible.
7. As a script author, I want a concise single-stage connector, so that two-command pipelines do not require a collection expression.
8. As a script author, I want conditional and generated stages to use ordinary F# collection-expression syntax, so that dynamic pipelines stay readable.
9. As a Flow user, I want an explicit `toFlow` conversion, so that the boundary between topology description and executable workflow is unmistakable.
10. As a script author, I want concise capture, console, and stream shortcuts, so that the common execution modes remain lightweight.
11. As a script author, I want full stdout and stderr capture, so that I can inspect output after successful completion.
12. As a script author, I want bounded head or tail capture, so that diagnostic output cannot consume unlimited memory.
13. As a script author, I want exact captured bytes and an encoding-aware text view, so that both binary and textual tools are supported.
14. As a script author, I want live line and chunk events, so that I can update consoles, logs, and progress displays while a process runs.
15. As a Flow user, I want streamed process events to be backpressured, so that slow consumers do not create unbounded queues.
16. As a Flow user, I want stopping a stream early to terminate its process topology, so that processes and handles are not leaked.
17. As a script author, I want strings, bytes, files, and Flow streams as stdin sources, so that common input redirection requires no manual plumbing.
18. As a .NET author, I want `Stream`, `TextReader`, `TextWriter`, and `StringBuilder` adapters, so that process I/O integrates with existing .NET APIs.
19. As a Fable author, I want Web Stream and Node stream adapters, so that the same source and target concepts work on JavaScript hosts.
20. As a script author, I want files, append files, console forwarding, true handle inheritance, discard, callbacks, streams, and capture as output targets, so that ordinary redirection is declarative.
21. As a script author, I want to tee output to multiple targets, so that I can observe, persist, and capture one stream concurrently.
22. As a terminal-tool user, I want true standard-handle inheritance, so that child terminal detection, colors, and interactive behavior remain intact.
23. As a process observer, I want console forwarding distinct from handle inheritance, so that I understand when capture and events remain possible.
24. As a Bash migrant, I want a typed equivalent of `2>&1`, so that stderr can follow the stdout route without file-descriptor syntax.
25. As a Bash migrant, I want a typed equivalent of `|&`, so that both producer channels can feed the next process.
26. As a script author, I want routing validation before execution, so that conflicting stream ownership fails before any child starts.
27. As a script author, I want intermediate-stage taps, so that piped output can also be logged or observed deliberately.
28. As a script author, I want every stage's exit status and timing, so that failures inside pipelines are not hidden by the last stage.
29. As a script author, I want configurable success codes per command, so that tools with nonzero success conventions remain type-safe.
30. As a script author, I want a bounded stderr tail for every stage, so that a failed pipeline identifies the responsible command with useful context.
31. As a script author, I want cancellation to terminate the whole process topology, so that descendants do not remain after a Flow is interrupted.
32. As a script author, I want multiple commands to run concurrently under structured concurrency, so that background work is awaited and cancellable.
33. As a script author, I want bounded parallel collection execution, so that batch scripts do not exhaust machine resources.
34. As a script author, I want line-framed live fan-in, so that multiple textual producers can safely feed one consumer.
35. As a binary-tool author, I want an explicit raw-byte fan-in operation, so that nondeterministic interleaving is never accidental.
36. As a test author, I want a redacted serializable execution plan, so that command topology can be asserted without starting processes.
37. As an application author, I want process access expressed through the Flow environment, so that dependencies and effects remain visible in types.
38. As a script author, I want a one-line shebang execution boundary, so that a single `.fsx` file behaves like an executable script.
39. As a script caller, I want failed process exit codes propagated by the script host, so that automation can rely on normal operating-system conventions.
40. As a FlowStream user, I want mapping, filtering, effectful transforms, accumulation, composition, resource cleanup, and terminal folds, so that process streams require no second streaming library.
41. As a Fable user, I want the portable FlowStream core to avoid .NET-only public types, so that workflows can cross-compile.
42. As a filesystem script author, I want typed file and directory symbolic-link operations, so that deployment scripts do not drop down to platform commands.
43. As a shell-oriented script author, I want one visual endpoint connector, so that compact pipelines remain close to Bash without learning separate pipe and redirection operators.
44. As a script author, I want concise truncating and appending file-output operations, so that common redirection does not require verbose target construction.
45. As a cross-platform author, I want the typed topology to behave consistently without a shell, so that one script can run on Windows, Linux, and macOS.
46. As a shell expert, I want explicit Bash, POSIX shell, and PowerShell escape hatches, so that I can use native shell language where it is genuinely clearer.
47. As a Windows author, I want child processes supervised with Job Objects, so that cancellation terminates descendants rather than only the immediate process.
48. As a script author, I want an input source to connect directly to a command, so that file, text, and byte input reads like the beginning of a pipeline.
49. As a shell-oriented author, I want explicit input and output file endpoints, so that complete file-to-process-to-file flows remain compact without ambiguous bare paths.

## Implementation Decisions

- Keep `Flow<'env, 'error, 'value>` as the only public workflow model. Process descriptions remain inert immutable values until converted to Flow.
- Treat command parsing/tokenization as a deep module. It owns literal whitespace, quoting, interpolation atomicity, invariant formatting, secret display values, and validation without invoking a shell.
- Make interpolated `cmd $"..."` the primary hand-authored command form. Keep the explicit executable-plus-argument-collection constructor for generated arguments and `cmdText` for fixed assembled text.
- Make `pipe [ ... ]` the canonical multi-stage DSL and `pipeTo` the foundational single connection combinator.
- Allow `InputSource`, `Command`, and `Pipeline` as the left endpoint of `pipeTo`; require a `Command` as its right endpoint. Connecting an input source creates a one-command topology with that source as primary stdin.
- Provide optional `=>` notation as the single typed endpoint connector. It connects `InputSource => Command`, `Command/Pipeline => Command`, and `Command/Pipeline => OutputTarget`; connecting a terminal output target converts the topology to Flow.
- Require explicit `Input.file path` and `Output.file path` endpoints. Do not overload bare strings as file paths.
- Do not introduce additional bind-, composition-, value-pipe-, or redirection-like spellings for endpoint connection.
- Treat process topology and I/O routing as a deep module. It validates stream ownership, constructs linear pipelines and fan-in graphs, and exposes a serializable redacted plan.
- Model stdin as an `InputSource` and output channels as `OutputTarget` values rather than adding one combinator per concrete I/O type.
- Support portable text, bytes, file, callback, and FlowStream adapters in the core contract. Isolate .NET and JavaScript host stream adapters behind platform-specific modules or packages so portable types do not mention `System.IO.Stream`.
- Rename the current managed forwarding behavior from `Inherit` to `Console`. Reserve `Inherit` for actual unredirected child handles.
- Make capture the default external stdout and stderr policy for a newly constructed topology. Retain exact bytes, decoded text, and truncation state.
- Provide `toFlow` as the universal explicit conversion. Provide `capture`, `captureResult`, `console`, and `stream` as mode-specific conversions, not compatibility aliases for `run`.
- Provide `writeTo` and `appendTo` as searchable named final-stdout file shortcuts which also convert to Flow. `Output.file` and `Output.appendFile` provide their endpoint equivalents. Do not make the configuring `stdout` function execute or convert implicitly.
- Remove `run`, `runResult`, and custom process connector operators before 1.0 once the final surface is adopted.
- Define `mergeStderr` as routing stderr to the stdout target set. It corresponds to the useful intent of `2>&1` without reproducing Bash's order-sensitive file-descriptor mutation.
- Define `pipeBothTo` as connecting both output channels of the producer to the destination stdin. It corresponds to Bash `|&`.
- State explicitly that cross-channel byte ordering for merged stdout and stderr is nondeterministic. Preserve ordering within each source channel.
- Support intermediate-stage output taps explicitly. Do not make teeing a producer into both a downstream stdin and another target implicit.
- Use genuine bounded byte pumping between process stages. Never decode and re-encode ordinary pipeline traffic.
- Make line framing an observation and safe fan-in feature, not the underlying representation of normal byte pipelines.
- Implement parallel commands through existing Flow structured-concurrency primitives. Add process-specific bounded collection helpers only where they remove meaningful ceremony.
- Implement `merge` as line-framed fan-in by default and require `mergeBytes` for raw fan-in.
- Treat execution supervision as a deep module. It owns startup rollback, process-tree cancellation, concurrent draining, backpressure, completion, and resource cleanup.
- Preserve a structured result containing final exit code, every stage result, stdout/stderr captures, start time, duration, and per-stage diagnostic stderr tails.
- Validate every configured stage success-code policy and report the first unsuccessful stage as typed `ProcessError.StageFailed` with the complete transcript.
- Keep the process service asynchronous and portable at its public boundary. Platform implementations may use the host's most efficient primitives internally.
- Expand `FlowStream` in the leaf `Axial.Flow` package and keep it independent of process-specific concepts.
- Use a platform implementation file when differences are small and dependency-free. Use separate adapter packages when .NET, browser, or Node stream types would otherwise leak into the portable package graph.
- Provide filesystem symlink operations through `IFileSystem`; return typed unsupported failures on targets that cannot implement them.
- Provide a .NET script environment and `Script.run` as the actual shebang interpreter boundary. It installs live services and maps Flow/process failures to the host exit code.
- Keep typed commands and topologies shell-independent on every host. Provide separate `bash`, `sh`, and `pwsh` constructors rather than a platform-selecting generic shell.
- Pass interpolated shell values out-of-band as shell arguments. Never concatenate interpolation values into shell program text; reserve `bashText`, `shText`, and `pwshText` for explicitly assembled unsafe text.
- Supervise process trees with Unix process groups and Windows Job Objects. Treat WSL as an explicit external host boundary.
- Organize user documentation into separate command/pipeline, I/O routing, capture/streaming, concurrency, failures/diagnostics, scripts, FlowStream, and Fable pages rather than one oversized process page.
- Do not modify Schema packages, tests, generated reference pages, or guides as part of this product slice.

## Testing Decisions

- Test only observable public behavior: argument vectors received by child processes, bytes delivered to each source/target, events observed, results returned, errors produced, cancellation effects, and rendered redacted plans.
- Add parser tests for whitespace, quotes, escapes, adjacent literal/interpolated text, spaces and shell metacharacters inside interpolation holes, format specifiers, empty input, malformed quotes, and secret redaction.
- Compile public examples using `InputSource => Command`, `Command/Pipeline => Command`, and `Command/Pipeline => OutputTarget` to lock down overload resolution, F# precedence, left association, and interaction with `|>`.
- Use small deterministic helper child processes for I/O tests rather than relying on shell parsing or platform-specific command output.
- Test linear pipelines with data larger than pipe buffers to detect deadlocks and accidental full buffering.
- Test exact binary pass-through, alternate text encodings, partial final lines, mixed stdout/stderr, and large output.
- Test every input source and output target independently, then test tee combinations and target I/O failures.
- Test direct `InputSource => Command` construction and its equivalence to `stdin`, including rejection of multiple primary input sources.
- Test `writeTo` truncation, `appendTo` append behavior, empty returned stdout, and explicit tee-to-capture combinations.
- Test that `Console` uses managed forwarding and remains observable; test that true `Inherit` is not capturable and preserves terminal/handle behavior where the test host permits it.
- Test `mergeStderr` and `pipeBothTo` for channel inclusion, per-channel ordering, completion, backpressure, and cancellation without asserting nondeterministic cross-channel order.
- Test invalid routing plans before process startup, including competing stdin owners and incompatible true inheritance plus capture/taps.
- Test intermediate-stage taps without changing bytes received by the downstream stage.
- Test acceptable and unacceptable exit codes at every pipeline position, startup failures after partial startup, and transcript completeness.
- Test secret values never appear in plans, stage results, error descriptions, or script-host diagnostics.
- Test stream early termination, observer failure, consumer slowness, cancellation, and exactly one completion event.
- Test structured parallel execution for concurrency limits, stable result ordering, sibling cancellation, and no surviving processes.
- Test line fan-in with complete records and raw-byte fan-in without assuming producer ordering.
- Test the shebang host with a real `.fsx` smoke script and assert stdout, stderr, and process exit code.
- Test Bash, POSIX shell, and PowerShell constructors only on hosts where those executables are available; assert that interpolation values remain out of program text and cannot inject shell syntax.
- Test Unix process-group and Windows Job Object cancellation with descendants, not only immediate child processes.
- Compile the portable Flow and Process contracts through Fable. Test platform adapters separately on .NET, browser, and Node hosts where supported.
- Follow existing integration-test style in the Flow integration projects and keep end-user examples in the intended `cmd`, `pipe`, and terminal-operation syntax.
- Regenerate source-backed reference documentation after the API stabilizes. Defer repository-wide documentation validation until the phase boundary to avoid interfering with concurrent Schema documentation work.

## Out Of Scope

- A general interactive terminal emulator or pseudo-terminal implementation. True inherited handles cover interactive tools; PTY support requires a separate proposal.
- Shell parsing, glob expansion, variable expansion, command substitution, shell built-ins, and arbitrary shell fragments. Users may explicitly invoke a shell command when shell semantics are required.
- Reproducing Bash's order-sensitive file-descriptor mutation syntax. The API models final typed routing intent instead.
- Guaranteeing a total chronological order between independently written stdout and stderr channels.
- Transparent remote execution, containers, SSH orchestration, or distributed process graphs.
- Persistent detached daemons that outlive the Flow scope. Structured concurrency is the default; an explicitly supervised daemon facility would require a separate lifecycle design.
- Every ProcessX or CliWrap convenience alias. Features are included only when they improve common scripting, correctness, composition, or diagnostics.
- Schema code, Schema documentation, or Schema-generated artifacts.

## Further Notes

The main usability test is whether a reader familiar with Bash can identify command boundaries, pipes, redirections, and
execution mode without learning symbolic punctuation. The main correctness test is whether cancellation, high-volume
mixed output, and intermediate failure remain safe without user-managed tasks or stream-draining code.

The names `capture`, `console`, `stream`, and `toFlow` should be reviewed together. `capture` is intentionally a little
longer because it promises wait-to-exit plus complete retained stdout/stderr. `toFlow` exists for mixed or fully explicit
target configurations where none of the named shortcuts accurately describes the result.

The proposed `2>&1` and `|&` equivalents support the practical routing intent while avoiding an inaccurate promise of
portable file-descriptor identity or cross-channel write ordering. Documentation must make this distinction visible next
to the first examples, not bury it in reference material.

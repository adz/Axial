---
title: Deferreds
description: "Ten completions race to complete one Deferred while awaiters are interrupted, and a late awaiter arrives after it is complete."
---

# Deferreds

Ten completers race to complete one [Deferred](../deferred-semaphore.html), each in a different way, while forty
awaiters wait and half of them are interrupted.

Run it with `dotnet run --project examples/Axial.TortureTest -- deferreds 100`.

## What it does

- Each completer picks `Deferred.succeed`, `fail`, `die`, `complete`, or `interrupt` at random. Each reports whether
  it won.
- The early awaiters are forked before any completion, and a random half are interrupted while the completers run.
- A late awaiter reads the outcome after everything has settled.

{{< snippet id="torture-deferreds" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| Exactly one completion won | A deferred completes once, and every other completion reports that it lost. |
| Every awaiter that was not interrupted saw the winning outcome | A success, a typed failure, a defect, or an interruption reaches every waiting awaiter unchanged. |
| An awaiter arriving afterwards sees the same outcome | The outcome is kept for every later awaiter. |

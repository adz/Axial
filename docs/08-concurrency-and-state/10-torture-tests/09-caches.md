---
title: Caches
description: "Many callers look up the same keys in a single-flight cache while some are interrupted, some lookups fail, and keys are invalidated."
---

# Caches

Two hundred callers look up eight keys in a [cache](../cache.html) at the same time. Some callers are interrupted while
they wait, some lookups fail, and an invalidator keeps removing keys.

Run it with `dotnet run --project examples/Axial.TortureTest -- caches 100`.

## What it does

- `Cache.make` wraps a lookup that sleeps briefly. Odd keys fail on their first run.
- Twenty-five callers per key call `Cache.get` at once, and a random fifth are interrupted.
- An invalidator calls `Cache.invalidate` on random keys while the callers run. Afterwards `invalidateAll` empties the
  cache.
- A second part shares one `Flow.memoize`d flow between fifty concurrent callers; its first run fails.

{{< snippet id="torture-caches" mode="no-check" reason="Compiled and run as part of examples/Axial.TortureTest, which also defines the Round and Check helpers it uses" >}}

## What each check proves

| Check | Guarantee |
| --- | --- |
| Every value a caller received came from a lookup that succeeded | A caller never sees a value no lookup produced. |
| A caller that was not interrupted got a value or the lookup's own failure | Interrupting one caller does not fail the others waiting on the same lookup. |
| A key succeeded at most once per invalidation | Concurrent callers share one lookup instead of each running their own. |
| A failed lookup was not cached | The next caller after a failure runs the lookup again. |
| A cached value is served without running the lookup again | A hit does not call the lookup. |
| invalidateAll empties the cache | `Cache.count` drops to zero. |
| The first failure was not remembered | `Flow.memoize` forgets a failed run. |
| Concurrent callers shared one successful run | Callers arriving during a run wait for it instead of starting another. |

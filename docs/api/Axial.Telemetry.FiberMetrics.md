# FiberMetrics

`FiberMetrics.observe` installs a fiber observer that records runtime health onto the `Axial` `Meter`. Subscribe with
`.AddMeter("Axial")` in `.WithMetrics(...)` and the instruments land in any OTLP backend.

| Instrument | Kind | Meaning |
| --- | --- | --- |
| `axial.flow.fibers.started` | counter | fibers forked |
| `axial.flow.fibers.live` | up-down counter | fibers currently running |
| `axial.flow.fibers.settled` | counter, tagged `axial.flow.fiber.status` | settles split by `Succeeded`/`Failed`/`Interrupted` |
| `axial.flow.fiber.duration` | histogram (seconds), tagged with status | fork-to-settle lifetime |
| `axial.flow.fibers.unobserved_defects` | counter | defects the runtime proved no code could observe |

A climbing `fibers.live` with flat `fibers.settled` is a fiber leak; a nonzero `unobserved_defects` rate is crashing
background work nobody joins. Read the [telemetry guide](/observability/telemetry/index.html) for the edge install.

# Schedule

A `Schedule<'env, 'input, 'output>` decides, after each run of a flow, whether to run it again and how long to wait
first. It holds no state, so one schedule value can drive any number of independent runs.

Build one from [`recurs`](#Axial.Schedule.recurs), [`spaced`](#Axial.Schedule.spaced),
[`exponential`](#Axial.Schedule.exponential), or [`fixedRate`](#Axial.Schedule.fixedRate), then shape it: cap it with
[`union`](#Axial.Schedule.union) or [`upTo`](#Axial.Schedule.upTo), bound it with
[`intersect`](#Axial.Schedule.intersect) or [`recursAtMost`](#Axial.Schedule.recursAtMost), filter by the retried
error with [`whileInput`](#Axial.Schedule.whileInput), sequence two with [`andThen`](#Axial.Schedule.andThen), and
restart the count after a healthy run with [`resetAfter`](#Axial.Schedule.resetAfter).

Drive it with [`Flow.retry`](/api/Axial.Flow.html#Axial.Flow.retry) after typed failures,
[`Flow.repeat`](/api/Axial.Flow.html#Axial.Flow.repeat) after successes, or
[`FlowStream.fromSchedule`](/api/Axial.FlowStreamModule.html#Axial.FlowStream.fromSchedule) as a stream of its outputs.
Delays are measured on the runtime's time, not the application clock. Read the
[Scheduling and retries guide](/scheduling-and-retries/index.html).

## Signature reference

| Function | Signature | Behavior |
| :--- | :--- | :--- |
| `recurs` | `int -> Schedule<'env, 'input, int>` | Allows exactly `n` additional runs and emits the zero-based recurrence index. |
| `spaced` | `TimeSpan -> Schedule<'env, 'input, int>` | Continues with a fixed delay and emits the zero-based recurrence index. |
| `exponential` | `TimeSpan -> Schedule<'env, 'input, TimeSpan>` | Continues with a delay that doubles after each run. |
| `jitteredWith` | `(unit -> float) -> Schedule<'env, 'input, 'output> -> Schedule<'env, 'input, 'output>` | Adjusts each delay by a sampled factor, normally from 0.5 to 1.5. |
| `fixedRate` | `TimeSpan -> Schedule<'env, 'input, int>` | Starts runs at `start + n * period` and emits the zero-based recurrence index. |
| `union` | `Schedule<'env, 'input, 'o2> -> Schedule<'env, 'input, 'o1> -> Schedule<'env, 'input, 'o1 option * 'o2 option>` | Continues while either continues, with the shorter delay. |
| `intersect` | `Schedule<'env, 'input, 'o2> -> Schedule<'env, 'input, 'o1> -> Schedule<'env, 'input, 'o1 * 'o2>` | Continues while both continue, with the longer delay. |
| `andThen` | `Schedule<'env, 'input, 'o2> -> Schedule<'env, 'input, 'o1> -> Schedule<'env, 'input, Choice<'o1, 'o2>>` | Runs the first schedule until it stops, then the second from its start. |
| `whileInput` / `untilInput` | `('input -> bool) -> Schedule<'env, 'input, 'output> -> Schedule<'env, 'input, 'output>` | Continues while (until) the retried error or repeated value satisfies the predicate. |
| `whileOutput` / `untilOutput` | `('output -> bool) -> Schedule<'env, 'input, 'output> -> Schedule<'env, 'input, 'output>` | Continues while (until) the schedule's own output satisfies the predicate. |
| `recursAtMost` | `int -> Schedule<'env, 'input, 'output> -> Schedule<'env, 'input, 'output>` | Stops after at most `n` recurrences, keeping the schedule's delays. |
| `upTo` | `TimeSpan -> Schedule<'env, 'input, 'output> -> Schedule<'env, 'input, 'output>` | Stops once a total time budget has passed since the first run. |
| `resetAfter` | `TimeSpan -> Schedule<'env, 'input, 'output> -> Schedule<'env, 'input, 'output>` | Restarts the count after a run that lasted at least the given time. |
| `elapsed` | `Schedule<'env, 'input, TimeSpan>` | Recurs without waiting and emits the time since the first run. |
| `map` | `('o1 -> 'o2) -> Schedule<'env, 'input, 'o1> -> Schedule<'env, 'input, 'o2>` | Transforms the schedule's output. |
| `Flow.retry` | `Schedule<'env, 'error, 'output> -> Flow<'env, 'error, 'value> -> Flow<'env, 'error, 'value>` | Retries the flow after `Cause.Fail`. |
| `Flow.repeat` | `Schedule<'env, 'value, 'output> -> Flow<'env, 'error, 'value> -> Flow<'env, 'error, 'value>` | Repeats the flow after success. |
| `FlowStream.fromSchedule` | `Schedule<'env, unit, 'output> -> FlowStream<'env, 'error, 'output>` | Emits each output after its delay; ends when the schedule stops. |

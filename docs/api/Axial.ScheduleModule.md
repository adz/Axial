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

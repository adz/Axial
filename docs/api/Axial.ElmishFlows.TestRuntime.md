# TestRuntime

`ElmishRuntime.test env` creates a test runtime with queued delivery. A manual clock in `env` controls workflow time
independently of the wall-clock settle timeout.

## Operations

- `TestRuntime.run command test` and `TestRuntime.settle timeout test` process callbacks.
- `TestRuntime.drain test` drains pending callbacks.
- `TestRuntime.dispatched test` and `TestRuntime.defects test` inspect captured messages and defects.

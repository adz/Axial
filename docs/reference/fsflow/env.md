---
title: Env
linkTitle: Env
---

Request token for binding a whole dependency inside a workflow.



## Remarks

Use this token when a workflow needs the dependency itself rather than a value projected from
that dependency. The `flow {}`, `asyncFlow {}`, and `taskFlow {}` builders
read it from any environment that implements `Needs&lt;'dep&gt;`.


## Definitions

### `type Env<'dep>`

#### Examples

```fsharp
type IClock =
    abstract UtcNow : unit -&gt; DateTimeOffset

type ClockCaps =
    inherit Needs&lt;IClock&gt;
    abstract Clock : IClock

let readClock : Flow&lt;#ClockCaps, unit, IClock&gt; =
    flow {
        let! clock = Env&lt;IClock&gt;
        return clock
    }
```

- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L115)

### `type Env<'dep, 'value>`

Request token for projecting a value from a dependency.

Builders read the dependency from the environment, apply the projection, and then reuse the
existing lift/bind behavior for the projected value. If the projection returns a
`Result`, `Async`, `Task`, `ValueTask`, `ColdTask`, `option`, or
`voption`, the existing workflow rules still apply.

#### Examples

```fsharp
type IClock =
    abstract UtcNow : unit -&gt; DateTimeOffset

type ClockCaps =
    inherit Needs&lt;IClock&gt;
    abstract Clock : IClock

let readClockNow : TaskFlow&lt;#ClockCaps, unit, DateTimeOffset&gt; =
    taskFlow {
        let! now = Env&lt;IClock&gt; _.UtcNow
        return now
    }
```

- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L144)

## Information

- **Module**: Global
- **Source**: [source](https://github.com/adz/FsFlow/blob/main/src/FsFlow/Core.fs#L115)


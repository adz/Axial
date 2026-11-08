---
title: Axial.ElmishFlows API
api:
  packageSections:
    - package: Axial.ElmishFlows
      id: commands
      title: Commands and subscriptions
      order: 10
      summary: Run Axial flows as Elmish commands and subscriptions.
      entities:
        - Axial.ElmishFlows.Cmd
        - Axial.ElmishFlows.Cmd.OfFlow
        - Axial.ElmishFlows.Cmd.OfFlow.Latest
        - Axial.ElmishFlows.Cmd.OfFlowStream
        - Axial.ElmishFlows.Sub
        - Axial.ElmishFlows.Sub.OfFlowStream
    - package: Axial.ElmishFlows
      id: input
      title: Input
      order: 20
      summary: Offer values from update and read them as a stream.
      entities:
        - Axial.ElmishFlows.Inbox
        - Axial.ElmishFlows.Inbox`1
    - package: Axial.ElmishFlows
      id: runtime
      title: Runtime
      order: 30
      summary: Own the program's root scope, its services, and its shutdown.
      entities:
        - Axial.ElmishFlows.ElmishRuntime
        - Axial.ElmishFlows.ElmishRuntime`1
        - Axial.ElmishFlows.ElmishRuntimeOptions
        - Axial.ElmishFlows.CommandDefect
        - Axial.ElmishFlows.ServiceStartError
    - package: Axial.ElmishFlows
      id: testing
      title: Testing
      order: 40
      summary: Drive commands with a manual clock and inspect the messages and defects they produce.
      entities:
        - Axial.ElmishFlows.TestRuntime
        - Axial.ElmishFlows.TestRuntime`1
---

# Axial.ElmishFlows API

The [`Cmd`](Axial.ElmishFlows.Cmd.html) module runs workflows as commands, [`Sub`](Axial.ElmishFlows.Sub.html)
subscriptions run streams while Elmish keeps their id, and [`Inbox`](Axial.ElmishFlows.Inbox.html) carries one-way input
from `update` to a service. [`ElmishRuntime`](Axial.ElmishFlows.ElmishRuntime.html) owns the root scope and
[`TestRuntime`](Axial.ElmishFlows.TestRuntime.html) drives commands in tests.

See the [Elmish guides](/elmish/index.html) for the runtime lifecycle, runtime-owned services, and a desktop recipe.

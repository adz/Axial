---
title: Axial.Process API
api:
  packageSections:
    - package: Axial.Process
      id: commands
      title: Commands and composition
      order: 10
      summary: Describe commands, arguments, and pipelines before execution.
      entities:
        - Axial.Process.ProcessCommand
        - Axial.Process.ProcessSpec
        - Axial.Process.ProcessInput
        - Axial.Process.InputSource
        - Axial.Process.SecretArgument
        - Axial.Process.StageDefaults
        - Axial.Process.StageDefaultsModule
        - Axial.Process.DSL
        - Axial.Process.DSL.EndpointConnector
        - Axial.Process.DSL.Input
        - Axial.Process.DSL.Output
    - package: Axial.Process
      id: execution
      title: Execution and host services
      order: 20
      summary: Run process workflows and provide live or test process services.
      entities:
        - Axial.Process.Process
        - Axial.Process.Process.Runner
        - Axial.Process.IProcess
        - Axial.Process.IHasProcess
        - Axial.Process.ProcessHostEnvironment
        - Axial.Process.ProcessHostEnvironmentModule
        - Axial.Process.TestProcess
        - Axial.Process.TestProcessHostEnvironment
    - package: Axial.Process
      id: output
      title: Output and streaming
      order: 30
      summary: Capture, route, and consume process output.
      entities:
        - Axial.Process.CapturedOutput
        - Axial.Process.OutputChannel
        - Axial.Process.OutputFraming
        - Axial.Process.OutputRetention
        - Axial.Process.OutputTarget
        - Axial.Process.ProcessOutput
        - Axial.Process.ProcessCompletion
        - Axial.Process.ProcessStream
        - Axial.Process.ProcessStream`2
        - Axial.Process.ProcessStreamError`1
    - package: Axial.Process
      id: results
      title: Results and failures
      order: 40
      summary: Inspect successful results, process plans, and expected failures.
      entities:
        - Axial.Process.ProcessResult
        - Axial.Process.StageResult
        - Axial.Process.StageFailure
        - Axial.Process.ProcessPlan
        - Axial.Process.ProcessStagePlan
        - Axial.Process.ProcessError
        - Axial.Process.ProcessErrorModule
        - Axial.Process.ProcessStartFailure
        - Axial.Process.ProcessStageTimeout
        - Axial.Process.ProcessTimeout
        - Axial.Process.ProcessIoFailure
        - Axial.Process.ProcessLineFailure
        - Axial.Process.ProcessObservationFailure
        - Axial.Process.ProcessRunException`1
---

# Axial.Process API

The reference is organized by the job each part of the API does: build commands, run them, handle output, or inspect
the result. The [`Process` operations](Axial.Process.Process.html) page groups its functions by that same path.
Use the [Process guides](/process/index.html) to choose an approach before looking up a member.

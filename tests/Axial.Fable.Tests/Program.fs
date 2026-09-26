module Axial.Fable.Tests.Program

open Axial.Fable.Tests

Harness.run (TimingTests.tests @ FiberTests.tests @ QueueTests.tests)

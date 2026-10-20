module Axial.Fable.Tests.Program

open Axial.Fable.Tests

Harness.run (SecureRandomTests.tests @ TimingTests.tests @ FiberTests.tests @ QueueTests.tests @ TortureTests.tests)

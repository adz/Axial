module Axial.Fable.Tests.Program

open Axial.Fable.Tests

Harness.run (PlatformScopeTests.tests @ TimingTests.tests @ FiberTests.tests @ QueueTests.tests @ RunOnTests.tests @ TortureTests.tests)

module Axial.Fable.Tests.Program

open Axial.Fable.Tests

let coreTests = HostLoggingTests.tests @ PlatformScopeTests.tests @ TimingTests.tests @ FiberTests.tests @ QueueTests.tests @ RunOnTests.tests
Harness.run (coreTests @ TortureTests.tests)

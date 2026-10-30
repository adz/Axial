module Axial.Fable.Tests.Program

open Axial.Fable.Tests

let coreTests = PlatformScopeTests.tests @ TimingTests.tests @ FiberTests.tests @ QueueTests.tests @ RunOnTests.tests
#if AXIAL_CORE_ONLY
Harness.run coreTests
#else
Harness.run (coreTests @ TortureTests.tests)
#endif

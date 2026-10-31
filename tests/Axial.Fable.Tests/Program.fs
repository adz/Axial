module Axial.Fable.Tests.Program

open Axial.Fable.Tests

let coreTests = TelemetryHostTests.tests @ HostLoggingTests.tests @ PlatformScopeTests.tests @ CultureTests.tests @ TimingTests.tests @ FiberTests.tests @ QueueTests.tests @ RunOnTests.tests @ ThrownDefectTests.tests @ ThrowSafetyMatrixTests.tests
Harness.run (coreTests @ TortureTests.tests)

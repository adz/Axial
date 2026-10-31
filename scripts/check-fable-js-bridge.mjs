// A handwritten JavaScript caller installs a clock around a flow without a clock-bearing environment.
// Run after scripts/run-fable-tests.sh compiles artifacts/fable-tests.
import { Clock_live } from "../artifacts/fable-tests/src/Axial/PlatformOperations.js";
import { Flow_sleep, Flow_toAsync, Flow_withClock } from "../artifacts/fable-tests/src/Axial/Flow.js";
import { startAsPromise } from "../artifacts/fable-tests/fable_modules/fable-library-js.5.6.0/Async.js";

const workflow = Flow_withClock(Clock_live, Flow_sleep(5));
const exit = await startAsPromise(Flow_toAsync(undefined, workflow));
if (exit.tag !== 0) throw new Error("Timed flow failed through the JavaScript bridge");
console.log("JavaScript clock bridge passed");

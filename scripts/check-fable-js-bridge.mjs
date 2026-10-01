// A handwritten JavaScript caller can use Axial's Fable-generated clock environment without knowing
// the mangled IHasClock method name. Run after scripts/run-fable-tests.sh compiles artifacts/fable-tests.
import { ClockEnvironment } from "../artifacts/fable-tests/src/Axial/Outcome.js";
import { Clock_live } from "../artifacts/fable-tests/src/Axial.PlatformService/Operations.js";
import { Flow_sleep, Flow_toAsync } from "../artifacts/fable-tests/src/Axial/Flow.js";
import { startAsPromise } from "../artifacts/fable-tests/fable_modules/fable-library-js.5.6.0/Async.js";

const environment = new ClockEnvironment(Clock_live);
const exit = await startAsPromise(Flow_toAsync(environment, Flow_sleep(5)));
if (exit.tag !== 0) throw new Error("Timed flow failed through the JavaScript bridge");
console.log("JavaScript clock bridge passed");

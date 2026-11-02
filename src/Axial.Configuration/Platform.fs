namespace Axial.Configuration

#if FABLE_COMPILER
open Fable.Core
open Fable.Core.JsInterop
#else
open System
open System.Collections
#endif

/// Runtime-specific reads for configuration sources. Every ambient read in Axial.Configuration is here.
module internal Platform =
    /// Reads the live process environment as name and value pairs, once.
#if FABLE_COMPILER
    [<Emit("typeof process !== 'undefined' && process?.env != null ? Object.entries(process.env).filter(([, v]) => v != null) : []")>]
    let environmentPairs () : (string * string) array = jsNative

    [<Emit("decodeURIComponent($0)")>]
    let decodeComponent (text: string) : string = jsNative
#else
    let environmentPairs () : (string * string) array =
        // axial-allow-AXG001: live process-environment read behind ConfigSource.environment
        [| for entry in Environment.GetEnvironmentVariables() do
               let entry = entry :?> DictionaryEntry
               yield string entry.Key, string entry.Value |]

    let decodeComponent (text: string) : string = Uri.UnescapeDataString text
#endif

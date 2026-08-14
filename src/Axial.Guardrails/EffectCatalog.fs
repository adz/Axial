/// The table of ambient .NET effects that Axial's core packages must not call directly.
///
/// Each entry names a category (used in `axial-allow-effect` suppression comments), a symbol
/// match rule, a human message, and the explicit Axial.PlatformService replacement.
module Axial.Guardrails.EffectCatalog

/// How a banned symbol is recognized against a resolved FSharpSymbolUse.
type SymbolMatch =
    /// The symbol's declaring entity full name equals this, and it is a constructor call.
    | ConstructorOf of entityFullName: string
    /// The symbol's declaring entity full name equals this, and the compiled member name is one of these.
    | MembersOf of entityFullName: string * memberNames: string list
    /// The symbol's declaring entity full name equals this; any member matches (wildcard).
    | AnyMemberOf of entityFullName: string

type EffectRule =
    { Category: string
      Match: SymbolMatch
      Message: string
      Replacement: string }

let rules: EffectRule list =
    [ { Category = "random"
        Match = ConstructorOf "System.Random"
        Message = "constructs System.Random directly, which makes behavior depend on ambient, untestable randomness"
        Replacement = "Axial.PlatformService.IRandom (Random.service / Random.nextDouble)" }

      { Category = "guid"
        Match = MembersOf("System.Guid", [ "NewGuid" ])
        Message = "calls Guid.NewGuid() directly, which makes generated identifiers untestable"
        Replacement = "Axial.PlatformService.IGuid (Guid.service / Guid.newGuid)" }

      { Category = "clock"
        Match = MembersOf("System.DateTime", [ "get_Now"; "Now"; "get_UtcNow"; "UtcNow"; "get_Today"; "Today" ])
        Message = "reads the ambient system clock through System.DateTime, which makes timing untestable"
        Replacement = "Axial.PlatformService.IClock (Clock.service / Clock.utcNow)" }

      { Category = "clock"
        Match = MembersOf("System.DateTimeOffset", [ "get_Now"; "Now"; "get_UtcNow"; "UtcNow" ])
        Message = "reads the ambient system clock through System.DateTimeOffset, which makes timing untestable"
        Replacement = "Axial.PlatformService.IClock (Clock.service / Clock.utcNow)" }

      { Category = "clock"
        Match = MembersOf("System.Threading.Tasks.Task", [ "Delay" ])
        Message = "calls Task.Delay directly, which makes scheduled waits untestable and bypasses fiber interruption"
        Replacement = "Flow.sleep / Schedule, or Axial.PlatformService.IClock for a raw delay" }

      { Category = "environment"
        Match =
            MembersOf(
                "System.Environment",
                [ "GetEnvironmentVariable"
                  "GetEnvironmentVariables"
                  "SetEnvironmentVariable"
                  "get_MachineName"
                  "MachineName"
                  "get_UserName"
                  "UserName"
                  "get_OSVersion"
                  "OSVersion"
                  "get_ProcessorCount"
                  "ProcessorCount"
                  "get_CurrentDirectory"
                  "CurrentDirectory" ]
            )
        Message = "reads or writes ambient process/OS environment state directly"
        Replacement = "Axial.PlatformService.IEnvironment, or an explicit configuration value passed through 'env" }

      { Category = "console"
        Match = AnyMemberOf "System.Console"
        Message = "touches System.Console directly, which makes output/input an untestable ambient effect"
        Replacement = "Axial.Console's IConsole service" }

      { Category = "filesystem"
        Match = AnyMemberOf "System.IO.File"
        Message = "touches System.IO.File directly, bypassing the explicit filesystem service"
        Replacement = "Axial.FileSystem's IFileSystem service" }

      { Category = "filesystem"
        Match = AnyMemberOf "System.IO.Directory"
        Message = "touches System.IO.Directory directly, bypassing the explicit filesystem service"
        Replacement = "Axial.FileSystem's IFileSystem service" }

      { Category = "process"
        Match = MembersOf("System.Diagnostics.Process", [ "Start" ])
        Message = "starts an OS process directly, bypassing the explicit process service"
        Replacement = "Axial.Process's IProcess service" }

      { Category = "sleep"
        Match = MembersOf("System.Threading.Thread", [ "Sleep" ])
        Message = "blocks a thread with Thread.Sleep, an ambient and untestable delay outside the fiber scheduler"
        Replacement = "Flow.sleep / Schedule" } ]

/// All category names that appear in `rules`, for validating suppression comments.
let knownCategories: Set<string> =
    rules |> List.map (fun r -> r.Category) |> Set.ofList

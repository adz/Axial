/// The table of ambient .NET effects that Axial's packages ask callers not to use directly.
///
/// Each rule belongs to a guardrail, named after the package whose service replaces the effect (`Axial.HttpClient`
/// for `new HttpClient`). A rule names a category (used in `axial-allow-effect` suppression comments), a symbol match,
/// where the match applies, a human message, and the explicit replacement. Which guardrails run, and at which
/// severity, comes from `GuardrailConfig`.
module Axial.Guardrails.EffectCatalog

/// How a banned symbol is recognized against a resolved FSharpSymbolUse.
type SymbolMatch =
    /// The symbol's declaring entity full name equals this, and it is a constructor call.
    | ConstructorOf of entityFullName: string
    /// The symbol's declaring entity full name equals this, and the compiled member name is one of these.
    | MembersOf of entityFullName: string * memberNames: string list
    /// The symbol's declaring entity full name equals this; any member matches (wildcard).
    | AnyMemberOf of entityFullName: string

/// Where a rule's match counts as a finding.
type RuleScope =
    /// Anywhere in the file.
    | Anywhere
    /// Only inside a `flow { }` computation expression.
    | InsideFlow
    /// Only outside a `flow { }` computation expression.
    | OutsideFlow

type EffectRule =
    { Id: string
      Guardrail: string
      Category: string
      Match: SymbolMatch
      Scope: RuleScope
      Message: string
      Replacement: string }

/// The guardrail that carries Axial's own conventions: the structural analyzers (raise in flow, fixtures, discarded
/// cancellation, reflection formatting, suppression integrity) and the effects the core runtime replaces.
[<Literal>]
let CoreGuardrail = "Axial"

let private rule id guardrail category matching message replacement =
    { Id = id
      Guardrail = guardrail
      Category = category
      Match = matching
      Scope = Anywhere
      Message = message
      Replacement = replacement }

let builtInRules: EffectRule list =
    [ rule "random.new" "Axial.PlatformService" "random" (ConstructorOf "System.Random")
          "constructs System.Random directly, which makes behavior depend on ambient, untestable randomness; use ISecureRandom instead for secrets, tokens, and nonces"
          "Axial.PlatformService.IRandom (Random.service / Random.nextDouble), or ISecureRandom for secrets"

      rule "random.cryptographic" "Axial.PlatformService" "secure-random"
          (MembersOf(
              "System.Security.Cryptography.RandomNumberGenerator",
              [ "Create"; "GetBytes"; "GetNonZeroBytes"; "Fill"; "GetInt32"; "GetHexString"; "GetString"; "GetItems"; "Shuffle" ]
          ))
          "uses RandomNumberGenerator directly, which makes security-sensitive values depend on ambient, untestable randomness"
          "Axial.PlatformService.ISecureRandom (SecureRandom.live / SecureRandom.service)"

      rule "guid.new" "Axial.PlatformService" "guid" (MembersOf("System.Guid", [ "NewGuid" ]))
          "calls Guid.NewGuid() directly, which makes generated identifiers untestable"
          "Axial.PlatformService.IGuid (Guid.service / Guid.newGuid)"

      rule "clock.datetime" CoreGuardrail "clock"
          (MembersOf("System.DateTime", [ "get_Now"; "Now"; "get_UtcNow"; "UtcNow"; "get_Today"; "Today" ]))
          "reads the ambient system clock through System.DateTime, which makes timing untestable"
          "Axial.IClock (Axial.PlatformService.Clock.now)"

      rule "clock.datetimeoffset" CoreGuardrail "clock"
          (MembersOf("System.DateTimeOffset", [ "get_Now"; "Now"; "get_UtcNow"; "UtcNow" ]))
          "reads the ambient system clock through System.DateTimeOffset, which makes timing untestable"
          "Axial.IClock (Axial.PlatformService.Clock.now)"

      rule "clock.local-timezone" "Axial.PlatformService" "environment"
          (MembersOf("System.TimeZoneInfo", [ "get_Local"; "Local" ]))
          "reads TimeZoneInfo.Local, which depends on the machine's configured time zone"
          "an explicit TimeZoneInfo supplied through 'env or configuration"

      rule "clock.stopwatch" CoreGuardrail "clock" (AnyMemberOf "System.Diagnostics.Stopwatch")
          "measures time with System.Diagnostics.Stopwatch directly, which makes durations untestable"
          "Axial.IClock (Axial.PlatformService.Clock.timed / Clock.elapsed)"

      rule "environment.state" "Axial.PlatformService" "environment"
          (MembersOf(
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
                "get_CurrentDirectory"
                "CurrentDirectory" ]
          ))
          "reads or writes ambient process/OS environment state directly"
          "Axial.PlatformService.IEnvironment, or an explicit configuration value passed through 'env"

      rule "environment.processor-count" CoreGuardrail "environment"
          (MembersOf("System.Environment", [ "get_ProcessorCount"; "ProcessorCount" ]))
          "reads Environment.ProcessorCount directly to size concurrency"
          "Parallelism.ofProcessors (fun n -> ...)"

      rule "clock.task-delay" CoreGuardrail "clock" (MembersOf("System.Threading.Tasks.Task", [ "Delay" ]))
          "calls Task.Delay directly, which makes scheduled waits untestable and bypasses fiber interruption"
          "Flow.sleep / Schedule, or Axial.IClock.Sleep for a raw delay"

      rule "sleep.thread" CoreGuardrail "sleep" (MembersOf("System.Threading.Thread", [ "Sleep" ]))
          "blocks a thread with Thread.Sleep, an ambient and untestable delay outside the fiber scheduler"
          "Flow.sleep / Schedule"

      { rule "blocking.get-result" CoreGuardrail "blocking"
            (MembersOf("System.Runtime.CompilerServices.TaskAwaiter`1", [ "GetResult" ]))
            "blocks a thread on a task with .GetAwaiter().GetResult() inside a flow, which stalls the workflow and ignores interruption"
            "let! (bind the task with Flow.fromTask / ColdTask), or Flow.fromBlocking for synchronous work" with
            Scope = InsideFlow }

      { rule "blocking.get-result-unit" CoreGuardrail "blocking"
            (MembersOf("System.Runtime.CompilerServices.TaskAwaiter", [ "GetResult" ]))
            "blocks a thread on a task with .GetAwaiter().GetResult() inside a flow, which stalls the workflow and ignores interruption"
            "do! (bind the task with Flow.fromTask / ColdTask), or Flow.fromBlocking for synchronous work" with
            Scope = InsideFlow }

      { rule "blocking.task-result" CoreGuardrail "blocking"
            (MembersOf("System.Threading.Tasks.Task`1", [ "get_Result"; "Result" ]))
            "blocks a thread on a task with .Result inside a flow, which stalls the workflow and ignores interruption"
            "let! (bind the task with Flow.fromTask / ColdTask), or Flow.fromBlocking for synchronous work" with
            Scope = InsideFlow }

      { rule "blocking.async" CoreGuardrail "blocking"
            (MembersOf("Microsoft.FSharp.Control.FSharpAsync", [ "RunSynchronously" ]))
            "blocks the calling thread waiting for an async computation"
            "Async.StartAsTask and await the task at the host boundary" with Scope = OutsideFlow }

      { rule "blocking.task-wait" CoreGuardrail "blocking"
            (MembersOf("System.Threading.Tasks.Task", [ "Wait" ]))
            "blocks the calling thread waiting for a task"
            "await the task asynchronously" with Scope = OutsideFlow }

      { rule "blocking.task-result-outside" CoreGuardrail "blocking"
            (MembersOf("System.Threading.Tasks.Task`1", [ "get_Result"; "Result" ]))
            "blocks the calling thread waiting for a task result"
            "await the task asynchronously" with Scope = OutsideFlow }

      { rule "blocking.value-task-result" CoreGuardrail "blocking"
            (MembersOf("System.Threading.Tasks.ValueTask`1", [ "get_Result"; "Result" ]))
            "blocks the calling thread waiting for a value task result"
            "await the value task asynchronously" with Scope = OutsideFlow }

      { rule "blocking.get-result-outside" CoreGuardrail "blocking"
            (MembersOf("System.Runtime.CompilerServices.TaskAwaiter`1", [ "GetResult" ]))
            "blocks the calling thread waiting for a task"
            "await the task asynchronously" with Scope = OutsideFlow }

      { rule "blocking.get-result-unit-outside" CoreGuardrail "blocking"
            (MembersOf("System.Runtime.CompilerServices.TaskAwaiter", [ "GetResult" ]))
            "blocks the calling thread waiting for a task"
            "await the task asynchronously" with Scope = OutsideFlow }

      // AXG008 is syntactic; this entry registers its suppression category.
      rule "scope.run" CoreGuardrail "scope" (MembersOf("Axial.__ScopeFromRun", []))
          "returns a scope-owned service" "use the service within its run"

      rule "console.any" "Axial.Console" "console" (AnyMemberOf "System.Console")
          "touches System.Console directly, which makes output/input an untestable ambient effect"
          "Axial.Console's IConsole service"

      rule "filesystem.file" "Axial.FileSystem" "filesystem" (AnyMemberOf "System.IO.File")
          "touches System.IO.File directly, bypassing the explicit filesystem service"
          "Axial.FileSystem's IFileSystem service"

      rule "filesystem.directory" "Axial.FileSystem" "filesystem" (AnyMemberOf "System.IO.Directory")
          "touches System.IO.Directory directly, bypassing the explicit filesystem service"
          "Axial.FileSystem's IFileSystem service"

      rule "filesystem.watcher" "Axial.FileSystem" "filesystem" (ConstructorOf "System.IO.FileSystemWatcher")
          "constructs a filesystem watcher directly, bypassing the explicit filesystem service"
          "Axial.FileSystem's IFileSystem.Watch service"

      rule "process.start" "Axial.Process" "process" (MembersOf("System.Diagnostics.Process", [ "Start" ]))
          "starts an OS process directly, bypassing the explicit process service"
          "Axial.Process's IProcess service"

      rule "websocket.new-client" "Axial.WebSocket" "websocket" (ConstructorOf "System.Net.WebSockets.ClientWebSocket")
          "constructs a WebSocket outside the mockable service boundary" "Axial.WebSocket IWebSocket service"
      rule "http.new-client" "Axial.HttpClient" "http" (ConstructorOf "System.Net.Http.HttpClient")
          "constructs HttpClient directly, which exhausts sockets when repeated and cannot be replaced in tests"
          "Axial.HttpClient's IHttp service (Http.live with one shared HttpClient)" ]

/// Every guardrail that Axial.Guardrails ships rules for.
let builtInGuardrails: Set<string> =
    builtInRules |> List.map _.Guardrail |> Set.ofList |> Set.add CoreGuardrail

/// Parses a symbol match written in a rules file: `ctor:Type`, `member:Type::A,B`, or `any:Type`.
let parseMatch (text: string) : SymbolMatch option =
    let text = text.Trim()

    if text.StartsWith "ctor:" then
        Some(ConstructorOf(text.Substring 5))
    elif text.StartsWith "any:" then
        Some(AnyMemberOf(text.Substring 4))
    elif text.StartsWith "member:" then
        match text.Substring(7).Split([| "::" |], System.StringSplitOptions.None) with
        | [| entity; members |] ->
            let names = members.Split(',') |> Array.map _.Trim() |> Array.filter ((<>) "") |> List.ofArray
            if List.isEmpty names then None else Some(MembersOf(entity, names))
        | _ -> None
    else
        None

module Axial.Configuration.Tests.ConfigurationTests

open System
open Xunit
open Swensen.Unquote
open Axial
open Axial.Configuration
open Axial.Configuration.Tests.Fixtures
open Axial.FileSystem
open Axial.PlatformService
open Reified
open Reified.Refinements

let private success flow =
    match run flow with
    | Exit.Success value -> value
    | Exit.Failure cause -> failwithf "Expected success, got %A" cause

let private valueOf exit =
    match exit with
    | Exit.Success value -> value
    | Exit.Failure cause -> failwithf "Expected success, got %A" cause

let private failure flow =
    match run flow with
    | Exit.Success value -> failwithf "Expected failure, got %A" value
    | Exit.Failure cause ->
        match cause with
        | Cause.Fail errors -> errors
        | other -> failwithf "Expected a typed failure, got %A" other

[<Fact>]
let ``later sources win and reversing the list reverses the winner`` () =
    let source order =
        order
        |> List.map (function
            | "env" -> ConfigSource.environmentOf None [ "Port", "1" ]
            | "args" -> ConfigSource.commandLine [ "--Port=2" ]
            | value -> ConfigSource.ofData value (Data.Object [ "Port", Data.Number "3" ]))

    let forward = success (Config.load (source [ "env"; "args" ]) (portSchema ()))
    let reversed = success (Config.load (source [ "args"; "env" ]) (portSchema ()))
    test <@ forward.Port = 2 @>
    test <@ reversed.Port = 1 @>

[<Fact>]
let ``a later null overrides an earlier value`` () =
    let sources =
        [ ConfigSource.environmentOf None [ "Name", "from-env"; "Port", "1" ]
          ConfigSource.json "later.json" """{"Name":null}""" ]

    let settings = success (Config.load sources (portSchema ()))
    test <@ settings.Name = None @>

[<Fact>]
let ``keys match case-insensitively across merged sources`` () =
    let sources =
        [ ConfigSource.environmentOf None [ "PORT", "1" ]
          ConfigSource.json "app.json" """{"port":"2"}"""
          ConfigSource.commandLine [ "--Port=3" ] ]

    let settings = success (Config.load sources (portSchema ()))
    test <@ settings.Port = 3 @>

[<Fact>]
let ``nested command-line arguments and positionals arrive from Reified`` () =
    let config = success (Config.loadData [ ConfigSource.commandLine [ "--Db:Port=5"; "positional" ] ])

    let fields =
        match config.Data with
        | Data.Object values -> values
        | other -> failwithf "Expected an object, got %A" other

    let positional =
        fields |> List.pick (fun (name, value) -> if name = "_" then Some value else None)

    test <@ positional = Data.List [ Data.Text "positional" ] @>

[<Fact>]
let ``three bad fields across two sources accumulate in one invalid error`` () =
    let sources =
        [ ConfigSource.environmentOf None [ "A", "not-an-int"; "B", "also-bad" ]
          ConfigSource.commandLine [ "--C=nope" ] ]

    match failure (Config.load sources (threeSchema ())) with
    | [ ConfigError.Invalid issues ] ->
        let paths = issues |> SchemaErrors.toList |> List.map (fun issue -> SchemaPath.format issue.Path) |> List.sort
        test <@ List.length paths = 3 @>
        test <@ paths = [ "a"; "b"; "c" ] @>
    | other -> failwithf "Expected one Invalid error, got %A" other

[<Fact>]
let ``two unreadable sources report two source failures and no decode error`` () =
    let sources =
        [ ConfigSource.json "one.json" "{bad"
          ConfigSource.json "two.json" "[bad" ]

    match failure (Config.load sources (threeSchema ())) with
    | [ ConfigError.SourceFailed("one.json", _); ConfigError.SourceFailed("two.json", _) ] -> ()
    | other -> failwithf "Expected exactly two SourceFailed, got %A" other

[<Fact>]
let ``describe lists every source spelling for an omitted path`` () =
    let config =
        success (
            Config.loadData
                [ ConfigSource.environmentOf (Some "MYAPP") []
                  ConfigSource.commandLine []
                  ConfigSource.ofData "appsettings.json" (Data.Object []) ]
        )

    let text = ConfigError.describe config (ConfigError.Missing "Db.Host")
    test <@ text.Contains("--Db:Host") @>
    test <@ text.Contains("MYAPP__Db__Host") @>
    test <@ text.Contains("appsettings.json Db.Host") @>

[<Fact>]
let ``plain string reads return text for numbers and booleans and type the absence`` () =
    let config =
        Config.ofData (
            Data.Object
                [ "Db", Data.Object [ "Host", Data.Text "localhost"; "Port", Data.Number "5432" ]
                  "Enabled", Data.Bool true ]
        )

    let environment = { Config = config; FileSystem = TestFileSystem() :> IFileSystem }

    test <@ valueOf (Config.required "db.host" |> Flow.run environment) = "localhost" @>
    test <@ valueOf (Config.required "db.port" |> Flow.run environment) = "5432" @>
    test <@ valueOf (Config.optional "Enabled" |> Flow.run environment) = Some "true" @>
    test <@ valueOf (Config.optional "Missing" |> Flow.run environment) = None @>
    test <@ Config.tryFind "Db" config = Error(ConfigError.NotText "Db") @>
    test <@ Config.tryFind "Missing" config = Ok None @>

    match Config.required "Missing" |> Flow.run environment with
    | Exit.Failure(Cause.Fail(ConfigError.Missing "Missing")) -> ()
    | other -> failwithf "Expected Missing, got %A" other

    match Config.tryFind "Db[" config with
    | Error(ConfigError.InvalidPath "Db[") -> ()
    | other -> failwithf "Expected InvalidPath, got %A" other

[<Fact>]
let ``a fake configuration parses through the same schema as a loaded one`` () =
    let loaded =
        success (
            Config.load
                [ ConfigSource.environmentOf None [ "Db__Host", "env-host"; "Db__Port", "1"; "Timeout", "30" ]
                  ConfigSource.commandLine [ "--Db:Host=fake-host"; "--Db:Port=2" ] ]
                (nestedSchema ())
        )

    let faked =
        Config.parse
            (nestedSchema ())
            (Config.ofData (
                Data.Object
                    [ "Db", Data.Object [ "Host", Data.Text "fake-host"; "Port", Data.Number "2" ]
                      "Timeout", Data.Number "30" ]
            ))

    test <@ loaded.Db.Host = "fake-host" @>
    test <@ loaded.Db.Port = 2 @>

    match faked with
    | Ok settings ->
        test <@ settings.Db.Host = "fake-host" @>
        test <@ settings.Timeout = 30 @>
    | Error errors -> failwithf "Expected a decoded fake, got %A" errors

[<Fact>]
let ``a JSON source parses text and a browser query string nests dotted keys`` () =
    let fromJson =
        success (
            Config.load [ ConfigSource.json "inline.json" """{"Db":{"Host":"json-host","Port":"7"},"Timeout":"9"}""" ] (nestedSchema ())
        )

    test <@ fromJson.Db.Host = "json-host" @>
    test <@ fromJson.Db.Port = 7 @>

    let fromQuery =
        success (
            Config.load [ ConfigSource.queryString "?Db.Host=query%20host&Db.Port=5432&Timeout=4" ] (nestedSchema ())
        )

    test <@ fromQuery.Db.Host = "query host" @>
    test <@ fromQuery.Db.Port = 5432 @>
    test <@ fromQuery.Timeout = 4 @>

[<Fact>]
let ``a secret value never appears in errors, display or logs`` () =
    let value = "super-secret-payload"
    let secret = Secret.create value
    test <@ not ((string secret).Contains value) @>
    test <@ not ((sprintf "%O" secret).Contains value) @>
    test <@ not ((sprintf "%A" secret).Contains value) @>

    let config = Config.ofData (Data.Object [ "Password", Data.Text value ])

    match Config.parse (secretSchema ()) config with
    | Ok _ -> failwith "Expected the non-numeric secret to fail"
    | Error errors ->
        let configWithSources =
            { new IConfig with
                member _.Data = config.Data
                member _.Sources = [] }

        let described = errors |> List.map (ConfigError.describe configWithSources) |> String.concat "\n"
        test <@ not (described.Contains value) @>
        test <@ not ((sprintf "%A" errors).Contains value) @>
        test <@ not ((sprintf "%O" errors).Contains value) @>

        let log = Log.memory ()
        valueOf (Log.warning described |> Flow.withLogSink log |> Flow.run ())
        let messages = log.Entries |> Seq.map _.Message |> String.concat "\n"
        test <@ not (messages.Contains value) @>
        test <@ messages.Contains "<redacted>" || messages.Contains "secret" || messages.Contains "int" @>

[<Fact>]
let ``the live environment source reads process variables under a prefix`` () =
    let name = "AXIAL_T32B_TEST__VALUE"
    Environment.SetEnvironmentVariable(name, "from-process")

    try
        let source = ConfigSource.environment (Some "AXIAL_T32B_TEST")
        let config = success (Config.loadData [ source ])
        test <@ Config.tryFind "Value" config = Ok(Some "from-process") @>
    finally
        Environment.SetEnvironmentVariable(name, null)

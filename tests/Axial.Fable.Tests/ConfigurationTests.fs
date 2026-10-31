module Axial.Fable.Tests.ConfigurationTests

open Axial
open Axial.Configuration
open Axial.Fable.Tests.Harness
open Reified

let private loaded sources =
    Config.loadData sources |> exitOf

let private textAt path config =
    match Config.tryFind path config with
    | Ok value -> value
    | Error error -> failwith (ConfigError.describe config error)

let tests =
    [ test "configuration sources merge with later-wins precedence on Fable" (flow {
          let! exit =
              loaded
                  [ ConfigSource.environmentOf None [ "Port", "1"; "Name", "env" ]
                    ConfigSource.commandLine [ "--Port=2" ] ]

          match exit with
          | Exit.Success config ->
              return
                  [ equal "argument wins over environment" (Some "2") (textAt "Port" config)
                    equal "environment value survives" (Some "env") (textAt "Name" config) ]
          | Exit.Failure cause -> return [ isTrue "configuration loaded" false ]
      })

      test "configuration keys match case-insensitively on Fable" (flow {
          let! exit =
              loaded
                  [ ConfigSource.environmentOf None [ "PORT", "1" ]
                    ConfigSource.json "inline" """{"port":"3"}""" ]

          match exit with
          | Exit.Success config -> return [ equal "later case variant wins" (Some "3") (textAt "port" config) ]
          | Exit.Failure cause -> return [ isTrue "configuration loaded" false ]
      })

      test "a browser query string nests dotted keys on Fable" (flow {
          let! exit = loaded [ ConfigSource.queryString "?Db.Port=7&Other=ignored" ]

          match exit with
          | Exit.Success config -> return [ equal "query nested port" (Some "7") (textAt "db.port" config) ]
          | Exit.Failure cause -> return [ isTrue "configuration loaded" false ]
      })

      test "a plain string read uses the fake configuration on Fable" (flow {
          let config = Config.ofData (Data.Object [ "Port", Data.Number "5432" ])
          let environment =
              { new IHasConfig with
                  member _.Config = config }

          let! exit = Config.required "port" |> Flow.localEnv (fun (_: unit) -> environment) |> exitOf

          match exit with
          | Exit.Success text -> return [ equal "required text" "5432" text ]
          | Exit.Failure cause -> return [ isTrue "plain read succeeded" false ]
      }) ]

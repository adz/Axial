module Axial.Configuration.Tests.Fixtures

open Axial
open Axial.Configuration
open Axial.FileSystem
open Reified
open Reified.Refinements
open Reified.SchemaDSL

type PortSettings = { Port: int; Name: string option }

type ThreeSettings = { A: int; B: int; C: int }

type SecretSettings = { Password: Secret<int> }

type DbSettings = { Host: string; Port: int }

type NestedSettings = { Db: DbSettings; Timeout: int; Proxy: string option }

type TestEnvironment =
    { Config: IConfig
      FileSystem: IFileSystem }
    interface IHasConfig with
        member this.Config = this.Config
    interface IHasFileSystem with
        member this.FileSystem = this.FileSystem

let portSchema () =
    schema<PortSettings> {
        field (fun (settings: PortSettings) -> settings.Port)
        field (fun (settings: PortSettings) -> settings.Name)
        construct (fun port name -> { Port = port; Name = name })
    }

let threeSchema () =
    schema<ThreeSettings> {
        field _.A
        field _.B
        field _.C
        construct (fun a b c -> { A = a; B = b; C = c })
    }

let secretSchema () =
    schema<SecretSettings> {
        field (fun (settings: SecretSettings) -> settings.Password) { withSchema (Schema.secretWith Schema.int) }
        construct (fun password -> { Password = password })
    }

let dbSchema () =
    schema<DbSettings> {
        field (fun (settings: DbSettings) -> settings.Host)
        field (fun (settings: DbSettings) -> settings.Port)
        construct (fun host port -> { Host = host; Port = port })
    }

let nestedSchema () =
    schema<NestedSettings> {
        field (fun (settings: NestedSettings) -> settings.Db) { withSchema (dbSchema ()) }
        field (fun (settings: NestedSettings) -> settings.Timeout)
        field (fun (settings: NestedSettings) -> settings.Proxy)
        construct (fun db timeout proxy ->
            { Db = db
              Timeout = timeout
              Proxy = proxy })
    }

let run (flow: Flow<unit, 'error, 'value>) : Exit<'value, 'error> = Flow.run () flow

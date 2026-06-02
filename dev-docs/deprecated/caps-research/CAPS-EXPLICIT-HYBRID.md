# Explicit Interface/Record Hybrid

Two variants are shown here:

1. interfaces and classes
2. interfaces and records

This is a useful explicit baseline, but it is not a new ergonomic strict model. It sits in the same family as the boilerplate and IServiceProvider approaches because it still requires named wiring.

/// Variant 1: interfaces and classes

open System

// ======================
// Effect type
// ======================

type App<'env, 'a> =
    App of ('env -> Async<'a>)

module App =

    let run<'env, 'a>
        (env : 'env)
        (App f : App<'env, 'a>)
        : Async<'a> =
        f env

    let result<'env, 'a>
        (x : 'a)
        : App<'env, 'a> =
        App (fun _ -> async.Return x)

    let bind<'env, 'a, 'b>
        (f : 'a -> App<'env, 'b>)
        (App x : App<'env, 'a>)
        : App<'env, 'b> =

        App (fun env ->
            async {
                let! value = x env

                let (App next) = f value

                return! next env
            })

    let access<'env, 'a>
        (f : 'env -> 'a)
        : App<'env, 'a> =

        App (fun env ->
            async.Return (f env))

    let accessAsync<'env, 'a>
        (f : 'env -> Async<'a>)
        : App<'env, 'a> =

        App f


// ======================
// Computation expression
// ======================

type AppBuilder() =

    member _.Bind<'env, 'a, 'b>
        (
            x : App<'env, 'a>,
            f : 'a -> App<'env, 'b>
        )
        : App<'env, 'b> =

        App.bind f x

    member _.Return<'env, 'a>
        (x : 'a)
        : App<'env, 'a> =

        App.result x

    member _.ReturnFrom<'env, 'a>
        (x : App<'env, 'a>)
        : App<'env, 'a> =

        x

let app : AppBuilder =
    AppBuilder()


// ======================
// Domain
// ======================

type User =
    { Id : int
      Email : string }


// ======================
// Service interfaces
// ======================

type IUserRepo =
    abstract GetUser : int -> Async<User>

type IClock =
    abstract Now : unit -> DateTimeOffset

type ILog =
    abstract Info : string -> Async<unit>

type IRandom =
    abstract NextInt : int * int -> int


// ======================
// Dependency capabilities
// ======================

type UserRepoDep =
    abstract UserRepo : IUserRepo

type ClockDep =
    abstract Clock : IClock

type LogDep =
    abstract Log : ILog

type RandomDep =
    abstract Random : IRandom


// Shared runtime deps

type RuntimeDeps =
    inherit ClockDep
    inherit LogDep
    inherit RandomDep


// Function-specific deps

type SayHiToUserDeps =
    inherit RuntimeDeps
    inherit UserRepoDep


// ======================
// Service accessors
// ======================

module UserRepo =

    let getUser
        (id : int)
        : App<#UserRepoDep, User> =

        App.accessAsync (_.UserRepo.GetUser id)


module Clock =

    let now
        : App<#ClockDep, DateTimeOffset> =

        App.access (_.Clock.Now())


module Log =

    let info
        (message : string)
        : App<#LogDep, unit> =

        App.accessAsync (_.Log.Info message)


module Random =

    let nextInt
        (minValue : int)
        (maxValue : int)
        : App<#RandomDep, int> =

        App.access (_.Random.NextInt(minValue, maxValue))


// ======================
// Business logic
// ======================

let sayHiToUser
    (id : int)
    : App<#SayHiToUserDeps, unit> =

    app {

        let! now =
            Clock.now

        let! greetingNumber =
            Random.nextInt 1000 9999

        do!
            Log.info
                $"[{now}] Looking up user {id}"

        let! user =
            UserRepo.getUser id

        do!
            Log.info
                $"Hi {user.Email}! Greeting #{greetingNumber}"
    }


// ======================
// Live implementations
// ======================

type UserRepoLive() =

    interface IUserRepo with

        member _.GetUser
            (id : int)
            : Async<User> =

            async {

                return
                    { Id = id
                      Email = "adam@example.com" }
            }


type ClockLive() =

    interface IClock with

        member _.Now()
            : DateTimeOffset =

            DateTimeOffset.Now


type ConsoleLogLive() =

    interface ILog with

        member _.Info
            (message : string)
            : Async<unit> =

            async {
                printfn "%s" message
            }


type RandomLive() =

    let rng =
        Random()

    interface IRandom with

        member _.NextInt
            (
                minValue : int,
                maxValue : int
            )
            : int =

            rng.Next(minValue, maxValue)


// ======================
// Environment
// ======================

type LiveEnv =
    { UserRepoValue : IUserRepo
      ClockValue : IClock
      LogValue : ILog
      RandomValue : IRandom }

    interface UserRepoDep with
        member x.UserRepo =
            x.UserRepoValue

    interface ClockDep with
        member x.Clock =
            x.ClockValue

    interface LogDep with
        member x.Log =
            x.LogValue

    interface RandomDep with
        member x.Random =
            x.RandomValue

    interface RuntimeDeps

    interface SayHiToUserDeps


// ======================
// Runtime
// ======================

let liveEnv : LiveEnv =
    { UserRepoValue =
        UserRepoLive() :> IUserRepo

      ClockValue =
        ClockLive() :> IClock

      LogValue =
        ConsoleLogLive() :> ILog

      RandomValue =
        RandomLive() :> IRandom }


// ======================
// Execute
// ======================

sayHiToUser 123
|> App.run liveEnv
|> Async.RunSynchronously




/// Variant 2: interfaces and records
open System

// ======================
// Effect type
// ======================

type App<'env, 'a> =
    App of ('env -> Async<'a>)

module App =

    let run<'env, 'a>
        (env : 'env)
        (App f : App<'env, 'a>)
        : Async<'a> =
        f env

    let result<'env, 'a>
        (x : 'a)
        : App<'env, 'a> =
        App (fun _ -> async.Return x)

    let bind<'env, 'a, 'b>
        (f : 'a -> App<'env, 'b>)
        (App x : App<'env, 'a>)
        : App<'env, 'b> =
        App (fun env ->
            async {
                let! value = x env
                let (App next) = f value
                return! next env
            })

    let access<'env, 'a>
        (f : 'env -> 'a)
        : App<'env, 'a> =
        App (fun env -> async.Return (f env))

    let accessAsync<'env, 'a>
        (f : 'env -> Async<'a>)
        : App<'env, 'a> =
        App f


// ======================
// Computation expression
// ======================

type AppBuilder() =

    member _.Bind<'env, 'a, 'b>
        (x : App<'env, 'a>, f : 'a -> App<'env, 'b>)
        : App<'env, 'b> =
        App.bind f x

    member _.Return<'env, 'a>
        (x : 'a)
        : App<'env, 'a> =
        App.result x

    member _.ReturnFrom<'env, 'a>
        (x : App<'env, 'a>)
        : App<'env, 'a> =
        x

let app : AppBuilder =
    AppBuilder()


// ======================
// Domain
// ======================

type User =
    { Id : int
      Email : string }


// ======================
// Services as records of functions
// ======================

type UserRepo =
    { GetUser : int -> Async<User> }

type Clock =
    { Now : unit -> DateTimeOffset }

type Log =
    { Info : string -> Async<unit> }

type RandomGen =
    { NextInt : int * int -> int }


// ======================
// Dependency capabilities
// ======================

type UserRepoDep =
    abstract UserRepo : UserRepo

type ClockDep =
    abstract Clock : Clock

type LogDep =
    abstract Log : Log

type RandomDep =
    abstract Random : RandomGen


// Common runtime deps

type RuntimeDeps =
    inherit ClockDep
    inherit LogDep
    inherit RandomDep


// Function-specific deps

type SayHiToUserDeps =
    inherit RuntimeDeps
    inherit UserRepoDep


// ======================
// Service accessors
// ======================

module UserRepo =

    let getUser
        (id : int)
        : App<#UserRepoDep, User> =
        App.accessAsync (_.UserRepo.GetUser id)


module Clock =

    let now
        : App<#ClockDep, DateTimeOffset> =
        App.access (_.Clock.Now())


module Log =

    let info
        (message : string)
        : App<#LogDep, unit> =
        App.accessAsync (_.Log.Info message)


module Random =

    let nextInt
        (minValue : int)
        (maxValue : int)
        : App<#RandomDep, int> =
        App.access (_.Random.NextInt(minValue, maxValue))


// ======================
// Business logic
// ======================

let sayHiToUser
    (id : int)
    : App<#SayHiToUserDeps, unit> =
    app {
        let! now =
            Clock.now

        let! greetingNumber =
            Random.nextInt 1000 9999

        do!
            Log.info
                $"[{now}] Looking up user {id}"

        let! user =
            UserRepo.getUser id

        do!
            Log.info
                $"Hi {user.Email}! Greeting #{greetingNumber}"
    }


// ======================
// Live service values
// ======================

let userRepoLive : UserRepo =
    { GetUser =
        fun id ->
            async {
                return
                    { Id = id
                      Email = "adam@example.com" }
            } }

let clockLive : Clock =
    { Now =
        fun () ->
            DateTimeOffset.Now }

let consoleLogLive : Log =
    { Info =
        fun message ->
            async {
                printfn "%s" message
            } }

let randomLive : RandomGen =
    let rng = System.Random()

    { NextInt =
        fun (minValue, maxValue) ->
            rng.Next(minValue, maxValue) }


// ======================
// Environment
// ======================

type LiveEnv =
    { UserRepoValue : UserRepo
      ClockValue : Clock
      LogValue : Log
      RandomValue : RandomGen }

    interface UserRepoDep with
        member x.UserRepo = x.UserRepoValue

    interface ClockDep with
        member x.Clock = x.ClockValue

    interface LogDep with
        member x.Log = x.LogValue

    interface RandomDep with
        member x.Random = x.RandomValue

    interface RuntimeDeps

    interface SayHiToUserDeps


// ======================
// Runtime
// ======================

let liveEnv : LiveEnv =
    { UserRepoValue = userRepoLive
      ClockValue = clockLive
      LogValue = consoleLogLive
      RandomValue = randomLive }


// ======================
// Execute
// ======================

sayHiToUser 123
|> App.run liveEnv
|> Async.RunSynchronously

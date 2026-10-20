# A base environment class for platform services

Status: sketch, not accepted. Prompted by `ISecureRandom` (0.12.0): each platform service adds an `IHas*`
interface that every application environment must implement by hand.

## Problem

An application environment is an F# record that implements one `IHas*` interface per service it carries. The
validstate template shows the cost: `AppEnv` repeats `PlatformEnv`'s three fields and three interface
implementations before adding its own `Requests`, `Listings` and `Post`. With `IHasLog`, `IHasRandom`,
`IHasSecureRandom`, `IHasGuid`, `IHasEnvironmentVariables`, `IHasClock`, `IHasFileSystem`, `IHasConsole` and
`IHasProcess` that is roughly thirty lines of boilerplate per environment, repeated in every test environment, and
a new platform service is a breaking change for every application that wants it.

Records cannot inherit, so the record-first guidance in `docs/05-dependencies/02-choosing-an-approach.md` forces the
repetition.

## Sketch

A class in `Axial.PlatformService` implements every platform `IHas*` with live defaults. An application inherits
it and adds its own services:

```fsharp
type PlatformEnv() =
    abstract Clock: IClock
    default _.Clock = Clock.live
    abstract SecureRandom: ISecureRandom
    default _.SecureRandom = SecureRandom.live
    // ... one abstract/default pair per platform service
    interface IHasClock with member this.Clock = this.Clock
    interface IHasSecureRandom with member this.SecureRandom = this.SecureRandom
    // ...

type AppEnv(db: IDatabase) =
    inherit PlatformEnv()
    member _.Db = db
    interface IHasDb with member _.Db = db

// A test overrides only what it fakes.
let testEnv db clock =
    { new AppEnv(db) with
        override _.Clock = clock }
```

A new platform service then costs applications nothing: the base class gains a member with a live default.

## Questions to settle

- Live defaults in a base class are ambient: an environment that forgets to override `Clock` in a test silently
  uses the real clock. Alternative: a `PlatformEnv(services: PlatformServices)` constructor taking one record of
  services, with `PlatformServices.live` and `PlatformServices.test` builders, so the choice is explicit once.
- Object expressions over a class with many members are verbose for tests; a `with`-style copy needs the record of
  services anyway. The constructor-record variant above handles both.
- Fable: classes with abstract/default members and interface implementations compile, but `[<Mangle>]` on the
  `IHas*` interfaces must keep working through inheritance; prove it in the Fable suite.
- Docs: the dependencies guide would recommend the class for applications and keep records for libraries,
  tests and small programs.
- Where `Layer` fits: a layer that builds `AppEnv` would construct the class instead of a record.

## Next step

Prototype the constructor-record variant in a branch of the validstate template and in one Axial example; compare
line counts and test ergonomics before deciding.

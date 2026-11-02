module Axial.Fable.Tests.CultureTests

open System
open Axial
open Axial.Fable.Tests.Harness

let private culture name =
    match Culture.ofName name with
    | Ok value -> value
    | Error message -> failwith message

let private zone id =
    match TimeZone.ofId id with
    | Ok value -> value
    | Error message -> failwith message

let tests =
    [ test "culture formatting and UTC-to-local conversion use Intl" (flow {
          let german = culture "de-DE"
          let english = culture "en-US"
          let berlin = zone "Europe/Berlin"
          let winter = DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero)
          let summer = DateTimeOffset(2025, 7, 15, 12, 0, 0, TimeSpan.Zero)

          return
              [ equal "german number" "1.234,50" (Format.number german 1234.5)
                equal "english number" "1,234.50" (Format.number english 1234.5)
                isTrue "date is non-empty" (not (String.IsNullOrWhiteSpace(Format.date german winter)))
                isTrue "time is non-empty" (not (String.IsNullOrWhiteSpace(Format.time english winter)))
                isTrue "currency is non-empty" (not (String.IsNullOrWhiteSpace(Format.currency german 12.5m)))
                equal "winter offset" (TimeSpan.FromHours 1.) (TimeZone.toLocal berlin winter).Offset
                equal "summer offset" (TimeSpan.FromHours 2.) (TimeZone.toLocal berlin summer).Offset
                equal "UTC offset" TimeSpan.Zero (TimeZone.toLocal TimeZone.utc winter).Offset ]
      }) ]

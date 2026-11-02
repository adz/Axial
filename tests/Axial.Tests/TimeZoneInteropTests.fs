namespace Axial.Tests

open System
open Axial
open Xunit

module TimeZoneInteropTests =
    let private customZone () =
        let rule =
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime(2020, 1, 1),
                DateTime(2030, 12, 31),
                TimeSpan.FromHours 1.0,
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(DateTime(1, 1, 1, 2, 0, 0), 3, 2, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(DateTime(1, 1, 1, 2, 0, 0), 11, 1, DayOfWeek.Sunday)
            )
        TimeZoneInfo.CreateCustomTimeZone(
            "Axial.Custom/NotInDatabase",
            TimeSpan.FromHours -5.0,
            "Axial custom zone",
            "Axial standard",
            "Axial daylight",
            [| rule |]
        )

    [<Fact>]
    let ``custom zone cannot be reconstructed from its id alone`` () =
        let zone = customZone ()
        match Axial.TimeZone.ofId zone.Id with
        | Ok _ -> failwith "the custom zone must not be resolvable from the OS database"
        | Error _ -> ()

    [<Fact>]
    let ``time zone info roundtrip preserves custom rules`` () =
        let info = customZone ()
        let zone = Axial.TimeZone.ofTimeZoneInfo info
        Assert.Equal(info.Id, Axial.TimeZone.id zone)
        let back = Axial.TimeZone.toTimeZoneInfo zone
        Assert.Same(info, back)
        for instant in [ DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero); DateTimeOffset(2024, 7, 1, 12, 0, 0, TimeSpan.Zero) ] do
            Assert.Equal(TimeZoneInfo.ConvertTime(instant, info), Axial.TimeZone.toLocal zone instant)

    [<Fact>]
    let ``null time zone info is an argument error`` () =
        Assert.Throws<ArgumentNullException>(fun () -> Axial.TimeZone.ofTimeZoneInfo null |> ignore) |> ignore

    [<Fact>]
    let ``built-in zones still convert to the same instant`` () =
        let zone = match Axial.TimeZone.ofId "Europe/Berlin" with Ok zone -> zone | Error error -> failwith error
        let instant = DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero)
        Assert.Equal(DateTimeOffset(2024, 1, 1, 13, 0, 0, TimeSpan.FromHours 1.0), Axial.TimeZone.toLocal zone instant)
        Assert.Equal(Axial.TimeZone.id Axial.TimeZone.utc, Axial.TimeZone.toTimeZoneInfo(Axial.TimeZone.utc).Id)

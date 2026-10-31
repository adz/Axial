namespace Axial.Tests

open System
open System.Globalization
open System.Reflection
open System.Threading
open Axial
open Xunit

module CultureAndTimeZoneTests =
    let private succeeded (exit: Exit<'value, 'error>) : 'value =
        match exit with
        | Exit.Success value -> value
        | Exit.Failure cause -> failwith (Cause.prettyPrint (fun _ -> "") cause)

    let private culture name =
        match Culture.ofName name with
        | Ok value -> value
        | Error message -> failwith message

    let private zone id =
        match TimeZone.ofId id with
        | Ok value -> value
        | Error message -> failwith message

    [<Fact>]
    let ``runTest supplies invariant culture and UTC regardless of the machine defaults`` () =
        let previousCulture = Thread.CurrentThread.CurrentCulture
        let previousUiCulture = Thread.CurrentThread.CurrentUICulture

        try
            Thread.CurrentThread.CurrentCulture <- CultureInfo.GetCultureInfo "de-DE"
            Thread.CurrentThread.CurrentUICulture <- CultureInfo.GetCultureInfo "de-DE"

            let actual =
                flow {
                    let! currentCulture = Culture.system
                    let! currentZone = TimeZone.local
                    return Culture.name currentCulture, TimeZone.id currentZone
                }
                |> Flow.runTest ()
                |> succeeded

            Assert.Equal("", fst actual)
            Assert.Equal("UTC", snd actual)
        finally
            Thread.CurrentThread.CurrentCulture <- previousCulture
            Thread.CurrentThread.CurrentUICulture <- previousUiCulture

    [<Fact>]
    let ``culture names are canonical and invalid names are typed errors`` () =
        let de = culture "de-DE"
        Assert.Equal("de-DE", Culture.name de)
        Assert.Equal("", Culture.name Culture.invariant)
        Assert.True((Culture.ofName "not_a_culture").IsError)

    [<Fact>]
    let ``time zone ids validate and UTC is available without the machine zone`` () =
        Assert.Equal("UTC", TimeZone.id TimeZone.utc)
        Assert.True((TimeZone.ofId "Not/AZone").IsError)

        let berlin = zone "Europe/Berlin"
        Assert.False(String.IsNullOrWhiteSpace(TimeZone.id berlin))

    [<Fact>]
    let ``formatting uses the supplied culture rather than the current thread culture`` () =
        let de = culture "de-DE"
        let en = culture "en-US"
        let instant = DateTimeOffset(2025, 6, 1, 13, 30, 0, TimeSpan.FromHours 2.)

        Assert.Equal("1.234,50", Format.number de 1234.5)
        Assert.Equal("1,234.50", Format.number en 1234.5)
        Assert.Equal("1,234.50", Format.number Culture.invariant 1234.5)
        Assert.Equal("01.06.2025", Format.date de instant)
        Assert.Equal("6/1/2025", Format.date en instant)
        Assert.Equal("06/01/2025", Format.date Culture.invariant instant)
        Assert.Equal("13:30", Format.time de instant)
        Assert.Equal("1:30 PM", Format.time en instant)
        Assert.Equal("13:30", Format.time Culture.invariant instant)
        Assert.Contains("€", Format.currency de 1234.5m)
        Assert.Contains("$", Format.currency en 1234.5m)

    [<Fact>]
    let ``setProcessDefault sets all four .NET culture properties`` () =
        let previousCulture = CultureInfo.CurrentCulture
        let previousUiCulture = CultureInfo.CurrentUICulture
        let previousDefaultCulture = CultureInfo.DefaultThreadCurrentCulture
        let previousDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture

        try
            Culture.setProcessDefault (culture "de-DE")
            |> Flow.runTest ()
            |> succeeded
            |> ignore

            Assert.Equal("de-DE", CultureInfo.CurrentCulture.Name)
            Assert.Equal("de-DE", CultureInfo.CurrentUICulture.Name)
            Assert.Equal("de-DE", CultureInfo.DefaultThreadCurrentCulture.Name)
            Assert.Equal("de-DE", CultureInfo.DefaultThreadCurrentUICulture.Name)
        finally
            CultureInfo.CurrentCulture <- previousCulture
            CultureInfo.CurrentUICulture <- previousUiCulture
            CultureInfo.DefaultThreadCurrentCulture <- previousDefaultCulture
            CultureInfo.DefaultThreadCurrentUICulture <- previousDefaultUiCulture

    [<Fact>]
    let ``toLocal converts a UTC instant into the zone wall clock`` () =
        let berlin = zone "Europe/Berlin"
        let newYork = zone "America/New_York"
        let winter = DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero)
        let summer = DateTimeOffset(2025, 7, 15, 12, 0, 0, TimeSpan.Zero)

        Assert.Equal(TimeSpan.FromHours 1., (TimeZone.toLocal berlin winter).Offset)
        Assert.Equal(TimeSpan.FromHours 2., (TimeZone.toLocal berlin summer).Offset)
        Assert.Equal(TimeSpan.FromHours -5., (TimeZone.toLocal newYork winter).Offset)
        Assert.Equal(TimeSpan.FromHours -4., (TimeZone.toLocal newYork summer).Offset)
        Assert.Equal(winter, TimeZone.toLocal TimeZone.utc winter)

    [<Fact>]
    let ``there is no ambient culture or time zone override API`` () =
        let forbidden = set [ "withCulture"; "withTimeZone"; "currentCulture"; "currentTimeZone" ]

        let publicMethods =
            typeof<Culture>.Assembly.GetExportedTypes()
            |> Array.collect (fun exported ->
                exported.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly))
            |> Array.map _.Name
            |> Set.ofArray

        Assert.Empty(Set.intersect forbidden publicMethods)

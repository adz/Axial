namespace Axial.FileSystem.Tests

open System
open Xunit

module NamespaceSurfaceTests =
    let private types () =
        typeof<Axial.FileSystem.IFileSystem>.Assembly.GetExportedTypes()
        |> Array.map (fun t -> t.FullName)
        |> Set.ofArray

    [<Fact>]
    let ``files and append log are exported from Axial.FileSystem`` () =
        Assert.Contains("Axial.FileSystem.Files", types ())
        Assert.Contains("Axial.FileSystem.AppendLog", types ())

    [<Fact>]
    let ``old Axial.Files namespace is not exported`` () =
        let oldPrefix = "Axial." + "Files"
        let old = types () |> Set.filter (fun name -> name.StartsWith(oldPrefix, StringComparison.Ordinal))
        Assert.True(old.IsEmpty, String.concat ", " old)

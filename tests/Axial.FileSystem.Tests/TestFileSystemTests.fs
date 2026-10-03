namespace Axial.Tests

open System.IO
open Axial
open Axial.FileSystem
open Swensen.Unquote
open Xunit

module TestFileSystemTests =
    [<Fact>]
    let ``in-memory file system reads writes and deletes`` () =
        let fs = FileSystem.test () :> IFileSystem

        fs.WriteAllText("/tmp/a.txt", "hello")

        test <@ fs.FileExists "/tmp/a.txt" @>
        test <@ fs.ReadAllText "/tmp/a.txt" = "hello" @>

        fs.DeleteFile "/tmp/a.txt"
        test <@ not (fs.FileExists "/tmp/a.txt") @>

    [<Fact>]
    let ``in-memory file system appends enumerates and manages directories`` () =
        let fs =
            TestFileSystem([ "/tmp/b.txt", "one"; "/tmp/c.txt", "two" ]) :> IFileSystem

        fs.AppendAllText("/tmp/b.txt", "plus")
        test <@ fs.ReadAllText "/tmp/b.txt" = "oneplus" @>

        let names =
            fs.GetFiles("/tmp", "*", SearchOption.TopDirectoryOnly)
            |> Array.map Path.GetFileName
            |> Array.sort

        test <@ names = [| "b.txt"; "c.txt" |] @>

        fs.CreateDirectory "/tmp/sub"
        test <@ fs.DirectoryExists "/tmp/sub" @>

        fs.WriteAllText("/tmp/sub/f.txt", "x")
        fs.DeleteDirectory("/tmp/sub", true)

        test <@ not (fs.DirectoryExists "/tmp/sub") @>
        test <@ not (fs.FileExists "/tmp/sub/f.txt") @>

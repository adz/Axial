namespace Axial.FileSystem.Tests

open System
open System.IO
open Xunit
open Axial
open Axial.FileSystem

type FileLogTests() =
    let withTemp test =
        let root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory root |> ignore
        try test (Path.Combine(root, "app.log"))
        finally Directory.Delete(root, true)

    let entry level text =
        { Level = level
          Message = text
          Error = None
          Annotations = Map.empty
          Locals = [] }

    [<Fact>]
    member _.``rolling moves the current file to a numbered archive``() =
        withTemp (fun path ->
            let sink = FileLog.sink FileSystem.live path { MaxFileSizeBytes = 100L; MaxArchives = 2 }

            sink.Write(entry LogLevel.Information (String('a', 80)))
            sink.Write(entry LogLevel.Information (String('b', 80)))

            Assert.True(File.Exists path)
            Assert.True(File.Exists(path + ".1")))

    [<Fact>]
    member _.``rolling shifts older archives up and deletes the oldest``() =
        withTemp (fun path ->
            let sink = FileLog.sink FileSystem.live path { MaxFileSizeBytes = 100L; MaxArchives = 2 }

            for letter in [ 'a'; 'b'; 'c'; 'd' ] do
                sink.Write(entry LogLevel.Information (String(letter, 80)))

            Assert.Contains(String('d', 80), File.ReadAllText path)
            Assert.Contains(String('c', 80), File.ReadAllText(path + ".1"))
            Assert.Contains(String('b', 80), File.ReadAllText(path + ".2"))
            Assert.False(File.Exists(path + ".3")))

    [<Fact>]
    member _.``with no archives the current file starts again instead of growing past the limit``() =
        withTemp (fun path ->
            let sink = FileLog.sink FileSystem.live path { MaxFileSizeBytes = 100L; MaxArchives = 0 }

            for letter in [ 'a'; 'b'; 'c' ] do
                sink.Write(entry LogLevel.Information (String(letter, 80)))

            Assert.True(FileInfo(path).Length <= 100L)
            Assert.Contains(String('c', 80), File.ReadAllText path)
            Assert.False(File.Exists(path + ".1")))

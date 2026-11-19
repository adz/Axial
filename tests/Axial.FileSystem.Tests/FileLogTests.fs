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

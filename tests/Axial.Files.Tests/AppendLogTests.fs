namespace Axial.Files.Tests

open System
open System.IO
open System.Text
open Xunit
open Axial
open Axial.FileSystem
open Axial.Files

type AppendLogTests() =
    let withTemp test =
        let root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
        Directory.CreateDirectory root |> ignore
        try test (Path.Combine(root, "log"))
        finally Directory.Delete(root, true)

    let env = { new IHasFileSystem with member _.FileSystem = FileSystem.live }
    let bytes (text: string) = Encoding.ASCII.GetBytes text
    let run operation =
        match Flow.run env operation with
        | Exit.Success value -> value
        | other -> failwithf "Unexpected exit: %A" other
    let write path cap records =
        flow {
            let! log = AppendLog.openLog path cap |> Flow.scopeResource
            do! records |> List.fold (fun acc record -> acc |> Flow.bind (fun () -> AppendLog.append record log)) (Flow.succeed ())
        } |> run
    let replay path cap =
        flow {
            let! log = AppendLog.openLog path cap |> Flow.scopeResource
            return! AppendLog.readAll log |> FlowStream.runCollect
        } |> run

    [<Fact>]
    member _.``reopen replays three records in order``() =
        withTemp (fun path ->
            let records = [ bytes "first"; bytes "second"; bytes "third" ]
            write path 1024L records
            Assert.Equal<byte array list>(records, replay path 1024L))

    [<Fact>]
    member _.``torn tail is removed on reopen``() =
        withTemp (fun path ->
            let records = [ bytes "first"; bytes "second" ]
            write path 1024L records
            let valid = FileInfo(path).Length
            use stream = new FileStream(path, FileMode.Append, FileAccess.Write)
            stream.Write(BitConverter.GetBytes(12), 0, 4)
            stream.Write(Array.zeroCreate<byte> 4, 0, 4)
            stream.Write(bytes "half!!", 0, 6)
            stream.Dispose()
            Assert.Equal<byte array list>(records, replay path 1024L)
            Assert.Equal(valid, FileInfo(path).Length))

    [<Fact>]
    member _.``bad checksum truncates from second record``() =
        withTemp (fun path ->
            let first = bytes "first"
            write path 1024L [ first; bytes "second" ]
            let valid = int64 (8 + first.Length)
            use stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite)
            stream.Position <- valid + 8L
            let original = stream.ReadByte()
            stream.Position <- valid + 8L
            stream.WriteByte(byte (original ^^^ 1))
            stream.Dispose()
            Assert.Equal<byte array list>([ first ], replay path 1024L)
            Assert.Equal(valid, FileInfo(path).Length))

    [<Fact>]
    member _.``rotation replays older file before current within cap``() =
        withTemp (fun path ->
            let records = [ bytes "one"; bytes "two"; bytes "three" ]
            write path 24L records
            Assert.True(File.Exists(path + ".1"))
            Assert.InRange(FileInfo(path + ".1").Length, 1L, 24L)
            Assert.InRange(FileInfo(path).Length, 1L, 24L)
            Assert.Equal<byte array list>(records, replay path 24L))

    [<Fact>]
    member _.``missing and empty logs replay no records``() =
        withTemp (fun path ->
            Assert.Empty(replay path 1024L)
            Assert.Empty(replay path 1024L))

    [<Fact>]
    member _.``CRC32 detects a payload change``() =
        withTemp (fun path ->
            write path 1024L [ bytes "123456789" ]
            let data = File.ReadAllBytes path
            Assert.Equal(0xCBF43926u, BitConverter.ToUInt32(data, 4))
            data[8] <- data[8] ^^^ 1uy
            File.WriteAllBytes(path, data)
            Assert.Empty(replay path 1024L))

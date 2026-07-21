namespace Axial.Schema.Json

open Axial

open System.Buffers
open System.Globalization
open System.Text
#if !FABLE_COMPILER
open System.Buffers.Text
#endif

/// <summary>Low-level byte reading and writing primitives for the compiled JSON codec runtime.</summary>
/// <remarks>
/// This module mirrors CodecMapper's byte runtime: a portable <c>byte[] + offset</c> reader state, a growable pooled
/// writer, and invariant-culture numeric fast paths that parse directly from UTF-8 bytes on .NET.
/// </remarks>
[<AutoOpen>]
module internal Buffers =
    /// A lightweight cursor for reading bytes.
    [<Struct>]
    type ByteSource =
        val Data: byte[]
        val Offset: int

        new(data, offset) = { Data = data; Offset = offset }

        /// Advances the source by `n` bytes.
        member inline x.Advance(n: int) = ByteSource(x.Data, x.Offset + n)

#if !FABLE_COMPILER
        /// Exposes the unread bytes so BCL helpers can scan without changing the portable state model.
        member inline x.RemainingSpan = System.ReadOnlySpan<byte>(x.Data, x.Offset, x.Data.Length - x.Offset)

        /// Builds a bounded span after a parser has already found the token limits.
        member inline x.SliceSpan(offset: int, length: int) = System.ReadOnlySpan<byte>(x.Data, offset, length)
#endif

    /// Abstraction for writing bytes.
    type IByteWriter =
        /// Ensures that at least `n` more bytes can be written without reallocating.
        abstract member Ensure: int -> unit
        /// Writes a single byte.
        abstract member WriteByte: byte -> unit
        /// Writes a UTF-8 string payload.
        abstract member WriteString: string -> unit
        /// Writes a slice of a string without allocating a substring.
        abstract member WriteStringSlice: string * int * int -> unit
        /// Writes raw bytes.
        abstract member WriteBytes: byte[] -> unit
        /// Writes an integer value.
        abstract member WriteInt: int -> unit
        /// Writes a `decimal` value.
        abstract member WriteDecimal: decimal -> unit
        /// Exposes the current backing storage.
        abstract member Data: byte[]
        /// Exposes the number of written bytes.
        abstract member Count: int

    /// Growable pooled in-memory byte buffer used by the compiled codecs.
    type ResizableBuffer =
        { mutable InternalData: byte[]
          mutable InternalCount: int
          mutable ReturnToPool: bool }

        static member private Rent(capacity: int) =
#if !FABLE_COMPILER
            ArrayPool<byte>.Shared.Rent(capacity)
#else
            Array.zeroCreate capacity
#endif

        /// Creates a new buffer with the requested initial capacity.
        static member Create(initialCapacity: int) =
            { InternalData = ResizableBuffer.Rent(initialCapacity)
              InternalCount = 0
              ReturnToPool = true }

        static member private Return(buffer: byte[]) =
#if !FABLE_COMPILER
            ArrayPool<byte>.Shared.Return(buffer, false)
#else
            ignore buffer
#endif

        /// Returns pooled storage after the final payload has been materialized.
        member x.Release() =
            if x.ReturnToPool then
                ResizableBuffer.Return(x.InternalData)
                x.InternalData <- [||]
                x.InternalCount <- 0
                x.ReturnToPool <- false

        interface IByteWriter with
            member x.Ensure(n: int) =
                let minCapacity = x.InternalCount + n

                if x.InternalData.Length < minCapacity then
                    let newCapacity = max (x.InternalData.Length * 2) minCapacity
                    let newData = ResizableBuffer.Rent(newCapacity)
                    System.Array.Copy(x.InternalData, 0, newData, 0, x.InternalCount)

#if !FABLE_COMPILER
                    if x.ReturnToPool then
                        ResizableBuffer.Return(x.InternalData)
#endif

                    x.InternalData <- newData

            member x.WriteByte(b: byte) =
                (x :> IByteWriter).Ensure(1)
                x.InternalData[x.InternalCount] <- b
                x.InternalCount <- x.InternalCount + 1

            member x.WriteString(s: string) =
#if !FABLE_COMPILER
                let maxBytes = Encoding.UTF8.GetMaxByteCount(s.Length)
                (x :> IByteWriter).Ensure(maxBytes)
                let written = Encoding.UTF8.GetBytes(s, 0, s.Length, x.InternalData, x.InternalCount)
                x.InternalCount <- x.InternalCount + written
#else
                let bytes = Encoding.UTF8.GetBytes(s)
                (x :> IByteWriter).Ensure(bytes.Length)
                System.Array.Copy(bytes, 0, x.InternalData, x.InternalCount, bytes.Length)
                x.InternalCount <- x.InternalCount + bytes.Length
#endif

            member x.WriteStringSlice(s: string, startIndex: int, length: int) =
#if !FABLE_COMPILER
                let maxBytes = Encoding.UTF8.GetMaxByteCount(length)
                (x :> IByteWriter).Ensure(maxBytes)
                let written = Encoding.UTF8.GetBytes(s, startIndex, length, x.InternalData, x.InternalCount)
                x.InternalCount <- x.InternalCount + written
#else
                let slice = s.Substring(startIndex, length)
                let bytes = Encoding.UTF8.GetBytes(slice)
                (x :> IByteWriter).Ensure(bytes.Length)
                System.Array.Copy(bytes, 0, x.InternalData, x.InternalCount, bytes.Length)
                x.InternalCount <- x.InternalCount + bytes.Length
#endif

            member x.WriteBytes(bytes: byte[]) =
                (x :> IByteWriter).Ensure(bytes.Length)
                System.Array.Copy(bytes, 0, x.InternalData, x.InternalCount, bytes.Length)
                x.InternalCount <- x.InternalCount + bytes.Length

            member x.WriteInt(value: int) =
                if value = 0 then
                    (x :> IByteWriter).WriteByte(byte '0')
                elif value = System.Int32.MinValue then
                    (x :> IByteWriter).WriteString("-2147483648")
                else
                    let mutable v = value

                    if v < 0 then
                        (x :> IByteWriter).WriteByte(byte '-')
                        v <- -v

                    let digits = Array.zeroCreate 10
                    let mutable pos = 0

                    while v > 0 do
                        digits[pos] <- byte (48 + (v % 10))
                        v <- v / 10
                        pos <- pos + 1

                    (x :> IByteWriter).Ensure(pos)

                    for i in 0 .. pos - 1 do
                        x.InternalData[x.InternalCount + i] <- digits[pos - 1 - i]

                    x.InternalCount <- x.InternalCount + pos

            member x.WriteDecimal(value: decimal) =
#if !FABLE_COMPILER
                let mutable written = 0
                (x :> IByteWriter).Ensure(40)

                let destination =
                    System.Span<byte>(x.InternalData, x.InternalCount, x.InternalData.Length - x.InternalCount)

                if Utf8Formatter.TryFormat(value, destination, &written) then
                    x.InternalCount <- x.InternalCount + written
                else
                    (x :> IByteWriter).WriteString(value.ToString(CultureInfo.InvariantCulture))
#else
                (x :> IByteWriter).WriteString(value.ToString(CultureInfo.InvariantCulture))
#endif

            member x.Data = x.InternalData
            member x.Count = x.InternalCount

    /// Parses a 32-bit integer directly from UTF-8 bytes with invariant semantics.
    let parseInt32Bytes (data: byte[]) (offset: int) (length: int) : int =
        let inline tokenText () = Encoding.UTF8.GetString(data, offset, length)

        if length = 0 then
            failwithf "Invalid int value: %s" (tokenText ())

        let mutable index = offset
        let endExclusive = offset + length
        let mutable negative = false

        if data[index] = byte '-' then
            negative <- true
            index <- index + 1

        if index >= endExclusive then
            failwithf "Invalid int value: %s" (tokenText ())

        let maxMagnitude = if negative then 2147483648UL else 2147483647UL
        let mutable magnitude = 0UL

        while index < endExclusive do
            let digit = int data[index] - int (byte '0')

            if digit < 0 || digit > 9 then
                failwithf "Invalid int value: %s" (tokenText ())

            let digitMagnitude = uint64 digit

            if magnitude > (maxMagnitude - digitMagnitude) / 10UL then
                failwithf "int value out of range: %s" (tokenText ())

            magnitude <- magnitude * 10UL + digitMagnitude
            index <- index + 1

        if negative then
            if magnitude = 2147483648UL then System.Int32.MinValue else -(int magnitude)
        else
            int magnitude

    /// Parses a decimal directly from UTF-8 bytes with invariant semantics.
    let parseDecimalBytes (data: byte[]) (offset: int) (length: int) : decimal =
#if !FABLE_COMPILER
        let token = System.ReadOnlySpan<byte>(data, offset, length)
        let mutable value = 0M
        let mutable consumed = 0

        if Utf8Parser.TryParse(token, &value, &consumed, 'G') && consumed = length then
            value
        else
            failwithf "Invalid decimal value: %s" (Encoding.UTF8.GetString(data, offset, length))
#else
        let token = Encoding.UTF8.GetString(data.[offset .. offset + length - 1])

        match System.Decimal.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture) with
        | true, value -> value
        | false, _ -> failwithf "Invalid decimal value: %s" token
#endif

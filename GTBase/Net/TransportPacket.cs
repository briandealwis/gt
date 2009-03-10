using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using GT.Utils;

namespace GT.Net
{
    /// <summary>
    /// Represents a byte-array with appropriately marshalled content ready to send
    /// across a particular transport.
    /// The purpse of this class is to provide efficient use of byte arrays.
    /// Unless noted in the comments, any byte arrays or portions added to
    /// a transport packet are assumed to belong to this packet instance
    /// <em>and should not be used elsewhere</em>!  These byte arrays are
    /// added a direct reference, and any changes made to byte array
    /// will be reflected in this packet's contents; any change made to this
    /// packet will also be reflected in the byte array's contents.
    /// Transport packets, once finished with, must be explicitly disposed of 
    /// using <see cref="Dispose"/> to deal with cleaning up any possibly 
    /// shared memory.
    /// </summary>
    public class TransportPacket : IList<ArraySegment<byte>>, IDisposable
    {
        /// <summary>
        /// Create a transport packet instance using a *copy* of
        /// <see cref="data"/>
        /// </summary>
        /// <param name="data">the data to be copied into the transport packet</param>
        /// <returns>a transport packet on a copy of <see cref="data"/></returns>
        public static TransportPacket CopyOf(byte[] data)
        {
            return CopyOf(data, 0, data.Length);
        }

        /// <summary>
        /// Create a transport packet instance using a *copy* of a portion
        /// of <see cref="data"/>
        /// </summary>
        /// <param name="data">the data to be copied into the transport packet</param>
        /// <param name="offset">the offset into <see cref="data"/></param>
        /// <param name="count">the number of bytes in <see cref="data"/></param>
        /// <returns>a transport packet on a copy of <see cref="data"/></returns>
        public static TransportPacket CopyOf(byte[] data, int offset, int count)
        {
            byte[] copy = new byte[count];
            Array.Copy(data, offset, copy, 0, count);
            return new TransportPacket(copy);
        }

        /// <summary>
        /// Create a new marshalled packet as a subset of another packet <see cref="source"/>
        /// Note: this method uses a *copy* of the appropriate portion of <see cref="source"/>.
        /// </summary>
        /// <param name="source">the provided marshalled packet</param>
        /// <param name="offset">the start position of the subset to include</param>
        /// <param name="count">the number of bytes of the subset to include</param>
        public static TransportPacket CopyOf(TransportPacket source, int offset, int count)
        {
            // FIXME: should allocate this from a pool
            byte[] newContents = new byte[count];
            source.CopyTo(offset, count, newContents, 0);
            return new TransportPacket(newContents, 0, count);
        }

        /// <summary>
        /// An ordered set of byte arrays; the packet is
        /// formed up of these segments laid one after the other.
        /// </summary>
        protected List<ArraySegment<byte>> list;

        /// <summary>
        /// The total number of bytes in this packet.  This should be 
        /// equal to the sum of the <see cref="ArraySegment{T}.Count"/> for
        /// each segment in <see cref="list"/>.
        /// </summary>
        protected int length = 0;

        /// <summary>
        /// Create a new 0-byte transport packet.
        /// </summary>
        public TransportPacket()
        {
            list = new List<ArraySegment<byte>>();
        }

        /// <summary>
        /// Create an instance with the capacity for <see cref="expectedSize"/> bytes.
        /// </summary>
        /// <param name="expectedSize">the expected number of bytes</param>
        public TransportPacket(uint expectedSize)
        {
            list = new List<ArraySegment<byte>>(1);
            if (expectedSize > 0) { Grow((int)expectedSize); }
        }

        /// <summary>
        /// Create a new transport packet from the provided byte array segment.
        /// The segment contents are now assumed to belong to this packet instance
        /// and should not be used elsewhere!
        /// </summary>
        /// <seealso cref="CopyOf(byte[],int,int)"/>
        /// <param name="segment"></param>
        public TransportPacket(ArraySegment<byte> segment)
        {
            list = new List<ArraySegment<byte>>(1);
            Add(segment);
        }

        /// <summary>
        /// Create a new transport packet from the provided byte array.
        /// The byte array is now assumed to belong to this packet instance
        /// and should not be used elsewhere!
        /// </summary>
        /// <seealso cref="CopyOf(byte[],int,int)"/>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public TransportPacket(byte[] bytes, int offset, int count)
            : this(new ArraySegment<byte>(bytes, offset, count)) { }

        /// <summary>
        /// Create a new transport packet from the provided stream contents.
        /// The stream content is now assumed to belong to this packet instance
        /// and should not be used elsewhere!
        /// </summary>
        /// <seealso cref="CopyOf(byte[])"/>
        /// <param name="ms"></param>
        public TransportPacket(MemoryStream ms)
            : this(ms.GetBuffer(), 0, (int)ms.Length) { }

        /// <summary>
        /// Create a new transport packet from the provided byte arrays.
        /// The byte arrays are now assumed to belong to this packet instance
        /// and should not be used elsewhere!
        /// </summary>
        /// <seealso cref="CopyOf(byte[],int,int)"/>
        /// <param name="byteArrays"></param>
        public TransportPacket(params byte[][] byteArrays)
        {
            list = new List<ArraySegment<byte>>(byteArrays.Length);
            foreach (byte[] bytes in byteArrays)
            {
                Add(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Return the number of bytes in this packet.
        /// </summary>
        public int Length { get { return length; } }

        /// <summary>
        /// Return a subset of this marshalled packet; this subset is 
        /// backed by this instance, such that any changes to the subset 
        /// are reflected in this instance too.
        /// </summary>
        /// <seealso cref="CopyOf(TransportPacket,int,int)"/>
        /// <param name="subsetStart">the start position of the subset</param>
        /// <param name="count">the number of bytes in the subset</param>
        /// <returns></returns>
        public TransportPacket Subset(int subsetStart, int count)
        {
            if (subsetStart == 0 && count == length)
            {
                return this;
            }

            TransportPacket subset = new TransportPacket();
            int subsetEnd = subsetStart + count - 1;    // index of last byte of interest
            int segmentStart = 0; // index of first byte of current <segment>
            foreach(ArraySegment<byte> segment in list)
            {
                int segmentEnd = segmentStart + segment.Count - 1; // index of last byte
                
                // This segment is of interest if 
                // listStart <= segmentEnd && listEnd >= segmentStart
                // IF: segmentEnd < subsetStart then we're too early
                // IF: subsetEnd < segmentStart then we've gone past
                if (segmentEnd >= subsetStart)
                {

                    // if this segment appears after the area of interest then we're finished:
                    // none of the remaining segments can possibly be in our AOI
                    if(segmentStart > subsetEnd)
                    {
                        break;
                    }
                    if(subsetStart <= segmentStart && segmentEnd <= subsetEnd)
                    {
                        subset.Add(segment);
                    }
                    else
                    {
                        int aoiStart = Math.Max(subsetStart, segmentStart);
                        int aoiEnd = Math.Min(subsetEnd, segmentEnd);
                        subset.Add(new ArraySegment<byte>(segment.Array,
                            segment.Offset + (int)(aoiStart - segmentStart),
                            (int)(aoiEnd - aoiStart + 1)));
                    }
                }
                segmentStart += segment.Count;
            }
            return subset;
        }

        /// <summary>
        /// Make a copy of the contents of this packet.
        /// </summary>
        /// <returns>a copy of the contents of this packet</returns>
        public TransportPacket Copy()
        {
            return CopyOf(this, 0, length);
        }

        /// <summary>
        /// Prepend the byte segment to this item.
        /// The byte segment is now assumed to belong to this packet instance
        /// and should not be used elsewhere!
        /// </summary>
        /// <param name="item"></param>
        public void Prepend(ArraySegment<byte> item)
        {
            // FIXME: should we add this?  Or allocate and copy?
            list.Insert(0, item);
            length += item.Count;
        }

        /// <summary>
        /// Prepend the byte segment to this item.
        /// The byte segment is now assumed to belong to this packet instance
        /// and should not be used elsewhere!
        /// </summary>
        /// <param name="source"></param>
        public void Prepend(byte[] source)
        {
            Prepend(new ArraySegment<byte>(source, 0, source.Length));
        }

        /// <summary>
        /// Append the byte segment to this item.  
        /// The byte segment is now assumed to belong to this packet instance
        /// and should not be used elsewhere!
        /// </summary>
        /// <param name="item"></param>
        public void Add(ArraySegment<byte> item)
        {
            // FIXME: should we add this?  Or allocate and copy?
            list.Add(item);
            length += item.Count;
        }

        /// <summary>
        /// Append the byte array to this item.  
        /// The byte array is now assumed to belong to this packet instance
        /// and should not be used elsewhere!
        /// </summary>
        /// <param name="source">source array</param>
        public void Add(byte[] source)
        {
            // FIXME: should we add this?  Or allocate and copy?
            Add(new ArraySegment<byte>(source, 0, source.Length));
        }


        /// <summary>
        /// Append the byte segment to this item.
        /// The byte array is now assumed to belong to this packet instance
        /// and should not be used elsewhere!
        /// </summary>
        /// <param name="source">source array</param>
        /// <param name="offset">offset into <see cref="source"/></param>
        /// <param name="count">number of bytes from <see cref="source"/> starting 
        ///     at <see cref="offset"/></param>
        public void Add(byte[] source, int offset, int count)
        {
            // FIXME: should we add this?  Or allocate and copy?
            Add(new ArraySegment<byte>(source, offset, count));
        }

        /// <summary>
        /// Dispose of the contents of this packet, restoring the
        /// packet to the same state as a newly-created instance.
        /// </summary>
        public void Clear()
        {
            // FIXME: return the arrays to the pool
            list.Clear();
            length = 0;
        }

        /// <summary>
        /// Copy the specified portion of this packet to the provided byte array.
        /// </summary>
        /// <param name="sourceStart">the starting offset into this packet</param>
        /// <param name="count">the number of bytes to copy</param>
        /// <param name="destination">the destination byte array</param>
        /// <param name="destIndex">the starting offset into the destination byte array</param>
        /// <param name="count">the number of bytes to copy</param>
        public void CopyTo(int sourceStart, int count, byte[] destination, int destIndex)
        {
            if (destIndex + count > destination.Length)
            {
                throw new ArgumentOutOfRangeException("destIndex",
                    "destination does not have enough space");
            }
            if (sourceStart + count > length)
            {
                throw new ArgumentOutOfRangeException("sourceStart",
                    "startOffset and count extend beyond the end of this instance");
            }

            // We proceed through our segments, copying those portions
            // that fall in our defined area of interest.
            int sourceEnd = sourceStart + count - 1; // index of last byte to be copied
            int segmentStart = 0; // index of first byte of current <segment>
            foreach (ArraySegment<byte> segment in list)
            {
                if (count == 0) { return; }
                int segmentEnd = segmentStart + segment.Count - 1; // index of last byte
                // This segment is of interest if 
                // sourceOffset >= segmentEnd && sourceEnd <= segmentStart
                // IF: sourceOffset < segmentEnd then we're too early
                // IF: sourceEnd > segmentStart then we've gone past

                // if this segment appears after the area of interest then we're finished:
                // none of the remaining segments can possibly be in our AOI
                if (segmentStart > sourceEnd)
                {
                    // Note: sholdn't happen since we're decrementing count anyways
                    return;
                }
                // but it this segment is at least partially contained within our area of interest
                if (sourceStart <= segmentEnd)
                {
                    int copyOffset = Math.Max(segmentStart, sourceStart) - segmentStart;
                    int copyLen = Math.Min(segmentEnd, sourceEnd) - 
                        Math.Max(segmentStart, sourceStart) + 1;
                    Array.Copy(segment.Array, segment.Offset + copyOffset, destination,
                        destIndex, copyLen);
                    destIndex += copyLen;
                    count -= copyLen;
                    Debug.Assert(count >= 0);
                }
                segmentStart += segment.Count;
            }
        }

        /// <summary>
        /// Piece together the contents of this byte array into a 
        /// single contiguous byte array.
        /// </summary>
        /// <returns>the contents of this packet</returns>
        public byte[] ToArray()
        {
            byte[] result = new byte[length];
            int offset = 0;
            foreach (ArraySegment<byte> segment in list)
            {
                Array.Copy(segment.Array, segment.Offset, result, offset, segment.Count);
                offset += segment.Count;
            }
            return result;
        }

        /// <summary>
        /// Piece together a portion of the contents of this byte array into a 
        /// single contiguous byte array.
        /// </summary>
        /// <returns>the contents of this packet</returns>
        public byte[] ToArray(int offset, int count)
        {
            byte[] result = new byte[count];
            CopyTo(offset, count, result, 0);
            return result;
        }

        /// <summary>
        /// Split this instance at the given position.  This instance
        /// will contain the first part, and the returned packet will
        /// contain the remainder.  This is more efficient than
        /// the equivlent using <see cref="ToArray(int,int)"/> and
        /// <see cref="RemoveBytes"/>.
        /// </summary>
        /// <param name="position">the position at which this instance 
        /// should be split; this instance will have the contents from
        /// [0,...,position-1] and the new instance returned will contain
        /// the remaining bytes</param>
        /// <returns>an instance containing the remaining bytes from 
        /// <see cref="position"/> onwards</returns>
        public TransportPacket SplitAt(int position) 
        {
            // FIXME: optimize this
            TransportPacket remainder = CopyOf(this, position, length - position);
            RemoveBytes(position, length - position);
            return remainder;
        }

        /// <summary>
        /// Replace the bytes from [sourceStart, sourceStart+count-1] with 
        /// buffer[bufferStart, ..., bufferStart+count-1]
        /// </summary>
        /// <param name="sourceStart">the starting point in this packet</param>
        /// <param name="count">the number of bytes to be replaced</param>
        /// <param name="buffer">the source for the replacement bytes</param>
        /// <param name="bufferStart">the starting point in <see cref="buffer"/>
        /// for the replacement bytes</param>
        public void Replace(int sourceStart, int count, byte[] buffer, int bufferStart)
        {
            if (bufferStart + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("bufferStart",
                    "buffer would be overrun");
            }
            if (sourceStart + count > length)
            {
                throw new ArgumentOutOfRangeException("sourceStart",
                    "offset and count extend beyond the end of this instance");
            }

            // We proceed through our segments, copying those portions
            // that fall in our defined area of interest.
            int sourceEnd = sourceStart + count - 1; // index of last byte to be copied
            int segmentStart = 0; // index of first byte of current <segment>
            foreach (ArraySegment<byte> segment in list)
            {
                int segmentEnd = segmentStart + segment.Count - 1; // index of last byte

                // This segment is of interest if 
                // sourceStart <= segmentEnd && sourceEnd >= segmentStart
                // IF: segmentEnd < sourceStart then we're too early
                // IF: sourceEnd < segmentStart then we've gone past

                // if this segment appears after the area of interest then we're finished:
                // none of the remaining segments can possibly be in our AOI
                if (sourceEnd < segmentStart) { break; }

                // but it this segment is at least partially contained within our area of interest
                if (sourceStart <= segmentEnd)
                {
                    int copyOffset = Math.Max(segmentStart, sourceStart) - segmentStart;
                    int copyLen = Math.Min(segmentEnd, sourceEnd) - segmentStart + 1;
                    Array.Copy(buffer, bufferStart, segment.Array, segment.Offset + copyOffset, copyLen);
                    bufferStart += copyLen;
                    count -= copyLen;
                    Debug.Assert(count >= 0);
                    if (count == 0) { return; }
                }
                segmentStart += segment.Count;
            }
        }

        /// <summary>
        /// Remove the bytes from [offset, ..., offset + count - 1]
        /// </summary>
        /// <param name="offset">starting point of bytes to remove</param>
        /// <param name="count">the number of bytes to remove from <see cref="offset"/></param>
        public void RemoveBytes(int offset, int count)
        {
            int segmentStart = 0;
            if (offset < 0 || count < 0) { throw new ArgumentException(); }
            if (offset + count > length) { throw new ArgumentOutOfRangeException(); }
            // Basically we find the segment containing offset.
            // From that point we trim the remainder of segments until
            // we reach a segment where there is a tail end hanging.
            // Special cases: where [offset,offset+count-1] fall within a segment.
            length -= count;
            for (int index = 0; index < list.Count && count > 0; )
            {
                int segmentEnd = segmentStart + list[index].Count - 1;
                // This segment is of interest if 
                // offset <= segmentEnd && offset + count - 1 >= segmentStart
                // IF: segmentEnd < offset then we're too early
                // IF: offset + count < segmentStart then we've gone past
                if (offset > segmentEnd)
                {
                    segmentStart += list[index].Count;
                    index++;
                    continue;
                }
                if (segmentStart == offset)
                {
                    // We either remove this segment entirely or trim off its beginning
                    if (list[index].Count <= count)
                    {
                        // If we encompass this whole segment, just remove it
                        count -= list[index].Count;
                        list.RemoveAt(index);
                        // don't increment index
                    }
                    else
                    {
                        // Trim off the beginning and we're done
                        ArraySegment<byte> original = list[index];
                        list[index++] = new ArraySegment<byte>(original.Array,
                            original.Offset + count, original.Count - count);
                        return;
                    }
                }
                else if (count + (offset - segmentStart) < list[index].Count)
                {
                    // We need to remove an interior part of this segment; we instead
                    // trim this segment, and add a new segment for the remainder
                    ArraySegment<byte> original = list[index];
                    list[index] = new ArraySegment<byte>(original.Array, original.Offset,
                        offset - segmentStart);
                    list.Insert(index + 1, new ArraySegment<byte>(original.Array,
                        original.Offset + (offset + count - segmentStart),
                        original.Count - (offset + count - segmentStart)));
                    return;
                }
                else
                {
                    // Trim off the end of this segment
                    int newSegCount = offset - segmentStart;
                    ArraySegment<byte> original = list[index];
                    Debug.Assert(count >= original.Count - newSegCount);
                    int removed = original.Count - newSegCount;
                    list[index++] = new ArraySegment<byte>(original.Array,
                        original.Offset, newSegCount);
                    count -= removed;
                    segmentStart += newSegCount;
                }
            }
        }

        /// <summary>
        /// Return the byte at the given offset.
        /// Note that this is not, and is not intended to be, an efficient operation.
        /// It's actually intended more for debugging.
        /// </summary>
        /// <param name="offset">the offset into this packet</param>
        /// <returns>the byte at the provided offset</returns>
        /// <exception cref="ArgumentOutOfRangeException">thrown if the offset is
        /// out of the range of this object</exception>
        public byte ByteAt(int offset)
        {
            int segmentOffset = 0;
            if (offset < 0 || offset >= length) { throw new ArgumentOutOfRangeException("offset"); }
            foreach (ArraySegment<byte> segment in list)
            {
                if (offset < segmentOffset + segment.Count)
                {
                    return segment.Array[segment.Offset + (offset - segmentOffset)];
                }
                segmentOffset += segment.Count;
            }
            throw new InvalidStateException("should never get here", this);
        }

        /// <summary>
        /// Invoke the provided delegate for the <see cref="count"/> bytes
        /// found at the <see cref="offset"/> in this packet.
        /// Note that this is not, and is not intended to be, a terribly
        /// efficient operation.
        /// It's actually intended more for debugging.
        /// </summary>
        /// <param name="offset">the offset into this packet</param>
        /// <returns>the byte at the provided offset</returns>
        /// <exception cref="ArgumentOutOfRangeException">thrown if the offset is
        /// out of the range of this object</exception>
        public void BytesAt(int offset, int count, Action<byte[], int> block)
        {
            int segmentOffset = 0;
            if (offset < 0 || count < 0 || offset + count > length)
            {
                throw new ArgumentOutOfRangeException();
            }
            foreach (ArraySegment<byte> segment in list)
            {
                if (offset < segmentOffset + segment.Count)
                {
                    // If the bytes are contiguous in one segment, call the block
                    // on the segment directly.  Else we need to invoke the block on
                    // a copy of the data
                    if (offset + count < segmentOffset + segment.Count)
                    {
                        block(segment.Array, segment.Offset + (offset - segmentOffset));
                        return;
                    }
                    block(ToArray(offset, count), 0);
                    return;
                }
                segmentOffset += segment.Count;
            }
            throw new InvalidStateException("should never get here", this);
        }

        /// <summary>
        /// Grow this packet to contain <see cref="newLength"/> bytes.
        /// Callers should not assume that any new bytes are initialized to
        /// some particular value.
        /// </summary>
        /// <param name="newLength"></param>
        public void Grow(int newLength)
        {
            int needed = newLength - length;
            if(list.Count > 0)
            {
                ArraySegment<byte> last = list[list.Count - 1];
                if (needed < last.Array.Length - last.Count - last.Offset)
                {
                    list[list.Count - 1] = new ArraySegment<byte>(last.Array, last.Offset,
                        last.Count + needed);
                    return;
                }
            }
            byte[] additional = new byte[newLength - length];    // FIXME: allocate from pool
            Add(additional, 0, newLength - length);
        }

        /// <summary>
        /// Open a *destructive* stream for reading from the contents of this
        /// packet.  This stream is destructive as the content retrieved
        /// through the stream is removed from the stream.
        /// Note: do not modify the packet whilst this stream is in use.
        /// </summary>
        /// <returns></returns>
        public Stream AsReadStream()
        {
            return new ReadStream(this);
        }

        /// <summary>
        /// Open a writeable stream on the contents of this packet.
        /// The stream is initially positioned at the beginning of
        /// the packet, thus data written will overwrite the contents
        /// of the stream.  The stream must be flushed to ensure that
        /// any new data is written out to the packet.
        /// Note: do not modify of access the packet whilst this stream is in use
        public Stream AsWriteStream()
        {
            return new WriteStream(this);
        }

        /// <summary>
        /// Packets, once finished with, must be explicitly disposed of to
        /// deal with cleaning up any possibly shared memory.
        /// </summary>
        public void Dispose()
        {
            // FIXME: return memory to the pool
        }

        #region IList<ArraySegment<byte>> implementation

        int IList<ArraySegment<byte>>.IndexOf(ArraySegment<byte> item)
        {
            return list.IndexOf(item);
        }

        void IList<ArraySegment<byte>>.Insert(int index, ArraySegment<byte> item)
        {
            list.Insert(index, item);
            length += item.Count;
        }

        void IList<ArraySegment<byte>>.RemoveAt(int index)
        {
            length -= list[index].Count;
            list.RemoveAt(index);
        }

        ArraySegment<byte> IList<ArraySegment<byte>>.this[int index]
        {
            get { return list[index]; }
            set
            {
                length -= list[index].Count;
                list[index] = value;
                length += value.Count;
            }
        }

        bool ICollection<ArraySegment<byte>>.Contains(ArraySegment<byte> item)
        {
            return list.Contains(item);
        }

        void ICollection<ArraySegment<byte>>.CopyTo(ArraySegment<byte>[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        bool ICollection<ArraySegment<byte>>.Remove(ArraySegment<byte> item)
        {
            if (!list.Remove(item)) { return false; }
            length -= item.Count;
            return true;
        }

        int ICollection<ArraySegment<byte>>.Count
        {
            get { return list.Count; }
        }

        bool ICollection<ArraySegment<byte>>.IsReadOnly
        {
            get { return false; }
        }

        IEnumerator<ArraySegment<byte>> IEnumerable<ArraySegment<byte>>.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        #endregion

        protected class ReadStream : Stream
        {
            protected readonly TransportPacket packet;

            protected internal ReadStream(TransportPacket p)
            {
                packet = p;
            }

            public override bool CanRead { get { return true; } }

            // Hmm, we are partially seekable, in that we can seek forward...
            public override bool CanSeek { get { return false; } }

            public override bool CanWrite { get { return false; } }

            public override long Seek(long offset, SeekOrigin origin)
            {
                // We could support forward movements?
                throw new NotSupportedException();

                //switch (origin)
                //{
                //case SeekOrigin.Begin:
                //    break;

                //case SeekOrigin.Current:
                //    offset += position;
                //    break;

                //case SeekOrigin.End:
                //    offset = packet.length - offset;
                //    break;
                //}
                //if (offset < 0 || offset > Length) { throw new ArgumentException("cannot seek backwards"); }
                //position = (int)offset;
                //return position;
            }

            public override void SetLength(long value)
            {
                packet.RemoveBytes((int)value, packet.Length - (int)value);
                throw new NotImplementedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                count = Math.Min(count, buffer.Length - offset);
                //if (buffer.Length < offset + count)
                //{
                //    throw new ArgumentException("buffer does not have sufficient capacity", "buffer");
                //}
                count = Math.Min(count, packet.Length);
                packet.CopyTo(0, count, buffer, offset);
                packet.RemoveBytes(0, count);
                return count;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Length { get { return packet.Length; } }

            public override long Position
            {
                get { return 0; }
                set { Seek(value, SeekOrigin.Current); }
            }

            public override void Flush()
            {
            }
        }

        /// <summary>
        /// A writeable stream on to a packet.  This stream will grow the packet
        /// as necessary; users must ensure they call <see cref="Flush"/> to
        /// append any new data.
        /// </summary>
        protected class WriteStream : Stream
        {
            protected readonly TransportPacket packet;
            protected byte[] interim = null;
            protected int position = 0;
            protected int newLength = 0;

            protected internal WriteStream(TransportPacket p)
            {
                packet = p;
                newLength = p.Length;
            }

            public override bool CanRead { get { return false; } }

            public override bool CanSeek { get { return true; } }

            public override bool CanWrite { get { return true; } }

            public override void Flush()
            {
                if (interim != null)
                {
                    packet.Add(interim, 0, (int)(newLength - packet.Length));
                    interim = null;
                    newLength = packet.Length;
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        break;
                    case SeekOrigin.Current:
                        offset += position;
                        break;
                    case SeekOrigin.End:
                        offset = Length - offset;
                        break;
                }
                if (offset < 0 || offset >= Length) { throw new ArgumentException("offset is out of range"); }
                position = (int)offset;
                return position;
            }

            public override void SetLength(long value)
            {
                if (value >= newLength) { return; }
                if (value > packet.Length)
                {
                    // if value < newLength but value > packet.Length then we have an 
                    // interim buffer, so we simply trim the endpointer into the interim 
                    // buffer appropriately.
                    newLength = (int)value;
                }
                else
                {
                    // The new value requires trimming the packet.
                    interim = null; // Toss the interim buffer (if any)
                    packet.RemoveBytes((int)value, packet.Length - (int)value);
                    newLength = (int)value;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                // Read out whatever we can from the packet itself, then whatever we need 
                // to our interim buffer
                int total = 0;
                if (position < packet.Length)
                {
                    int numBytes = Math.Min(count, packet.Length - position);
                    packet.CopyTo(position, numBytes, buffer, offset);
                    position += numBytes;
                    offset += numBytes;
                    count -= numBytes;
                    total += numBytes;
                    Debug.Assert(count == 0 || position == packet.Length);
                }
                if (count > 0 && position < newLength)
                {
                    Debug.Assert(position >= packet.Length);
                    // translate position into the interim buffer
                    int interimIndex = position - packet.Length;
                    int numBytes = Math.Min(interim.Length - interimIndex, count);
                    Array.Copy(interim, interimIndex, buffer, offset, numBytes);
                    position += numBytes;
                    offset += numBytes;
                    count -= numBytes;
                    total += numBytes;
                }
                return total;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                // Write out whatever we can to the packet, then whatever we need 
                // to our interim buffer
                if (position < packet.Length)
                {
                    int numBytes = Math.Min(count, packet.Length - position);
                    packet.Replace(position, numBytes, buffer, offset);
                    position += numBytes;
                    offset += numBytes;
                    count -= numBytes;
                    Debug.Assert(count == 0 || position == packet.Length);
                }
                while (count > 0)
                {
                    Debug.Assert(position >= packet.Length);
                    if (interim == null)
                    {
                        // FIXME: should allocate from a pool
                        interim = new byte[1024];
                        Debug.Assert(position == packet.Length);
                    }
                    // translate position into the interim buffer
                    int interimIndex = position - packet.Length;
                    int numBytes = Math.Min(interim.Length - interimIndex, count);
                    Array.Copy(buffer, offset, interim, interimIndex, numBytes);
                    position += numBytes;
                    interimIndex += numBytes;
                    offset += numBytes;
                    count -= numBytes;
                    newLength = Math.Max(newLength, position);
                    // if we've reached the end of this interim buffer, then flush it
                    if (interimIndex == interim.Length) { Flush(); }
                }
            }

            public override long Length { get { return newLength; } }

            public override long Position
            {
                get { return position; }
                set { Seek(value, SeekOrigin.Begin); }
            }
        }
    }

}

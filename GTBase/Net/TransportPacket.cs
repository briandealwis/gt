//
// GT: The Groupware Toolkit for C#
// Copyright (C) 2006 - 2009 by the University of Saskatchewan
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later
// version.
// 
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
// 02110-1301  USA
// 

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using GT.Utils;

namespace GT.Net
{
    /// <summary>
    /// Represents a byte-array with appropriately marshalled content ready to send
    /// across a particular transport.
    /// The purpse of this class is to provide efficient use of byte arrays.
    /// Transport packets, once finished with, must be explicitly disposed of 
    /// using <see cref="Dispose"/> to deal with cleaning up any possibly 
    /// shared memory.
    /// </summary>
    public class TransportPacket : IList<ArraySegment<byte>>, IDisposable
    {
        /// <summary>
        /// Create a transport packet that uses <see cref="byteArrays"/>
        /// as its backing store.  This approach should be compared to
        /// the various constructors that copy the contents onto newly 
        /// allocated memory (via a pool), increasing the chance that 
        /// the byteArrays will be made contiguous (assuming byteArrays.Length > 1); 
        /// contiguous memory may be advantageous for sending across 
        /// some transports.
        /// </summary>
        /// <param name="byteArrays">the byte arrays to use as the backing store</param>
        /// <returns>the packet</returns>
        public static TransportPacket On(params byte[][] byteArrays)
        {
            TransportPacket packet = new TransportPacket();
            foreach (byte[] byteArray in byteArrays)
            {
                packet.AppendSegment(new ArraySegment<byte>(byteArray));
            }
            return packet;
        }

        /// <summary>
        /// Create a new marshalled packet as a subset of another packet <see cref="source"/>
        /// Note: this method makes an independent *copy* of the appropriate 
        /// portion of <see cref="source"/>; this behaviour should be compared
        /// to <see cref="Subset"/> which uses <see cref="source"/> as a backing
        /// store.
        /// The caller is responsible for the disposal of this instance
        /// through <see cref="Dispose"/>.
        /// </summary>
        /// <param name="source">the provided marshalled packet</param>
        /// <param name="offset">the start position of the subset to include</param>
        /// <param name="count">the number of bytes of the subset to include</param>
        public static TransportPacket CopyOf(TransportPacket source, int offset, int count)
        {
            if (count <= _maxSegmentSize) {
                ArraySegment<byte> segment = AllocateSegment((uint)count);
                source.CopyTo(offset, segment.Array, segment.Offset, count);
                return new TransportPacket(segment);
            }
            byte[] newContents = new byte[count];
            source.CopyTo(offset, newContents, 0, count);
            return On(newContents);
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
        /// Try to re-use streams if possible, and also to commit any
        /// pending changes in a stream.
        /// </summary>
        protected Stream activeStream;

        /// <summary>
        /// TransportPackets maintain a reference count.  
        /// </summary>
        protected int referenceCount = 1;

        /// <summary>
        /// Create a new 0-byte transport packet.
        /// </summary>
        public TransportPacket()
        {
            list = new List<ArraySegment<byte>>();
        }

        /// <summary>
        /// Create a new marshalled packet as a subset of another packet <see cref="source"/>
        /// Note: this method uses a *copy* of the appropriate portion of <see cref="source"/>.
        /// </summary>
        /// <param name="source">the provided marshalled packet</param>
        /// <param name="offset">the start position of the subset to include</param>
        /// <param name="count">the number of bytes of the subset to include</param>
        public TransportPacket(TransportPacket source, int offset, int count)
        {
            list = new List<ArraySegment<byte>>();
            while (count > 0)
            {
                int segSize = Math.Min(count, (int)_maxSegmentSize);
                ArraySegment<byte> segment = AllocateSegment((uint)segSize);
                source.CopyTo(offset, segment.Array, segment.Offset, segSize);
                AppendSegment(segment);
                offset += segSize;
                count -= segSize;
            }
        }


        /// <summary>
        /// Create an instance with <see cref="initialSize"/> bytes.
        /// These bytes are uninitialized.
        /// </summary>
        /// <param name="initialSize">the initial size</param>
        public TransportPacket(uint initialSize)
        {
            list = new List<ArraySegment<byte>>(1);
            if (initialSize > 0) { Grow((int)initialSize); }
        }

        /// <summary>
        /// Create a new transport packet from the provided byte array segment.
        /// </summary>
        /// <param name="segment"></param>
        public TransportPacket(ArraySegment<byte> segment)
        {
            list = new List<ArraySegment<byte>>(1);
            if(IsManagedSegment(segment))
            {
                AppendSegment(segment);
            }
            else
            {
                Grow(segment.Count);
                Replace(0, segment.Array, segment.Offset, segment.Count);
            }
        }


        /// <summary>
        /// Create a new transport packet from the provided byte array.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public TransportPacket(byte[] bytes, int offset, int count)
        {
            list = new List<ArraySegment<byte>>(1);
            Grow(count);
            Replace(0, bytes, offset, count);
        }

        /// <summary>
        /// Create a new transport packet from the provided stream contents.
        /// </summary>
        /// <param name="ms"></param>
        public TransportPacket(MemoryStream ms)
            : this(ms.GetBuffer(), 0, (int)ms.Length) { }

        /// <summary>
        /// Add the contents of the provided byte arrays.
        /// </summary>
        /// <param name="byteArrays">the set of byte arrays</param>
        public TransportPacket(params byte[][] byteArrays)
            : this()
        {
            int size = 0;
            foreach (byte[] byteArray in byteArrays)
            {
                size += byteArray.Length;
            }
            Grow(size);
            int offset = 0;
            foreach (byte[] byteArray in byteArrays)
            {
                Replace(offset, byteArray, 0, byteArray.Length);
                offset += byteArray.Length;
            }
        }

        /// <summary>
        /// Return the number of bytes in this packet.
        /// </summary>
        public int Length
        {
            get
            {
                ValidateAndSync();
                return length;
            }
        }

        /// <summary>
        /// Return a subset of this marshalled packet; this subset is 
        /// backed by this instance, such that any changes to the contents
        /// of the subset are reflected in this instance too.
        /// The caller is responsible for the disposal of the subset
        /// through <see cref="Dispose"/>.
        /// </summary>
        /// <param name="subsetStart">the start position of the subset</param>
        /// <param name="count">the number of bytes in the subset</param>
        /// <returns>a new packet representing the requested subset</returns>
        public TransportPacket Subset(int subsetStart, int count)
        {
            ValidateAndSync();
            TransportPacket subset = new TransportPacket();
            subset.Append(this, subsetStart, count);
            return subset;
        }

        /// <summary>
        /// Make a copy of the contents of this packet; this copy is 
        /// backed by this instance, such that any changes to the contents
        /// of the copy are reflected in this instance too.
        /// The caller is responsible for the disposal of the copy
        /// through <see cref="Dispose"/>.
        /// </summary>
        /// <returns>a copy of the contents of this packet</returns>
        public TransportPacket Copy()
        {
            ValidateAndSync();
            TransportPacket copy = new TransportPacket();
            foreach (ArraySegment<byte> segment in list)
            {
                copy.AppendSegment(segment);
            }
            return copy;
        }

        /// <summary>
        /// Prepend the byte segment to this item.
        /// </summary>
        /// <param name="item"></param>
        public void Prepend(ArraySegment<byte> item)
        {
            Prepend(item.Array, item.Offset, item.Count);
        }

        /// <summary>
        /// Prepend the byte segment to this item.
        /// </summary>
        /// <param name="item"></param>
        public void Prepend(byte[] item)
        {
            Prepend(item, 0, item.Length);
        }

        /// <summary>
        /// Prepend the byte segment to this item.
        /// The byte segment is now assumed to belong to this packet instance
        /// and should not be used elsewhere!
        /// </summary>
        /// <param name="source">the source of the bytes</param>
        /// <param name="offset">the starting point into the source</param>
        /// <param name="count">the number of bytes to copy out from the source</param>
        public void Prepend(byte[] source, int offset, int count)
        {
            ValidateAndSync();
            if (list.Count > 0)
            {
                // Check to see if there is space available at the beginning of
                // list[0]; can only do it if the segment isn't being shared with
                // someone else (as they might be using that part of the segment)
                ArraySegment<byte> segment = list[0];
                lock(segment.Array)
                {
                    if(count <= segment.Offset - HeaderSize
                        && IsManagedSegment(segment) && GetRefCount(segment) == 1)
                    {
                        segment = new ArraySegment<byte>(segment.Array, segment.Offset - count,
                            segment.Count + count);
                        Buffer.BlockCopy(source, offset, segment.Array, segment.Offset, count);
                        list[0] = segment;
                        length += count;
                        return;
                    }
                }
            }
            while (count > 0)
            {
                int segSize = Math.Min(count, (int)_maxSegmentSize);
                ArraySegment<byte> segment = AllocateSegment((uint)segSize);
                Buffer.BlockCopy(source, offset + count - segSize, segment.Array, segment.Offset, segSize);
                PrependSegment(segment);
                count -= segSize;
            }
        }

        /// <summary>
        /// Append the appropriate segments of <see cref="source"/> to this instance.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public void Append(TransportPacket source, int offset, int count) {
            ValidateAndSync();
            int subsetEnd = offset + count - 1;    // index of last byte of interest
            int segmentStart = 0; // index of first byte of current <segment>
            foreach (ArraySegment<byte> segment in source.list)
            {
                int segmentEnd = segmentStart + segment.Count - 1; // index of last byte

                // This segment is of interest if 
                // listStart <= segmentEnd && listEnd >= segmentStart
                // IF: segmentEnd < subsetStart then we're too early
                // IF: subsetEnd < segmentStart then we've gone past
                if (segmentEnd >= offset)
                {
                    // if this segment appears after the area of interest then we're finished:
                    // none of the remaining segments can possibly be in our AOI
                    if (segmentStart > subsetEnd)
                    {
                        break;
                    }
                    if (offset <= segmentStart && segmentEnd <= subsetEnd)
                    {
                        AppendSegment(segment);  // not subset's responsibility
                    }
                    else
                    {
                        int aoiStart = Math.Max(offset, segmentStart);
                        int aoiEnd = Math.Min(subsetEnd, segmentEnd);
                        AppendSegment(new ArraySegment<byte>(segment.Array,
                            segment.Offset + (int)(aoiStart - segmentStart),
                            (int)(aoiEnd - aoiStart + 1)));  // not subset's responsibility
                    }
                }
                segmentStart += segment.Count;
            }
        }

        /// <summary>
        /// Append the contents of <see cref="item"/> to this item.  
        /// </summary>
        /// <param name="item">source array</param>
        public void Append(ArraySegment<byte> item)
        {
            Append(item.Array, item.Offset, item.Count);
        }

        /// <summary>
        /// Append the contents of <see cref="source"/> to this item.  
        /// </summary>
        /// <param name="source">source array</param>
        public void Append(byte[] source)
        {
            Append(source, 0, source.Length);
        }


        /// <summary>
        /// Append the specified portion of the contents of <see cref="source"/> to this item.  
        /// </summary>
        /// <param name="source">source array</param>
        /// <param name="offset">offset into <see cref="source"/></param>
        /// <param name="count">number of bytes from <see cref="source"/> starting 
        ///     at <see cref="offset"/></param>
        public void Append(byte[] source, int offset, int count)
        {
            ValidateAndSync();
            if (count < 0 || offset < 0 || offset + count > source.Length) { throw new ArgumentOutOfRangeException(); }
            int l = length;
            Grow(length + count);
            Replace(l, source, offset, count);
        }

        /// <summary>
        /// Transfer responsibility for our first segment to the caller.
        /// This means that we remove it from our consideration, but don't 
        /// decrement its reference count.  Used by <see cref="ReadStream"/>.
        /// </summary>
        internal ArraySegment<byte> TransferFirstSegment()
        {
            ArraySegment<byte> segment = list[0];
            list.RemoveAt(0);
            length -= segment.Count;
            return segment;
        }

        /// <summary>
        /// Prepend the provided segment to our segment list.
        /// </summary>
        /// <param name="segment">a segment allocated through <see cref="AllocateSegment"/></param>
        internal void PrependSegment(ArraySegment<byte> segment)
        {
            IncrementRefCount(segment);
            list.Insert(0, segment);
            length += segment.Count;
        }

        /// <summary>
        /// Append the provided segment to our segment list.
        /// </summary>
        /// <param name="segment">a segment allocated through <see cref="AllocateSegment"/></param>
        internal void AppendSegment(ArraySegment<byte> segment)
        {
            IncrementRefCount(segment);
            list.Add(segment);
            length += segment.Count;
        }

        /// <summary>
        /// Clear out the contents of this packet, restoring the
        /// packet to the same state as a newly-created instance.
        /// </summary>
        public void Clear()
        {
            ValidateAndSync();
            // return the arrays to the pool
            foreach (ArraySegment<byte> segment in list)
            {
                ReleaseSegment(segment);
            }
            list.Clear();
            length = 0;
        }

        /// <summary>
        /// Retain a hold on this packet beyond its normal lifetime.
        /// This adds to the packet's reference count.  The return value
        /// must be checked as it may be too late to be retained.
        /// </summary>
        /// <returns>true if the object was successfully retained</returns>
        public bool Retain()
        {
            if (list == null) { return false; } // too late
            return Interlocked.Increment(ref referenceCount) > 0;
        }

        /// <summary>
        /// Packets, once finished with, must be explicitly disposed of to
        /// deal with cleaning up any possibly shared memory.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Decrement(ref referenceCount) > 0) { return; }
            if (list != null) { Clear(); }
            list = null;

            // tell the GC not to bother finalizing
            GC.SuppressFinalize(this);
        }

        // FIXME: need to extract the segment-management stuff to a separate
        // class, as otherwise we have packets that are being finalized (during
        // testing) that are trying to return segments to a trashed segment-managemnt.
        ~TransportPacket()
        {
            referenceCount = 0;
            Dispose();
        }

        /// <summary>
        /// Copy the specified portion of this packet to the provided byte array.
        /// </summary>
        /// <param name="sourceStart">the starting offset into this packet</param>
        /// <param name="destination">the destination byte array</param>
        /// <param name="destIndex">the starting offset into the destination byte array</param>
        /// <param name="count">the number of bytes to copy</param>
        public void CopyTo(int sourceStart, byte[] destination, int destIndex, int count)
        {
            ValidateAndSync();
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
                    Buffer.BlockCopy(segment.Array, segment.Offset + copyOffset, destination,
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
            ValidateAndSync();
            byte[] result = new byte[length];
            int offset = 0;
            foreach (ArraySegment<byte> segment in list)
            {
                Buffer.BlockCopy(segment.Array, segment.Offset, result, offset, segment.Count);
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
            ValidateAndSync();
            byte[] result = new byte[count];
            CopyTo(offset, result, 0, count);
            return result;
        }

        /// <summary>
        /// Split this instance at the given position.  This instance
        /// will contain the first part, and the returned packet will
        /// contain the remainder.  This is more efficient than
        /// the equivlent using <see cref="ToArray(int,int)"/> and
        /// <see cref="RemoveBytes"/>.
        /// The caller is responsible for the disposal of the new instance
        /// through <see cref="Dispose"/>.
        /// </summary>
        /// <param name="splitPosition">the position at which this instance 
        /// should be split; this instance will have the contents from
        /// [0,...,splitPosition-1] and the new instance returned will contain
        /// the remaining bytes</param>
        /// <returns>an instance containing the remaining bytes from 
        /// <see cref="splitPosition"/> onwards</returns>
        public TransportPacket SplitAt(int splitPosition)
        {
            ValidateAndSync();
            if (splitPosition >= length) { throw new ArgumentOutOfRangeException("splitPosition"); }

            int segmentOffset = 0;
            int segmentIndex = 0;
            // skip over those segments that remain in this instance
            while (segmentIndex < list.Count && segmentOffset + list[segmentIndex].Count - 1 < splitPosition)
            {
                segmentOffset += list[segmentIndex++].Count;
            }
            TransportPacket remainder = new TransportPacket();
            // So: segmentOffset <= splitPosition < segmentOffset + list[segmentIndex].Count
            // If segmentOffset == splitPosition then list[segmentIndex] belongs in remainder
            // Else gotta split list[segmentIndex] between the two
            if (splitPosition != segmentOffset)
            {
                // split list[segmentIndex] appropriately
                ArraySegment<byte> segment = list[segmentIndex];
                int segCount = splitPosition - segmentOffset;
                list[segmentIndex++] = new ArraySegment<byte>(segment.Array, segment.Offset, segCount);
                remainder.AppendSegment(new ArraySegment<byte>(segment.Array, segment.Offset + segCount,
                    segment.Count - segCount));
            }

            // Copy the remaining segments to remainder
            for (int i = segmentIndex; i < list.Count; i++)
            {
                remainder.AppendSegment(list[i]);
                ReleaseSegment(list[i]);
            }
            list.RemoveRange(segmentIndex, list.Count - segmentIndex);
            length = splitPosition;
            return remainder;
        }

        /// <summary>
        /// Split the first <see cref="count"/> bytes of this instance
        /// into a new packet, and remove the bytes from this instance.
        /// This can be seen as the opposite of <see cref="SplitAt"/>.
        /// This is more efficient than the equivlent using 
        /// <see cref="ToArray(int,int)"/> and <see cref="RemoveBytes"/>.
        /// The caller is responsible for the disposal of the new instance
        /// through <see cref="Dispose"/>.
        /// </summary>
        /// <param name="count">the number of bytes that should be
        /// split out from this instance.  The new instance returned will 
        /// contain the contents from [0,...,<see cref="count"/> - 1] and 
        /// this instance will have the remaining bytes.</param>
        /// <returns>an instance containing the bytes from 
        /// [0,...,<see cref="count"/> - 1]</returns>
        public TransportPacket SplitOut(int count)
        {
            ValidateAndSync();
            if (count < 0 || count > length) { throw new ArgumentOutOfRangeException("count"); }

            int segmentOffset = 0;
            int segmentIndex = 0;
            TransportPacket initial = new TransportPacket();

            // Copy the remaining segments to remainder
            while(segmentIndex < list.Count && segmentOffset + list[segmentIndex].Count < count)
            {
                initial.AppendSegment(list[segmentIndex]);
                ReleaseSegment(list[segmentIndex]);
                segmentOffset += list[segmentIndex++].Count;
            }

            // So: segmentOffset <= count < segmentOffset + list[segmentIndex].Count
            // If count <= segmentOffset then list[segmentIndex] belongs in remainder
            // Else gotta split list[segmentIndex] between the two
            if (count > segmentOffset)
            {
                // split list[segmentIndex] appropriately
                ArraySegment<byte> segment = list[segmentIndex];
                int segCount = count - segmentOffset;
                initial.AppendSegment(new ArraySegment<byte>(segment.Array, segment.Offset, segCount));
                list[segmentIndex] = new ArraySegment<byte>(segment.Array, 
                    segment.Offset + segCount, segment.Count - segCount);
            }

            list.RemoveRange(0, segmentIndex);
            length -= count;
            return initial;
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
        public void Replace(int sourceStart, byte[] buffer, int bufferStart, int count)
        {
            ValidateAndSync();
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
                    int copyLen = Math.Min(segmentEnd, sourceEnd) -
                        Math.Max(segmentStart, sourceStart) + 1;
                    Buffer.BlockCopy(buffer, bufferStart, segment.Array, segment.Offset + copyOffset, copyLen);
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
        /// <exception cref="ArgumentOutOfRangeException">thrown if offset or count are
        /// invalid</exception>
        public void RemoveBytes(int offset, int count)
        {
            ValidateAndSync();
            int segmentStart = 0;
            if (offset < 0 || count < 0 || offset + count > length) { throw new ArgumentOutOfRangeException(); }
            // Basically we find the segment containing offset.
            // From that point we trim the remainder of segments until
            // we reach a segment where there is a tail end hanging.
            // Special cases: where [offset,offset+count-1] fall within a segment.
            length -= count;
            for (int index = 0; index < list.Count && count > 0; )
            {
                ArraySegment<byte> segment = list[index];
                int segmentEnd = segmentStart + list[index].Count - 1;
                // This segment is of interest if 
                // offset <= segmentEnd && offset + count - 1 >= segmentStart
                // IF: segmentEnd < offset then we're too early
                // IF: offset + count < segmentStart then we've gone past
                if (offset > segmentEnd)
                {
                    segmentStart += segment.Count;
                    index++;
                    continue;
                }
                if (segmentStart == offset)
                {
                    // We either remove this segment entirely or trim off its beginning
                    if (segment.Count <= count)
                    {
                        // If we encompass this whole segment, just remove it
                        // Note: we don't increment index
                        count -= segment.Count;
                        list.RemoveAt(index);
                        ReleaseSegment(segment);
                    }
                    else
                    {
                        // Trim off the beginning and we're done
                        list[index++] = new ArraySegment<byte>(segment.Array,
                            segment.Offset + count, segment.Count - count);
                        return;
                    }
                }
                else if (count + (offset - segmentStart) < list[index].Count)
                {
                    // We need to remove an interior part of this segment; we instead
                    // trim this segment, and add a new segment for the remainder
                    list[index] = new ArraySegment<byte>(segment.Array, segment.Offset,
                        offset - segmentStart);
                    IncrementRefCount(segment);
                    list.Insert(index + 1, new ArraySegment<byte>(segment.Array,
                        segment.Offset + (offset + count - segmentStart),
                        segment.Count - (offset + count - segmentStart)));
                    return;
                }
                else
                {
                    // Trim off the end of this segment
                    int newSegCount = offset - segmentStart;
                    Debug.Assert(count >= segment.Count - newSegCount);
                    int removed = segment.Count - newSegCount;
                    list[index++] = new ArraySegment<byte>(segment.Array,
                        segment.Offset, newSegCount);
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
            ValidateAndSync();
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
        /// <param name="count">the number of bytes to return</param>
        /// <param name="block">the action block to receive the bytes.</param>
        /// <returns>the byte at the provided offset</returns>
        /// <exception cref="ArgumentOutOfRangeException">thrown if the offset is
        /// out of the range of this object</exception>
        public void BytesAt(int offset, int count, Action<byte[], int> block)
        {
            ValidateAndSync();
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
            ValidateAndSync();
            int need = newLength - length;
            if (list.Count > 0)
            {
                ArraySegment<byte> last = list[list.Count - 1];
                lock (last.Array)
                {
                    // we can only resize this segment if we're the only ones using the segment
                    int available = last.Array.Length - last.Offset - last.Count;
                    if (GetRefCount(last) == 1 && available > 0)
                    {
                        int taken = Math.Min(need, available);
                        list[list.Count - 1] = new ArraySegment<byte>(last.Array, last.Offset,
                            last.Count + taken);
                        length += taken;
                    }
                }
            }
            while ((need = newLength - length) > 0)
            {
                AppendSegment(AllocateSegment(Math.Min(_maxSegmentSize, (uint)need)));
            }
        }

        public override string ToString()
        {
            if (list == null) { return "<<disposed>>"; }
            StringBuilder result = new StringBuilder();
            if (activeStream != null) { result.Append("stream may have changes"); }
            result.Append(length);
            result.Append(" bytes; ");
            result.Append(list.Count);
            result.Append(" segments (");
            uint managed = 0;
            foreach (ArraySegment<byte> seg in list) { if(IsManagedSegment(seg)) { managed++; } }
            result.Append(managed);
            result.Append(" managed): ");
            //result.Append(ByteUtils.HexDump(ToArray(0, Math.Min(length, 128))));
            return result.ToString();
        }

        /// <summary>
        /// Attempt to consolidate this instance to a single segment.
        /// </summary>
        public void Consolidate() {
            ValidateAndSync();
            if (list.Count <= 1) { return; }    // already consolidated
            List<ArraySegment<byte>> newSegments = new List<ArraySegment<byte>>(1);
            ArraySegment<byte> consolidated;
            int offset = 0;         // how far we are through the byte of this instance
            int segmentIndex = 0;   // segment under consideration
            int segmentOffset = 0;  // offset into segment under consideration
            do
            {
                consolidated = AllocateSegment((uint)Math.Min(_maxSegmentSize, length - offset));
                int consolidatedOffset = 0;
                while (segmentIndex < list.Count && consolidatedOffset < consolidated.Count)
                {
                    int numBytes = Math.Min(list[segmentIndex].Count - segmentOffset,
                        consolidated.Count - consolidatedOffset);
                    Buffer.BlockCopy(list[segmentIndex].Array, list[segmentIndex].Offset + segmentOffset, 
                        consolidated.Array, consolidated.Offset + consolidatedOffset, numBytes);
                    offset += numBytes;
                    consolidatedOffset += numBytes;
                    segmentOffset += numBytes;
                    if (segmentOffset == list[segmentIndex].Count)
                    {
                        segmentIndex++;
                        segmentOffset = 0;
                    }
                }
                newSegments.Add(consolidated);
            } while(offset < length);

            Clear();    // release all current segments and then add their replacements
            foreach (ArraySegment<byte> seg in newSegments) { AppendSegment(seg); }
        }

        /// <summary>
        /// Open a *destructive* stream for reading from the contents of this
        /// packet.  This stream is destructive as the content retrieved
        /// through the stream is removed from the stream.
        /// The stream can be flushed to commit any changes to the packet.  
        /// The stream is automatically flushed upon any access to the packet.
        /// The stream is automatically closed if a write stream is opened
        /// upon this instance.
        /// </summary>
        /// <seealso cref="ReadStream"/>
        /// <returns></returns>
        public Stream AsReadStream()
        {
            if (activeStream != null)
            {
                if(activeStream is ReadStream) { return activeStream; }
                activeStream.Close();
            }
            return activeStream = new ReadStream(this);
        }

        /// <summary>
        /// Open a writeable stream on the contents of this packet.
        /// The stream is initially positioned at the beginning of
        /// the packet, thus data written will overwrite the contents
        /// of the stream.  
        /// The stream can be flushed to commit any changes to the packet.  
        /// The stream is automatically flushed upon any access to the packet.
        /// The stream is automatically closed if a read stream is opened
        /// upon this instance.
        /// </summary>
        public Stream AsWriteStream()
        {
            if (activeStream != null)
            {
                if (activeStream is WriteStream) { return activeStream; }
                activeStream.Close();
            }
            return activeStream = new WriteStream(this);
        }

        protected void ValidateAndSync()
        {
            if (list == null) { throw new ObjectDisposedException("packet has been disposed"); }
            if (activeStream == null) { return; }
            activeStream.Flush();
        }

        #region Managed Segment Allocation and Deallocation
        /// These methods act as a sort of malloc-like system.
        /// Segments may be shared between multiple packets; the segments use
        /// a fixed number of bytes at the beginning to record a header.
        /// This header records a reference count.  As segments are added
        /// to a packet, it should increment the ref count.  As segments
        /// are removed or the packet is disposed, the segments should be
        /// released.  When a segment has refcount == 1, then only one
        /// user is using the segment, and the segment may be resized with 
        /// impunity.  Such resizing should only be done by acquiring the
        /// segment array's lock.

        private static uint _minSegmentSize = 1024;
        private static uint _maxSegmentSize = 64 * 1024; // chosen as it's the max UDP packet

        /// <summary>
        /// The smallest length allocated for a segment (not including the
        /// internal segment header).  This is expected to be a power of 2.
        /// </summary>
        public static uint MinSegmentSize
        {
            get { return _minSegmentSize; }
            set
            {
                if (memoryPools != null)
                {
                    throw new InvalidOperationException("cannot be set when pools are in use");
                }
                if (!BitUtils.IsPowerOf2(value))
                {
                    throw new ArgumentException("must be a power of 2");
                }
                if (value > _maxSegmentSize)
                {
                    throw new ArgumentException("cannot be greater than MaxSegmentSize");
                }
                _minSegmentSize = value;
            }
        }

        /// <summary>
        /// The maximum length allocated for a segment (not including the
        /// internal segment header).  This is expected to be a power of 2.
        /// </summary>
        public static uint MaxSegmentSize
        {
            get { return _maxSegmentSize; }
            set
            {
                if (memoryPools != null)
                {
                    throw new InvalidOperationException("cannot be set when pools are in use");
                }
                if (!BitUtils.IsPowerOf2(value))
                {
                    throw new ArgumentException("must be a power of 2");
                }
                if (value < _minSegmentSize)
                {
                    throw new ArgumentException("cannot be greater than MinSegmentSize");
                }
                _maxSegmentSize = value;
            }
        }

        /// <summary>
        /// Reserve this many bytes at the beginning of each segment to
        /// support prepending additional data headers in-place.
        /// </summary>
        public static uint ReservedInitialBytes = 16;

        /// <summary>
        /// Although the memoryPools, once allocated, can be accessed in a thread-safe
        /// manner, there is a possibility of a race condition until they are initialized.
        /// This object is used to synchronize the initialization
        /// </summary>
        protected static object staticLockObject = new object();
        protected static Pool<byte[]>[] memoryPools;

        /// <summary>
        /// Each segment has 4 bytes for recording the ref count of the segment.
        /// The first 3 bytes should be 0xC0FFEE; this is used for detecting 
        /// segments not allocated through these functions and for possible
        /// segment overruns.  The 4th byte records the ref count.  Byte arrays
        /// can be reused when this goes to 0.
        /// </summary>
        protected static readonly byte[] segmentHeader = { 0xC0, 0xFF, 0xEE };  // valid header
        protected static readonly byte[] deadbeef = { 0xDE, 0xAD, 0xBE, 0xEF }; // dead segment
        protected const int HeaderSize = 4; // # bytes in the internal segment header
        protected const int RefCountLocation = 3;

        /// <summary>
        /// Allocate a segment of at least <see cref="minimumLength"/> bytes.  This must be
        /// less than <see cref="MaxSegmentSize"/>.  The actual byte array allocated may be
        /// larger than the requested length.  This segment has a ref
        /// count of 0 -- it must be retained such as through an
        /// <see cref="AppendSegment"/> or explicitly though <see cref="IncrementRefCount"/>.
        /// </summary>
        /// <param name="minimumLength">the minimum number of bytes required</param>
        /// <returns>a suitable byte segment</returns>
        /// <exception cref="ArgumentOutOfRangeException">thrown when requesting an
        ///     invalid length</exception>
        protected static ArraySegment<byte> AllocateSegment(uint minimumLength)
        {
            if (memoryPools == null) { InitMemoryPools(); }
            if (minimumLength == 0 || minimumLength > _maxSegmentSize)
            {
                throw new ArgumentOutOfRangeException("minimumLength");
            }
            uint reservedInitialSpace = Math.Min(ReservedInitialBytes, MaxSegmentSize - minimumLength);
            byte[] allocd = memoryPools[PoolIndex(reservedInitialSpace + minimumLength)].Obtain();
            Debug.Assert(allocd.Length - HeaderSize - reservedInitialSpace >= minimumLength);
            // Copy over the segment header to indicate that it is a valid segment
            Buffer.BlockCopy(segmentHeader, 0, allocd, 0, segmentHeader.Length);
            // A new segment's ref count is 0; will be incremented when referenced
            // such as by TransportPacket.AddSegment()
            allocd[RefCountLocation] = 0;
            return new ArraySegment<byte>(allocd, HeaderSize + (int)reservedInitialSpace, (int)minimumLength);
        }

        /// <summary>
        /// Increment the reference count on the provided segment.
        /// </summary>
        /// <param name="segment">the referenced segment</param>
        protected static void IncrementRefCount(ArraySegment<byte> segment)
        {
            if (!IsManagedSegment(segment)) { return; }
            lock (segment.Array)
            {
                segment.Array[RefCountLocation]++;
            }
        }
        
        /// <summary>
        /// Decrement the reference count on the provided segment.
        /// </summary>
        /// <param name="segment">the referenced segment</param>
        protected static void DecrementRefCount(ArraySegment<byte> segment)
        {
            if (!IsManagedSegment(segment)) { return; }
            lock (segment.Array)
            {
                segment.Array[RefCountLocation]--;
            }
        }

        protected static uint GetRefCount(ArraySegment<byte> segment)
        {
            lock (segment.Array)
            {
                Debug.Assert(IsManagedSegment(segment));
                return segment.Array[RefCountLocation];
            }
        }

        protected static void ReleaseSegment(ArraySegment<byte> segment)
        {
            if (!IsManagedSegment(segment)) { return; }
            Debug.Assert(BitUtils.IsPowerOf2((uint)segment.Array.Length - HeaderSize));
            lock (segment.Array)
            {
                if (segment.Array[RefCountLocation] > 0)
                {
                    segment.Array[RefCountLocation]--;
                }
                if (segment.Array[RefCountLocation] > 0)
                {
                    return;
                }

                Debug.Assert(segment.Array.Length - HeaderSize <= _maxSegmentSize);
                memoryPools[PoolIndex((uint)segment.Array.Length - HeaderSize)].Return(segment.Array);
            }
        }

        /// <summary>
        /// This method is only meant for testing purposes.
        /// </summary>
        /// <param name="segment">the segment</param>
        /// <returns>true if the segment is a valid packet segment</returns>
        public static bool IsManagedSegment(ArraySegment<byte> segment)
        {
            if (segment.Offset < HeaderSize) { return false; }
            for (int i = 0; i < segmentHeader.Length; i++)
            {
                if (segment.Array[i] != segmentHeader[i]) { return false; }
            }
            return true;
        }

        private static void InitMemoryPools()
        {
            lock (staticLockObject)
            {
                if(memoryPools != null) { return; }
                // Number of bits required (ceil(lg(n)) = # highest bit + 1
                int numSegs = 1 + PoolIndex(_maxSegmentSize);
                memoryPools = new Pool<byte[]>[numSegs];
                for(int i = 0; i < numSegs; i++)
                {
                    // Needed to push this to a new function to ensure the
                    // Pool's lambda's had the right variable in scope.
                    // Weirdness.
                    memoryPools[i] = CreatePool((1u << i) * _minSegmentSize);
                }
            }
        }

        private static Pool<byte[]> CreatePool(uint segSize)
        {
            return new Pool<byte[]>(0, 5,
                () => new byte[HeaderSize + segSize],
                // RehabilitateSegment,
                b =>
                {
                    Debug.Assert(b.Length == HeaderSize + segSize);
                    RehabilitateSegment(b);
                }, RehabilitateSegment);
        }

        /// <summary>
        /// Return the approopriate pool for a buffer of length <see cref="segLength"/>.
        /// </summary>
        /// <param name="segLength"></param>
        /// <returns></returns>
        private static int PoolIndex(uint segLength)
        {
            Debug.Assert(0 < segLength && segLength <= _maxSegmentSize);
            return BitUtils.HighestBitSet((Math.Min(segLength, _maxSegmentSize) - 1) / _minSegmentSize) + 1;
        }

#if DEBUG
        /// <summary>
        /// This is public for testing purposes only.
        /// </summary>
        /// <param name="segLength"></param>
        public static int TestingPoolIndex(uint segLength)
        {
            return PoolIndex(segLength);
        }

        /// <summary>
        /// This is public for testing purposes only.
        /// </summary>
        public static Pool<byte[]>[] TestingDiscardPools()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return Interlocked.Exchange(ref memoryPools, null);
        }
#endif

        private static void RehabilitateSegment(byte[] seg)
        {
#if DEBUG
            // Write over memory so that any bad users see problems
            for (int i = 0; i < seg.Length; i++)
            {
                seg[i] = deadbeef[i % deadbeef.Length];
            }
#else
            deadbeef.CopyTo(seg, 0);
#endif
        }

        #endregion

        #region IList<ArraySegment<byte>> implementation

        void ICollection<ArraySegment<byte>>.Add(ArraySegment<byte> item)
        {
            Append(item);
        }

        int IList<ArraySegment<byte>>.IndexOf(ArraySegment<byte> item)
        {
            ValidateAndSync();
            return list.IndexOf(item);
        }

        void IList<ArraySegment<byte>>.Insert(int index, ArraySegment<byte> item)
        {
            ValidateAndSync();
            list.Insert(index, item);
            length += item.Count;
            IncrementRefCount(item);
        }

        void IList<ArraySegment<byte>>.RemoveAt(int index)
        {
            ValidateAndSync();
            ArraySegment<byte> segment = list[index];
            length -= list[index].Count;
            list.RemoveAt(index);
            ReleaseSegment(segment);
        }

        ArraySegment<byte> IList<ArraySegment<byte>>.this[int index]
        {
            get {
                ValidateAndSync();
                return list[index];
            }
            set
            {
                ValidateAndSync();
                IncrementRefCount(value);
                ReleaseSegment(list[index]);
                length -= list[index].Count;
                list[index] = value;
                length += value.Count;
            }
        }

        bool ICollection<ArraySegment<byte>>.Contains(ArraySegment<byte> item)
        {
            ValidateAndSync();
            return list.Contains(item);
        }

        void ICollection<ArraySegment<byte>>.CopyTo(ArraySegment<byte>[] array, int arrayIndex)
        {
            ValidateAndSync();
            list.CopyTo(array, arrayIndex);
        }

        bool ICollection<ArraySegment<byte>>.Remove(ArraySegment<byte> item)
        {
            ValidateAndSync();
            if (!list.Remove(item)) { return false; }
            DecrementRefCount(item);
            length -= item.Count;
            return true;
        }

        int ICollection<ArraySegment<byte>>.Count
        {
            get {
                ValidateAndSync();
                return list.Count;
            }
        }

        bool ICollection<ArraySegment<byte>>.IsReadOnly
        {
            get { return false; }
        }

        IEnumerator<ArraySegment<byte>> IEnumerable<ArraySegment<byte>>.GetEnumerator()
        {
            ValidateAndSync();
            return list.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            ValidateAndSync();
            return list.GetEnumerator();
        }

        #endregion

        /// <summary>
        /// A destructive read stream on a packet. This stream is not seekable,
        /// and as such, Length and Position don't actually have to work as
        /// might be expected (i.e., Position records the number of bytes retrieved
        /// from the stream, and length is the total number of bytes available since
        /// the stream was created).  For this stream, Length and Position are
        /// *relatively* correct, but not absolutely correct.  That is,
        /// Length - Position will return the correct number of bytes available,
        /// but Length will not necessarily be the packet's *original* length,
        /// nor Position the number of bytes retrieved from this stream.
        /// </summary>
        protected class ReadStream : Stream
        {
            protected TransportPacket packet;

            /// <summary>
            /// An index into <see cref="activeSegment"/>, relative to 
            /// <see cref="activeSegment"/>'s <see cref="ArraySegment{T}.Offset"/>.
            /// If &gt;= 0, then we are currently processing a segment.  If &lt; 0, then
            /// we are not currently processing a segment.
            /// </summary>
            protected int activeOffset = -1;

            /// <summary>
            /// The segment being actively processed.
            /// </summary>
            protected ArraySegment<byte> activeSegment;

            protected internal ReadStream(TransportPacket p)
            {
                packet = p;
            }

            public override bool CanRead { get { return true; } }

            // Hmm, we are partially seekable, in that we can seek forward...
            public override bool CanSeek { get { return false; } }

            public override bool CanWrite { get { return false; } }

            public override long Length
            {
                get
                {
                    if (packet == null) { throw new ObjectDisposedException("stream was closed"); }
                    // must use packet.length: calling the property will otherwise call commit.
                    return packet.length + (activeOffset < 0 ? 0 : activeSegment.Count);
                }
            }

            public override long Position
            {
                get
                {
                    if (packet == null) { throw new ObjectDisposedException("stream was closed"); }
                    return activeOffset < 0 ? 0 : activeOffset;
                }
                set { Seek(value, SeekOrigin.Current); }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (packet == null) { throw new ObjectDisposedException("stream was closed"); }
                int bytesRead = 0;
                while (count > 0 && Length > 0)
                {
                    if (activeOffset < 0)
                    {
                        activeSegment = packet.TransferFirstSegment();
                        activeOffset = 0;
                    }

                    int available = Math.Min(count, activeSegment.Count - activeOffset);
                    Debug.Assert(available > 0);
                    //if (buffer.Length < offset + count)
                    //{
                    //    throw new ArgumentException("buffer does not have sufficient capacity", "buffer");
                    //}
                    Buffer.BlockCopy(activeSegment.Array, activeSegment.Offset + activeOffset, 
                        buffer, offset, available);
                    offset += available;
                    count -= available;
                    bytesRead += available;
                    activeOffset += available;
                    Debug.Assert(activeOffset <= activeSegment.Count);
                    if(activeOffset == activeSegment.Count)
                    {
                        ReleaseSegment(activeSegment);
                        activeOffset = -1;
                    }
                }
                return bytesRead;
            }

            public override void Flush()
            {
                if(packet == null || activeOffset < 0) { return; }
                Debug.Assert(activeOffset < activeSegment.Count);
                packet.PrependSegment(new ArraySegment<byte>(activeSegment.Array,
                    activeSegment.Offset + activeOffset, activeSegment.Count - activeOffset));
                // PrependSegment increments the ref count, so we need to reduce it
                // since we had the retain count transferred by TransferFirstPacket
                if (IsManagedSegment(activeSegment))
                {
                    DecrementRefCount(activeSegment);
                    Debug.Assert(GetRefCount(activeSegment) != 0);
                }
                activeOffset = -1;
            }

            public override void Close()
            {
                Flush();
                packet = null;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();

                // We could support forward movements?
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
                // packet.RemoveBytes((int)value, packet.Length - (int)value);
                throw new NotImplementedException();
            }


        }

        /// <summary>
        /// A writeable stream on to a packet.  This stream will grow the packet
        /// as necessary; users must ensure they call <see cref="Flush"/> to
        /// append any new data.
        /// </summary>
        protected class WriteStream : Stream
        {
            protected TransportPacket packet;
            protected ArraySegment<byte> interim = default(ArraySegment<byte>);
            protected int position = 0;
            protected int newLength = -1;

            protected internal WriteStream(TransportPacket p)
            {
                packet = p;
            }

            public override bool CanRead { get { return false; } }

            public override bool CanSeek { get { return true; } }

            public override bool CanWrite { get { return true; } }

            public override void Flush()
            {
                if(packet == null || newLength < 0) { return; }
                Debug.Assert(newLength >= packet.length);
                if (newLength > packet.length)
                {
                    packet.AppendSegment(new ArraySegment<byte>(interim.Array, interim.Offset,
                        newLength - packet.length));
                    newLength = -1;
                }
            }

            public override void Close()
            {
                Flush();
                packet = null;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                if (packet == null) { throw new ObjectDisposedException("stream was closed"); }
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
                if (offset < 0 || offset > Length) { throw new ArgumentException("offset is out of range"); }
                position = (int)offset;
                return position;
            }

            public override void SetLength(long value)
            {
                if (packet == null) { throw new ObjectDisposedException("stream was closed"); }
                if (value >= packet.length)
                {
                    // if it's within our interim buffer, then just trim or grow as apporpriate.
                    // otherwise flush and allocate whatever new stuff is needed
                    if (value < newLength)  // if newLength < 0 then value > newLength
                    {
                        newLength = (int)value; // and will be trimmed appropriately in Flush()
                    }
                    else if(newLength >= 0 && 
                        (int)value - packet.length <= interim.Array.Length - interim.Offset)
                    {
                        // value fits within the capacity of the interim buffer
                        newLength = (int)value;
                    } 
                    else
                    {
                        // Flush out any interim buffers and grow as necessary
                        Flush();
                        packet.Grow((int)value);
                    }
                }
                else
                {
                    Flush();    // could be cleverer and deallocate interim if necessary
                    // The new value requires trimming the packet.
                    packet.RemoveBytes((int)value, packet.length - (int)value);
                    position = Math.Min((int)value, packet.length);
                    Debug.Assert(newLength < 0);
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (packet == null) { throw new ObjectDisposedException("stream was closed"); }
                Flush();
                int numBytes = Math.Min(count, (int)Length - position);
                packet.CopyTo(position, buffer, offset, numBytes);
                position += numBytes;
                count -= numBytes;
                Debug.Assert(count == 0 || position == packet.Length);
                return numBytes;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (packet == null) { throw new ObjectDisposedException("stream was closed"); }
                Debug.Assert(position <= Length);
                // Write out whatever we can to the packet, then whatever we need 
                // to our interim buffer
                if (position < packet.length) {
                    int numBytes = Math.Min(count, packet.length - position);
                    packet.Replace(position, buffer, offset, numBytes);
                    count -= numBytes;
                    offset += numBytes;
                    position += numBytes;
                }
                Debug.Assert(position <= Length);
                Debug.Assert(count == 0 || packet.length == 0 || position >= packet.length);

                while (count > 0)
                {
                    // if position < packet.length then a Flush() has happened out of turn
                    Debug.Assert(position >= packet.length && position <= Length);
                    if (newLength < 0)
                    {
                        uint desiredSize = (uint)Math.Min(count, _maxSegmentSize);
                        interim = AllocateSegment(desiredSize);
                        newLength = packet.length;
                    }
                    Debug.Assert(newLength >= 0);
                    Debug.Assert(position >= packet.length);
                    int interimPosition = position - packet.length;
                    Debug.Assert(interimPosition >= 0);

                    int interimAvailable = interim.Array.Length - interim.Offset - interimPosition;
                    // There must be space available: if there was no space, then end of loop 
                    // causes a Flush, meaning that new space is allocated at the top of the loop.
                    Debug.Assert(interimAvailable > 0);

                    int numBytes = Math.Min(count, interimAvailable);
                    Buffer.BlockCopy(buffer, offset, interim.Array,
                        interim.Offset + interimPosition, numBytes);
                    position += numBytes;
                    offset += numBytes;
                    count -= numBytes;
                    interimPosition += numBytes;
                    newLength += numBytes;
                    // if no more available space, then flush the interim buffer so
                    // that a new interim buffer will be allocated at the top of the loop
                    if (interim.Array.Length == interim.Offset + interimPosition) { Flush(); }
                }
                Debug.Assert(count == 0 && position <= Length);
            }

            public override long Length
            {
                get
                {
                    if (packet == null) { throw new ObjectDisposedException("stream was closed"); }
                    return newLength < 0 ? packet.Length : newLength;
                }
            }

            public override long Position
            {
                get
                {
                    if(packet == null) { throw new ObjectDisposedException("stream was closed"); }
                    return position;
                }
                set { Seek(value, SeekOrigin.Begin); }
            }
        }
    }
}

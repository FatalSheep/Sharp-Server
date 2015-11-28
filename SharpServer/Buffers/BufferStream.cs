﻿using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using StringBuilder = System.Text.StringBuilder;
using BitConverter = System.BitConverter;
using Array = System.Array;

namespace SharpServer.Buffers {
    /// <summary>
    /// Enumerator that represents the different support datatypes of the BufferStream class.
    /// </summary>
    public enum BufferType {
        None, Bool, Byte, SByte,
        UInt16, Int16, UInt32, Int32,
        Single, Double, String, Bytes
    }

    /// <summary>
    /// Enumerator that represents the size of each datatype supported by the BufferStream class.
    /// </summary>
    public enum BufferTypeSize {
        None = 0, Bool = 1, Byte = 1, SByte = 1,
        UInt16 = 2, Int16 = 2, UInt32 = 4, Int32 = 4,
        Single = 4, Double = 8, String = -1, Bytes = -1
    }

    /// <summary>
    /// Provides a stream designed for reading TCP packets and UDP datagrams by type.
    /// </summary>
    public class BufferStream {
        private const byte bTrue = 1, bFalse = 0;
        private int fastAlign, fastAlignNot;
        private int iterator, length, alignment;
        private byte[] memory;
        
        /// <summary>
        /// Gets the underlying array of memory for this BufferStream.
        /// </summary>
        public byte[] Memory { get { return memory; } }
        /// <summary>
        /// Gets the length of the buffer in bytes.
        /// </summary>
        public int Length { get { return length; } }
        /// <summary>
        /// Gets the current iterator(read/write position) for this BufferStream.
        /// </summary>
        public int Iterator { get { return iterator; } }

        /// <summary>
        /// Instantiates an instance of the BufferStream class with the specified stream length and alignment.
        /// </summary>
        /// <param name="length">Length of the BufferStream in bytes.</param>
        /// <param name="alignment">Alignment of the BufferStream in bytes.</param>
        public BufferStream( int length, int alignment ) {
            fastAlign = alignment - 1;
            fastAlignNot = ~fastAlign;
            this.length = length;
            this.alignment = alignment;
            memory = new byte[ AlignedIterator( length, alignment ) ];
            iterator = 0;
        }

        /// <summary>
        /// (forced inline) Takes an iterator and aligns it to the specified alignment size.
        /// </summary>
        /// <param name="iterator">Read/write position.</param>
        /// <param name="alignment">Size in bytes to align the iterator too.</param>
        /// <returns>The aligned iterator value.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int AlignedIterator( int iterator, int alignment ) {
            return ( ( iterator + ( alignment - 1 ) ) & ~( alignment - 1 ) );
        }

        /// <summary>
        /// (forced inline) Checks if the specified index with the specified length in bytes is within bounds of the buffer.
        /// </summary>
        /// <param name="iterator">Read/write position.</param>
        /// <param name="length">Index to check the bounds of.</param>
        /// <param name="alignment">Size in bytes to align the iterator too.</param>
        /// <returns></returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public bool IsWithinMemoryBounds( int iterator, int length, bool align = true ) {
            int iterBegin = ( align ) ? ( ( iterator + fastAlign ) & fastAlignNot ) : iterator;
            int iterEnd = iterBegin + length;
            return ( iterBegin < 0 || iterEnd >= length );
        }

        /// <summary>
        /// (forced inline) Returns the size of the specified type uspported by BufferStream.
        /// </summary>
        /// <param name="type">Type to get the size of.</param>
        /// <returns>Returns size of the specified type.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public int SizeFromType( BufferType type ) {
            switch( type ) {
                case BufferType.Bool:
                    return (int)BufferTypeSize.Bool;
                case BufferType.Byte:
                    return (int)BufferTypeSize.Byte;
                case BufferType.SByte:
                    return (int)BufferTypeSize.SByte;
                case BufferType.UInt16:
                    return (int)BufferTypeSize.UInt16;
                case BufferType.Int16:
                    return (int)BufferTypeSize.Int16;
                case BufferType.UInt32:
                    return (int)BufferTypeSize.UInt32;
                case BufferType.Int32:
                    return (int)BufferTypeSize.Int32;
                case BufferType.Single:
                    return (int)BufferTypeSize.Single;
                case BufferType.Double:
                    return (int)BufferTypeSize.Double;
                default:
                    return (int) BufferTypeSize.None;
            }
        }

        /// <summary>
        /// Allocates a new block of memory for the BufferStream with the specified length and alignment--freeing the old one if it exists.
        /// </summary>
        /// <param name="length">Length in bytes of the new block of memory.</param>
        /// <param name="alignment">Alignment in bytes to align the block of memory too.</param>
        public void Allocate( int length, int alignment ) {
            if ( memory != null ) Deallocate();

            memory = new byte[ AlignedIterator( length, alignment ) ];
            this.alignment = alignment;
            this.length = length;
        }

        /// <summary>
        /// Frees up the existing block of memory and sets the iterator to 0.
        /// </summary>
        public void Deallocate() {
            memory = null;
            iterator = 0;
        }

        /// <summary>
        /// Sets all elements in this buffer to 0.
        /// </summary>
        /// <exception cref="System.ArgumentNullException"/>
        public void ZeroMemory() {
            Array.Clear( memory, 0, memory.Length );
        }

        /// <summary>
        /// Sets all elements in this buffer to the specified value.
        /// </summary>
        /// <param name="value">The value to zero the memory too.</param>
        public void ZeroMemory( byte value ) {
            for( int i = 0; i++ < memory.Length; ) memory[ i ] = value;
        }

        /// <summary>
        /// Creates a copy of this buffer and all it's contents.
        /// </summary>
        /// <returns>A new clone BufferStream.</returns>
        /// <exception cref="System.ArgumentNullException"/>
        public BufferStream CloneBufferStream() {
            BufferStream clone = new BufferStream( memory.Length, alignment );
            Array.Copy( memory, clone.Memory, memory.Length );
            clone.iterator = iterator;
            return clone;
        }

        /// <summary>
        /// Copies the specified number of bytes from this buffer to the destination buffer, given the start position(s) in each buffer.
        /// </summary>
        /// <param name="destBuffer">Buffer to copy the contents of this buffer too.</param>
        /// <param name="srceIndex">Start position to begin copying the data from in this buffer.</param>
        /// <param name="destIndex">Start position to begin copying the data to in the destination buffer.</param>
        /// <param name="length">Number of bytes to copy from this buffer to the destination buffer.</param>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <exception cref="System.ArgumentException"/>
        public void BlockCopy( BufferStream destBuffer, int srceIndex, int destIndex, int length ) {
            Array.Copy( memory, srceIndex, destBuffer.Memory, destIndex, length );
        }

        /// <summary>
        /// Copes the specified number of bytes from this buffer to the destination buffer, given the shared start position for both buffers.
        /// </summary>
        /// <param name="destBuffer">Buffer to copy the contents of this buffer too.</param>
        /// <param name="startIndex">Shared start index of both buffers to start copying data from/to.</param>
        /// <param name="length">Number of bytes to copy from this buffer to the destination buffer.</param>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <exception cref="System.ArgumentException"/>
        public void BlockCopy( BufferStream destBuffer, int startIndex, int length ) {
            Array.Copy( memory, startIndex, destBuffer.Memory, startIndex, length );
        }

        /// <summary>
        /// Copes the specified number of bytes from the start of this buffer to the start of the destination buffer.
        /// </summary>
        /// <param name="destBuffer">Buffer to copy the contents of this buffer too.</param>
        /// <param name="length">Number of bytes to copy from this buffer to the destination buffer.</param>
        /// <exception cref="System.ArgumentNullException"/>
        /// <exception cref="System.ArgumentOutOfRangeException"/>
        /// <exception cref="System.ArgumentException"/>
        public void BlockCopy( BufferStream destBuffer, int length ) {
            Array.Copy( memory, destBuffer.Memory, length );
        }

        /// <summary>
        /// Copies the entire contents of this buffer to the destination buffer.
        /// </summary>
        /// <param name="destBuffer">Buffer to copy the contents of this buffer too.</param>
        public void BlockCopy( BufferStream destBuffer ) {
            int length = ( destBuffer.Memory.Length > memory.Length ) ? memory.Length : destBuffer.Memory.Length;
            Array.Copy( memory, destBuffer.Memory, length );
        }

        /// <summary>
        /// Resizes the block of memory for this buffer.
        /// </summary>
        /// <param name="size">Size in bytes of the resized block of memory.</param>
        public void ResizeBuffer( int size ) {
            Array.Resize<byte>( ref memory, size );
            length = memory.Length;
        }

        /// <summary>
        /// Writes a value of the specified type to this buffer.
        /// </summary>
        /// <param name="value">BOOLEAN value to be written.</param>
        /// <exception cref="System.IndexOutOfRangeException"/>
        public void Write( bool value ) {
            memory[ iterator++ ] = ( value ) ? bTrue : bFalse;
            iterator = ( iterator + fastAlign ) & fastAlignNot;
        }

        /// <summary>
        /// Writes a value of the specified type to this buffer.
        /// </summary>
        /// <param name="value">BYTE value to be written.</param>
        /// <exception cref="System.IndexOutOfRangeException"/>
        public void Write( byte value ) {
            memory[ iterator++ ] = value;
            iterator = ( iterator + fastAlign ) & fastAlignNot;
        }

        /// <summary>
        /// Writes a value of the specified type to this buffer.
        /// </summary>
        /// <param name="value">SBYTE value to be written.</param>
        /// <exception cref="System.IndexOutOfRangeException"/>
        public void Write( sbyte value ) {
            memory[ iterator++ ] = (byte)value;
            iterator = ( iterator + fastAlign ) & fastAlignNot;
        }

        /// <summary>
        /// Writes a value of the specified type to this buffer.
        /// </summary>
        /// <param name="value">USHORT value to be written.</param>
        /// <exception cref="System.IndexOutOfRangeException"/>
        public void Write( ushort value ) {
            memory[ iterator++ ] = (byte)value;
            memory[ iterator++ ] = (byte)( value >> 8 );
            iterator = ( iterator + fastAlign ) & fastAlignNot;
        }

        /// <summary>
        /// Writes a value of the specified type to this buffer.
        /// </summary>
        /// <param name="value">SHORT value to be written.</param>
        /// <exception cref="System.IndexOutOfRangeException"/>
        public void Write( short value ) {
            memory[ iterator++ ] = (byte)value;
            memory[ iterator++ ] = (byte)( value >> 8 );
            iterator = ( iterator + fastAlign ) & fastAlignNot;
        }

        /// <summary>
        /// Writes a value of the specified type to this buffer.
        /// </summary>
        /// <param name="value">UINT value to be written.</param>
        /// <exception cref="System.IndexOutOfRangeException"/>
        public void Write( uint value ) {
            memory[ iterator++ ] = (byte)value;
            memory[ iterator++ ] = (byte)( value >> 8 );
            memory[ iterator++ ] = (byte)( value >> 16 );
            memory[ iterator++ ] = (byte)( value >> 24 );
            iterator = ( iterator + fastAlign ) & fastAlignNot;
        }

        /// <summary>
        /// Writes a value of the specified type to this buffer.
        /// </summary>
        /// <param name="value">INT value to be written.</param>
        /// <exception cref="System.IndexOutOfRangeException"/>
        public void Write( int value ) {
            memory[ iterator++ ] = (byte)value;
            memory[ iterator++ ] = (byte)( value >> 8 );
            memory[ iterator++ ] = (byte)( value >> 16 );
            memory[ iterator++ ] = (byte)( value >> 24 );
            iterator = ( iterator + fastAlign ) & fastAlignNot;
        }

        /// <summary>
        /// Writes a value of the specified type to this buffer.
        /// </summary>
        /// <param name="value">FLOAT value to be written.</param>
        /// <exception cref="System.IndexOutOfRangeException"/>
        public void Write( float value ) {
            byte[] bytes = BitConverter.GetBytes( value );
            for( int i = 0; i < bytes.Length; i++ ) memory[ iterator++ ] = bytes[ i ];
            iterator = ( iterator + fastAlign ) & fastAlignNot;
        }

        /// <summary>
        /// Writes a value of the specified type to this buffer.
        /// </summary>
        /// <param name="value">DOUBLE value to be written.</param>
        /// <exception cref="System.IndexOutOfRangeException"/>
        public void Write( double value ) {
            byte[] bytes = BitConverter.GetBytes( value );
            for( int i = 0; i < bytes.Length; i++ ) memory[ iterator++ ] = bytes[ i ];
            iterator = ( iterator + fastAlign ) & fastAlignNot;
        }

        /// <summary>
        /// Writes a value of the specified type to this buffer.
        /// </summary>
        /// <param name="value">STRING value to be written.</param>
        /// <exception cref="System.IndexOutOfRangeException"/>
        public void Write( string value ) {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes( (string)value );
            for( int i = 0; i < bytes.Length; i++ ) { memory[ iterator++ ] = bytes[ i ]; }
            memory[ iterator++ ] = 0;
            iterator = ( iterator + fastAlign ) & fastAlignNot;
        }
        
        /// <summary>
        /// Writes a value of the specified type to this buffer.
        /// </summary>
        /// <param name="value">BYTE[] array to be written.</param>
        /// <exception cref="System.IndexOutOfRangeException"/>
        public void Write( byte[] value ) {
            for( int i = 0; i < value.Length; i ++ ) memory[ iterator++ ] = value[ i ];
            iterator = ( iterator + fastAlign ) & fastAlignNot;
        }

        /// <summary>
        /// Reads the specified type from the buffer.
        /// </summary>
        /// <param name="type">Type to read from the buffer.</param>
        /// <param name="length">(optional) Length of the type (bytep[] only).</param>
        /// <returns>Returns the value of the type read from the buffer.</returns>
        /// <exception cref="System.IndexOutOfRangeException"/>
        public object Read( BufferType type, int length = 0 ) {
            object value = null;

            switch( type ) {
                case BufferType.Bool:
                    value = memory[ iterator ] >= 0;
                    iterator += (int)BufferTypeSize.Bool;
                break;
                case BufferType.Byte:
                    value = memory[ iterator ];
                    iterator += (int)BufferTypeSize.Byte;
                break;
                case BufferType.SByte:
                    value = (sbyte)memory[ iterator ];
                    iterator += (int)BufferTypeSize.SByte;
                break;
                case BufferType.UInt16:
                    value = BitConverter.ToUInt16( memory, iterator );
                    iterator += (int)BufferTypeSize.UInt16;
                break;
                case BufferType.Int16:
                    value = BitConverter.ToInt16( memory, iterator );
                    iterator += (int)BufferTypeSize.Int16;
                break;
                case BufferType.UInt32:
                    value = BitConverter.ToUInt32( memory, iterator );
                    iterator += (int)BufferTypeSize.UInt32;
                break;
                case BufferType.Int32:
                    value = BitConverter.ToInt32( memory, iterator );
                    iterator += (int)BufferTypeSize.Int32;
                break;
                case BufferType.Single:
                    value = BitConverter.ToSingle( memory, iterator );
                    iterator += (int)BufferTypeSize.Single;
                break;
                case BufferType.Double:
                    value = BitConverter.ToDouble( memory, iterator );
                    iterator += (int)BufferTypeSize.Double;
                break;
                case BufferType.String:
                    StringBuilder str = new StringBuilder();
                    
                    for( char c = '\0'; iterator < length; ) {
                        c = (char)memory[ iterator++ ];
                        if ( c == '\0' ) break;
                        str.Append( c );
                    }

                    value = str.ToString();
                break;
                case BufferType.Bytes:
                    byte[] bytes = new byte[ length ];
                    for( int i = 0; i < length; i++ ) bytes[ i ] = memory[ iterator ++ ];
                    value = bytes;
                break;
            }

            iterator = ( iterator + fastAlign ) & fastAlignNot;
            return value;
        }

        /// <summary>
        /// Sets the iterator(read/write position) to the specified index, aligned to this buffer's alignment.
        /// </summary>
        /// <param name="iterator">Index to set the iterator to.</param>
        public void Seek( int iterator ) {
            this.iterator = ( iterator + fastAlign ) & fastAlignNot;
        }

        /// <summary>
        /// Sets the iterator(read/write position) to the specified index, aligned to this buffer's alignment if alignment is specified as true.
        /// </summary>
        /// <param name="iterator">Index to set the iterator to.</param>
        /// <param name="align">Whether to align the iterator or not.</param>
        public void Seek( int iterator, bool align = false ) {
            this.iterator = ( align ) ? ( iterator + fastAlign ) & fastAlignNot : iterator;
        }
    }
}
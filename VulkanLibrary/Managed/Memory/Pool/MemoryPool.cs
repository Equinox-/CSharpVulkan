using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace VulkanLibrary.Managed.Memory.Pool
{
    public class MemoryPool
    {
        private const uint NullBlockHeader = uint.MaxValue;

        public struct Memory : IPooledMemory
        {
            /// <summary>
            /// Points to an element in <see cref="MemoryPool._blocks"/>.
            /// </summary>
            internal readonly uint BlockId;

            /// <summary>
            /// Offset in memory for this handle.
            /// </summary>
            public ulong Offset
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get;
            }
            
            /// <summary>
            /// Size of this handle.
            /// </summary>
            public ulong Size
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get;
            }

            internal Memory(uint block, ulong offset, ulong size)
            {
                BlockId = block;
                Offset = offset;
                Size = size;
            }
        }

        private struct MemoryBlock
        {
            /// <summary>
            /// Points to an element in <see cref="MemoryPool._blocks"/>.
            /// </summary>
            internal uint PrevBlock, NextBlock;

            internal bool Free;
            internal ulong Offset, Size;

            internal void Zero()
            {
                PrevBlock = NextBlock = NullBlockHeader;
                Free = false;
                Offset = Size = 0;
            }
        }

        private readonly Stack<uint> _freeBlockPtr;

        /// <summary>
        /// Zero based index+1 of last used element in <see cref="_blocks"/>.
        /// </summary>
        private uint _firstContiguousFreeBlock;

        private MemoryBlock[] _blocks;
        private readonly uint _alignMask;

        /// <summary>
        /// Total capacity.
        /// </summary>
        public ulong Capacity { get; private set; }

        /// <summary>
        /// Remaining free space.
        /// </summary>
        public ulong FreeSpace { get; private set; }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="size">Size of the pool</param>
        /// <param name="alignmentBits">Number of bits that must be aligned.</param>
        public MemoryPool(ulong size, uint alignmentBits)
        {
            Capacity = size;
            FreeSpace = size;

            _freeBlockPtr = new Stack<uint>();
            _blocks = new MemoryBlock[System.Math.Min(size >> (int) alignmentBits, 128)];
            _alignMask = (uint) ((1 << (int) alignmentBits) - 1);
            _firstContiguousFreeBlock = 0;

            _blocks[FindFreeBlockHeader()] = new MemoryBlock()
            {
                PrevBlock = NullBlockHeader,
                NextBlock = NullBlockHeader,
                Free = true,
                Offset = 0,
                Size = AlignValue(size)
            };
        }

        private ulong AlignValue(ulong input)
        {
            return (input | _alignMask) + 1L;
        }

        private uint FindFreeBlockHeader()
        {
            if (_freeBlockPtr.Count > 0)
            {
                var res =_freeBlockPtr.Pop();
                _blocks[res].Zero();
                return res;
            }
            if (_firstContiguousFreeBlock >= _blocks.Length)
            {
                var newLength = _blocks.Length << 1;
                while (_firstContiguousFreeBlock >= newLength)
                    newLength <<= 1;
                Array.Resize(ref _blocks, newLength);
            }
            var blockIndex = _firstContiguousFreeBlock;
            _firstContiguousFreeBlock++;
            _blocks[blockIndex].Zero();
            return blockIndex;
        }

        private void FreeBlockHeader(uint index)
        {
            if (_firstContiguousFreeBlock - 1 == index)
                _firstContiguousFreeBlock = index;
            else
                _freeBlockPtr.Push(index);
        }

        public void Resize(ulong newSize)
        {
            if (newSize == Capacity)
                return;
            if (newSize <= Capacity)
                throw new ArgumentException("Cannot shrink memory pool", nameof(newSize));
            uint cid = 0;
            while (_blocks[cid].NextBlock != NullBlockHeader)
                cid = _blocks[cid].NextBlock;
            var expand = newSize - Capacity;
            if (_blocks[cid].Free)
                _blocks[cid].Size += expand;
            else
            {
                var next = FindFreeBlockHeader();
                _blocks[cid].NextBlock = next;
                _blocks[next].PrevBlock = cid;
                _blocks[next].Free = true;
                _blocks[next].Offset = _blocks[cid].Offset + _blocks[cid].Size;
                _blocks[next].Size = expand;
            }
            FreeSpace += expand;
            Capacity += expand;
        }

        /// <summary>
        /// Describes the memory blocks in this pool 
        /// </summary>
        /// <returns>Block descriptions</returns>
        public string ToStringVerbose()
        {
            var s = new StringBuilder("MemoryPool._blocks: [\n");
            for (uint i = 0; i < _firstContiguousFreeBlock; i++)
                if (!_freeBlockPtr.Contains(i))
                {
                    var free = _blocks[i].Free ? "free" : "used";
                    s.AppendLine(
                        $"\t{i,4}: {free} prev: {_blocks[i].PrevBlock,-4} next: {_blocks[i].NextBlock,-4} offset: {_blocks[i].Offset,8:X} end: {_blocks[i].Offset + _blocks[i].Size,8:X}");
                }
            s.AppendLine("]");
            return s.ToString();
        }

        public Memory Allocate(ulong size)
        {
            size = AlignValue(size);
            if (FreeSpace < size)
                throw new OutOfMemoryException("No space left");
            uint cid = 0;
            while (cid != NullBlockHeader)
            {
                if (_blocks[cid].Free && _blocks[cid].Size >= size)
                {
                    if (_blocks[cid].Size >= size + AlignValue(0))
                    {
                        var splitSize = _blocks[cid].Size - size;
                        uint next = FindFreeBlockHeader();

                        _blocks[cid].Size = size;
                        _blocks[cid].Free = false;
                        if (_blocks[cid].NextBlock != NullBlockHeader)
                            _blocks[_blocks[cid].NextBlock].PrevBlock = next;

                        _blocks[next] = new MemoryBlock()
                        {
                            NextBlock = _blocks[cid].NextBlock,
                            PrevBlock = cid,
                            Free = true,
                            Offset = _blocks[cid].Offset + _blocks[cid].Size,
                            Size = splitSize
                        };

                        // Update *after* we create the new block
                        _blocks[cid].NextBlock = next;
                    }
                    else
                    {
                        _blocks[cid].Free = false;
                    }
                    FreeSpace -= _blocks[cid].Size;
                    return new Memory(cid, _blocks[cid].Offset, _blocks[cid].Size);
                }
                cid = _blocks[cid].NextBlock;
            }
            throw new OutOfMemoryException("No space left");
        }

        private MemoryBlock? Block(uint id)
        {
            return id == NullBlockHeader ? null : new MemoryBlock?(_blocks[id]);
        }

        public void Free(Memory handle)
        {
            _blocks[handle.BlockId].Free = true;
            FreeSpace += _blocks[handle.BlockId].Size;

            var block = _blocks[handle.BlockId];
            var prevBlock = Block(block.PrevBlock);
            var nextBlock = Block(block.NextBlock);

            if (prevBlock.HasValue && prevBlock.Value.Free)
            {
                if (nextBlock.HasValue && nextBlock.Value.Free)
                {
                    _blocks[block.PrevBlock].NextBlock = nextBlock.Value.NextBlock;
                    if (nextBlock.Value.NextBlock != NullBlockHeader)
                        _blocks[nextBlock.Value.NextBlock].PrevBlock = block.PrevBlock;
                    _blocks[block.PrevBlock].Size =
                        nextBlock.Value.Offset + nextBlock.Value.Size - prevBlock.Value.Offset;
                    FreeBlockHeader(block.NextBlock);
                    FreeBlockHeader(handle.BlockId);
                }
                else
                {
                    if (block.NextBlock != NullBlockHeader)
                        _blocks[block.NextBlock].PrevBlock = block.PrevBlock;
                    _blocks[block.PrevBlock].NextBlock = block.NextBlock;
                    _blocks[block.PrevBlock].Size = block.Offset + block.Size - prevBlock.Value.Offset;
                    FreeBlockHeader(handle.BlockId);
                }
            }
            else if (nextBlock.HasValue && nextBlock.Value.Free)
            {
                _blocks[handle.BlockId].NextBlock = nextBlock.Value.NextBlock;
                _blocks[handle.BlockId].Size = nextBlock.Value.Offset + nextBlock.Value.Size - block.Offset;
                if (nextBlock.Value.NextBlock != NullBlockHeader)
                    _blocks[nextBlock.Value.NextBlock].PrevBlock = handle.BlockId;
                FreeBlockHeader(block.NextBlock);
            }
        }
    }
}
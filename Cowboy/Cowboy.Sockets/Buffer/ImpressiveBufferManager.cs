using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Buffer
{
    /// <summary>
    /// A manager to handle buffers for the socket connections.
    /// </summary>
    /// <remarks>
    /// When used in an async call a buffer is pinned. Large numbers of pinned buffers
    /// cause problem with the GC (in particular it causes heap fragmentation).
    /// This class maintains a set of large segments and gives clients pieces of these
    /// segments that they can use for their buffers. The alternative to this would be to
    /// create many small arrays which it then maintained. This methodology should be slightly
    /// better than the many small array methodology because in creating only a few very
    /// large objects it will force these objects to be placed on the LOH. Since the
    /// objects are on the LOH they are at this time not subject to compacting which would
    /// require an update of all GC roots as would be the case with lots of smaller arrays
    /// that were in the normal heap.
    /// </remarks>
    public class ImpressiveBufferManager
    {
        private const int TrialsCount = 100;

        private static ImpressiveBufferManager _defaultBufferManager;

        private readonly int _segmentChunks;
        private readonly int _chunkSize;
        private readonly int _segmentSize;
        private readonly bool _allowedToCreateMemory;

        private readonly ConcurrentStack<ArraySegment<byte>> _buffers = new ConcurrentStack<ArraySegment<byte>>();

        private readonly List<byte[]> _segments;
        private readonly object _creatingNewSegmentLock = new object();

        public static ImpressiveBufferManager Default
        {
            get
            {
                // default to 1024 1kb buffers if people don't want to manage it on their own;
                if (_defaultBufferManager == null)
                    _defaultBufferManager = new ImpressiveBufferManager(1024, 1024, 1);
                return _defaultBufferManager;
            }
        }

        public static void SetDefaultBufferManager(ImpressiveBufferManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException("manager");
            _defaultBufferManager = manager;
        }

        public int ChunkSize
        {
            get { return _chunkSize; }
        }

        public int SegmentsCount
        {
            get { return _segments.Count; }
        }

        public int SegmentChunksCount
        {
            get { return _segmentChunks; }
        }

        public int AvailableBuffers
        {
            get { return _buffers.Count; }
        }

        public int TotalBufferSize
        {
            get { return _segments.Count * _segmentSize; }
        }

        public ImpressiveBufferManager(int segmentChunks, int chunkSize)
            : this(segmentChunks, chunkSize, 1) { }

        public ImpressiveBufferManager(int segmentChunks, int chunkSize, int initialSegments)
            : this(segmentChunks, chunkSize, initialSegments, true) { }

        /// <summary>
        /// Constructs a new <see cref="ImpressiveBufferManager"></see> object
        /// </summary>
        /// <param name="segmentChunks">The number of chunks to create per segment</param>
        /// <param name="chunkSize">The size of a chunk in bytes</param>
        /// <param name="initialSegments">The initial number of segments to create</param>
        /// <param name="allowedToCreateMemory">If false when empty and checkout is called an exception will be thrown</param>
        public ImpressiveBufferManager(int segmentChunks, int chunkSize, int initialSegments, bool allowedToCreateMemory)
        {
            if (segmentChunks <= 0)
                throw new ArgumentException("segmentChunks");
            if (chunkSize <= 0)
                throw new ArgumentException("chunkSize");
            if (initialSegments < 0)
                throw new ArgumentException("initialSegments");

            _segmentChunks = segmentChunks;
            _chunkSize = chunkSize;
            _segmentSize = _segmentChunks * _chunkSize;

            _segments = new List<byte[]>();

            _allowedToCreateMemory = true;
            for (int i = 0; i < initialSegments; i++)
            {
                CreateNewSegment(true);
            }
            _allowedToCreateMemory = allowedToCreateMemory;
        }

        private void CreateNewSegment(bool forceCreation)
        {
            if (!_allowedToCreateMemory)
                throw new UnableToCreateMemoryException();

            lock (_creatingNewSegmentLock)
            {
                if (!forceCreation && _buffers.Count > _segmentChunks / 2)
                    return;

                var bytes = new byte[_segmentSize];
                _segments.Add(bytes);
                for (int i = 0; i < _segmentChunks; i++)
                {
                    var chunk = new ArraySegment<byte>(bytes, i * _chunkSize, _chunkSize);
                    _buffers.Push(chunk);
                }

                Console.WriteLine("Segments count: {0}, buffers count: {1}, should be when full: {2}",
                    _segments.Count,
                    _buffers.Count,
                    _segments.Count * _segmentChunks);
            }
        }

        public ArraySegment<byte> BorrowBuffer()
        {
            int trial = 0;
            while (trial < TrialsCount)
            {
                ArraySegment<byte> result;
                if (_buffers.TryPop(out result))
                    return result;
                CreateNewSegment(false);
                trial++;
            }
            throw new UnableToAllocateBufferException();
        }

        public IEnumerable<ArraySegment<byte>> BorrowBuffers(int toGet)
        {
            var result = new ArraySegment<byte>[toGet];
            var count = 0;
            var totalReceived = 0;

            try
            {
                while (count < TrialsCount)
                {
                    ArraySegment<byte> piece;
                    while (totalReceived < toGet)
                    {
                        if (!_buffers.TryPop(out piece))
                            break;
                        result[totalReceived] = piece;
                        ++totalReceived;
                    }
                    if (totalReceived == toGet)
                        return result;
                    CreateNewSegment(false);
                    count++;
                }
                throw new UnableToAllocateBufferException();
            }
            catch
            {
                if (totalReceived > 0)
                    ReturnBuffers(result.Take(totalReceived));
                throw;
            }
        }

        public void ReturnBuffer(ArraySegment<byte> buffer)
        {
            CheckBuffer(buffer);
            _buffers.Push(buffer);
        }

        public void ReturnBuffers(IEnumerable<ArraySegment<byte>> buffersToReturn)
        {
            if (buffersToReturn == null)
                throw new ArgumentNullException("buffersToReturn");

            foreach (var buf in buffersToReturn)
            {
                CheckBuffer(buf);
                _buffers.Push(buf);
            }
        }

        public void ReturnBuffers(params ArraySegment<byte>[] buffersToReturn)
        {
            if (buffersToReturn == null)
                throw new ArgumentNullException("buffersToReturn");

            foreach (var buf in buffersToReturn)
            {
                CheckBuffer(buf);
                _buffers.Push(buf);
            }
        }

        private void CheckBuffer(ArraySegment<byte> buffer)
        {
            if (buffer.Array == null || buffer.Count == 0 || buffer.Array.Length < buffer.Offset + buffer.Count)
                throw new Exception("Attempt to check in invalid buffer");
            if (buffer.Count != _chunkSize)
                throw new ArgumentException("Buffer was not of the same chunk size as the buffer manager", "buffer");
        }
    }
}

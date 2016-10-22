using System;

namespace Cowboy.Buffer
{
    public class SegmentBufferDeflector
    {
        public static void AppendBuffer(
            ISegmentBufferManager bufferManager,
            ref ArraySegment<byte> receiveBuffer,
            int receiveCount,
            ref ArraySegment<byte> sessionBuffer,
            ref int sessionBufferCount)
        {
            if (sessionBuffer.Count < (sessionBufferCount + receiveCount))
            {
                ArraySegment<byte> autoExpandedBuffer = bufferManager.BorrowBuffer();
                if (autoExpandedBuffer.Count < (sessionBufferCount + receiveCount) * 2)
                {
                    bufferManager.ReturnBuffer(autoExpandedBuffer);
                    autoExpandedBuffer = new ArraySegment<byte>(new byte[(sessionBufferCount + receiveCount) * 2]);
                }

                Array.Copy(sessionBuffer.Array, sessionBuffer.Offset, autoExpandedBuffer.Array, autoExpandedBuffer.Offset, sessionBufferCount);

                var discardBuffer = sessionBuffer;
                sessionBuffer = autoExpandedBuffer;
                bufferManager.ReturnBuffer(discardBuffer);
            }

            Array.Copy(receiveBuffer.Array, receiveBuffer.Offset, sessionBuffer.Array, sessionBuffer.Offset + sessionBufferCount, receiveCount);
            sessionBufferCount = sessionBufferCount + receiveCount;
        }

        public static void ShiftBuffer(
            ISegmentBufferManager bufferManager,
            int shiftStart,
            ref ArraySegment<byte> sessionBuffer,
            ref int sessionBufferCount)
        {
            if ((sessionBufferCount - shiftStart) < shiftStart)
            {
                Array.Copy(sessionBuffer.Array, sessionBuffer.Offset + shiftStart, sessionBuffer.Array, sessionBuffer.Offset, sessionBufferCount - shiftStart);
                sessionBufferCount = sessionBufferCount - shiftStart;
            }
            else
            {
                ArraySegment<byte> copyBuffer = bufferManager.BorrowBuffer();
                if (copyBuffer.Count < (sessionBufferCount - shiftStart))
                {
                    bufferManager.ReturnBuffer(copyBuffer);
                    copyBuffer = new ArraySegment<byte>(new byte[sessionBufferCount - shiftStart]);
                }

                Array.Copy(sessionBuffer.Array, sessionBuffer.Offset + shiftStart, copyBuffer.Array, copyBuffer.Offset, sessionBufferCount - shiftStart);
                Array.Copy(copyBuffer.Array, copyBuffer.Offset, sessionBuffer.Array, sessionBuffer.Offset, sessionBufferCount - shiftStart);
                sessionBufferCount = sessionBufferCount - shiftStart;

                bufferManager.ReturnBuffer(copyBuffer);
            }
        }

        public static void ReplaceBuffer(
            ISegmentBufferManager bufferManager,
            ref ArraySegment<byte> receiveBuffer,
            ref int receiveBufferOffset,
            int receiveCount)
        {
            if ((receiveBufferOffset + receiveCount) < receiveBuffer.Count)
            {
                receiveBufferOffset = receiveBufferOffset + receiveCount;
            }
            else
            {
                ArraySegment<byte> autoExpandedBuffer = bufferManager.BorrowBuffer();
                if (autoExpandedBuffer.Count < (receiveBufferOffset + receiveCount) * 2)
                {
                    bufferManager.ReturnBuffer(autoExpandedBuffer);
                    autoExpandedBuffer = new ArraySegment<byte>(new byte[(receiveBufferOffset + receiveCount) * 2]);
                }

                Array.Copy(receiveBuffer.Array, receiveBuffer.Offset, autoExpandedBuffer.Array, autoExpandedBuffer.Offset, receiveBufferOffset + receiveCount);
                receiveBufferOffset = receiveBufferOffset + receiveCount;

                var discardBuffer = receiveBuffer;
                receiveBuffer = autoExpandedBuffer;
                bufferManager.ReturnBuffer(discardBuffer);
            }
        }
    }
}

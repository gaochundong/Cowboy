using System;

namespace Cowboy.Sockets.Buffer
{
    public class BufferDeflector
    {
        public static void AppendBuffer(IBufferManager bufferManager, ref byte[] receiveBuffer, int receiveCount, ref byte[] sessionBuffer, ref int sessionBufferCount)
        {
            if (sessionBuffer.Length < (sessionBufferCount + receiveCount))
            {
                byte[] autoExpandedBuffer = bufferManager.BorrowBuffer();
                if (autoExpandedBuffer.Length < (sessionBufferCount + receiveCount) * 2)
                {
                    bufferManager.ReturnBuffer(autoExpandedBuffer);
                    autoExpandedBuffer = new byte[(sessionBufferCount + receiveCount) * 2];
                }

                Array.Copy(sessionBuffer, 0, autoExpandedBuffer, 0, sessionBufferCount);

                var discardBuffer = sessionBuffer;
                sessionBuffer = autoExpandedBuffer;
                bufferManager.ReturnBuffer(discardBuffer);
            }

            Array.Copy(receiveBuffer, 0, sessionBuffer, sessionBufferCount, receiveCount);
            sessionBufferCount = sessionBufferCount + receiveCount;
        }

        public static void ShiftBuffer(IBufferManager bufferManager, int shiftStart, ref byte[] sessionBuffer, ref int sessionBufferCount)
        {
            if ((sessionBufferCount - shiftStart) < shiftStart)
            {
                Array.Copy(sessionBuffer, shiftStart, sessionBuffer, 0, sessionBufferCount - shiftStart);
                sessionBufferCount = sessionBufferCount - shiftStart;
            }
            else
            {
                byte[] copyBuffer = bufferManager.BorrowBuffer();
                if (copyBuffer.Length < (sessionBufferCount - shiftStart))
                {
                    bufferManager.ReturnBuffer(copyBuffer);
                    copyBuffer = new byte[sessionBufferCount - shiftStart];
                }

                Array.Copy(sessionBuffer, shiftStart, copyBuffer, 0, sessionBufferCount - shiftStart);
                Array.Copy(copyBuffer, 0, sessionBuffer, 0, sessionBufferCount - shiftStart);
                sessionBufferCount = sessionBufferCount - shiftStart;

                bufferManager.ReturnBuffer(copyBuffer);
            }
        }

        public static void ReplaceBuffer(IBufferManager bufferManager, ref byte[] receiveBuffer, ref int receiveBufferOffset, int receiveCount)
        {
            if ((receiveBufferOffset + receiveCount) < receiveBuffer.Length)
            {
                receiveBufferOffset = receiveBufferOffset + receiveCount;
            }
            else
            {
                byte[] autoExpandedBuffer = bufferManager.BorrowBuffer();
                if (autoExpandedBuffer.Length < (receiveBufferOffset + receiveCount) * 2)
                {
                    bufferManager.ReturnBuffer(autoExpandedBuffer);
                    autoExpandedBuffer = new byte[(receiveBufferOffset + receiveCount) * 2];
                }

                Array.Copy(receiveBuffer, 0, autoExpandedBuffer, 0, receiveBufferOffset + receiveCount);
                receiveBufferOffset = receiveBufferOffset + receiveCount;

                var discardBuffer = receiveBuffer;
                receiveBuffer = autoExpandedBuffer;
                bufferManager.ReturnBuffer(discardBuffer);
            }
        }
    }
}

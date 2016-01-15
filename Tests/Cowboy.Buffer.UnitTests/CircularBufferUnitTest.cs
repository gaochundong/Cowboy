using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cowboy.Buffer.UnitTests
{
    [TestClass]
    public class CircularBufferUnitTest
    {
        [TestMethod]
        public void copy_from_byte_array_to_reach_capacity()
        {
            var buffer1 = new CircularBuffer<byte>(50, 100);

            var data1 = new byte[50];
            for (int i = 0; i < data1.Length; i++)
            {
                data1[i] = 1;
            }

            buffer1.CopyFrom(data1, 0, data1.Length);
            Assert.AreEqual(buffer1.Count, data1.Length);
        }

        [TestMethod]
        public void copy_from_byte_array_to_expand_capacity()
        {
            var buffer1 = new CircularBuffer<byte>(50, 100);

            var data1 = new byte[60];
            for (int i = 0; i < data1.Length; i++)
            {
                data1[i] = 1;
            }

            buffer1.CopyFrom(data1, 0, data1.Length);
            Assert.AreEqual(buffer1.Count, data1.Length);
            Assert.AreEqual(buffer1.MaxCapacity, 100);
        }

        [TestMethod]
        public void copy_from_byte_array_to_overflow_max_capacity()
        {
            var buffer1 = new CircularBuffer<byte>(50, 100);

            var data1 = new byte[200];
            for (int i = 0; i < data1.Length; i++)
            {
                data1[i] = 1;
            }

            bool thrown = false;
            try
            {
                buffer1.CopyFrom(data1, 0, data1.Length);
            }
            catch
            {
                thrown = true;
            }

            Assert.IsTrue(thrown);
        }

        [TestMethod]
        public void append_from_another_buffer()
        {
            var buffer1 = new CircularBuffer<byte>(50, 100);
            var buffer2 = new CircularBuffer<byte>(100, 100);

            var data1 = new byte[50];
            var data2 = new byte[100];
            for (int i = 0; i < data1.Length; i++)
            {
                data1[i] = 1;
            }
            for (int i = 0; i < data2.Length; i++)
            {
                data2[i] = 2;
            }

            buffer1.CopyFrom(data1, 0, data1.Length);
            Assert.AreEqual(buffer1.Count, data1.Length);

            buffer2.CopyFrom(data2, 0, data2.Length);
            Assert.AreEqual(buffer2.Count, data2.Length);

            buffer1.AppendFrom(buffer2, 80, 10);
            Console.WriteLine(buffer1.Count == 50 + 10);
        }
    }
}

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cowboy.Buffer.UnitTests
{
    [TestClass]
    public class CircularBufferUnitTest
    {
        [TestMethod]
        public void circular_buffer__copy_from_byte_array_to_reach_capacity()
        {
            var b1 = new CircularBuffer<byte>(50, 100);

            var data1 = new byte[50];
            for (int i = 0; i < data1.Length; i++)
            {
                data1[i] = 1;
            }

            b1.CopyFrom(data1, 0, data1.Length);
            Assert.AreEqual(b1.Count, data1.Length);
        }

        [TestMethod]
        public void circular_buffer__copy_from_byte_array_to_expand_capacity()
        {
            var b1 = new CircularBuffer<byte>(50, 100);

            var data1 = new byte[60];
            for (int i = 0; i < data1.Length; i++)
            {
                data1[i] = 1;
            }

            b1.CopyFrom(data1, 0, data1.Length);
            Assert.AreEqual(b1.Count, data1.Length);
            Assert.AreEqual(b1.MaxCapacity, 100);
        }

        [TestMethod]
        public void circular_buffer__copy_from_byte_array_to_overflow_max_capacity()
        {
            var b1 = new CircularBuffer<byte>(50, 100);

            var data1 = new byte[200];
            for (int i = 0; i < data1.Length; i++)
            {
                data1[i] = 1;
            }

            bool thrown = false;
            try
            {
                b1.CopyFrom(data1, 0, data1.Length);
            }
            catch
            {
                thrown = true;
            }

            Assert.IsTrue(thrown);
        }

        [TestMethod]
        public void circular_buffer__copy_from__tail_count__head_offset__head_offset_count()
        {
            var b1 = new CircularBuffer<byte>(50, 100);
            var b2 = new CircularBuffer<byte>(100, 100);

            for (int i = 0; i < 50; i++)
            {
                b1.Add(1);
            }

            for (int i = 0; i < 100; i++)
            {
                b2.Add(2);
            }

            b2.Skip(10);

            b1.CopyFrom(b2, 10, 10);

            Assert.AreEqual(b1.Count, 60);
            Assert.AreEqual(b1[49], 1);
            Assert.AreEqual(b1[50], 2);
        }

        [TestMethod]
        public void circular_buffer__copy_from__tail_count__head_offset__count_head_offset()
        {
            var b1 = new CircularBuffer<byte>(50, 100);
            var b2 = new CircularBuffer<byte>(100, 100);

            for (int i = 0; i < 50; i++)
            {
                b1.Add(1);
            }

            for (int i = 0; i < 100; i++)
            {
                b2.Add(2);
            }

            b2.Skip(80);

            for (int i = 0; i < 10; i++)
            {
                b2.Add(3);
            }

            b1.CopyFrom(b2, 15, 10);

            Assert.AreEqual(b1.Count, 60);
            Assert.AreEqual(b1[49], 1);
            Assert.AreEqual(b1[54], 2);
            Assert.AreEqual(b1[55], 3);
        }

        [TestMethod]
        public void circular_buffer__copy_from__tail_count__offset_head()
        {
            var b1 = new CircularBuffer<byte>(50, 100);
            var b2 = new CircularBuffer<byte>(100, 100);

            for (int i = 0; i < 50; i++)
            {
                b1.Add(1);
            }

            for (int i = 0; i < 100; i++)
            {
                b2.Add(2);
            }

            b2.Skip(80);

            for (int i = 0; i < 50; i++)
            {
                b2.Add(3);
            }

            b1.CopyFrom(b2, 30, 10);

            Assert.AreEqual(b1.Count, 60);
            Assert.AreEqual(b1[49], 1);
            Assert.AreEqual(b1[50], 3);
            Assert.AreEqual(b1[55], 3);
        }

        [TestMethod]
        public void circular_buffer__copy_from__count_tail__head_offset__head_offset_count()
        {
            var b1 = new CircularBuffer<byte>(50, 100);
            var b2 = new CircularBuffer<byte>(100, 100);

            for (int i = 0; i < 50; i++)
            {
                b1.Add(1);
            }

            for (int i = 0; i < 100; i++)
            {
                b2.Add(2);
            }

            b1.Skip(40);
            b2.Skip(10);

            b1.CopyFrom(b2, 70, 15);

            Assert.AreEqual(b1.Count, 25);
            Assert.AreEqual(b1[9], 1);
            Assert.AreEqual(b1[10], 2);
        }

        [TestMethod]
        public void circular_buffer__copy_from__count_tail__head_offset__count_head_offset()
        {
            var b1 = new CircularBuffer<byte>(50, 100);
            var b2 = new CircularBuffer<byte>(100, 100);

            for (int i = 0; i < 50; i++)
            {
                b1.Add(1);
            }

            for (int i = 0; i < 100; i++)
            {
                b2.Add(2);
            }

            b1.Skip(40);
            b2.Skip(80);

            for (int i = 0; i < 50; i++)
            {
                b2.Add(3);
            }

            b1.CopyFrom(b2, 10, 30);

            Assert.AreEqual(b1.Count, 40);
            Assert.AreEqual(b1[9], 1);
            Assert.AreEqual(b1[10], 2);
            Assert.AreEqual(b1[20], 3);
        }

        [TestMethod]
        public void circular_buffer__copy_from__count_tail__head_offset__count_head_offset_part()
        {
            var b1 = new CircularBuffer<byte>(50, 100);
            var b2 = new CircularBuffer<byte>(100, 100);

            for (int i = 0; i < 50; i++)
            {
                b1.Add(1);
            }

            for (int i = 0; i < 100; i++)
            {
                b2.Add(2);
            }

            b1.Skip(40);
            b2.Skip(90);

            for (int i = 0; i < 50; i++)
            {
                b2.Add(3);
            }

            b1.CopyFrom(b2, 5, 30);

            Assert.AreEqual(b1.Count, 40);
            Assert.AreEqual(b1[9], 1);
            Assert.AreEqual(b1[14], 2);
            Assert.AreEqual(b1[15], 3);
        }

        [TestMethod]
        public void circular_buffer__copy_from__count_tail__offset_head()
        {
            var b1 = new CircularBuffer<byte>(50, 100);
            var b2 = new CircularBuffer<byte>(100, 100);

            for (int i = 0; i < 50; i++)
            {
                b1.Add(1);
            }

            for (int i = 0; i < 100; i++)
            {
                b2.Add(2);
            }

            b1.Skip(40);
            b2.Skip(50);

            for (int i = 0; i < 40; i++)
            {
                b2.Add(3);
            }

            b1.CopyFrom(b2, 60, 30);

            Assert.AreEqual(b1.Count, 40);
            Assert.AreEqual(b1[9], 1);
            Assert.AreEqual(b1[29], 3);
            Assert.AreEqual(b1[30], 3);
        }
    }
}

using System;

namespace Cowboy.Buffer.TestCircularBuffer
{
    class Program
    {
        static void Main(string[] args)
        {
            var buffer1 = new CircularBuffer<byte>(50, 100);
            var buffer2 = new CircularBuffer<byte>(50, 100);

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
            Console.WriteLine(buffer1.Count == data1.Length);

            buffer2.CopyFrom(data2, 0, data2.Length);
            Console.WriteLine(buffer2.Count == data2.Length);

            buffer1.CopyFrom(buffer2, 80, 10);
            Console.WriteLine(buffer1.Count == 50 + 10);
            Console.WriteLine(buffer1.Tail == 50 + 10);

            Console.ReadKey();
        }
    }
}

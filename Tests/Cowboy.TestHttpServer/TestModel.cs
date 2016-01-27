using System;

namespace Cowboy.TestHttpServer
{
    public class TestModel
    {
        public TestModel()
        {
            this.String = "Hello World, 你好世界";
            this.DateTime = System.DateTime.Now;
            this.Int32 = int.MaxValue;
            this.Int64 = long.MaxValue;
            this.Float = 22.22222222222222f;
            this.Double = 33.33333333333333333d;
            this.Decimal = 444444444444444.4444444444444444444444444444444444m;
        }

        public string String { get; set; }
        public DateTime DateTime { get; set; }
        public int Int32 { get; set; }
        public long Int64 { get; set; }
        public float Float { get; set; }
        public double Double { get; set; }
        public decimal Decimal { get; set; }

        public TestModel This { get; set; }
    }
}

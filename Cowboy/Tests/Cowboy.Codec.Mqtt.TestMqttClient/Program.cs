using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Codec.Mqtt.TestMqttClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var c = new CONNECT();

            c.BuildPayload();

            Console.ReadKey();
        }
    }
}

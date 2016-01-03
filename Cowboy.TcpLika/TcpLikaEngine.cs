using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.TcpLika
{
    internal class TcpLikaEngine
    {
        private TcpLikaCommandLineOptions _options;

        public TcpLikaEngine(TcpLikaCommandLineOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            _options = options;
        }

        public void Start()
        {
            throw new NotImplementedException();
        }
    }
}

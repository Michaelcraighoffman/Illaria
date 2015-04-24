using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace IllariaServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Logger logger = LogManager.GetCurrentClassLogger();
            try
            {
                NetworkServer n = new NetworkServer();
                n.Start();
                Console.ReadKey();
                n.Stop();
            }
            catch (Exception e)
            {
                logger.Fatal("Caught top-level exception.  Terminating.", e);
            }
        }
    }
}

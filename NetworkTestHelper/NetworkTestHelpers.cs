using NetworkUtil;
using System;
using System.Net.Sockets;
using System.Threading;

namespace NetworkTestHelper
{
    public class NetworkTestHelpers
    {
        // 5 seconds should be more than enough time for any reasonable network operation
        public const int timeout = 5000;


        /// <summary>
        /// Waits for either the specified number of milliseconds, or until expr is true,
        /// whichever comes first.
        /// </summary>
        /// <param name="expr">The expression we expect to eventually become true</param>
        /// <param name="ms">The max wait time</param>
        public static void WaitForOrTimeout(Func<bool> expr, int ms)
        {
            int waited = 0;
            while (!expr() && waited < ms)
            {
                Thread.Sleep(15);
                // Note that Sleep is not accurate, so we didn't necessarily wait for 15ms (but probably close enough)
                waited += 15;
            }
        }


        public static void SetupSingleConnectionTest(out TcpListener listener, out SocketState client, out SocketState server)
        {
            SocketState clientResult = null;
            SocketState serverResult = null;

            void saveClientState(SocketState x)
            {
                if (x.ErrorOccured)
                {
                    Console.WriteLine(x.ErrorMessage);
                }
                clientResult = x;
            }

            void saveServerState(SocketState x)
            {
                if (x.ErrorOccured)
                {
                    Console.WriteLine(x.ErrorMessage); 
                }
                serverResult = x;

            }

            listener = Networking.StartServer(saveServerState, 2112);
            Networking.ConnectToServer(saveClientState, "localhost", 2112);

            WaitForOrTimeout(() => (clientResult != null) && (serverResult != null), timeout);
            client = clientResult;
            server = serverResult;
        }
    }
}

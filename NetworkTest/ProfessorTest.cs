// Author: Daniel Kopta, 2019
// Staff unit tests for CS 3500 Networking library (part of SpaceWars project)
// University of Utah

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetworkUtil;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NetworkUtilTests
{
    [TestClass]
    public class ProfessorTest
    {
        private const int timeout = 5000;

        // When testing network code, we have some necessary global state,
        // since open sockets are system-wide (managed by the OS)
        // Therefore, we need some per-test setup and cleanup
        private TcpListener testListener;
        private SocketState testLocalSocketState, testRemoteSocketState;
        private static byte[] buffer;


        [TestInitialize]
        public void Init()
        {
            testListener = null;
            testLocalSocketState = null;
            testRemoteSocketState = null;
            buffer = new byte[SocketState.BufferSize];
        }


        [TestCleanup]
        public void Cleanup()
        {
            StopTestServer(testListener, testLocalSocketState, testRemoteSocketState);
        }

        private TcpListener StartTestServer(int port)
        {
            TcpListener listener = NetworkUtil.Networking.StartServer(OnNewClient, port);
            return listener;
        }

        private void OnNewClient(SocketState state)
        {
            testRemoteSocketState = state;
            state.OnNetworkAction = x => { };
        }

        private void StopTestServer(TcpListener listener, SocketState socket1, SocketState socket2)
        {
            try
            {
                // '?.' is just shorthand for null checks
                listener?.Stop();
                socket1?.TheSocket?.Shutdown(SocketShutdown.Both);
                socket1?.TheSocket?.Close();
                socket2?.TheSocket?.Shutdown(SocketShutdown.Both);
                socket2?.TheSocket?.Close();
            }
            // Do nothing with the exception, since shutting down the server will likely result in 
            // a prematurely closed socket
            // If the timeout is long enough, the shutdown should succeed
            catch (Exception) { }
        }


        private void WaitForOrTimeout(Func<bool> expr, int ms)
        {
            int waited = 0;
            while (!expr() && waited < ms)
            {
                Thread.Sleep(15);
                // TODO: we didn't necessarily wait for 15ms (but probably close enough)
                waited += 15;
            }
        }


        private TcpListener SetupSingleClientTest()
        {
            Action<SocketState> saveState =
              x =>
              {
                  testLocalSocketState = x;
                  testLocalSocketState.OnNetworkAction = s => { };
              };
            TcpListener listener = StartTestServer(2112);
            Networking.ConnectToServer(saveState, "localhost", 2112);

            WaitForOrTimeout(() => (testLocalSocketState != null) && (testRemoteSocketState != null), timeout);

            return listener;
        }


        private TcpListener SetupSingleServerTest()
        {

            void saveServerState(SocketState x)
            {
                testLocalSocketState = x;
                testLocalSocketState.OnNetworkAction = s => { };
            }

            void saveClientState(SocketState x)
            {
                testRemoteSocketState = x;
            }

            TcpListener listener = Networking.StartServer(saveServerState, 2112);

            Networking.ConnectToServer(saveClientState, "localhost", 2112);

            WaitForOrTimeout(() => (testLocalSocketState != null) && (testRemoteSocketState != null), timeout);
            return listener;
        }



        public void SetupTestConnections(bool clientSide)
        {
            if (clientSide)
            {
                testListener = SetupSingleClientTest();
            }
            else
            {
                testListener = SetupSingleServerTest();
            }

            Assert.IsNotNull(testRemoteSocketState);
            Assert.IsNotNull(testLocalSocketState);
        }

        private void ReceiveLoop(SocketState state)
        {
            if (state.ErrorOccured)
                return;
            Networking.GetData(state);
        }

        private void StartReceiveLoop()
        {
            testRemoteSocketState.OnNetworkAction = ReceiveLoop;
            Networking.GetData(testRemoteSocketState);
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("1")]
        public void TestConnect(bool clientSide)
        {
            SetupTestConnections(clientSide);

            Assert.IsTrue(testRemoteSocketState.TheSocket.Connected);
            Assert.IsTrue(testLocalSocketState.TheSocket.Connected);
        }


        [TestMethod]
        [TestCategory("2")]
        public void TestConnectNoServer()
        {
            bool isCalled = false;

            void saveClientState(SocketState x)
            {
                isCalled = true;
                testLocalSocketState = x;
            }

            Networking.ConnectToServer(saveClientState, "localhost", 2112);
            WaitForOrTimeout(() => isCalled, timeout);

            Assert.IsTrue(isCalled);
            Assert.IsTrue(testLocalSocketState.ErrorOccured);
        }


        [TestMethod]
        [TestCategory("3")]
        public void TestConnectBadAddress()
        {
            bool isCalled = false;

            void saveClientState(SocketState x)
            {
                isCalled = true;
                testLocalSocketState = x;
            }

            Networking.ConnectToServer(saveClientState, "this is not an address", 2112);
            WaitForOrTimeout(() => isCalled, timeout);

            Assert.IsTrue(isCalled);
            Assert.IsTrue(testLocalSocketState.ErrorOccured);
        }


        [TestMethod]
        [TestCategory("4")]
        public void TestConnectBadIP()
        {
            bool isCalled = false;

            void saveClientState(SocketState x)
            {
                isCalled = true;
                testLocalSocketState = x;
            }

            Networking.ConnectToServer(saveClientState, "999.999.999.999", 2112);
            WaitForOrTimeout(() => isCalled, timeout);

            Assert.IsTrue(isCalled);
            Assert.IsTrue(testLocalSocketState.ErrorOccured);
        }


        [TestMethod]
        [TestCategory("5")]
        public void TestConnectTimeout()
        {
            bool isCalled = false;

            void saveClientState(SocketState x)
            {
                isCalled = true;
                testLocalSocketState = x;
            }

            Networking.ConnectToServer(saveClientState, "google.com", 2112);
            WaitForOrTimeout(() => isCalled, timeout);

            Assert.IsTrue(isCalled);
            Assert.IsTrue(testLocalSocketState.ErrorOccured);
        }


        [TestMethod]
        [TestCategory("6")]
        public void TestConnectCallsDelegate()
        {
            bool isCalled = false;

            void saveServerState(SocketState x)
            {
                testLocalSocketState = x;
                isCalled = true;
            }

            void saveClientState(SocketState x)
            {
                testRemoteSocketState = x;
            }

            testListener = Networking.StartServer(saveServerState, 2112);
            Networking.ConnectToServer(saveClientState, "localhost", 2112);
            WaitForOrTimeout(() => isCalled, timeout);

            Assert.IsTrue(isCalled);
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("7")]
        public void TestDisconnectLocalThenSend(bool clientSide)
        {
            SetupTestConnections(clientSide);

            testLocalSocketState.TheSocket.Shutdown(SocketShutdown.Both);

            // No assertions, but the following should not result in an unhandled exception
            Networking.Send(testLocalSocketState.TheSocket, "a");
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("8")]
        public void TestDisconnectRemoteThenSend(bool clientSide)
        {
            SetupTestConnections(clientSide);

            testRemoteSocketState.TheSocket.Shutdown(SocketShutdown.Both);

            // No assertions, but the following should not result in an unhandled exception
            Networking.Send(testLocalSocketState.TheSocket, "a");
        }


        /*** Begin Sending Tests ***/

        // In these tests, "local" means the SocketState associated with testing the student's code
        // and "remote" is the test harness
        // Each test will check the students' code sending as the client and as the server,
        // in order to defeat statically-saved SocketStates

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("9")]
        public void TestSendTinyMessage(bool clientSide)
        {
            SetupTestConnections(clientSide);

            Networking.Send(testLocalSocketState.TheSocket, "a");
            Networking.GetData(testRemoteSocketState);
            WaitForOrTimeout(() => testRemoteSocketState.GetData().Length > 0, timeout);

            Assert.AreEqual("a", testRemoteSocketState.GetData());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("10")]
        public void TestConsecutiveSends(bool clientSide)
        {
            SetupTestConnections(clientSide);

            Networking.Send(testLocalSocketState.TheSocket, "a");
            Networking.Send(testLocalSocketState.TheSocket, "b");

            StartReceiveLoop();

            WaitForOrTimeout(() => testRemoteSocketState.GetData().Length > 1, timeout);

            Assert.AreEqual("ab", testRemoteSocketState.GetData());
        }



        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("11")]
        public void TestDelayedSends(bool clientSide)
        {
            SetupTestConnections(clientSide);

            Networking.Send(testLocalSocketState.TheSocket, "a");

            StartReceiveLoop();

            WaitForOrTimeout(() => testRemoteSocketState.GetData().Length > 0, timeout);
            Networking.Send(testLocalSocketState.TheSocket, "b");

            WaitForOrTimeout(() => testRemoteSocketState.GetData().Length > 1, timeout);

            Assert.AreEqual("ab", testRemoteSocketState.GetData());
        }



        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("12")]
        public void TestSendNewlines(bool clientSide)
        {
            SetupTestConnections(clientSide);
            testRemoteSocketState.OnNetworkAction = ReceiveLoop;

            string message = "\na\n\nb\n";

            Networking.Send(testLocalSocketState.TheSocket, message);

            StartReceiveLoop();

            WaitForOrTimeout(() => testRemoteSocketState.GetData().Length == message.Length, timeout);

            Assert.AreEqual(message, testRemoteSocketState.GetData());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("13")]
        public void TestSendBufferSize(bool clientSide)
        {
            SetupTestConnections(clientSide);

            StringBuilder message = new StringBuilder();
            message.Append('a', SocketState.BufferSize);

            Networking.Send(testLocalSocketState.TheSocket, message.ToString());

            StartReceiveLoop();

            WaitForOrTimeout(() => testRemoteSocketState.GetData().Length == message.Length, timeout);

            Assert.AreEqual(message.ToString(), testRemoteSocketState.GetData());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("14")]
        public void TestSendLargerThanBufferSize(bool clientSide)
        {
            SetupTestConnections(clientSide);

            StringBuilder message = new StringBuilder();
            message.Append('a', SocketState.BufferSize + 1);

            Networking.Send(testLocalSocketState.TheSocket, message.ToString());

            StartReceiveLoop();

            WaitForOrTimeout(() => testRemoteSocketState.GetData().Length == message.Length, timeout);

            Assert.AreEqual(message.ToString(), testRemoteSocketState.GetData());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("15")]
        public void TestSendHugeMessage(bool clientSide)
        {
            SetupTestConnections(clientSide);

            StringBuilder message = new StringBuilder();
            message.Append('a', (int)(SocketState.BufferSize * 7.5));

            Networking.Send(testLocalSocketState.TheSocket, message.ToString());

            StartReceiveLoop();

            WaitForOrTimeout(() => testRemoteSocketState.GetData().Length == message.Length, timeout);

            Assert.AreEqual(message.ToString(), testRemoteSocketState.GetData());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("16")]
        public void TestRapidSendWhileReceiving(bool clientSide)
        {
            SetupTestConnections(clientSide);

            // Start the receive loop before starting the sends
            StartReceiveLoop();

            for (int i = 0; i < 10000; i++)
            {
                char c = (char)('a' + (i % 26));
                Networking.Send(testLocalSocketState.TheSocket, "" + c);
            }

            WaitForOrTimeout(() => testRemoteSocketState.GetData().Length == 10000, timeout);

            // Reconstruct the original message outside the send loop
            // to (in theory) make the send operations happen more rapidly.
            StringBuilder message = new StringBuilder();
            for (int i = 0; i < 10000; i++)
            {
                char c = (char)('a' + (i % 26));
                message.Append(c);
            }

            Assert.AreEqual(message.ToString(), testRemoteSocketState.GetData());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("17")]
        public void TestRapidSendThenReceive(bool clientSide)
        {
            SetupTestConnections(clientSide);

            for (int i = 0; i < 10000; i++)
            {
                char c = (char)('a' + (i % 26));
                Networking.Send(testLocalSocketState.TheSocket, "" + c);
            }

            // Start the receive loop after starting the sends
            StartReceiveLoop();

            WaitForOrTimeout(() => testRemoteSocketState.GetData().Length == 10000, timeout);

            // Reconstruct the original message outside the send loop
            // to (in theory) make the send operations happen more rapidly.
            StringBuilder message = new StringBuilder();
            for (int i = 0; i < 10000; i++)
            {
                char c = (char)('a' + (i % 26));
                message.Append(c);
            }

            Assert.AreEqual(message.ToString(), testRemoteSocketState.GetData());
        }

        /*** End Sending Tests ***/



        /*** Begin Receiving Tests ***/

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("18")]
        public void TestReceiveTinyMessage(bool clientSide)
        {
            SetupTestConnections(clientSide);

            testLocalSocketState.OnNetworkAction = (x) => { };

            Networking.Send(testRemoteSocketState.TheSocket, "a");

            Networking.GetData(testLocalSocketState);
            WaitForOrTimeout(() => testLocalSocketState.GetData().Length > 0, timeout);

            Assert.AreEqual("a", testLocalSocketState.GetData());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("19")]
        public void TestReceiveNewlines(bool clientSide)
        {
            SetupTestConnections(clientSide);

            testLocalSocketState.OnNetworkAction = (x) => { };

            string message = "\na\n\nb\n";

            Networking.Send(testRemoteSocketState.TheSocket, message);
            Networking.GetData(testLocalSocketState);
            WaitForOrTimeout(() => testLocalSocketState.GetData().Length > 1, timeout);

            Assert.AreEqual(message, testLocalSocketState.GetData());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("20")]
        public void TestNoEventLoop(bool clientSide)
        {
            SetupTestConnections(clientSide);

            int calledCount = 0;

            // This callMe will not ask for more data after receiving one message,
            // so it should only ever receive one message
            testLocalSocketState.OnNetworkAction = (x) => calledCount++;

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            Networking.GetData(testLocalSocketState);
            WaitForOrTimeout(() => testLocalSocketState.GetData().Length > 0, timeout);

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            WaitForOrTimeout(() => false, timeout);

            Assert.AreEqual(1, calledCount);
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("22")]
        public void TestEventLoop(bool clientSide)
        {
            SetupTestConnections(clientSide);

            int calledCount = 0;

            // This delegate asks for more data, starting an event loop
            testLocalSocketState.OnNetworkAction = (x) =>
            {
                if (x.ErrorOccured)
                    return;
                calledCount++;
                Networking.GetData(x);
            };

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            Networking.GetData(testLocalSocketState);
            WaitForOrTimeout(() => calledCount == 1, timeout);

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            WaitForOrTimeout(() => calledCount == 2, timeout);

            Assert.AreEqual(2, calledCount);
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("23")]
        public void TestChangeOnNetworkAction(bool clientSide)
        {
            SetupTestConnections(clientSide);

            int firstCalledCount = 0;
            int secondCalledCount = 0;

            void firstOnNetworkAction(SocketState state)
            {
                if (state.ErrorOccured)
                    return;
                firstCalledCount++;
                state.OnNetworkAction = secondOnNetworkAction;
                Networking.GetData(testLocalSocketState);
            }

            void secondOnNetworkAction(SocketState state)
            {
                if (state.ErrorOccured)
                    return;
                secondCalledCount++;
            }

            // Change the callMe after the first invokation
            testLocalSocketState.OnNetworkAction = firstOnNetworkAction;

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            Networking.GetData(testLocalSocketState);
            WaitForOrTimeout(() => firstCalledCount == 1, timeout);

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            WaitForOrTimeout(() => secondCalledCount == 1, timeout);

            Assert.AreEqual(1, firstCalledCount);
            Assert.AreEqual(1, secondCalledCount);
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("24")]
        public void TestRapidReceive(bool clientSide)
        {
            SetupTestConnections(clientSide);

            testLocalSocketState.OnNetworkAction = (x) =>
            {
                if (x.ErrorOccured)
                    return;
                Networking.GetData(x);
            };

            Networking.GetData(testLocalSocketState);

            for (int i = 0; i < 10000; i++)
            {
                char c = (char)('a' + (i % 26));
                Networking.Send(testRemoteSocketState.TheSocket, "" + c);
            }


            WaitForOrTimeout(() => testLocalSocketState.GetData().Length == 10000, timeout);

            // Reconstruct the original message outside the send loop
            // to (in theory) make the send operations happen more rapidly.
            StringBuilder message = new StringBuilder();
            for (int i = 0; i < 10000; i++)
            {
                char c = (char)('a' + (i % 26));
                message.Append(c);
            }

            Assert.AreEqual(message.ToString(), testLocalSocketState.GetData());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("25")]
        public void TestReceiveRemovesAll(bool clientSide)
        {
            SetupTestConnections(clientSide);

            StringBuilder localCopy = new StringBuilder();

            void removeMessage(SocketState state)
            {
                if (state.ErrorOccured)
                    return;
                localCopy.Append(state.GetData());
                state.RemoveData(0, state.GetData().Length);
                Networking.GetData(state);
            }

            testLocalSocketState.OnNetworkAction = removeMessage;

            Networking.GetData(testLocalSocketState);

            for (int i = 0; i < 10000; i++)
            {
                char c = (char)('a' + (i % 26));
                Networking.Send(testRemoteSocketState.TheSocket, "" + c);
            }

            WaitForOrTimeout(() => localCopy.Length == 10000, timeout);

            // Reconstruct the original message outside the send loop
            // to (in theory) make the send operations happen more rapidly.
            StringBuilder message = new StringBuilder();
            for (int i = 0; i < 10000; i++)
            {
                char c = (char)('a' + (i % 26));
                message.Append(c);
            }

            Assert.AreEqual(message.ToString(), localCopy.ToString());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("26")]
        public void TestReceiveRemovesPartial(bool clientSide)
        {
            SetupTestConnections(clientSide);

            const string toSend = "abcdefghijklmnopqrstuvwxyz";

            // Use a static seed for reproducibility
            Random rand = new Random(0);

            StringBuilder localCopy = new StringBuilder();

            void removeMessage(SocketState state)
            {
                if (state.ErrorOccured)
                    return;
                int numToRemove = rand.Next(state.GetData().Length);
                localCopy.Append(state.GetData().Substring(0, numToRemove));
                state.RemoveData(0, numToRemove);
                Networking.GetData(state);
            }

            testLocalSocketState.OnNetworkAction = removeMessage;

            Networking.GetData(testLocalSocketState);

            for (int i = 0; i < 1000; i++)
            {
                Networking.Send(testRemoteSocketState.TheSocket, toSend);
            }

            WaitForOrTimeout(() => false, timeout);

            // Add any remaining characters that didn't get copied in the last receive
            localCopy.Append(testLocalSocketState.GetData());

            // Reconstruct the original message outside the send loop
            // to (in theory) make the send operations happen more rapidly.
            StringBuilder message = new StringBuilder();
            for (int i = 0; i < 1000; i++)
            {
                message.Append(toSend);
            }

            Assert.AreEqual(message.ToString(), localCopy.ToString());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("27")]
        public void TestReceiveBufferSize(bool clientSide)
        {
            SetupTestConnections(clientSide);

            testLocalSocketState.OnNetworkAction = (x) =>
            {
                if (x.ErrorOccured)
                    return;
                Networking.GetData(x);
            };

            Networking.GetData(testLocalSocketState);

            StringBuilder message = new StringBuilder();
            message.Append('a', SocketState.BufferSize);

            Networking.Send(testRemoteSocketState.TheSocket, message.ToString());

            WaitForOrTimeout(() => testLocalSocketState.GetData().Length == message.Length, timeout);

            Assert.AreEqual(message.ToString(), testLocalSocketState.GetData());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        [TestCategory("28")]
        public void TestReceiveHugeMessage(bool clientSide)
        {
            SetupTestConnections(clientSide);

            testLocalSocketState.OnNetworkAction = (x) =>
            {
                if (x.ErrorOccured)
                    return;
                Networking.GetData(x);
            };

            Networking.GetData(testLocalSocketState);

            StringBuilder message = new StringBuilder();
            message.Append('a', (int)(SocketState.BufferSize * 7.5));

            Networking.Send(testRemoteSocketState.TheSocket, message.ToString());

            WaitForOrTimeout(() => testLocalSocketState.GetData().Length == message.Length, timeout);

            Assert.AreEqual(message.ToString(), testLocalSocketState.GetData());
        }


        /*** End Receiving Tests ***/


        /*** Multi-Server Test ***/
        [TestMethod]
        [TestCategory("29")]
        public void TestTwoServers()
        {
            SocketState server2 = null;
            SocketState client2 = null;
            bool server2Connected = false;
            bool receive1Called = false;
            bool receive2Called = false;
            string client1Message = "";
            string client2Message = "";
            string server1Message = "";
            string server2Message = "";

            void Server2OnConnect(SocketState s)
            {
                if (s.ErrorOccured)
                    return;
                server2Connected = true;
                server2 = s;
                server2.OnNetworkAction = Server2OnReceive;
            }

            void Client2OnConnect(SocketState s)
            {
                if (s.ErrorOccured)
                    return;
                client2 = s;
            }

            void Server1OnReceive(SocketState s)
            {
                if (s.ErrorOccured)
                    return;
                receive1Called = true;
                server1Message = s.GetData();
            }

            void Server2OnReceive(SocketState s)
            {
                if (s.ErrorOccured)
                    return;
                receive2Called = true;
                server2Message = s.GetData();
            }

            void Client1OnReceive(SocketState s)
            {
                if (s.ErrorOccured)
                    return;
                receive1Called = true;
                client1Message = s.GetData();
            }

            void Client2OnReceive(SocketState s)
            {
                if (s.ErrorOccured)
                    return;
                receive2Called = true;
                client2Message = s.GetData();
            }

            // setup first server and client using the normal helper
            SetupTestConnections(false);
            testLocalSocketState.OnNetworkAction = Server1OnReceive;

            // setup second server and client
            TcpListener listener2 = Networking.StartServer(Server2OnConnect, 2222);
            Networking.ConnectToServer(Client2OnConnect, "localhost", 2222);

            // wait for the second pair to connect
            WaitForOrTimeout(() => server2 != null && client2 != null, timeout);

            // Receive on server1
            Networking.GetData(testLocalSocketState);
            // Receive on server2
            Networking.GetData(server2);

            // Receive on client1
            testRemoteSocketState.OnNetworkAction = Client1OnReceive;
            Networking.GetData(testRemoteSocketState);
            // Receive on client2
            client2.OnNetworkAction = Client2OnReceive;
            Networking.GetData(client2);

            // Send from client1 to server1
            Networking.Send(testRemoteSocketState.TheSocket, "a");

            // Send from client2 to server2
            Networking.Send(client2.TheSocket, "b");

            // Send from server1 to client1
            Networking.Send(testLocalSocketState.TheSocket, "c");

            // Send from server2to client2
            Networking.Send(server2.TheSocket, "d");

            WaitForOrTimeout(() => server2Connected && receive1Called && receive2Called, timeout);

            Assert.IsTrue(server2Connected);
            Assert.IsTrue(receive1Called);
            Assert.IsTrue(receive2Called);
            Assert.AreEqual("a", server1Message);
            Assert.AreEqual("b", server2Message);
            Assert.AreEqual("c", client1Message);
            Assert.AreEqual("d", client2Message);
        }


        /*** Begin Integration Tests ***/
        /*   These tests use the student's code for both client and server */

        /// <summary>
        /// Helper to simulate a server loop with one client
        /// </summary>
        private StringBuilder SingleClientServerLoop()
        {
            Random rand = new Random(0);
            StringBuilder serverSent = new StringBuilder();

            // Start a stopwatch to simulate a game server's tick-rate
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            int nextChar = 0;
            // Run for 100 frames
            for (int frame = 0; frame < 100; frame++)
            {
                // Create several messages ranging in size between 0x - 1.5x the buffer size,
                StringBuilder messages = new StringBuilder();
                for (int i = 0; i < 5; i++)
                {
                    for (int j = 0; j < rand.Next((int)(SocketState.BufferSize * 1.5)); j++)
                        messages.Append((char)('a' + (nextChar++ % 26)));

                    // Newline separator indicates end of object
                    messages.Append("\n");
                }

                serverSent.Append(messages);
                Networking.Send(testRemoteSocketState.TheSocket, messages.ToString());

                // Simulate a 60 FPS tick-rate
                while (watch.ElapsedMilliseconds < 17)
                { /* just waiting */ }

                watch.Restart();

            } // end frame loop

            return serverSent;
        }


        // Helper to simualte "processing" a message by looking for a newline separator
        void ProcessMessage(SocketState state, StringBuilder clientReceived)
        {
            string[] objects = Regex.Split(state.GetData(), @"(?<=[\n])");

            foreach (string obj in objects)
            {
                if (obj.Length == 0)
                    continue;

                if (obj[obj.Length - 1] != '\n')
                    continue;

                clientReceived.Append(obj);
                state.RemoveData(0, obj.Length);
            }
        }


        [TestMethod]
        [TestCategory("30")]
        public void TestSimulateClientAndServer()
        {
            // For this test, we will use testRemoteSocketState as the server's SocketState
            // and testLocalSocketState as the client's SocketState

            Random rand = new Random(0);
            StringBuilder serverReceived = new StringBuilder();
            StringBuilder clientReceived = new StringBuilder();
            StringBuilder clientSent = new StringBuilder();

            // Local function to serve as a callMe delegate
            void serverFirstContact(SocketState state)
            {
                if (state.ErrorOccured)
                    return;
                // Save the socket state globally so that the Cleanup method can close it
                testRemoteSocketState = state;

                // Start a receive loop from the client
                state.OnNetworkAction = removeMessage;
                Networking.GetData(state);
            }

            // Local function to serve as a callMe delegate
            // Receive loop for the server that just removes and saves the client's messages
            void removeMessage(SocketState state)
            {
                if (state.ErrorOccured)
                    return;
                serverReceived.Append(state.GetData());
                state.RemoveData(0, state.GetData().Length);
                Networking.GetData(state);
            }

            // Local function to serve as a callMe delegate
            void clientFirstContact(SocketState state)
            {
                if (state.ErrorOccured)
                    return;
                // Save the socket state globally so that the Cleanup method can close it
                testLocalSocketState = state;

                // Start a receive loop from the server
                state.OnNetworkAction = handleMessage;
                Networking.GetData(state);
            }

            // Local function to serve as a callMe delegate
            // Receive loop for the client that simualtes "processing" a message by 
            // looking for a newline separator
            void handleMessage(SocketState state)
            {
                if (state.ErrorOccured)
                    return;
                ProcessMessage(state, clientReceived);

                // Reply to the server about half of the time
                if (rand.Next(2) == 0)
                {
                    StringBuilder reply = new StringBuilder();
                    for (int i = 0; i < rand.Next(10); i++)
                        reply.Append((char)('a' + rand.Next(26)));
                    reply.Append("\n");
                    clientSent.Append(reply);
                    Networking.Send(state.TheSocket, reply.ToString());
                }

                // Continue the receive loop
                Networking.GetData(state);
            }

            // Start the listener
            testListener = Networking.StartServer(serverFirstContact, 2112);
            // Start the client
            Networking.ConnectToServer(clientFirstContact, "localhost", 2112);
            // Wait for both sides to connect
            WaitForOrTimeout(() => testLocalSocketState != null && testRemoteSocketState != null, timeout);

            // Run the server loop
            StringBuilder serverSent = SingleClientServerLoop();

            WaitForOrTimeout(() => clientReceived.Length == serverSent.Length, timeout);
            WaitForOrTimeout(() => clientSent.Length == serverReceived.Length, timeout);

            Assert.AreEqual(clientReceived.ToString(), serverSent.ToString());
            Assert.AreEqual(clientSent.ToString(), serverReceived.ToString());
        }


        private void GameServer(
          out Dictionary<long, StringBuilder> serverSentMessages,
          out Dictionary<long, StringBuilder> serverReceivedMessages)
        {
            object serverConnectionsLock = new object();
            Dictionary<long, SocketState> serverSocketStates = new Dictionary<long, SocketState>();
            Dictionary<long, StringBuilder> sentMessages = new Dictionary<long, StringBuilder>();
            Dictionary<long, StringBuilder> receivedMessages = new Dictionary<long, StringBuilder>();
            Random rand = new Random(0);

            // Local function to serve as a callMe delegate
            void serverFirstContact(SocketState state)
            {
                if (state.ErrorOccured)
                    return;
                // Save the socket state so we can broadcast to all clients
                // Also save the rest of the per-connection state for validating the test
                lock (serverConnectionsLock)
                {
                    // Use one of the stringbuilder lists as a unique socket ID
                    // since they will strictly grow (the SocketState lists will grow and shrink)
                    //state.uid = sentMessages.Count;

                    sentMessages[state.ID] = new StringBuilder();
                    receivedMessages[state.ID] = new StringBuilder();
                    serverSocketStates[state.ID] = state;

                    // send the client's ID 
                    // important to do this inside the lock so the broadcast loop doesn't
                    // run before sending the client's ID
                    sentMessages[state.ID].Append("" + state.ID + "\n");
                    Networking.Send(state.TheSocket, "" + state.ID + "\n");
                }

                // Start a receive loop from the client
                state.OnNetworkAction = removeMessage;
                Networking.GetData(state);
            }

            // Local function to serve as a callMe delegate
            // Receive loop for the server that just removes and saves the client's messages
            void removeMessage(SocketState state)
            {
                if (state.ErrorOccured)
                    return;
                StringBuilder rcv;

                lock (serverConnectionsLock)
                {
                    rcv = receivedMessages[state.ID];
                }

                rcv.Append(state.GetData());
                state.RemoveData(0, state.GetData().Length);
                Networking.GetData(state);
            }

            testListener = Networking.StartServer(serverFirstContact, 2112);

            // Start a stopwatch to simulate a game server's tick-rate
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            int nextChar = 0;
            // Run for 150 frames
            for (int frame = 0; frame < 150; frame++)
            {
                // Create several messages ranging in size between 0x - 0.5x the buffer size,
                StringBuilder messages = new StringBuilder();
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < rand.Next((int)(SocketState.BufferSize * 0.5)); j++)
                        messages.Append((char)('a' + (nextChar++ % 26)));

                    // Newline separator indicates end of object
                    messages.Append("\n");
                }

                lock (serverConnectionsLock)
                {
                    foreach (StringBuilder sb in sentMessages.Values)
                        sb.Append(messages);

                    foreach (SocketState ss in serverSocketStates.Values)
                    {
                        if (ss.TheSocket.Connected)
                            Networking.Send(ss.TheSocket, messages.ToString());
                    }
                }

                // Simulate slow-ish server that gives the clients enough time to receive 
                // between frames so that we can assume that everything sent is also received
                while (watch.ElapsedMilliseconds < 30)
                { /* just waiting */ }

                watch.Restart();

            } // end frame loop

            serverSentMessages = sentMessages;
            serverReceivedMessages = receivedMessages;
        }

        private void GameClients(
          out Dictionary<long, StringBuilder> clientSentMessages,
          out Dictionary<long, StringBuilder> clientReceivedMessages,
          out Dictionary<long, SocketState> clientSockets)
        {
            object clientConnectionsLock = new object();
            //Dictionary<long, SocketState> clientSocketStates = new Dictionary<long, SocketState>();
            Dictionary<long, StringBuilder> sentMessages = new Dictionary<long, StringBuilder>();
            Dictionary<long, StringBuilder> receivedMessages = new Dictionary<long, StringBuilder>();
            Dictionary<long, SocketState> sockets = new Dictionary<long, SocketState>();
            Dictionary<long, Random> randoms = new Dictionary<long, Random>();
            Dictionary<long, long> clientIDs = new Dictionary<long, long>();
            Random rand = new Random(0);


            // Local function to serve as a callMe delegate
            void clientFirstContact(SocketState state)
            {
                if (state.ErrorOccured)
                    return;
                // Start a receive loop from the server
                state.OnNetworkAction = handleMessage;
                Networking.GetData(state);
            }

            // Local function to serve as a callMe delegate
            // Receive loop for the client that simualtes "processing" a message by 
            // looking for a newline separator
            void handleMessage(SocketState state)
            {
                if (state.ErrorOccured)
                    return;
                lock (clientConnectionsLock)
                {
                    // If this is the first message, save the per-connection state for validating the test
                    if (!clientIDs.ContainsKey(state.ID))
                    {
                        // first message contains ID
                        clientIDs[state.ID] = int.Parse(state.GetData().Substring(0, state.GetData().IndexOf("\n") + 1));

                        sentMessages[clientIDs[state.ID]] = new StringBuilder();
                        receivedMessages[clientIDs[state.ID]] = new StringBuilder();
                        sockets[clientIDs[state.ID]] = state;
                        randoms[clientIDs[state.ID]] = new Random((int)clientIDs[state.ID]);
                    }
                }


                StringBuilder rcv, sent;
                Random rnd;
                lock (clientConnectionsLock)
                {
                    rcv = receivedMessages[clientIDs[state.ID]];
                    sent = sentMessages[clientIDs[state.ID]];
                    rnd = randoms[clientIDs[state.ID]];
                }

                ProcessMessage(state, rcv);

                // Decide if we will disconnect or not (1% chance)
                bool disconnect = rnd.Next(100) == 0;

                // Reply to the server about half of the time
                if (!disconnect && rnd.Next(2) == 0)
                {
                    StringBuilder reply = new StringBuilder();
                    for (int i = 0; i < rand.Next(10); i++)
                        reply.Append((char)('a' + rand.Next(26)));
                    reply.Append("\n");
                    sent.Append(reply);
                    Networking.Send(state.TheSocket, reply.ToString());
                }

                // Continue the receive loop or disconnect
                if (disconnect)
                {
                    state.TheSocket.Shutdown(SocketShutdown.Both);
                }
                else
                    Networking.GetData(state);
            }


            // Start a stopwatch to simulate clients connecting and disconnecting over time
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            // 75 opportunities over time for clients to connect
            for (int i = 0; i < 75; i++)
            {
                // Connect 1 - 3 clients
                int numClients = 1 + rand.Next(3);
                for (int j = 0; j < numClients; j++)
                {
                    Networking.ConnectToServer(clientFirstContact, "localhost", 2112);
                }

                while (watch.ElapsedMilliseconds < 30)
                { /* just waiting */
                }

                watch.Restart();
            }

            clientSentMessages = sentMessages;
            clientReceivedMessages = receivedMessages;
            clientSockets = sockets;
        }


        [TestMethod]
        [TestCategory("31")]
        public void TestSimulateGame()
        {
            //Assert.IsTrue(false);
            Dictionary<long, StringBuilder> serverSent = null,
              serverReceived = null,
              clientSent = null,
              clientReceived = null;
            Dictionary<long, SocketState> clientSockets = null;


            Task server = new Task(() => GameServer(out serverSent, out serverReceived));
            Task clients = new Task(() => GameClients(out clientSent, out clientReceived, out clientSockets));
            server.Start();
            clients.Start();

            server.Wait();
            clients.Wait();

            Assert.IsNotNull(serverSent);
            Assert.IsNotNull(serverReceived);
            Assert.IsNotNull(clientSent);
            Assert.IsNotNull(clientReceived);

            // Just to be safe, wait for everything to finish up
            WaitForOrTimeout(() => false, 1000);

            // Make sure everything that got sent was also received
            foreach (long id in serverSent.Keys)
            {
                // Check if the client disconnected
                if (!clientSockets[id].TheSocket.Connected)
                {
                    // if so, we can only assert that it received a substring of what the server sent
                    Assert.IsTrue(serverSent[id].ToString().IndexOf(clientReceived[id].ToString()) == 0);
                }
                else
                    Assert.AreEqual(serverSent[id].ToString(), clientReceived[id].ToString());
            }

        }

        /*** End Integration Tests ***/

    }
}
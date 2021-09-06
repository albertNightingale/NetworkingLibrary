using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetworkUtil;
using NetworkTestHelper;
using System;
using System.Net.Sockets;
using System.Text;

namespace NetworkTest
{
    [TestClass]
    public class NetworkTests
    {
        // When testing network code, we have some necessary global state,
        // since open sockets are system-wide (managed by the OS)
        // Therefore, we need some per-test setup and cleanup
        private TcpListener testListener;
        private SocketState testLocalSocketState, testRemoteSocketState;


        [TestInitialize]
        public void Init()
        {
            testListener = null;
            testLocalSocketState = null;
            testRemoteSocketState = null;
        }


        [TestCleanup]
        public void Cleanup()
        {
            StopTestServer(testListener, testLocalSocketState, testRemoteSocketState);
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



        public void SetupTestConnections(bool clientSide,
          out TcpListener listener, out SocketState local, out SocketState remote)
        {
            if (clientSide)
            {
                NetworkTestHelpers.SetupSingleConnectionTest(
                  out listener,
                  out local,    // local becomes client
                  out remote);  // remote becomes server
            }
            else
            {
                NetworkTestHelpers.SetupSingleConnectionTest(
                  out listener,
                  out remote,   // remote becomes client
                  out local);   // local becomes server
            }

            Assert.IsNotNull(local);
            Assert.IsNotNull(remote);
        }


        /*** Begin Basic Connectivity Tests ***/
        [TestMethod]
        public void TestConnect()
        {
            NetworkTestHelpers.SetupSingleConnectionTest(out testListener, out testLocalSocketState, out testRemoteSocketState);

            Assert.IsNotNull(testRemoteSocketState);
            Assert.IsNotNull(testLocalSocketState);
            //Assert.AreEqual("",testLocalSocketState.ErrorMessage);
            Assert.IsFalse(testLocalSocketState.ErrorOccured);
            Assert.IsFalse(testRemoteSocketState.ErrorOccured);
            
            Assert.IsTrue(testRemoteSocketState.TheSocket.Connected);
            Assert.IsTrue(testLocalSocketState.TheSocket.Connected);

            //Assert.AreEqual("127.0.0.1:2112", testLocalSocketState.TheSocket.RemoteEndPoint.ToString());
        }


        [TestMethod]
        public void TestConnectNoServer()
        {
            bool isCalled = false;

            void saveClientState(SocketState x)
            {
                isCalled = true;
                testLocalSocketState = x;
            }

            // Try to connect without setting up a server first.
            Networking.ConnectToServer(saveClientState, "localhost", 2112);
            NetworkTestHelpers.WaitForOrTimeout(() => isCalled, NetworkTestHelpers.timeout);

            Assert.IsTrue(isCalled);
            Assert.IsTrue(testLocalSocketState.ErrorOccured);
        }


        [TestMethod]
        public void TestConnectTimeout()
        {
            bool isCalled = false;

            void saveClientState(SocketState x)
            {
                isCalled = true;
                testLocalSocketState = x;
            }

            Networking.ConnectToServer(saveClientState, "google.com", 2112);

            // The connection should timeout after 3 seconds. NetworkTestHelper.timeout is 5 seconds.
            NetworkTestHelpers.WaitForOrTimeout(() => isCalled, NetworkTestHelpers.timeout);

            Assert.IsTrue(isCalled);
            Assert.IsTrue(testLocalSocketState.ErrorOccured);
        }


        [TestMethod]
        public void TestConnectCallsDelegate()
        {
            bool serverActionCalled = false;
            bool clientActionCalled = false;

            void saveServerState(SocketState x)
            {
                testLocalSocketState = x;
                serverActionCalled = true;
            }

            void saveClientState(SocketState x)
            {
                testRemoteSocketState = x;
                clientActionCalled = true;
            }

            testListener = Networking.StartServer(saveServerState, 2112);
            Networking.ConnectToServer(saveClientState, "localhost", 2112);
            NetworkTestHelpers.WaitForOrTimeout(() => serverActionCalled, NetworkTestHelpers.timeout);
            NetworkTestHelpers.WaitForOrTimeout(() => clientActionCalled, NetworkTestHelpers.timeout);

            Assert.IsTrue(serverActionCalled);
            Assert.IsTrue(clientActionCalled);
        }


        /// <summary>
        /// This is an example of a parameterized test. 
        /// DataRow(true) and DataRow(false) means this test will be 
        /// invoked once with an argument of true, and once with false.
        /// This way we can test your Send method from both
        /// client and server sockets. In theory, there should be no 
        /// difference, but odd things can happen if you save static
        /// state (such as sockets) in your networking library.
        /// </summary>
        /// <param name="clientSide"></param>
        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestDisconnectLocalThenSend(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            testLocalSocketState.TheSocket.Shutdown(SocketShutdown.Both);

            // No assertions, but the following should not result in an unhandled exception
            Networking.Send(testLocalSocketState.TheSocket, "a");
        }

        /*** End Basic Connectivity Tests ***/


        /*** Begin Send/Receive Tests ***/

        // In these tests, "local" means the SocketState doing the sending,
        // and "remote" is the one doing the receiving.
        // Each test will run twice, swapping the sender and receiver between
        // client and server, in order to defeat statically-saved SocketStates
        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestSendTinyMessage(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            // Set the action to do nothing
            testLocalSocketState.OnNetworkAction = x => { };
            testRemoteSocketState.OnNetworkAction = x => { };

            Networking.Send(testLocalSocketState.TheSocket, "a");

            Networking.GetData(testRemoteSocketState);

            // Note that waiting for data like this is *NOT* how the networking library is 
            // intended to be used. This is only for testing purposes.
            // Normally, you would provide an OnNetworkAction that handles the data.
            NetworkTestHelpers.WaitForOrTimeout(() => testRemoteSocketState.GetData().Length > 0, NetworkTestHelpers.timeout);

            Assert.AreEqual("a", testRemoteSocketState.GetData());
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestNoEventLoop(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            int calledCount = 0;

            // This OnNetworkAction will not ask for more data after receiving one message,
            // so it should only ever receive one message
            testLocalSocketState.OnNetworkAction = (x) => calledCount++;

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            Networking.GetData(testLocalSocketState);
            // Note that waiting for data like this is *NOT* how the networking library is 
            // intended to be used. This is only for testing purposes.
            // Normally, you would provide an OnNetworkAction that handles the data.
            NetworkTestHelpers.WaitForOrTimeout(() => testLocalSocketState.GetData().Length > 0, NetworkTestHelpers.timeout);

            // Send a second message (which should not increment calledCount)
            Networking.Send(testRemoteSocketState.TheSocket, "a");
            NetworkTestHelpers.WaitForOrTimeout(() => false, NetworkTestHelpers.timeout);

            Assert.AreEqual(1, calledCount);
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestDelayedSends(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            // Set the action to do nothing
            testLocalSocketState.OnNetworkAction = x => { };
            testRemoteSocketState.OnNetworkAction = x => { };

            Networking.Send(testLocalSocketState.TheSocket, "a");
            Networking.GetData(testRemoteSocketState);
            // Note that waiting for data like this is *NOT* how the networking library is 
            // intended to be used. This is only for testing purposes.
            // Normally, you would provide an OnNetworkAction that handles the data.
            NetworkTestHelpers.WaitForOrTimeout(() => testRemoteSocketState.GetData().Length > 0, NetworkTestHelpers.timeout);

            Networking.Send(testLocalSocketState.TheSocket, "b");
            Networking.GetData(testRemoteSocketState);
            // Note that waiting for data like this is *NOT* how the networking library is 
            // intended to be used. This is only for testing purposes.
            // Normally, you would provide an OnNetworkAction that handles the data.
            NetworkTestHelpers.WaitForOrTimeout(() => testRemoteSocketState.GetData().Length > 1, NetworkTestHelpers.timeout);

            Assert.AreEqual("ab", testRemoteSocketState.GetData());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestEventLoop(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            int calledCount = 0;

            // This OnNetworkAction asks for more data, creating an event loop
            testLocalSocketState.OnNetworkAction = (x) =>
            {
                if (x.ErrorOccured)
                    return;
                calledCount++;
                Networking.GetData(x);
            };

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            Networking.GetData(testLocalSocketState);
            NetworkTestHelpers.WaitForOrTimeout(() => calledCount == 1, NetworkTestHelpers.timeout);

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            NetworkTestHelpers.WaitForOrTimeout(() => calledCount == 2, NetworkTestHelpers.timeout);

            Assert.AreEqual(2, calledCount);
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestChangeOnNetworkAction(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            int firstCalledCount = 0;
            int secondCalledCount = 0;

            // This is an example of a nested method (just another way to make a quick delegate)
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
                secondCalledCount++;
            }

            // Change the OnNetworkAction after the first invokation
            testLocalSocketState.OnNetworkAction = firstOnNetworkAction;

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            Networking.GetData(testLocalSocketState);
            NetworkTestHelpers.WaitForOrTimeout(() => firstCalledCount == 1, NetworkTestHelpers.timeout);

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            NetworkTestHelpers.WaitForOrTimeout(() => secondCalledCount == 1, NetworkTestHelpers.timeout);

            Assert.AreEqual(1, firstCalledCount);
            Assert.AreEqual(1, secondCalledCount);
        }



        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestReceiveRemovesAll(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            StringBuilder localCopy = new StringBuilder();

            void removeMessage(SocketState state)
            {
                if (state.ErrorOccured)
                    return;
                localCopy.Append(state.GetData());
                state.RemoveData(0, state.GetData().Length);
                Networking.GetData(state);  // continue the receiving process. 
            }

            testLocalSocketState.OnNetworkAction = removeMessage;

            // Start a receive loop
            Networking.GetData(testLocalSocketState);

            for (int i = 0; i < 10000; i++)
            {
                char c = (char)('a' + (i % 26));
                Networking.Send(testRemoteSocketState.TheSocket, "" + c);
            }

            //NetworkTestHelpers.WaitForOrTimeout(() => localCopy.Length == 10000, NetworkTestHelpers.timeout);

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
        public void TestReceiveRemovesPartial(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

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

            // Start a receive loop
            Networking.GetData(testLocalSocketState);

            for (int i = 0; i < 1000; i++)
            {
                Networking.Send(testRemoteSocketState.TheSocket, toSend);
            }

            // Wait a while
            NetworkTestHelpers.WaitForOrTimeout(() => false, NetworkTestHelpers.timeout);

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
        public void TestReceiveHugeMessage(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

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

            NetworkTestHelpers.WaitForOrTimeout(() => testLocalSocketState.GetData().Length == message.Length, NetworkTestHelpers.timeout);

            Assert.AreEqual(message.ToString(), testLocalSocketState.GetData());
        }

        /*** End Send/Receive Tests ***/
        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestSendCloseHugeMessage(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            testLocalSocketState.OnNetworkAction = (x) =>
            {
                if (x.ErrorOccured)
                    return;
                Networking.GetData(x);
            };

            Networking.GetData(testLocalSocketState);

            StringBuilder message = new StringBuilder();
            message.Append('a', (int)(SocketState.BufferSize * 7.5));

            bool isCompleted = Networking.SendAndClose(testRemoteSocketState.TheSocket, message.ToString());

            Assert.IsTrue(isCompleted); 

            NetworkTestHelpers.WaitForOrTimeout(() => testLocalSocketState.GetData().Length == message.Length, NetworkTestHelpers.timeout);

            Assert.AreEqual(message.ToString(), testLocalSocketState.GetData());
            Assert.IsFalse(testRemoteSocketState.TheSocket.Connected);
        }


    }
}

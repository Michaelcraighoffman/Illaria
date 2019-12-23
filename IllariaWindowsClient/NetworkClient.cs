using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using NLog;
using System.Threading;
using IllariaShared;

namespace Illaria
{
    internal class NetworkClient
    {
        private NetClient networkClient;
        private Logger logger = LogManager.GetCurrentClassLogger();

        private Thread networkThread;
        private bool ServerIsRunning;

        public NetworkClient()
        {
            
        }

        public bool Connect(string host, int port)
        {
            NetPeerConfiguration config = new NetPeerConfiguration("Illaria");

            networkClient = new NetClient(config);
            networkClient.Start();
            var connectionAttempt=networkClient.Connect(host, port);
            Thread.Sleep(100);
            DateTime connectionAttemptTime = DateTime.Now;
            while (connectionAttempt.Status != NetConnectionStatus.Disconnected &&
                  networkClient.Status != NetPeerStatus.Running &&
                  (DateTime.Now-connectionAttemptTime).TotalSeconds < 10)
            {
                Thread.Sleep(100);
            }
            if (networkClient.Status != NetPeerStatus.Running)
            {
                networkClient.Disconnect("");
                logger.Warn("Could not connect to server: " + host + " : " + port);
                return false;
            }
            ServerIsRunning = true;
            Thread t = new Thread(new ThreadStart(ProcessMessages));
            return true;
        }

        private void ProcessMessages()
        {
            NetIncomingMessage msg;
            while(ServerIsRunning)
            {
                while ((msg = networkClient.WaitMessage(100)) != null)
                {
                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.VerboseDebugMessage:
                        case NetIncomingMessageType.DebugMessage:
                            logger.Debug(msg.ReadString());
                            networkClient.Recycle(msg);
                            break;
                        case NetIncomingMessageType.WarningMessage:
                            logger.Warn(msg.ReadString());
                            networkClient.Recycle(msg);
                            break;
                        case NetIncomingMessageType.ErrorMessage:
                            logger.Error(msg.ReadString());
                            networkClient.Recycle(msg);
                            break;
                        case NetIncomingMessageType.Data:
                        case NetIncomingMessageType.UnconnectedData:
                            ProcessMessage(msg);
                            break;
                        default:
                            logger.Warn("Unhandled type: " + msg.MessageType);
                            break;
                    }
                }
            }
            logger.Info("Stopping message receiving loop.");
        }

        private void ProcessMessage(NetIncomingMessage msg)
        {
            try
            {
                GameMessageType m = (GameMessageType)msg.ReadByte();
                switch (m)
                {
                    case GameMessageType.CharacterLocation:

                        break;
                    default:
                        logger.Warn("Malformed message.  Invalid message type byte: " + (byte)m);
                        break;
                }
            }
            catch (Exception e)
            {
                logger.Warn(e,"Unknown processing Game Message");
            }
            finally
            {
                networkClient.Recycle(msg);
            }
        }
    }
}

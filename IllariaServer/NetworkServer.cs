using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;
using IllariaShared;
using System.Collections.Concurrent;
using System.Threading;
using NLog;


namespace IllariaServer
{
    class NetworkServer
    {
        private NetServer networkServer;
        private bool ServerRunning;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private Thread systemMonitorThread;
        private Thread messageReceiveLoopThread;
        private DateTime lastEmptyQueue;
        /// <summary>
        /// The maximum number of clients that can be connected to the server
        /// </summary>
        public int MaxClients { get { return maxClients; } }
        private int maxClients;

        public NetworkServer()
        {
            maxClients = 100;
        }

        public void Start()
        {
            NetPeerConfiguration config = new NetPeerConfiguration("Illaria");
            config.Port = 17541;

            networkServer = new NetServer(config);
            networkServer.Start();

            ServerRunning = true;

            systemMonitorThread = new Thread(this.SystemMonitorLoop);
            systemMonitorThread.Start();

            messageReceiveLoopThread = new Thread(this.MessageReceiveLoop);
            messageReceiveLoopThread.Start();           
        }
        public void Stop()
        {
            ServerRunning = false;
            if (!messageReceiveLoopThread.Join(1500))
            {
                logger.Warn("Message loop thread did not finish.");
            }
            if (!systemMonitorThread.Join(1500))
            {
                logger.Warn("System Processing thread did not finish.");
            }
            networkServer.Shutdown("Shutting Down");
        }

        private void MessageReceiveLoop()
        {
            logger.Info("Starting message receiving loop.");
            NetIncomingMessage msg;
            while (ServerRunning)
            {
                while ((msg = networkServer.ReadMessage()) != null)
                {
                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.VerboseDebugMessage:
                        case NetIncomingMessageType.DebugMessage:
                            logger.Debug(msg.ReadString());
                            break;
                        case NetIncomingMessageType.WarningMessage:
                            logger.Warn(msg.ReadString());
                            break;
                        case NetIncomingMessageType.ErrorMessage:
                            logger.Error(msg.ReadString());
                            break;
                        case NetIncomingMessageType.Data:
                        case NetIncomingMessageType.UnconnectedData:
                            RouteMessage(msg);
                            break;
                        default:
                            logger.Warn("Unhandled type: " + msg.MessageType);
                            break;
                    }
                    networkServer.Recycle(msg);
                }
                lastEmptyQueue = DateTime.Now;
            }
            logger.Info("Stopping message receiving loop.");
        }

        private void SystemMonitorLoop()
        {
            logger.Info("Starting system monitor loop.");
            while (ServerRunning)
            {
                var elapsed = (int)(DateTime.Now - lastEmptyQueue).TotalMilliseconds;
                if (elapsed > 15)
                {
                    logger.Warn(String.Format("Message queue running slowly.  Lagged {0} ms.", elapsed));
                }
            }
            logger.Info("Stopping System Monitor loop.");
        }

        private void RouteMessage(NetIncomingMessage msg)
        {
            try
            {
                MessageDestination dest=(MessageDestination)msg.ReadByte();
                switch (dest)
                {
                    case MessageDestination.Game:
                        //GameManager.AddMessage(msg)
                        break;
                    case MessageDestination.Lobby:
                        //LobbyManager.AddMessage(msg)
                        break;
                    case MessageDestination.System:
                        AddMessage(msg);
                        break;
                    default:
                        logger.Warn("Malformed message.  Invalid desination byte: " + (byte)dest);
                        break;
                }
            }
            catch (IndexOutOfRangeException)
            {
                logger.Warn("Malformed message.  Could not read desination byte.");
            }
            catch (Exception e)
            {
                logger.Warn("Unknown exception during Message routing: ", e);
            }
        }

        private void AddMessage(NetIncomingMessage msg)
        {
            try
            {
                SystemMessageType m=(SystemMessageType)msg.ReadByte();
                switch (m)
                {
                    case SystemMessageType.GetMessageLagTime:
                        var elapsed = DateTime.Now - lastEmptyQueue;
                        NetOutgoingMessage result = networkServer.CreateMessage();
                        result.Write((byte)SystemMessageType.GetMessageLagTime);
                        result.Write((Int32)elapsed.TotalMilliseconds);
                        //networkServer.SendMessage(result, msg.SenderConnection, NetDeliveryMethod.ReliableSequenced,(int)ReliableSequencedChannels.SystemMessageLagTime);
                        logger.Info("Current Message lag time: " + elapsed.TotalMilliseconds);
                        break;
                    default:
                        logger.Warn("Malformed message.  Invalid message type byte: " + (byte)m);
                        break;
                }
            }
            catch (Exception e)
            {
                logger.Warn("Unknown exception adding System Message: ", e);
            } 
        }

        public void RecycleMessage(NetIncomingMessage msg)
        {
            networkServer.Recycle(msg);
        }




    }
}

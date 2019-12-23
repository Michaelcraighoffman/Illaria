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
using NLog.Fluent;

namespace IllariaServer
{
    class NetworkServer : INetworkManager
    {
        private NetServer networkServer;
        private bool ServerRunning;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private Thread systemMonitorThread;
        private Thread messageReceiveLoopThread;
        private DateTime lastEmptyQueue;
        private List<Player> players;
        private ServerConsole console;
        /// <summary>
        /// The maximum number of clients that can be connected to the server
        /// </summary>
        public int MaxClients { get { return maxClients; } }
        private int maxClients;

        public NetworkServer()
        {
            maxClients = 12;
            players = new List<Player>(maxClients);
            console = new ServerConsole();
            console.Start(Console.WindowHeight, (string s) => { return ProcessLocalCommand(s); });
        }

        public void Start()
        {
            NetPeerConfiguration config = new NetPeerConfiguration("Illaria");
            config.Port = 17540;
            config.MaximumConnections = maxClients + 1;

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
            console.Stop();
            networkServer.Shutdown("Shutting Down");
        }

        private void MessageReceiveLoop()
        {
            console.WriteInfo("Starting message receiving loop.");
            NetIncomingMessage msg;
            while (ServerRunning)
            {
                while ((msg = networkServer.WaitMessage(100)) != null)
                {
                    switch (msg.MessageType)
                    {
                        
                        case NetIncomingMessageType.VerboseDebugMessage:
                        case NetIncomingMessageType.DebugMessage:
                            console.WriteInfo(msg.ReadString());
                            networkServer.Recycle(msg);
                            break;
                        case NetIncomingMessageType.WarningMessage:
                            console.WriteWarn(msg.ReadString());
                            networkServer.Recycle(msg);
                            break;
                        case NetIncomingMessageType.ErrorMessage:
                            console.WriteError(msg.ReadString());
                            networkServer.Recycle(msg);
                            break;
                        case NetIncomingMessageType.Data:
                        case NetIncomingMessageType.UnconnectedData:
                            RouteMessage(msg);
                            break;
                        case NetIncomingMessageType.StatusChanged:
                            console.WriteInfo("Client {0} is now: {1}", msg.SenderConnection.RemoteUniqueIdentifier, 
                                Enum.GetName(typeof(NetConnectionStatus), msg.SenderConnection.Status));
                            break;
                        default:
                            console.WriteWarn("Unhandled type: " + msg.MessageType);
                            break;
                    }
                }
                lastEmptyQueue = DateTime.Now;
                //Thread.Sleep(1);
            }
            console.WriteInfo("Stopping message receiving loop.");
        }

        private void SystemMonitorLoop()
        {
            console.WriteInfo("Starting system monitor loop.");
            while (ServerRunning)
            {
                if ((int)(DateTime.Now - lastEmptyQueue).TotalMilliseconds > 200)
                {
                    console.WriteWarn(String.Format("Message queue running slowly.  Lagged {0} ms.", (int)(DateTime.Now - lastEmptyQueue).TotalMilliseconds));
                }
                Thread.Sleep(100);
            }
            console.WriteInfo("Stopping System Monitor loop.");
        }

        private void RouteMessage(NetIncomingMessage msg)
        {
            try
            {
                MessageDestination dest=(MessageDestination)msg.ReadByte();
                switch (dest)
                {
                    case MessageDestination.Game:
                        ProcessGameMessage(msg);
                        break;
                    case MessageDestination.System:
                        ProcessServerMessage(msg);
                        break;
                    default:
                        console.WriteWarn("Malformed message.  Invalid desination byte: " + (byte)dest);
                        RecycleMessage(msg);
                        break;
                }
            }
            catch (IndexOutOfRangeException)
            {
                console.WriteWarn("Malformed message.  Could not read desination byte.");
                RecycleMessage(msg);
            }
            catch (Exception e)
            {
                console.WriteWarn("Unknown exception during Message routing: ", e);
                RecycleMessage(msg);
            }
        }

        private void ProcessGameMessage(NetIncomingMessage msg)
        {
            try
            {
                GameMessageType m = (GameMessageType)msg.ReadByte();
                switch (m)
                {
                    case GameMessageType.PlayerDestination:
                        Byte playerId = msg.ReadByte();
                        if(playerId>maxClients)
                        {
                            logger.ConditionalTrace("Player with uniqueId " + msg.SenderConnection.RemoteUniqueIdentifier + " tried to move out of range player " + playerId);
                        }

                        if(msg.SenderConnection.RemoteUniqueIdentifier == players[playerId].UniqueId)
                        {
                            Point location;
                            location.x = msg.ReadInt32();
                            location.y = msg.ReadInt32();
                            players[playerId].UpdateDestination(location);
                        }
                        else
                        {
                            logger.ConditionalTrace("Player with uniqueId "+ msg.SenderConnection.RemoteUniqueIdentifier+" tried to move non-owned player "+playerId);
                        }
                        break;
                    case GameMessageType.CharacterLocation:
                        break;
                    default:
                        console.WriteWarn("Malformed message.  Invalid message type byte: " + (byte)m);
                        break;
                }
            }
            catch (Exception e)
            {
                //console.WriteWarn(e, "Unknown processing Game Message");
            }
            finally
            {
                RecycleMessage(msg);
            }
        }

        private void ProcessServerMessage(NetIncomingMessage msg)
        {
            try
            {
                SystemMessageType m=(SystemMessageType)msg.ReadByte();
                switch (m)
                {
                    case SystemMessageType.GetMessageLagTime:
                        var elapsed = DateTime.Now - lastEmptyQueue;
                        NetOutgoingMessage result = networkServer.CreateMessage();
                        result.Write((byte)MessageDestination.System);
                        result.Write((byte)SystemMessageType.GetMessageLagTime);
                        result.Write((Int32)elapsed.TotalMilliseconds);
                        networkServer.SendMessage(result, msg.SenderConnection, NetDeliveryMethod.ReliableSequenced,(int)ReliableSequencedChannels.SystemMessageLagTime);
                        console.WriteInfo("Current Message lag time: " + elapsed.TotalMilliseconds);
                        break;
                    default:
                        console.WriteWarn("Malformed message.  Invalid message type byte: " + (byte)m);
                        break;
                }
            }
            catch (Exception e)
            {
                console.WriteWarn("Unknown exception adding System Message: ", e);
            }
            finally
            {
                RecycleMessage(msg);
            }
        }

        public bool ProcessLocalCommand(string command)
        {
            var fixedCommand = command.Trim().ToLowerInvariant();
            try {
                if (fixedCommand == "stop")
                {
                    console.WriteWarn("Shutting down server!");
                    Stop();
                    return true;
                }
                if (fixedCommand == "list connections")
                {
                    var connections = networkServer.Connections.Select(x => x.RemoteUniqueIdentifier.ToString());
                    console.WriteInfo("Active Connections: {0}", String.Join(", ", connections));
                    return true;
                }
                if (fixedCommand.StartsWith("kick"))
                {
                    var connectionId = fixedCommand.Split(' ')[1];
                    if(String.IsNullOrWhiteSpace(connectionId))
                    {
                        console.WriteError("No connection specified");
                        return false;
                    }

                    var applicableConnections = networkServer.Connections.Where(x => x.RemoteUniqueIdentifier.ToString().Contains(connectionId));
                    if(applicableConnections.Count()==0)
                    {
                        console.WriteError("{0} does not match an active connection", connectionId);
                        return false;
                    }
                    if (applicableConnections.Count()> 1)
                    {
                        console.WriteError("{0} matches multiple active connections: {1}", connectionId, String.Join(", ", applicableConnections.Select(x=>x.RemoteUniqueIdentifier)));
                        return false;
                    }
                    // Send a friendly kick message as well
                    console.WriteInfo("Kicking {0}", applicableConnections.First().RemoteUniqueIdentifier);
                    applicableConnections.First().Disconnect("Kicked by admin");
                    return true;
                }
            }
            catch(Exception) { }
            console.WriteError("Unknown command: " + fixedCommand);
            return false;
        }

        public void RecycleMessage(NetIncomingMessage msg)
        {
            networkServer.Recycle(msg);
        }




    }
}

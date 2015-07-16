using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Lidgren.Network;
using NLog;

namespace IllariaShared
{
    public class GameServer
    {
        private ConcurrentQueue<NetIncomingMessage> unprocessedMessages;
        private static Logger logger = LogManager.GetCurrentClassLogger();
        private List<Player> players; 
        /// <summary>
        /// Whether or not this GameServer is acting as an authoritative server
        /// </summary>
        private bool actingAsServer;
        private INetworkManager networkManager;

        public GameServer(INetworkManager netManager, IEnumerable<Player> newPlayers, bool CreateAsServer) {
            unprocessedMessages = new ConcurrentQueue<NetIncomingMessage>();
            networkManager = netManager;
            actingAsServer = CreateAsServer;
            players = new List<Player>(newPlayers);
        }


        public void AddMessage(NetIncomingMessage msg)
        {
            try
            {
                unprocessedMessages.Enqueue(msg);
            }
            catch (Exception e)
            {
                logger.Warn("Unknown exception adding GameServer Message: ", e);
                networkManager.RecycleMessage(msg);
            }
        }
        public void ProcessMessage()
        {
            NetIncomingMessage msg;
            if (!unprocessedMessages.TryDequeue(out msg))
            {
                return;
            }
            try
            {
                GameMessageType m = (GameMessageType)msg.ReadByte();
                var isServerMessage = m.HasFlag(GameMessageType.ServerFlag);
                m &= ~GameMessageType.ServerFlag; //Clear the server flag if it's set
                if (isServerMessage == actingAsServer)
                {
                    //This message is either from an authoritative server when we're the authoritative server
                    //Or from a client when we're also a client
                    logger.Warn("Got a Game server message with the wrong flag.  Ignoring.");
                    logger.Trace(Encoding.UTF8.GetString(msg.Data));
                    networkManager.RecycleMessage(msg);
                    return;
                }
                switch (m)
                {
                    case GameMessageType.CharacterLocation:
                        CharacterLocation c=new CharacterLocation();
                        msg.ReadAllFields(c);
                        logger.Trace("Updating character {0} location to x: {1} y: {2} ", c.CharacterId, c.x, c.y);
                        break;
                    default:
                        logger.Warn("Malformed message.  Invalid Game message type byte: " + (byte)m);
                        break;
                }
            }
            catch (Exception e)
            {
                logger.Warn("Malformed Game message.  Exception: ", e);
            }
            finally
            {
                networkManager.RecycleMessage(msg);
            }
        }
        public void Shutdown()
        {

        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IllariaShared;
using Lidgren.Network;
using System.Threading;
using NLog;

namespace IllariaServer
{
    internal class ServerPool
    {
        private Dictionary<byte, GameServer> gameServers;
        Logger logger;
        private INetworkManager networkManager;
        private Thread thread;
        private bool poolRunning;

        public ServerPool(INetworkManager network)
        {
            networkManager = network;
            gameServers = new Dictionary<byte, GameServer>(5);
            poolRunning = true;
            thread = new Thread(new ThreadStart(this.Tick));
            thread.Start();
        }

        public void AddMessage(NetIncomingMessage msg)
        {
            try
            {
                byte server = msg.ReadByte();
                gameServers[server].AddMessage(msg);
            }
            catch (Exception e)
            {
                logger.Warn("Unknown exception adding GameServer Message: ", e);
                networkManager.RecycleMessage(msg);
            }
        }

        public int ServerCount()
        {
            return gameServers.Count;
        }

        public void Shutdown()
        {
            foreach (var s in gameServers)
            {
                s.Value.Shutdown();
            }
            poolRunning = false;
        }

        public void AddServer(IEnumerable<Player> players)
        {
            var newServer = new GameServer(networkManager, players, true);
            byte id;
            if (gameServers.Count>0)
                id=(byte)(gameServers.Keys.Max()+1);
            else
                id=0;
            
            gameServers.Add(id, newServer);
        }

        public void Tick()
        {
            while (poolRunning)
            {
                foreach (var s in gameServers)
                {
                    s.Value.ProcessMessage();
                }
                Thread.Sleep(10);
            }
        }

    }
    class GameManager
    {
        private Dictionary<byte, ServerPool> gameServers;
        private INetworkManager networkManager;
        private int maxServersPerPool = 30;
        private int serversPerPool = 1;
        private int maxPools = 10;
        private Logger logger;

        public GameManager(INetworkManager network)
        {
            networkManager = network;
            gameServers = new Dictionary<byte, ServerPool>(maxPools);
            for (int i = 0; i < 10; i++)
            {
                gameServers.Add((byte)i,new ServerPool(networkManager));
            }
            logger = LogManager.GetCurrentClassLogger();
        }

        public void AddMessage(NetIncomingMessage msg)
        {
            try
            {
                byte pool = msg.ReadByte();
                gameServers[pool].AddMessage(msg);
            }
            catch (Exception e)
            {
                logger.Warn("Unknown exception adding GameServer Message: ", e);
                networkManager.RecycleMessage(msg);
            }
        }

        public bool AddServer(IEnumerable<Player> players)
        {
            foreach (var server in gameServers)
            {
                if (server.Value.ServerCount() < serversPerPool)
                {
                    server.Value.AddServer(players);
                    return true;
                }
            }

            //All the servers are currently load balanced at capacity, try to increase capacity
            if (serversPerPool < maxServersPerPool)
            {
                serversPerPool++;
            }
            else
            {
                logger.Warn("Server at capacity {0} pools {1} servers per pool", gameServers.Count, serversPerPool);
                return false;
            }

            foreach (var server in gameServers)
            {
                if (server.Value.ServerCount() < serversPerPool)
                {
                    server.Value.AddServer(players);
                    return true;
                }
            }

            logger.Error("Could not allocate a server.  Current capacity {0} pools {1} servers per pool", gameServers.Count, serversPerPool);
            return false;
        }
    }
}

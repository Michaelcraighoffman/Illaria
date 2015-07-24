using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;

namespace IllariaShared
{
    public class Player
    {
        public long UniqueId { get { return uniqueId; } }
        private long uniqueId;
        private Point destination;

        public Player(int id)
        {
            uniqueId = id;
        }

        public void UpdateDestination(Point target)
        {
            destination = target;
        }

    }
}

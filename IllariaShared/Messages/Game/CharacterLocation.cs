using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lidgren.Network;

namespace IllariaShared
{
    struct CharacterLocation
    {
        public Int16 CharacterId { get; set; }
        public Int32 x { get; set; }
        public Int32 y { get; set; }
    }
}


﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IllariaShared
{
    public enum MessageDestination : byte 
    {
        /// <summary>
        /// Should not appear in practice
        /// </summary>
        Unknown=0,  
        /// <summary>
        /// Messages intended to be handled by the system 
        /// </summary>
        System=1,
        /// <summary>
        /// Messages sent by users interacting with the lobby
        /// </summary>
        Lobby=2,
        /// <summary>
        /// Messages for gameplay
        /// </summary>
        Game=3
    }

    public enum SystemMessageType : byte
    {
        /// <summary>
        /// Should not appear in practice
        /// </summary>
        Unknown=0,
        /// <summary>
        /// Returns the current number of unprocessed network messages
        /// </summary>
        GetMessageLagTime=1,
        /// <summary>
        /// Returns the current number of connected users
        /// </summary>
        GetConnectedUsers=2
    }

    public enum ReliableSequencedChannels : byte
    {
        SystemMessageLagTime=0
    }
    public static class Constants
    {
    }
}
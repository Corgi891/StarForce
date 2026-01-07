//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Network;
using ProtoBuf;

namespace StarForce
{
    public abstract class PacketBase : Packet
    {
        public PacketBase()
        {
        }

        public abstract PacketType PacketType
        {
            get;
        }
    }
}

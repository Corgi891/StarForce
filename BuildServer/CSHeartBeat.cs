using ProtoBuf;
using System;

namespace StarForce
{
    [Serializable, ProtoContract(Name = @"CSHeartBeat")]
    public class CSHeartBeat : CSPacketBase
    {
        public CSHeartBeat()
        {
        }

        [ProtoIgnore] // Id 不应该在消息包体中序列化，它只在消息包头中
        public override int Id
        {
            get
            {
                return 1;
            }
        }

        public override void Clear()
        {
        }
    }
}


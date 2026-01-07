using ProtoBuf;

namespace StarForce
{
    [ProtoContract]
    public sealed class SCPacketHeader : PacketHeaderBase
    {
        public override PacketType PacketType
        {
            get { return PacketType.ServerToClient; }
        }

        // 重新定义字段，确保 ProtoBuf 能够正确序列化
        [ProtoMember(1)]
        public new int Id
        {
            get { return base.Id; }
            set { base.Id = value; }
        }

        [ProtoMember(2)]
        public new int PacketLength
        {
            get { return base.PacketLength; }
            set { base.PacketLength = value; }
        }
    }
}


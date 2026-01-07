using ProtoBuf;

namespace StarForce
{
    [ProtoContract]
    public sealed class CSPacketHeader : PacketHeaderBase
    {
        public override PacketType PacketType
        {
            get { return PacketType.ClientToServer; }
        }

        // 重新定义字段，确保 ProtoBuf 能够正确序列化（与客户端保持一致）
        [ProtoMember(1)]
        public new int Id
        {
            get;
            set;
        }

        [ProtoMember(2)]
        public new int PacketLength
        {
            get;
            set;
        }
    }
}


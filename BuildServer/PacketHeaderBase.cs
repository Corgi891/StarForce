using ProtoBuf;

namespace StarForce
{
    public abstract class PacketHeaderBase
    {
        public abstract PacketType PacketType { get; }

        [ProtoMember(1)]
        public int Id { get; set; }

        [ProtoMember(2)]
        public int PacketLength { get; set; }

        public bool IsValid
        {
            get
            {
                return PacketType != PacketType.Undefined && Id > 0 && PacketLength >= 0;
            }
        }

        public void Clear()
        {
            Id = 0;
            PacketLength = 0;
        }
    }
}


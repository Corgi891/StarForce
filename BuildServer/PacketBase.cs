namespace StarForce
{
    public abstract class PacketBase
    {
        public abstract PacketType PacketType { get; }
        public abstract int Id { get; }
        public abstract void Clear();
    }
}


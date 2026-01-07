using ProtoBuf;
using System;

namespace StarForce
{
    [Serializable, ProtoContract(Name = @"CSTestLogin")]
    public class CSTestLogin : CSPacketBase
    {
        public CSTestLogin()
        {
        }

        [ProtoIgnore] // Id 不应该在消息包体中序列化，它只在消息包头中
        public override int Id
        {
            get
            {
                return 100; // 测试登录包ID
            }
        }

        [ProtoMember(1)]
        public string Username { get; set; }

        [ProtoMember(2)]
        public string Password { get; set; }

        public override void Clear()
        {
            Username = null;
            Password = null;
        }
    }
}


using ProtoBuf;
using System;

namespace StarForce
{
    [Serializable, ProtoContract(Name = @"SCTestLogin")]
    public class SCTestLogin : SCPacketBase
    {
        public SCTestLogin()
        {
        }

        [ProtoIgnore] // Id 不应该在消息包体中序列化，它只在消息包头中
        public override int Id
        {
            get
            {
                return 101; // 测试登录响应包ID
            }
        }

        [ProtoMember(1)]
        public bool Success { get; set; }

        [ProtoMember(2)]
        public string Message { get; set; }

        [ProtoMember(3)]
        public int UserId { get; set; }

        public override void Clear()
        {
            Success = false;
            Message = null;
            UserId = 0;
        }
    }
}


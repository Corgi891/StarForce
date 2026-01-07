//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

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


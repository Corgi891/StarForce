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
    [Serializable, ProtoContract(Name = @"CSTestLogin")]
    public class CSTestLogin : CSPacketBase
    {
        public CSTestLogin()
        {
        }

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


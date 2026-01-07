//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework.Network;
using UnityGameFramework.Runtime;

namespace StarForce
{
    public class SCTestLoginHandler : PacketHandlerBase
    {
        public override int Id
        {
            get
            {
                return 101; // 测试登录响应包ID
            }
        }

        public override void Handle(object sender, Packet packet)
        {
            SCTestLogin packetImpl = (SCTestLogin)packet;
            // Log.Info("收到登录响应: Success={0}, Message={1}, UserId={2}", 
            //     packetImpl.Success, packetImpl.Message, packetImpl.UserId);
        }
    }
}


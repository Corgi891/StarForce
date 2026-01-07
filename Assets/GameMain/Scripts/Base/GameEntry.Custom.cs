//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using UnityEngine;

namespace StarForce
{
    /// <summary>
    /// 游戏入口。
    /// </summary>
    public partial class GameEntry : MonoBehaviour
    {
        public static BuiltinDataComponent BuiltinData
        {
            get;
            private set;
        }

        public static HPBarComponent HPBar
        {
            get;
            private set;
        }

        /// <summary>
        /// 获取游戏网络管理器。
        /// </summary>
        public static GameNetworkManager NetworkManager
        {
            get;
            private set;
        }

        private void InitCustomComponents()
        {
            BuiltinData = UnityGameFramework.Runtime.GameEntry.GetComponent<BuiltinDataComponent>();
            HPBar = UnityGameFramework.Runtime.GameEntry.GetComponent<HPBarComponent>();
            
            // 尝试获取网络管理器组件
            NetworkManager = UnityGameFramework.Runtime.GameEntry.GetComponent<GameNetworkManager>();
            
            // 如果场景中没有该组件，则自动创建
            if (NetworkManager == null)
            {
                // 查找或创建 Customs 节点
                GameObject customsNode = GameObject.Find("Customs");
                if (customsNode == null)
                {
                    customsNode = new GameObject("Customs");
                    UnityGameFramework.Runtime.Log.Info("Customs node was automatically created.");
                }
                
                // 在 Customs 节点下创建 Network Manager GameObject
                GameObject networkManagerGO = new GameObject("Network Manager");
                networkManagerGO.transform.SetParent(customsNode.transform);
                
                // 添加 GameNetworkManager 组件
                NetworkManager = networkManagerGO.AddComponent<GameNetworkManager>();
                UnityGameFramework.Runtime.Log.Info("GameNetworkManager component was automatically created under Customs node.");
            }
        }
    }
}

//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityGameFramework.Editor.ResourceTools;

namespace StarForce.Editor
{
    public sealed class StarForceBuildEventHandler : IBuildEventHandler
    {
        // 保存打包参数，用于生成版本文件
        private string m_OutputDirectory;
        private string m_ApplicableGameVersion;
        private int m_InternalResourceVersion;

        public bool ContinueOnFailure
        {
            get
            {
                return false;
            }
        }

        public void OnPreprocessAllPlatforms(string productName, string companyName, string gameIdentifier, string gameFrameworkVersion, string unityVersion, string applicableGameVersion, int internalResourceVersion,
            Platform platforms, AssetBundleCompressionType assetBundleCompression, string compressionHelperTypeName, bool additionalCompressionSelected, bool forceRebuildAssetBundleSelected, string buildEventHandlerTypeName, string outputDirectory, BuildAssetBundleOptions buildAssetBundleOptions,
            string workingPath, bool outputPackageSelected, string outputPackagePath, bool outputFullSelected, string outputFullPath, bool outputPackedSelected, string outputPackedPath, string buildReportPath)
        {
            // 保存打包参数
            m_OutputDirectory = outputDirectory;
            m_ApplicableGameVersion = applicableGameVersion;
            m_InternalResourceVersion = internalResourceVersion;

            string streamingAssetsPath = Utility.Path.GetRegularPath(Path.Combine(Application.dataPath, "StreamingAssets"));
            string[] fileNames = Directory.GetFiles(streamingAssetsPath, "*", SearchOption.AllDirectories);
            foreach (string fileName in fileNames)
            {
                if (fileName.Contains(".gitkeep"))
                {
                    continue;
                }

                File.Delete(fileName);
            }

            Utility.Path.RemoveEmptyDirectory(streamingAssetsPath);
        }

        public void OnPostprocessAllPlatforms(string productName, string companyName, string gameIdentifier, string gameFrameworkVersion, string unityVersion, string applicableGameVersion, int internalResourceVersion,
            Platform platforms, AssetBundleCompressionType assetBundleCompression, string compressionHelperTypeName, bool additionalCompressionSelected, bool forceRebuildAssetBundleSelected, string buildEventHandlerTypeName, string outputDirectory, BuildAssetBundleOptions buildAssetBundleOptions,
            string workingPath, bool outputPackageSelected, string outputPackagePath, bool outputFullSelected, string outputFullPath, bool outputPackedSelected, string outputPackedPath, string buildReportPath)
        {
        }

        public void OnPreprocessPlatform(Platform platform, string workingPath, bool outputPackageSelected, string outputPackagePath, bool outputFullSelected, string outputFullPath, bool outputPackedSelected, string outputPackedPath)
        {
        }

        public void OnBuildAssetBundlesComplete(Platform platform, string workingPath, bool outputPackageSelected, string outputPackagePath, bool outputFullSelected, string outputFullPath, bool outputPackedSelected, string outputPackedPath, AssetBundleManifest assetBundleManifest)
        {
        }

        /// <summary>
        /// 输出版本列表数据时自动生成 {Platform}Version.txt 文件
        /// </summary>
        public void OnOutputUpdatableVersionListData(Platform platform, string versionListPath, int versionListLength, int versionListHashCode, int versionListCompressedLength, int versionListCompressedHashCode)
        {
            // 生成版本信息 JSON
            VersionInfoData versionInfo = new VersionInfoData
            {
                ForceUpdateGame = false,
                LatestGameVersion = m_ApplicableGameVersion,
                InternalGameVersion = 0,
                InternalResourceVersion = m_InternalResourceVersion,
                UpdatePrefixUri = GetUpdatePrefixUri(platform),
                VersionListLength = versionListLength,
                VersionListHashCode = versionListHashCode,
                VersionListCompressedLength = versionListCompressedLength,
                VersionListCompressedHashCode = versionListCompressedHashCode
            };

            // 生成版本文件路径：Build/{Platform}Version.txt
            string versionFilePath = Path.Combine(m_OutputDirectory, $"{platform}Version.txt");
            string versionJson = JsonUtility.ToJson(versionInfo, true);
            File.WriteAllText(versionFilePath, versionJson);

            Debug.Log($"[StarForceBuildEventHandler] 已生成版本文件: {versionFilePath}");
        }

        /// <summary>
        /// 获取资源更新前缀 URI
        /// </summary>
        private string GetUpdatePrefixUri(Platform platform)
        {
            // 根据实际部署情况修改这里的服务器地址
            // 格式：{服务器地址}/Full/{版本号}/{平台}/
            string versionFolder = $"{m_ApplicableGameVersion.Replace('.', '_')}_{m_InternalResourceVersion}";
            
            // 这里可以根据不同平台返回不同的服务器地址
            // 本地测试使用 localhost，正式环境替换为实际服务器地址
            return $"http://localhost:8080/Full/{versionFolder}/{platform}";
        }

        /// <summary>
        /// 版本信息数据结构（与运行时 VersionInfo 相同）
        /// </summary>
        [System.Serializable]
        private class VersionInfoData
        {
            public bool ForceUpdateGame;
            public string LatestGameVersion;
            public int InternalGameVersion;
            public int InternalResourceVersion;
            public string UpdatePrefixUri;
            public int VersionListLength;
            public int VersionListHashCode;
            public int VersionListCompressedLength;
            public int VersionListCompressedHashCode;
        }

        public void OnPostprocessPlatform(Platform platform, string workingPath, bool outputPackageSelected, string outputPackagePath, bool outputFullSelected, string outputFullPath, bool outputPackedSelected, string outputPackedPath, bool isSuccess)
        {
            if (!outputPackageSelected)
            {
                return;
            }

            if (platform != Platform.Windows)
            {
                return;
            }

            string streamingAssetsPath = Utility.Path.GetRegularPath(Path.Combine(Application.dataPath, "StreamingAssets"));
            string[] fileNames = Directory.GetFiles(outputPackagePath, "*", SearchOption.AllDirectories);
            foreach (string fileName in fileNames)
            {
                string destFileName = Utility.Path.GetRegularPath(Path.Combine(streamingAssetsPath, fileName.Substring(outputPackagePath.Length)));
                FileInfo destFileInfo = new FileInfo(destFileName);
                if (!destFileInfo.Directory.Exists)
                {
                    destFileInfo.Directory.Create();
                }

                File.Copy(fileName, destFileName);
            }
        }
    }
}

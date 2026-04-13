using PluginSet.Core;
using PluginSet.Core.Editor;
using UnityEngine;

namespace PluginSet.AndroidNativeAudio.Editor
{
    [BuildChannelsParams("AndroidNativeAudio", "Android原生音频")]
    [VisibleCaseBoolValue("SupportAndroid", true)]
    public class BuildAndroidNativeAudioParams : ScriptableObject
    {
        [Tooltip("是否启用ANA")]
        public bool Enable = true;

        [Tooltip("是否从本地文件中加载音频文件")]
        public bool LoadNativeAudioFromCache = true;

        [Tooltip("是否自动加载所有音频文件")]
        public bool AutoLoadAllAudios = true;
    }
}
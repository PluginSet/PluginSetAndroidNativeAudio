using System;
using PluginSet.Core;
using PluginSet.Core.Editor;
using UnityEngine;

namespace PluginSet.AndroidNativeAudio.Editor
{
    [BuildChannelsParams("AndroidNativeAudio", "Android原生音频")]
    [VisibleCaseBoolValue("SupportAndroid", true)]
    public class BuildAndroidNativeAudioParams : ScriptableObject
    {
        [Serializable]
        public struct NativeAudioConfig
        {
            public AudioClip AudioClip;
            public AudioType Type;
            public bool AutoPreload;
            public bool UseUnityAudio;
        }

        [Tooltip("强制使用UnityAudio")]
        public bool UseUnityAudioForce = false;

        [Tooltip("是否从本地文件中加载音频文件")]
        public bool LoadNativeAudioFromCache = true;

        [Tooltip("最大音效播放通道")]
        public int MaxSoundStream = 16;

        [Tooltip("原生音频配置")]
        [SerializableDict("stringValue", "")]
        public SerializableDict<string, NativeAudioConfig> NativeAudioConfigs = new SerializableDict<string, NativeAudioConfig>();
    }
}
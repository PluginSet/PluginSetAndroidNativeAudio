using System;
using System.Collections.Generic;
using UnityEngine;

namespace PluginSet.AndroidNativeAudio
{
    public enum AudioType
    {
        Music,
        Sound
    }
    
    public class PluginAndroidNativeAudioConfig : ScriptableObject
    {
        [Serializable]
        public struct NativeAudioConfig
        {
            public AudioType Type;
            public string Name;
            public AudioClip Asset;
            public string AssetPath;
            public bool AutoPreload;
        }

        public int MaxSoundStream;
        public List<NativeAudioConfig> NativeAudioConfigs = new List<NativeAudioConfig>();
    }
}
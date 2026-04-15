using System;
using System.Collections.Generic;
using UnityEngine;

namespace PluginSet.AndroidNativeAudio
{
    [Serializable]
    public struct NativeAudioConfig
    {
        public string Name;
        public string FileName;
        public string AssetPath;
        public string OwnPatch;
        public string BundleName;
    }
    
    public class PluginAndroidNativeAudioConfig : ScriptableObject
    {
        public List<NativeAudioConfig> NativeAudioConfigs = new List<NativeAudioConfig>();
        
        public bool AutoLoadAllAudios = true;
    }
}
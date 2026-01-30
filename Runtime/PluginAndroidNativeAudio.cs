using System;
using System.Collections;
using System.Collections.Generic;
using PluginSet.Core;
using UnityEngine;

namespace PluginSet.AndroidNativeAudio
{
    [PluginRegister]
    public class PluginAndroidNativeAudio : PluginBase, IStartPlugin
    {
        public override string Name => "AndroidNativeAudio";

        public int StartOrder => PluginsStartOrder.Default;
        
        public bool IsRunning { get; private set; }

        private int _maxSoundStream;
        private List<PluginAndroidNativeAudioConfig.NativeAudioConfig> _audioConfigs;

        protected override void Init(PluginSetConfig config)
        {
            _audioConfigs = new List<PluginAndroidNativeAudioConfig.NativeAudioConfig>();
            var cfg = config.Get<PluginAndroidNativeAudioConfig>();
            _maxSoundStream = cfg.MaxSoundStream;
            _audioConfigs.AddRange(cfg.NativeAudioConfigs);
            
            AddEventListener(PluginConstants.NOTIFY_APPLICATION_ENTER_BACKGROUND, () =>
            {
                ANASoundsManager.OnApplicationPause(true);
            });
            
            AddEventListener(PluginConstants.NOTIFY_APPLICATION_ENTER_FOREGROUND, () =>
            {
                ANASoundsManager.OnApplicationPause(false);
            });
        }

        public IEnumerator StartPlugin()
        {
            ANASoundsManager.Init(_maxSoundStream, _audioConfigs);
            IsRunning = true;
            yield break;
        }

        public void DisposePlugin(bool isAppQuit = false)
        {
            ANASoundsManager.Release();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            ANASoundsManager.OnApplicationPause(pauseStatus);
        }
    }
}
#if ENABLE_ANA_PLUGIN
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

        private List<NativeAudioConfig> _audioConfigs;
        private bool _autoPreloadAllAudios = false;

        protected override void Init(PluginSetConfig config)
        {
            _audioConfigs = new List<NativeAudioConfig>();
            var cfg = config.Get<PluginAndroidNativeAudioConfig>();
            _autoPreloadAllAudios = cfg.AutoLoadAllAudios;
#if UNITY_EDITOR
            _audioConfigs.AddRange(cfg.NativeAudioConfigs);
#endif
            
            AddEventListener(PluginConstants.NOTIFY_APPLICATION_ENTER_BACKGROUND, () =>
            {
                ANASoundsManager.OnApplicationPause(true);
            });
            
            AddEventListener(PluginConstants.NOTIFY_APPLICATION_ENTER_FOREGROUND, () =>
            {
                ANASoundsManager.OnApplicationPause(false);
            });

            if (_autoPreloadAllAudios)
            {
                AddEventListener(PluginConstants.NOTIFY_ON_SEARCH_PATH_ADDED, (context) =>
                {
                    var patchName = (string)context.Data;
                    ANASoundsManager.PreloadAllInPatch(patchName);
                });
                
                AddEventListener(PluginConstants.NOTIFY_ON_SEARCH_PATH_REMOVE, (context) =>
                {
                    var patchName = (string)context.Data;
                    ANASoundsManager.ReleaseAllInPatch(patchName);
                });
            }
        }

        public IEnumerator StartPlugin()
        {
            ANASoundsManager.Init(_audioConfigs);
            if (_autoPreloadAllAudios)
            {
                ANASoundsManager.PreloadAllInPatch(ResourcesManager.Instance.StreamingAssetsName);
                ANASoundsManager.Logger.Debug($"start preload patches : {string.Join(",", ResourcesManager.Instance.SearchPaths)}");
                foreach (var patch in ResourcesManager.Instance.SearchPaths)
                {
                    ANASoundsManager.PreloadAllInPatch(patch);
                }
            }
            IsRunning = true;
            yield break;
        }

        public void DisposePlugin(bool isAppQuit = false)
        {
            ANASoundsManager.Release();
            if (isAppQuit)
                ANASoundsManager.Purge();
            IsRunning = false;
        }

        private void Update()
        {
            ANASoundsManager.Update(Time.deltaTime);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            ANASoundsManager.OnApplicationPause(pauseStatus);
        }
    }
}
#endif
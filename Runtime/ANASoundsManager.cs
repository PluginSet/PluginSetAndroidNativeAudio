#if ENABLE_ANA_PLUGIN
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PluginSet.Core;
using UnityEngine;
using Logger = PluginSet.Core.Logger;

namespace PluginSet.AndroidNativeAudio
{
    public static class ANASoundsManager
    {
        internal static readonly Logger Logger = LoggerManager.GetLogger("ANASoundsManager");

        private static readonly int InvalidAudioId = AudioEngineBridge.InvalidAudioId;
        
        private static bool _isPaused = false;
        private static readonly Dictionary<string, string> PreloadAudios = new Dictionary<string, string>();
        
        private static float _musicBaseVolume = 1.0f;
        private static float _soundBaseVolume = 1.0f;

        private static bool _musicEnable = true;
        private static bool _soundEnable = true;

        private static int _round = 0;

        private static readonly Dictionary<string, string> PatchResourceVersion = new Dictionary<string, string>();
        private static readonly Dictionary<string, Dictionary<string, NativeAudioConfig>> NativeAudioConfigs = new Dictionary<string, Dictionary<string, NativeAudioConfig>>();
        private static readonly Dictionary<string, List<NativeAudioConfig>> PatchAudioConfigs = new Dictionary<string, List<NativeAudioConfig>>();

        private static string _currentMusic;

        private static string _patchesSavePrefix = "";

        private static NativeAudioConfig? GetAudio(string name)
        {
            name = name.ToLower();
            if (!NativeAudioConfigs.TryGetValue(name, out var dict))
                return null;

            if (dict.Count == 1)
            {
                return dict.First().Value;
            }
            
            var searchPatches = ResourcesManager.Instance.SearchPaths;
            foreach (var patch in searchPatches)
            {
                if (dict.TryGetValue(patch, out var config))
                    return config;

                return null;
            }

            return null;
        }
        
        private static string GetFileName(in NativeAudioConfig config)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(_patchesSavePrefix))
            {
                return config.AssetPath;
            }
#endif
            var name = config.FileName;
            var fileName = ResourcesManager.Instance.GetRealFilePath(name);
            if (string.IsNullOrEmpty(fileName))
                return null;

            return _patchesSavePrefix + fileName;
        }

        private static bool UsePersistentDataPath
        {
            get
            {
#if ANA_USE_PERSISTENT_DATA_PATH
                return true;
#else
                return false;
#endif
            }
        }

        private static string CacheKey(in NativeAudioConfig config)
        {
            return $"{config.Name}_{config.OwnPatch}_{_round}";
        }
        
        
        public static void OnApplicationPause(bool pauseStatus)
        {
            if (_isPaused == pauseStatus)
                return;

            _isPaused = pauseStatus;
            if (_isPaused)
                AudioEngineBridge.EnterBackground();
            else
                AudioEngineBridge.EnterForeground();
        }
        

        [AOT.MonoPInvokeCallback(typeof(AudioEngineBridge.LogCallback))]
        private static void LogCallback(string msg)
        {
            Debug.Log("[AudioEngine] " + msg);
        }

        public static void Init(List<NativeAudioConfig> nativeAudioConfigs)
        {
            NativeAudioConfigs.Clear();
            _round++;

#if DEBUG
            AudioEngineBridge.ccau_audio_set_log_callback(LogCallback);
#endif
            AudioEngineBridge.LazyInit();

#if ANA_USE_PERSISTENT_DATA_PATH && !UNITY_EDITOR
            _patchesSavePrefix = Path.GetFullPath(ResourcesManager.PatchesSavePath).Replace("\\", "/");
#else
            _patchesSavePrefix = ResourcesManager.StreamingInternalUrlPrefix.Replace("\\", "/");
#endif
            if (!_patchesSavePrefix.EndsWith("/"))
                _patchesSavePrefix += "/";

            Logger.Debug($"init:::::: completed patchSave {_patchesSavePrefix}");
            
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(_patchesSavePrefix) && Directory.Exists(_patchesSavePrefix))
                return;

            _patchesSavePrefix = "";
            foreach (var nativeAudioConfig in nativeAudioConfigs)
            {
                if (!NativeAudioConfigs.TryGetValue(nativeAudioConfig.Name, out var dict))
                {
                    dict = new Dictionary<string, NativeAudioConfig>();
                    NativeAudioConfigs.Add(nativeAudioConfig.Name, dict);
                }

                if (!PatchAudioConfigs.TryGetValue(nativeAudioConfig.OwnPatch, out var list))
                {
                    list = new List<NativeAudioConfig>();
                    PatchAudioConfigs.Add(nativeAudioConfig.OwnPatch, list);
                }
                
                dict.Add(nativeAudioConfig.OwnPatch, nativeAudioConfig);
                list.Add(nativeAudioConfig);
                if (!PatchResourceVersion.ContainsKey(nativeAudioConfig.OwnPatch))
                    PatchResourceVersion.Add(nativeAudioConfig.OwnPatch, "0.0.0-0");
            }
#endif
        }

        private static void LoadAudioConfig(string patchName)
        {
            var newVersion = ResourcesManager.Instance.GetPatchVersion(patchName);
            Logger.Debug($"LoadAudioConfig >>>>>> get version {patchName}:: {newVersion}");
            if (string.IsNullOrEmpty(newVersion))
                return;

            if (PatchResourceVersion.TryGetValue(patchName, out var version))
            {
                if (version == newVersion)
                    return;
                
#if !UNITY_EDITOR
                ClearAudioConfig(patchName);
#endif
            }
            
            var jsonContent = ResourcesManager.Instance.ReadFile($"audio_config_{patchName.ToLower()}.json");
            if (jsonContent != null)
            {
                var json = JsonUtil.FromJson<List<NativeAudioConfig>>(Encoding.UTF8.GetString(jsonContent));
                if (json != null)
                {
                    ClearAudioConfig(patchName);
                    
                    foreach (var nativeAudioConfig in json)
                    {
                        if (!NativeAudioConfigs.TryGetValue(nativeAudioConfig.Name, out var dict))
                        {
                            dict = new Dictionary<string, NativeAudioConfig>();
                            NativeAudioConfigs.Add(nativeAudioConfig.Name, dict);
                        }

                        if (!PatchAudioConfigs.TryGetValue(nativeAudioConfig.OwnPatch, out var list))
                        {
                            list = new List<NativeAudioConfig>();
                            PatchAudioConfigs.Add(nativeAudioConfig.OwnPatch, list);
                        }
                
                        dict.Add(nativeAudioConfig.OwnPatch, nativeAudioConfig);
                        list.Add(nativeAudioConfig);
                    }
                    
                    Logger.Debug($"Add {json.Count} native audio config in patch {patchName}, update to version {newVersion}");
                }
                PatchResourceVersion.Add(patchName, newVersion);
            }
        }

        public static void ClearAudioConfig(string patchName)
        {
            foreach (var pair in NativeAudioConfigs)
            {
                pair.Value.Remove(patchName);
            }

            PatchAudioConfigs.Remove(patchName);
            PatchResourceVersion.Remove(patchName);
        }

        public static void Update(float dt)
        {
            AudioEngineBridge.Tick(dt);
        }

        public static void Release()
        {
            AudioEngineBridge.UncacheAll();
            
            PreloadAudios.Clear();
        }

        public static void Purge()
        {
            AudioEngineBridge.End();
        }
        
        public static void PreloadMusic(string name)
        {
            PreloadAudio(name);
        }

        public static void PreloadSound(string name)
        {
            PreloadAudio(name);
        }

        public static void PreloadAudio(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            name = name.ToLower();
            if (!NativeAudioConfigs.ContainsKey(name))
                return;
            
            PreloadAnaAudio(name);
        }

        public static void PreloadAllInPatch(string patchName)
        {
            Logger.Debug($"PreloadAllInPatch >>>>>>>>>>>>>>>>> {patchName}");
            LoadAudioConfig(patchName);
            
            if (!PatchAudioConfigs.TryGetValue(patchName, out var list))
                return;

            foreach (var config in list)
            {
                PreloadAnaAudio(CacheKey(config), GetFileName(config));
            }
        }

        public static void ReleaseMusic(string name)
        {
            ReleaseAudio(name);
        }

        public static void ReleaseSound(string name)
        {
            ReleaseAudio(name);
        }
        
        public static void ReleaseAudio(string name)
        {
            var config = GetAudio(name);
            if (!config.HasValue)
            {
                ReleaseAnaAudio(name);
            }
            else
            {
                ReleaseAnaAudio(CacheKey(config.Value));
            }
        }

        private static void ReleaseAnaAudio(string key)
        {
            if (PreloadAudios.TryGetValue(key, out var fileName))
            {
                AudioEngineBridge.Uncache(fileName);
                PreloadAudios.Remove(key);
            }
        }
            

        public static void ReleaseAllInPatch(string patchName)
        {
            if (!PatchAudioConfigs.TryGetValue(patchName, out var list))
                return;

            foreach (var config in list)
            {
                ReleaseAnaAudio(CacheKey(config));
            }
        }

        private static string PreloadAnaAudio(string key, string fileName)
        {
            Logger.Debug($"PreloadAnaSound ==================== = {key}:: {fileName}");
            if (string.IsNullOrEmpty(fileName))
                return fileName;
            
            if (PreloadAudios.ContainsKey(key))
            {
                Logger.Debug($"PreloadAnaSound ==================== preload = {key}:: fileName::  {fileName}");
                return fileName;
            }
            
            AudioEngineBridge.Preload(fileName);
            PreloadAudios.Add(key, fileName);
            return fileName;
        }

        private static string PreloadAnaAudio(string name)
        {
            var config = GetAudio(name);
            if (!config.HasValue)
            {
                return PreloadAnaAudio(name, name);
            }
            else
            {
                return PreloadAnaAudio(CacheKey(config.Value), GetFileName(config.Value));
            }
        }
        
        public static void PlayMusic(string musicName, bool replay = false)
        {
            var fileName = PreloadAnaAudio(musicName);
            if (string.IsNullOrEmpty(fileName))
                return;
            
            if (replay)
                AudioEngineBridge.StopMusic();
            
            AudioEngineBridge.PlayMusic(fileName, _musicBaseVolume, true);
            _currentMusic = musicName;
            if (!_musicEnable)
                AudioEngineBridge.PauseMusic();
        }

        public static void StopMusic(string musicName = "")
        {
            if (string.IsNullOrEmpty(musicName) || musicName == _currentMusic)
            {
                AudioEngineBridge.StopMusic();
            }
        }

        public static void PlaySound(string soundName, float volume = -1)
        {
            if (!_soundEnable)
                return;
            
            var fileName = PreloadAnaAudio(soundName);
            if (string.IsNullOrEmpty(fileName))
                return;

            if (volume < 0)
                volume = _soundBaseVolume;
            else
                volume = _soundBaseVolume * volume;
            
            AudioEngineBridge.PlaySound(fileName, volume, false);
        }

        public static void SetMusicVolume(float volume)
        {
            _musicBaseVolume = volume;
            AudioEngineBridge.SetMusicVolume(_musicEnable ? volume : 0);
        }

        public static void SetSoundVolume(float volume)
        {
            _soundBaseVolume = volume;
            AudioEngineBridge.SetSoundVolume(volume);
        }

        public static void SetMusicEnable(bool enable)
        {
            if (_musicEnable == enable)
                return;

            _musicEnable = enable;
            AudioEngineBridge.SetMusicVolume(enable ? _musicBaseVolume : 0);
            if (enable)
                AudioEngineBridge.ResumeMusic();
            else
                AudioEngineBridge.PauseMusic();
        }

        public static void SetSoundEnable(bool enable)
        {
            if (_soundEnable == enable)
                return;

            _soundEnable = enable;
            AudioEngineBridge.SetSoundEnabled(enable);
            if (!enable)
                AudioEngineBridge.StopAllSounds();
        }

        public static void PauseMusic()
        {
            AudioEngineBridge.PauseMusic();
        }

        public static void ResumeMusic()
        {
            AudioEngineBridge.ResumeMusic();
        }
    }
}
#endif
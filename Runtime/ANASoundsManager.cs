using System;
using System.Collections.Generic;
using System.IO;
using PluginSet.Core;
using UnityEngine;

namespace PluginSet.AndroidNativeAudio
{
    public static class ANASoundsManager
    {
        private static bool _isPaused = false;
        private static Dictionary<string, int> _preloadMusics = new Dictionary<string, int>();
        private static HashSet<string> _loadingMusics = new HashSet<string>();
        private static Dictionary<string, int> _preloadSounds = new Dictionary<string, int>();
        private static HashSet<string> _loadingSounds = new HashSet<string>();

        private static int _round = 0;
        
        private static Dictionary<string, PluginAndroidNativeAudioConfig.NativeAudioConfig> _nativeAudioConfigs = new Dictionary<string, PluginAndroidNativeAudioConfig.NativeAudioConfig>();

        private static string _currentMusic;
        private static int _currentMusicId;

#if ANA_USE_PERSISTENT_DATA_PATH
        private static string _patchesSavePrefix = "";
#endif
        
        private static string GetFileName(string name)
        {
#if ANA_USE_PERSISTENT_DATA_PATH
            return _patchesSavePrefix + name;
#else
            return name;
#endif
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

        private static string CacheKey(string name)
        {
            return $"{name}_{_round}";
        }
        
        
        public static void OnApplicationPause(bool pauseStatus)
        {
            if (_isPaused == pauseStatus)
                return;

            _isPaused = pauseStatus;
            ANAMusic.OnApplicationPause(pauseStatus);
        }

        public static void Init(int maxSoundStream, List<PluginAndroidNativeAudioConfig.NativeAudioConfig> nativeAudioConfigs)
        {
            AndroidNativeAudio.makePool(maxSoundStream);
            _nativeAudioConfigs.Clear();
            _round++;

#if ANA_USE_PERSISTENT_DATA_PATH
            var persistent = Path.GetFullPath(Application.persistentDataPath).Replace("\\", "/");
            var savePath = Path.GetFullPath(ResourcesManager.PatchesSavePath).Replace("\\", "/");
            if (!persistent.EndsWith("/"))
                persistent += "/";
            _patchesSavePrefix = savePath.Replace(persistent, "");
            if (!_patchesSavePrefix.EndsWith("/"))
                _patchesSavePrefix += "/";
#endif
            
            foreach (var nativeAudioConfig in nativeAudioConfigs)
            {
                _nativeAudioConfigs.Add(nativeAudioConfig.Name, nativeAudioConfig);
                if (nativeAudioConfig.AutoPreload)
                {
                    switch (nativeAudioConfig.Type)
                    {
                        case AudioType.Music:
                            PreloadMusic(nativeAudioConfig.Name);
                            break;
                        case AudioType.Sound:
                            PreloadSound(nativeAudioConfig.Name);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        public static void Release()
        {
            foreach (var pair in _preloadMusics)
            {
                ANAMusic.release(pair.Value);
            }

            foreach (var pair in _preloadSounds)
            {
                AndroidNativeAudio.unload(pair.Value);
            }
            
            _preloadMusics.Clear();
            _preloadSounds.Clear();
            _loadingMusics.Clear();
            _loadingSounds.Clear();
        }

        private static void PreloadMusic(string name)
        {
    // #if UNITY_ANDROID && !UNITY_EDITOR
            PreloadAnaMusic(name);
    // #endif
        }

        private static void PreloadSound(string name)
        {
// #if UNITY_ANDROID && !UNITY_EDITOR
            PreloadAnaSound(name);
// #endif
        }
        
        private static int PreloadAnaMusic(string name)
        {
            var key = CacheKey(name);
            if (!_loadingMusics.Add(key))
                return -1;

            if (_preloadMusics.TryGetValue(key, out var streamId))
                return streamId;

            var id = ANAMusic.load(GetFileName(name), UsePersistentDataPath, true, id =>
            {
                if (_preloadMusics.TryGetValue(key, out var value))
                {
                    _loadingMusics.Remove(key);
                    if (value != id)
                    {
                        ANAMusic.release(value);
                    }
                    return;
                }
                
                if (_loadingMusics.Contains(key))
                {
                    _loadingMusics.Remove(key);
                    _preloadMusics.Add(key, id);
                    
                    if (_currentMusic == name && !_isPaused)
                    {
                        PlayAnaMusicInternal(id);
                    }
                }
                else
                {
                    ANAMusic.release(id);
                }
            });

            if (id != -1)
            {
                _loadingMusics.Remove(key);
                _preloadMusics.Add(key, id);
            }
            
            return id;
        }

        private static int PreloadAnaSound(string name)
        {
            var key =  CacheKey(name);
            if (!_loadingSounds.Add(key))
                return -1;

            if (_preloadSounds.TryGetValue(key, out var streamId))
                return streamId;

            var id = AndroidNativeAudio.load(GetFileName(name), UsePersistentDataPath, id =>
            {
                if (_preloadSounds.TryGetValue(key, out var value))
                {
                    _loadingSounds.Remove(key);
                    if (value != id)
                    {
                        AndroidNativeAudio.unload(value);
                    }
                    return;
                }

                if (_loadingSounds.Contains(key))
                {
                    _loadingSounds.Remove(key);
                    _preloadSounds.Add(key, id);
                }
                else
                {
                    AndroidNativeAudio.unload(id);
                }
            });

            if (id != -1)
            {
                _loadingSounds.Remove(key);
                _preloadSounds.Add(key, id);
            }

            return id;
        }

        private static void PlayAnaMusicInternal(int id)
        {
            _currentMusicId = id;
            ANAMusic.play(id);
            ANAMusic.setLooping(id, true);
        }

        private static void PlayAnaMusic(string musicName)
        {
            var id = PreloadAnaMusic(musicName);
            if (id != -1)
            {
                PlayAnaMusicInternal(id);
            }
        }

        private static void PlayAnaSound(string soundName, float  volume = 1, int priority = 1)
        {
            var id = PreloadAnaSound(soundName);
            if (id == -1) return;
            AndroidNativeAudio.play(id, volume, volume, priority);
        }
        
        public static void PlayMusic(string musicName, bool replay = false)
        {
            if (!replay && musicName == _currentMusic) return;
            StopMusic();
            
            if (string.IsNullOrEmpty(musicName))
                return;
            
            _currentMusic = musicName;
            if (_isPaused) return;
            
            PlayAnaMusic(musicName);
        }

        public static void StopMusic(string musicName = "")
        {
            if (!string.IsNullOrEmpty(musicName) &&  musicName != _currentMusic) return;
            
            // stop ana music
            if (_currentMusicId != -1)
                ANAMusic.pause(_currentMusicId);
            _currentMusicId = -1;
            _currentMusic = null;
        }

        public static void PlaySound(string soundName, float volume = 1, int priority = 1)
        {
            if (_isPaused) return;
            
            PlayAnaSound(soundName, volume, 65535 - priority);
        }
    }
}
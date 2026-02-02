using System;
using System.Collections.Generic;
using System.IO;
using PluginSet.Core;
using UnityEngine;
using Logger = PluginSet.Core.Logger;

namespace PluginSet.AndroidNativeAudio
{
    public static class ANASoundsManager
    {
        private static readonly Logger Logger = LoggerManager.GetLogger("ANASoundsManager");
        
        private static bool _isPaused = false;
        private static readonly Dictionary<string, int> PreloadMusics = new Dictionary<string, int>();
        private static readonly HashSet<string> LoadingMusics = new HashSet<string>();
        private static readonly Dictionary<string, int> PreloadSounds = new Dictionary<string, int>();
        private static readonly HashSet<string> LoadingSounds = new HashSet<string>();
        
        private static float _musicBaseVolume = 1.0f;
        private static float _soundBaseVolume = 1.0f;

        private static int _round = 0;
        
        private static readonly Dictionary<string, PluginAndroidNativeAudioConfig.NativeAudioConfig> NativeAudioConfigs = new Dictionary<string, PluginAndroidNativeAudioConfig.NativeAudioConfig>();

        private static string _currentMusic;
        private static int _currentMusicId = -1;
        private static int _initPoolCount = -1;

#if ANA_USE_PERSISTENT_DATA_PATH
        private static string _patchesSavePrefix = "";
#endif
        
        private static string GetFileName(string name)
        {
            if (!NativeAudioConfigs.TryGetValue(name, out var config))
            {
                return null;
            }

            name += Path.GetExtension(config.AssetPath);
            var fileName = ResourcesManager.Instance.GetRealFilePath(name);
            if (string.IsNullOrEmpty(fileName))
                return null;

#if ANA_USE_PERSISTENT_DATA_PATH
            return _patchesSavePrefix + fileName;
#else
            return fileName;
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
            CheckResumeMusic();
        }

        public static void Init(int maxSoundStream, List<PluginAndroidNativeAudioConfig.NativeAudioConfig> nativeAudioConfigs)
        {
            if (_initPoolCount != maxSoundStream)
            {
                AndroidNativeAudio.makePool(maxSoundStream);
                _initPoolCount = maxSoundStream;
            }
            
            NativeAudioConfigs.Clear();
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
                NativeAudioConfigs.Add(nativeAudioConfig.Name, nativeAudioConfig);
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
            foreach (var pair in PreloadMusics)
            {
                ANAMusic.release(pair.Value);
            }

            foreach (var pair in PreloadSounds)
            {
                AndroidNativeAudio.unload(pair.Value);
            }
            
            PreloadMusics.Clear();
            PreloadSounds.Clear();
            LoadingMusics.Clear();
            LoadingSounds.Clear();
        }

        public static void PreloadMusic(string name)
        {
    // #if UNITY_ANDROID && !UNITY_EDITOR
            PreloadAnaMusic(name);
    // #endif
        }

        public static void PreloadSound(string name)
        {
// #if UNITY_ANDROID && !UNITY_EDITOR
            PreloadAnaSound(name);
// #endif
        }

        public static void ReleaseMusic(string name)
        {
            var key = CacheKey(name);
            if (PreloadMusics.TryGetValue(key, out var id))
            {
                ANAMusic.release(id);
                PreloadMusics.Remove(key);
            }

            LoadingMusics.Remove(key);
        }

        public static void ReleaseSound(string name)
        {
            var key = CacheKey(name);
            if (PreloadSounds.TryGetValue(key, out var id))
            {
                AndroidNativeAudio.unload(id);
                PreloadSounds.Remove(key);
            }

            LoadingSounds.Remove(key);
        }
        
        private static int PreloadAnaMusic(string name)
        {
            var fileName =  GetFileName(name);
            Logger.Debug($"PreloadAnaMusic ==================== = {name}:: {fileName}");
            if (string.IsNullOrEmpty(fileName))
                return -1;
            
            var key = CacheKey(name);
            if (PreloadMusics.TryGetValue(key, out var streamId))
                return streamId;
            
            if (!LoadingMusics.Add(key))
                return -1;

            var id = ANAMusic.load(fileName, UsePersistentDataPath, true, id =>
            {
                if (!LoadingMusics.Contains(key))
                {
                    if (id != -1)
                        ANAMusic.release(id);
                    return;
                }
                
                LoadingMusics.Remove(key);
                if (id == -1) return;
                
                if (PreloadMusics.TryGetValue(key, out var value))
                {
                    Logger.Warn($"PreloadAnaMusic ============= callback preload music repeated >> {name}: {key} >>  {value}:: {id} ");
                    if (value != id)
                    {
                        ANAMusic.release(value);
                    }
                }
                else
                {
                    PreloadMusics.Add(key, id);
                    if (_currentMusic == name && !_isPaused)
                    {
                        PlayAnaMusicInternal(id, false);
                    }
                }
            });

            // 确保已加载完，在此不返回id
            // if (id != -1)
            // {
            //     _loadingMusics.Remove(key);
            //     _preloadMusics.Add(key, id);
            // }
            
            return -1;
        }

        private static int PreloadAnaSound(string name)
        {
            var fileName = GetFileName(name);
            Logger.Debug($"PreloadAnaSound ==================== = {name}:: {fileName}");
            if (string.IsNullOrEmpty(fileName))
                return -1;
            
            var key =  CacheKey(name);
            if (PreloadSounds.TryGetValue(key, out var streamId))
                return streamId;
            
            if (!LoadingSounds.Add(key))
                return -1;

            var id = AndroidNativeAudio.load(fileName, UsePersistentDataPath, id =>
            {
                if (!LoadingSounds.Contains(key))
                {
                    if (id != -1)
                        AndroidNativeAudio.unload(id);
                    return;
                }

                LoadingSounds.Remove(key);
                if (id == -1) return;
                
                if (PreloadSounds.TryGetValue(key, out var value))
                {
                    Logger.Warn($"PreloadAnaSound ============= callback preload music repeated >> {name}: {key} >>  {value}:: {id} ");
                    if (value != id)
                    {
                        AndroidNativeAudio.unload(value);
                    }
                }
                else
                {
                    PreloadSounds.Add(key, id);
                }
            });

            // 确保已加载完，在此不返回id
            // if (id != -1)
            // {
            //     _loadingSounds.Remove(key);
            //     _preloadSounds.Add(key, id);
            // }

            return -1;
        }

        private static void PlayAnaMusicInternal(int id, bool replay)
        {
            Logger.Debug($"PlayAnaMusicInternal =============  {id}");
            _currentMusicId = id;
            ANAMusic.play(id);
            ANAMusic.setLooping(id, true);
            ANAMusic.setVolume(id, _musicBaseVolume, _musicBaseVolume);
            if (replay)
                ANAMusic.seekTo(id, 0);
        }

        private static void PlayAnaMusic(string musicName, bool replay)
        {
            var id = PreloadAnaMusic(musicName);
            if (id != -1)
            {
                PlayAnaMusicInternal(id, replay);
            }
            else
            {
                // 标记需要使用ana进行播放
                _currentMusicId = -2;
            }
        }

        private static void PlayAnaSound(string soundName, float  volume = 1, int priority = 1)
        {
            var id = PreloadAnaSound(soundName);
            if (id == -1) return;
            AndroidNativeAudio.play(id, volume, volume, priority);
        }
        
        public static bool PlayMusic(string musicName, bool replay = false)
        {
            if (!NativeAudioConfigs.ContainsKey(musicName))
                return false;
            
            if (!replay && musicName == _currentMusic) return true;
            StopMusic();
            
            if (string.IsNullOrEmpty(musicName))
                return true;
            
            _currentMusic = musicName;
            if (_isPaused) return true;
            
            PlayAnaMusic(musicName, replay);
            return true;
        }

        public static void StopMusic(string musicName = "")
        {
            if (!string.IsNullOrEmpty(musicName) &&  musicName != _currentMusic) return;
            
            // stop ana music
            if (_currentMusicId >= 0)
            {
                ANAMusic.pause(_currentMusicId);
            }
            _currentMusicId = -1;
            _currentMusic = null;
        }

        private static void CheckResumeMusic()
        {
            if (!_isPaused && _musicBaseVolume > 0)
            {
                ANAMusic.resumeAll();
                if (!string.IsNullOrEmpty(_currentMusic) && _currentMusicId == -2)
                    PlayMusic(_currentMusic, true);
            }
        }

        public static bool PlaySound(string soundName, float volume = 1, int priority = 1)
        {
            if (!NativeAudioConfigs.ContainsKey(soundName))
                return false;
            
            if (_isPaused) return true;
            
            PlayAnaSound(soundName, volume * _soundBaseVolume, 65535 - priority);
            return true;
        }

        public static void SetMusicVolume(float volume)
        {
            var lastVolume = _musicBaseVolume;
            _musicBaseVolume = volume;
            if (_currentMusicId >= 0)
            {
                ANAMusic.setVolume(_currentMusicId, volume, volume);
            }
            if (volume <= 0 && lastVolume > 0)
            {
                ANAMusic.pauseAll();
            } else if (lastVolume <= 0 && volume > 0)
            {
                CheckResumeMusic();
            }
        }

        public static void SetSoundVolume(float volume)
        {
            var lastVolume = _soundBaseVolume;
            _soundBaseVolume = volume;
            if (volume <= 0 && lastVolume > 0)
            {
                AndroidNativeAudio.pauseAll();
            } else if (lastVolume <= 0 && volume > 0)
            {
                AndroidNativeAudio.resumeAll();
            }
        }
    }
}
#if ENABLE_ANA_PLUGIN
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

/// <summary>
/// Complete C# bridge for the native AudioEngine plugin.
/// All methods are static — mirrors the C++ AudioEngine static API.
/// Place <see cref="AudioEngineDriver"/> on a persistent GameObject to
/// automatically drive tick / enterBackground / enterForeground.
/// </summary>
namespace PluginSet.AndroidNativeAudio
{
    public static class AudioEngineBridge
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string Lib = "__Internal";
#else
        private const string Lib = "audio_unity_plugin";
#endif

        // ── AudioState ──

        public enum AudioState
        {
            Error = -1,
            Initializing = 0,
            Playing = 1,
            Paused = 2
        }

        // ── Native callback delegates (prevent GC collection) ──

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FinishCallbackNative(int audioId,
            [MarshalAs(UnmanagedType.LPStr)] string filePath);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PreloadCallbackNative(
            [MarshalAs(UnmanagedType.LPStr)] string filePath, int isSuccess);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void FilePathResolverNative(
            [MarshalAs(UnmanagedType.LPStr)] string path, IntPtr outBuf, int bufSize);

        private static readonly Dictionary<int, FinishCallbackNative> s_FinishDelegates =
            new Dictionary<int, FinishCallbackNative>();

        private static readonly List<PreloadCallbackNative> s_PreloadDelegates =
            new List<PreloadCallbackNative>();

        private static FilePathResolverNative s_ResolverDelegate;

        private static readonly HashSet<int> s_playingSoundIds = new HashSet<int>();

        private static int s_playingMusicId = InvalidAudioId;
        private static string s_playingMusicFilePath = null;

        private static float s_musicVolume = 1.0f;
        private static float s_soundVolume = 1.0f;

        private static bool s_musicEnabled = true;
        private static bool s_soundEnabled = true;

        public static float MusicVolume
        {
            get => s_musicVolume;
        }

        public static float SoundVolume
        {
            get => s_soundVolume;
        }


        // ── P/Invoke declarations ──

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ccau_audio_invalid_audio_id();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern float ccau_audio_time_unknown();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ccau_audio_lazy_init();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_tick(float dt);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_enter_background();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_enter_foreground();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_end();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_set_file_path_resolver(FilePathResolverNative resolver);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ccau_audio_play2d(
            [MarshalAs(UnmanagedType.LPStr)] string filePath, int loop, float volume);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_set_loop(int audioId, int loop);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ccau_audio_is_loop(int audioId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_set_volume(int audioId, float volume);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern float ccau_audio_get_volume(int audioId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_pause(int audioId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_pause_all();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_resume(int audioId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_resume_all();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_stop(int audioId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_stop_all();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ccau_audio_set_current_time(int audioId, float sec);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern float ccau_audio_get_current_time(int audioId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern float ccau_audio_get_duration(int audioId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ccau_audio_get_state(int audioId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_set_finish_callback(int audioId,
            FinishCallbackNative callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ccau_audio_get_max_audio_instance();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ccau_audio_set_max_audio_instance(int maxInstances);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_uncache(
            [MarshalAs(UnmanagedType.LPStr)] string filePath);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_uncache_all();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_preload(
            [MarshalAs(UnmanagedType.LPStr)] string filePath);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_preload_with_callback(
            [MarshalAs(UnmanagedType.LPStr)] string filePath,
            PreloadCallbackNative callback);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ccau_audio_get_playing_audio_count();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_set_enabled(int isEnabled);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ccau_audio_is_enabled();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void ccau_audio_set_profile(
            [MarshalAs(UnmanagedType.LPStr)] string profileName,
            uint maxInstances, double minDelay);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ccau_audio_play2d_with_profile(
            [MarshalAs(UnmanagedType.LPStr)] string filePath,
            int loop, float volume,
            [MarshalAs(UnmanagedType.LPStr)] string profileName);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ccau_audio_default_profile_get_name(IntPtr outBuf, int bufSize);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint ccau_audio_default_profile_get_max_instances();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern double ccau_audio_default_profile_get_min_delay();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ccau_audio_profile_get_name_by_id(int audioId,
            IntPtr outBuf, int bufSize);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint ccau_audio_profile_get_max_instances_by_id(int audioId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern double ccau_audio_profile_get_min_delay_by_id(int audioId);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint ccau_audio_profile_get_max_instances_by_name(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern double ccau_audio_profile_get_min_delay_by_name(
            [MarshalAs(UnmanagedType.LPStr)] string name);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogCallback(string message);

        [DllImport("audio_unity_plugin")]
        public static extern void ccau_audio_set_log_callback(LogCallback callback);

        // ── Public C# API ──

        public static readonly int InvalidAudioId = ccau_audio_invalid_audio_id();
        public static readonly float TimeUnknown = ccau_audio_time_unknown();

        public static bool LazyInit() => ccau_audio_lazy_init() != 0;

        public static void Tick(float dt) => ccau_audio_tick(dt);

        public static void EnterBackground() => ccau_audio_enter_background();

        public static void EnterForeground() => ccau_audio_enter_foreground();

        public static void End()
        {
            s_FinishDelegates.Clear();
            s_PreloadDelegates.Clear();
            s_ResolverDelegate = null;
            ccau_audio_set_log_callback(null);
            ccau_audio_set_file_path_resolver(null);
            ccau_audio_end();
        }

        /// <summary>
        /// Register a file-path resolver. The resolver receives a relative path
        /// and should return the platform-specific full path.
        /// Pass null to clear.
        /// </summary>
        public static void SetFilePathResolver(Func<string, string> resolver)
        {
            if (resolver == null)
            {
                s_ResolverDelegate = null;
                ccau_audio_set_file_path_resolver(null);
                return;
            }

            s_ResolverDelegate = (path, outBuf, bufSize) =>
            {
                string resolved = resolver(path);
                if (string.IsNullOrEmpty(resolved)) return;

                byte[] bytes = Encoding.UTF8.GetBytes(resolved);
                int len = Math.Min(bytes.Length, bufSize - 1);
                Marshal.Copy(bytes, 0, outBuf, len);
                Marshal.WriteByte(outBuf, len, 0);
            };

            ccau_audio_set_file_path_resolver(s_ResolverDelegate);
        }

        public static int Play2d(string filePath, bool loop = false, float volume = 1.0f)
            => ccau_audio_play2d(filePath, loop ? 1 : 0, volume);

        public static void SetLoop(int audioId, bool loop)
            => ccau_audio_set_loop(audioId, loop ? 1 : 0);

        public static bool IsLoop(int audioId)
            => ccau_audio_is_loop(audioId) != 0;

        public static void SetVolume(int audioId, float volume)
            => ccau_audio_set_volume(audioId, volume);

        public static float GetVolume(int audioId)
            => ccau_audio_get_volume(audioId);

        public static void Pause(int audioId) => ccau_audio_pause(audioId);
        public static void PauseAll() => ccau_audio_pause_all();
        public static void Resume(int audioId) => ccau_audio_resume(audioId);
        public static void ResumeAll() => ccau_audio_resume_all();
        public static void Stop(int audioId) => ccau_audio_stop(audioId);
        public static void StopAll() => ccau_audio_stop_all();

        public static bool SetCurrentTime(int audioId, float sec)
            => ccau_audio_set_current_time(audioId, sec) != 0;

        public static float GetCurrentTime(int audioId)
            => ccau_audio_get_current_time(audioId);

        public static float GetDuration(int audioId)
            => ccau_audio_get_duration(audioId);

        public static AudioState GetState(int audioId)
            => (AudioState)ccau_audio_get_state(audioId);

        /// <summary>
        /// Register a callback invoked when the audio instance finishes playing.
        /// Pass null to clear. The delegate is prevented from being GC-collected.
        /// </summary>
        public static void SetFinishCallback(int audioId, Action<int, string> callback)
        {
            if (callback == null)
            {
                s_FinishDelegates.Remove(audioId);
                ccau_audio_set_finish_callback(audioId, null);
                return;
            }

            FinishCallbackNative native = (id, path) => callback(id, path);
            s_FinishDelegates[audioId] = native;
            ccau_audio_set_finish_callback(audioId, native);
        }

        public static int GetMaxAudioInstance() => ccau_audio_get_max_audio_instance();
        public static bool SetMaxAudioInstance(int max) => ccau_audio_set_max_audio_instance(max) != 0;

        public static void Uncache(string filePath) => ccau_audio_uncache(filePath);
        public static void UncacheAll() => ccau_audio_uncache_all();

        public static void Preload(string filePath) => ccau_audio_preload(filePath);

        /// <summary>
        /// Preload with completion callback. <paramref name="callback"/> receives
        /// (filePath, isSuccess).
        /// </summary>
        public static void Preload(string filePath, Action<string, bool> callback)
        {
            if (callback == null)
            {
                ccau_audio_preload(filePath);
                return;
            }

            PreloadCallbackNative native = (path, ok) => callback(path, ok != 0);
            s_PreloadDelegates.Add(native);
            ccau_audio_preload_with_callback(filePath, native);
        }

        public static int GetPlayingAudioCount() => ccau_audio_get_playing_audio_count();

        public static void SetEnabled(bool enabled) => ccau_audio_set_enabled(enabled ? 1 : 0);
        public static bool IsEnabled() => ccau_audio_is_enabled() != 0;

        // ── Profile helpers ──

        public static void SetProfile(string profileName, uint maxInstances, double minDelay)
            => ccau_audio_set_profile(profileName, maxInstances, minDelay);

        public static int Play2dWithProfile(string filePath, bool loop, float volume,
            string profileName)
            => ccau_audio_play2d_with_profile(filePath, loop ? 1 : 0, volume, profileName);

        public static string GetDefaultProfileName()
        {
            IntPtr buf = Marshal.AllocHGlobal(256);
            try
            {
                if (ccau_audio_default_profile_get_name(buf, 256) == 0) return null;
                return Marshal.PtrToStringAnsi(buf);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        public static uint GetDefaultProfileMaxInstances() => ccau_audio_default_profile_get_max_instances();
        public static double GetDefaultProfileMinDelay() => ccau_audio_default_profile_get_min_delay();

        public static string GetProfileNameById(int audioId)
        {
            IntPtr buf = Marshal.AllocHGlobal(256);
            try
            {
                if (ccau_audio_profile_get_name_by_id(audioId, buf, 256) == 0) return null;
                return Marshal.PtrToStringAnsi(buf);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        public static uint GetProfileMaxInstancesById(int audioId) =>
            ccau_audio_profile_get_max_instances_by_id(audioId);

        public static double GetProfileMinDelayById(int audioId) => ccau_audio_profile_get_min_delay_by_id(audioId);

        public static uint GetProfileMaxInstancesByName(string name) =>
            ccau_audio_profile_get_max_instances_by_name(name);

        public static double GetProfileMinDelayByName(string name) => ccau_audio_profile_get_min_delay_by_name(name);

        public static int PlaySound(string filePath, float volume = -1.0f, bool loop = false)
        {
            if (volume < -0.1f)
            {
                volume = s_soundVolume;
            }

            int id = Play2d(filePath, loop, volume);
            if (id == InvalidAudioId)
                return id;

            s_playingSoundIds.Add(id);
            SetFinishCallback(id, (_1, _2) =>
            {
                s_playingSoundIds.Remove(id);
                SetFinishCallback(id, null);
            });
            return id;
        }

        public static void StopSound(int audioId)
        {
            s_playingSoundIds.Remove(audioId);
            Stop(audioId);
        }

        public static void StopAllSounds()
        {
            foreach (int id in s_playingSoundIds)
            {
                Stop(id);
            }

            s_playingSoundIds.Clear();
        }

        public static void SetSoundVolume(float volume, int audioId = -1)
        {
            if (audioId == -1)
            {
                foreach (int id in s_playingSoundIds)
                {
                    SetVolume(id, volume);
                }
            }
            else
            {
                SetVolume(audioId, volume);
            }

            s_soundVolume = volume;
        }

        public static int PlayMusic(string filePath, float volume = -1.0f, bool loop = true)
        {
            if (s_playingMusicFilePath == filePath)
            {
                return s_playingMusicId;
            }

            StopMusic();

            if (volume < -0.1f)
            {
                volume = s_musicVolume;
            }

            int id = Play2d(filePath, loop, volume);
            if (id == InvalidAudioId)
                return id;

            s_playingMusicId = id;
            s_playingMusicFilePath = filePath;
            return id;
        }

        public static void StopMusic(string filePath)
        {
            if (s_playingMusicFilePath == filePath)
            {
                StopMusic();
            }
        }

        public static void StopMusic(int audioId = -1)
        {
            if (audioId == -1)
            {
                if (s_playingMusicId != InvalidAudioId)
                {
                    Stop(s_playingMusicId);
                    s_playingMusicId = InvalidAudioId;
                    s_playingMusicFilePath = null;
                }
            }
            else
            {
                if (s_playingMusicId == audioId)
                {
                    Stop(audioId);
                    s_playingMusicId = InvalidAudioId;
                    s_playingMusicFilePath = null;
                }
            }
        }

        public static void SetMusicVolume(float volume)
        {
            if (s_playingMusicId != InvalidAudioId)
            {
                SetVolume(s_playingMusicId, volume);
            }

            s_musicVolume = volume;
        }

        public static void SetMusicEnabled(bool enabled)
        {
            s_musicEnabled = enabled;
            if (!enabled)
            {
                StopMusic();
            }
        }

        public static void SetSoundEnabled(bool enabled)
        {
            s_soundEnabled = enabled;
            if (!enabled)
            {
                StopAllSounds();
            }
        }

        public static void PauseSound(int audioId = -1)
        {
            if (audioId == -1)
            {
                foreach (int id in s_playingSoundIds)
                {
                    Pause(id);
                }
            }
            else if (s_playingSoundIds.Contains(audioId))
            {
                Pause(audioId);
            }
        }

        public static void PauseMusic(int audioId = -1)
        {
            if (audioId == -1)
            {
                if (s_playingMusicId != InvalidAudioId)
                {
                    Pause(s_playingMusicId);
                }
            }
            else if (s_playingMusicId == audioId)
            {
                Pause(audioId);
            }
        }

        public static void ResumeSound(int audioId = -1)
        {
            if (audioId == -1)
            {
                foreach (int id in s_playingSoundIds)
                {
                    Resume(id);
                }
            }
            else if (s_playingSoundIds.Contains(audioId))
            {
                Resume(audioId);
            }
        }

        public static void ResumeMusic(int audioId = -1)
        {
            if (audioId == -1)
            {
                if (s_playingMusicId != InvalidAudioId)
                {
                    Resume(s_playingMusicId);
                }
            }
            else if (s_playingMusicId == audioId)
            {
                Resume(audioId);
            }
        }
    }
}
#endif
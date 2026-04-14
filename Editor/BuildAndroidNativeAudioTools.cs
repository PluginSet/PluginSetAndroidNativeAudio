using System;
using System.Collections.Generic;
using System.IO;
using PluginSet.Core;
using PluginSet.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace PluginSet.AndroidNativeAudio.Editor
{
    [BuildTools]
    public static class BuildAndroidNativeAudioTools
    {
        [OnBuildPatches(int.MinValue)]
        [OnBuildBundles(int.MinValue)]
        [OnSyncEditorSetting(int.MinValue)]
        public static void OnSyncEditorSettingFirst(BuildProcessorContext context)
        {
            if (context.BuildTarget != BuildTarget.Android)
                return;
            
            var buildParams = context.BuildChannels.Get<BuildAndroidNativeAudioParams>();
            if (!buildParams.Enable)
                return;
            
            context.SetSupportAudioPatch(true);
        }
        
        [OnSyncEditorSetting(int.MaxValue)]
        public static void OnSyncEditorSettingLast(BuildProcessorContext context)
        {
            if (!context.IsSupportAudioPatch())
                return;
            
            context.Symbols.Add("ENABLE_ANA_PLUGIN");
            
            var buildParams = context.BuildChannels.Get<BuildAndroidNativeAudioParams>();
            if (buildParams.LoadNativeAudioFromCache)
                context.Symbols.Add("ANA_USE_PERSISTENT_DATA_PATH");
            
            var pluginConfig = context.Get<PluginSetConfig>("pluginsConfig");
            var config = pluginConfig.AddConfig<PluginAndroidNativeAudioConfig>("NativeAudioConfig");
            config.AutoLoadAllAudios = buildParams.AutoLoadAllAudios;

            var musicPatchFiles = context.GetMusicPatchFiles();
            if (!Application.isBatchMode)
            {
                foreach (var pair in musicPatchFiles)
                {
                    var patchName = pair.Key;
                    foreach (var info in pair.Value)
                    {
                        var item = new NativeAudioConfig()
                        {
                            Type = AudioType.Music,
                            Name = info.Name.ToLower(),
                            FileName = $"{Path.GetFileName(info.Name)}_{patchName}" + Path.GetExtension(info.File),
                            OwnPatch = patchName,
                            AssetPath = info.File,
                            BundleName = info.Bundle
                        };
                        config.NativeAudioConfigs.Add(item);
                    }
                }
            }
            musicPatchFiles.Clear();

            var soundPatchFiles = context.GetSoundPatchFiles();
            if (!Application.isBatchMode)
            {
                foreach (var pair in soundPatchFiles)
                {
                    var patchName = pair.Key;
                    foreach (var info in pair.Value)
                    {
                        var item = new NativeAudioConfig()
                        {
                            Type = AudioType.Sound,
                            Name = info.Name.ToLower(),
                            FileName = $"{Path.GetFileName(info.Name)}_{patchName}" + Path.GetExtension(info.File),
                            OwnPatch = patchName,
                            AssetPath = info.File,
                            BundleName = info.Bundle,
                        };
                        config.NativeAudioConfigs.Add(item);
                    }
                }
            }
            soundPatchFiles.Clear();
        }
        
        [OnBuildBundlesCompleted]
        public static void OnBuildBundlesCompleted(BuildProcessorContext context, string streamingPath,
            string streamingName, AssetBundleManifest manifest, bool patchBundle = false)
        {
            if (!context.IsSupportAudioPatch())
                return;

            var configs = new List<NativeAudioConfig>();
            var musicPatchFiles = context.GetMusicPatchFiles();
            foreach (var pair in musicPatchFiles)
            {
                var patchName = pair.Key;
                if (patchName != streamingName)
                {
                    Debug.LogError($"Music patch file own by {patchName} is collected when build {streamingName}");
                    continue;
                }
                
                foreach (var info in pair.Value)
                {
                    var file = info.File;
                    file = Path.Combine(Application.dataPath, "..", file);
                    if (string.IsNullOrEmpty(file) || !File.Exists(file))
                        throw new Exception("Cannot find Audio file: " + file);

                    var copyPath = $"{Path.GetFileName(info.Name)}_{patchName}" + Path.GetExtension(file);
                    copyPath = copyPath.ToLower();
                    var fullPath = Path.GetFullPath(Path.Combine(streamingPath, copyPath));
                    Global.CheckAndCopyFile(file, fullPath);
                    
                    var item = new NativeAudioConfig()
                    {
                        Type = AudioType.Music,
                        Name = info.Name.ToLower(),
                        FileName = copyPath,
                        OwnPatch = patchName,
                        AssetPath = info.File,
                        BundleName = info.Bundle,
                    };
                    if (Application.isBatchMode)
                        item.AssetPath = string.Empty;
                    configs.Add(item);
                }
            }

            var soundPatchFiles = context.GetSoundPatchFiles();
            foreach (var pair in soundPatchFiles)
            {
                var patchName = pair.Key;
                if (patchName != streamingName)
                {
                    Debug.LogError($"Sound patch file own by {patchName} is collected when build {streamingName}");
                    continue;
                }
                
                foreach (var info in pair.Value)
                {
                    var file = info.File;
                    file = Path.Combine(Application.dataPath, "..", file);
                    if (string.IsNullOrEmpty(file) || !File.Exists(file))
                        throw new Exception("Cannot find Audio file: " + file);

                    var copyPath = $"{Path.GetFileName(info.Name)}_{patchName}" + Path.GetExtension(file);
                    copyPath = copyPath.ToLower();
                    var fullPath = Path.GetFullPath(Path.Combine(streamingPath, copyPath));
                    Global.CheckAndCopyFile(file, fullPath);
                    
                    var item = new NativeAudioConfig()
                    {
                        Type = AudioType.Sound,
                        Name = info.Name.ToLower(),
                        FileName = copyPath,
                        OwnPatch = patchName,
                        AssetPath = info.File,
                        BundleName = info.Bundle,
                    };
                    if (Application.isBatchMode)
                        item.AssetPath = string.Empty;
                    configs.Add(item);
                }
            }

            var audioJsonFile = Path.Combine(streamingPath, $"audio_config_{streamingName}.json");
            File.WriteAllText(audioJsonFile, JsonUtil.ToJson(configs));
            musicPatchFiles.Clear();
            soundPatchFiles.Clear();
        }
    }
}

using System;
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
        [OnSyncEditorSetting]
        public static void OnSyncEditorSetting(BuildProcessorContext context)
        {
            var buildParams = context.BuildChannels.Get<BuildAndroidNativeAudioParams>();
            if (buildParams.LoadNativeAudioFromCache && !buildParams.UseUnityAudioForce)
                context.Symbols.Add("ANA_USE_PERSISTENT_DATA_PATH");
            
            var useUnityAudioForce = buildParams.UseUnityAudioForce;
            var pluginConfig = context.Get<PluginSetConfig>("pluginsConfig");
            var config = pluginConfig.AddConfig<PluginAndroidNativeAudioConfig>();
            config.MaxSoundStream = buildParams.MaxSoundStream;
            foreach (var pair in buildParams.NativeAudioConfigs.Pairs)
            {
                var item = new PluginAndroidNativeAudioConfig.NativeAudioConfig()
                {
                    Type = pair.Value.Type,
                    Name = pair.Key,
                    AssetPath = AssetDatabase.GetAssetPath(pair.Value.AudioClip),
                    AutoPreload = pair.Value.AutoPreload,
                };

                if (useUnityAudioForce || pair.Value.UseUnityAudio || context.BuildTarget != BuildTarget.Android)
                {
                    item.Asset = pair.Value.AudioClip;
                    item.AutoPreload = false;
                }

                config.NativeAudioConfigs.Add(item);
            }
        }

        [BuildProjectCompleted]
        public static void BuildProjectCompleted(BuildProcessorContext context, string exportPath)
        {
            if (context.BuildTarget != BuildTarget.Android)
                return;

            var buildParams = context.BuildChannels.Get<BuildAndroidNativeAudioParams>();
            if (buildParams.UseUnityAudioForce)
                return;

            if (buildParams.NativeAudioConfigs.Pairs.Length <= 0)
                return;

            var assetsPath = Global.GetProjectAssetsPath(context.BuildTarget, exportPath);
            if (string.IsNullOrEmpty(assetsPath))
                return;

            foreach (var pair in buildParams.NativeAudioConfigs.Pairs)
            {
                var file = AssetDatabase.GetAssetPath(pair.Value.AudioClip);
                file = Path.Combine(Application.dataPath, "..", file);
                if (string.IsNullOrEmpty(file) || !File.Exists(file))
                    throw new Exception("Cannot find Audio file: " + file);

                var copyPath = pair.Key;
                var fullPath = Path.GetFullPath(Path.Combine(assetsPath, copyPath));
                Global.CheckAndCopyFile(file, fullPath);
                context.ExtendStreamingFiles(copyPath);
            }
        }
    }
}

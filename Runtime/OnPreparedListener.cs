using System;
using UnityEngine;

namespace PluginSet.AndroidNativeAudio
{
	public class OnPreparedListener : AndroidJavaProxy
	{
		public Action<int> Callback;


		public OnPreparedListener(Action<int> callback) : base("android.media.MediaPlayer$OnPreparedListener")
		{
			Callback = callback;
		}


		void onPrepared(AndroidJavaObject mediaPlayer)
		{
			Callback(mediaPlayer.Call<int>("getAudioSessionId"));
		}
	}
}

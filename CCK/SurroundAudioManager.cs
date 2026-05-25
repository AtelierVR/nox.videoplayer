using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nox.CCK.VideoPlayer {
	[Serializable]
	public class AudioChannelConfiguration {
		public AudioSpeakerMode mode;
		public int channelCount;
		public string description;
		public bool isSurroundSound;
		
		public AudioChannelConfiguration(AudioSpeakerMode mode, int channels, string desc, bool surround) {
			this.mode = mode;
			this.channelCount = channels;
			this.description = desc;
			this.isSurroundSound = surround;
		}
	}

	public class SurroundAudioManager : MonoBehaviour {
		[Header("Audio 5.1 Configuration")]
		public AudioSource frontLeft;
		public AudioSource frontRight;
		public AudioSource frontCenter;
		public AudioSource lowFrequencyEffects; // Subwoofer
		public AudioSource rearLeft;
		public AudioSource rearRight;
		
		[Header("Settings")]
		public float masterVolume = 1.0f;
		public float centerChannelBoost = 1.0f;
		public float lfeBoost = 1.2f;
		public bool enableDynamicRangeCompression = false;
		
		private Dictionary<AudioSpeakerMode, AudioChannelConfiguration> supportedConfigurations;
		private AudioChannelConfiguration currentConfiguration;
		private float[] channelData;
		private int sampleRate = 48000;
		
		// Events
		public event Action<AudioSpeakerMode> OnAudioModeChanged;
		public event Action<float> OnVolumeChanged;
		
		private void Awake() {
			InitializeSupportedConfigurations();
			SetupAudioSources();
		}
		
		private void InitializeSupportedConfigurations() {
			supportedConfigurations = new Dictionary<AudioSpeakerMode, AudioChannelConfiguration> {
				{ AudioSpeakerMode.Mono, new AudioChannelConfiguration(AudioSpeakerMode.Mono, 1, "Mono", false) },
				{ AudioSpeakerMode.Stereo, new AudioChannelConfiguration(AudioSpeakerMode.Stereo, 2, "Stéréo", false) },
				{ AudioSpeakerMode.Quad, new AudioChannelConfiguration(AudioSpeakerMode.Quad, 4, "Quadraphonique", true) },
				{ AudioSpeakerMode.Mode5point1, new AudioChannelConfiguration(AudioSpeakerMode.Mode5point1, 6, "5.1 Surround", true) },
				{ AudioSpeakerMode.Mode7point1, new AudioChannelConfiguration(AudioSpeakerMode.Mode7point1, 8, "7.1 Surround", true) }
			};
			
			// Configuration par défaut
			currentConfiguration = supportedConfigurations[AudioSpeakerMode.Stereo];
		}
		
		private void SetupAudioSources() {
			// Configuration des AudioSources pour le 5.1
			if (frontLeft == null) frontLeft = CreateAudioSource("FrontLeft");
			if (frontRight == null) frontRight = CreateAudioSource("FrontRight");
			if (frontCenter == null) frontCenter = CreateAudioSource("FrontCenter");
			if (lowFrequencyEffects == null) lowFrequencyEffects = CreateAudioSource("LFE");
			if (rearLeft == null) rearLeft = CreateAudioSource("RearLeft");
			if (rearRight == null) rearRight = CreateAudioSource("RearRight");
			
			// Positionnement spatial des sources audio
			PositionAudioSources();
		}
		
		private AudioSource CreateAudioSource(string name) {
			GameObject audioObj = new GameObject($"AudioChannel_{name}");
			audioObj.transform.SetParent(transform);
			AudioSource source = audioObj.AddComponent<AudioSource>();
			source.spatialBlend = 1.0f; // Audio 3D complet
			source.rolloffMode = AudioRolloffMode.Linear;
			source.maxDistance = 100f;
			return source;
		}
		
		private void PositionAudioSources() {
			// Positionnement selon la norme 5.1
			if (frontLeft != null) frontLeft.transform.localPosition = new Vector3(-1.5f, 0, 1.5f);
			if (frontRight != null) frontRight.transform.localPosition = new Vector3(1.5f, 0, 1.5f);
			if (frontCenter != null) frontCenter.transform.localPosition = new Vector3(0, 0, 2f);
			if (lowFrequencyEffects != null) lowFrequencyEffects.transform.localPosition = new Vector3(0, -0.5f, 0);
			if (rearLeft != null) rearLeft.transform.localPosition = new Vector3(-1.5f, 0, -1.5f);
			if (rearRight != null) rearRight.transform.localPosition = new Vector3(1.5f, 0, -1.5f);
		}
		
		public bool SetAudioConfiguration(AudioSpeakerMode mode) {
			if (!supportedConfigurations.ContainsKey(mode)) {
				Debug.LogWarning($"Mode audio non supporté: {mode}");
				return false;
			}
			
			currentConfiguration = supportedConfigurations[mode];
			ApplyAudioConfiguration();
			OnAudioModeChanged?.Invoke(mode);
			
			Debug.Log($"Configuration audio changée vers: {currentConfiguration.description}");
			return true;
		}
		
		private void ApplyAudioConfiguration() {
			// Désactiver toutes les sources d'abord
			DisableAllAudioSources();
			
			switch (currentConfiguration.mode) {
				case AudioSpeakerMode.Mono:
					frontCenter.enabled = true;
					break;
					
				case AudioSpeakerMode.Stereo:
					frontLeft.enabled = true;
					frontRight.enabled = true;
					break;
					
				case AudioSpeakerMode.Quad:
					frontLeft.enabled = true;
					frontRight.enabled = true;
					rearLeft.enabled = true;
					rearRight.enabled = true;
					break;
					
				case AudioSpeakerMode.Mode5point1:
					frontLeft.enabled = true;
					frontRight.enabled = true;
					frontCenter.enabled = true;
					lowFrequencyEffects.enabled = true;
					rearLeft.enabled = true;
					rearRight.enabled = true;
					break;
			}
		}
		
		private void DisableAllAudioSources() {
			frontLeft.enabled = false;
			frontRight.enabled = false;
			frontCenter.enabled = false;
			lowFrequencyEffects.enabled = false;
			rearLeft.enabled = false;
			rearRight.enabled = false;
		}
		
		public void ProcessAudioFrame(float[] audioData, int channels) {
			if (audioData == null || audioData.Length == 0) return;
			
			switch (channels) {
				case 2: // Stéréo vers 5.1
					ProcessStereoTo51(audioData);
					break;
				case 6: // 5.1 natif
					Process51Audio(audioData);
					break;
				default:
					ProcessMonoAudio(audioData);
					break;
			}
		}
		
		private void ProcessStereoTo51(float[] stereoData) {
			// Upmixing stéréo vers 5.1
			for (int i = 0; i < stereoData.Length; i += 2) {
				float left = stereoData[i] * masterVolume;
				float right = stereoData[i + 1] * masterVolume;
				
				// Distribution vers les canaux 5.1
				SendToChannel(frontLeft, left);
				SendToChannel(frontRight, right);
				SendToChannel(frontCenter, (left + right) * 0.5f * centerChannelBoost);
				SendToChannel(rearLeft, left * 0.3f); // Signal ambiant
				SendToChannel(rearRight, right * 0.3f);
				
				// LFE (filtre passe-bas simulé)
				float lfe = (left + right) * 0.1f * lfeBoost;
				SendToChannel(lowFrequencyEffects, lfe);
			}
		}
		
		private void Process51Audio(float[] audioData) {
			// Traitement audio 5.1 natif
			for (int i = 0; i < audioData.Length; i += 6) {
				SendToChannel(frontLeft, audioData[i] * masterVolume);
				SendToChannel(frontRight, audioData[i + 1] * masterVolume);
				SendToChannel(frontCenter, audioData[i + 2] * masterVolume * centerChannelBoost);
				SendToChannel(lowFrequencyEffects, audioData[i + 3] * lfeBoost);
				SendToChannel(rearLeft, audioData[i + 4] * masterVolume);
				SendToChannel(rearRight, audioData[i + 5] * masterVolume);
			}
		}
		
		private void ProcessMonoAudio(float[] monoData) {
			for (int i = 0; i < monoData.Length; i++) {
				float sample = monoData[i] * masterVolume;
				SendToChannel(frontCenter, sample);
			}
		}
		
		private void SendToChannel(AudioSource channel, float sample) {
			if (channel != null && channel.enabled) {
				// Ici, on devrait envoyer le sample vers le canal audio
				// Dans une implémentation complète, on utiliserait OnAudioFilterRead
				channel.volume = Mathf.Abs(sample);
			}
		}
		
		public void SetMasterVolume(float volume) {
			masterVolume = Mathf.Clamp01(volume);
			OnVolumeChanged?.Invoke(masterVolume);
		}
		
		public void SetCenterChannelBoost(float boost) {
			centerChannelBoost = Mathf.Clamp(boost, 0.5f, 2.0f);
		}
		
		public void SetLFEBoost(float boost) {
			lfeBoost = Mathf.Clamp(boost, 0.5f, 3.0f);
		}
		
		public AudioChannelConfiguration GetCurrentConfiguration() {
			return currentConfiguration;
		}
		
		public bool IsSurroundSoundCapable() {
			return currentConfiguration.isSurroundSound;
		}
		
		public List<AudioChannelConfiguration> GetSupportedConfigurations() {
			return new List<AudioChannelConfiguration>(supportedConfigurations.Values);
		}
	}
}

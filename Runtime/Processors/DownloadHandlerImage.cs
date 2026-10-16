using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.VideoPlayer.Runtime.Processors {
	/// <summary>
	/// <see cref="DownloadHandlerScript"/> that turns the response body into a
	/// <see cref="Texture2D"/>, including the formats Unity cannot read by itself
	/// (webp, jxl…). Remote thumbnails are frequently served as webp, and
	/// <see cref="UnityEngine.Networking.DownloadHandlerTexture"/> fails on them with a
	/// <c>DataProcessingError</c> — this is the overload that handles them: the body is
	/// decoded natively when possible, otherwise transcoded to png by the bundled ffmpeg.
	/// </summary>
	public class DownloadHandlerImage : DownloadHandlerScript {
		private readonly List<byte> _content = new();

		private Texture2D _texture;
		private bool      _decoded;

		/// <summary>The raw response body.</summary>
		public byte[] rawData
			=> _content.ToArray();

		/// <summary>
		/// The image decoded natively (png, jpeg, exr, tga…), or null when the body uses a
		/// format Unity cannot read — use <see cref="GetTextureAsync"/> for those.
		/// </summary>
		public Texture2D texture {
			get {
				DecodeNatively();
				return _texture;
			}
		}

		/// <summary>Whether the response is a WebP image (checked on its magic bytes).</summary>
		public bool isWebP {
			get {
				var data = _content;
				return data.Count > 11
					&& data[0]  == 0x52 && data[1]  == 0x49 && data[2]  == 0x46 && data[3]  == 0x46 // RIFF
					&& data[8]  == 0x57 && data[9]  == 0x45 && data[10] == 0x42 && data[11] == 0x50; // WEBP
			}
		}

		/// <summary>
		/// The decoded image: natively when Unity can, otherwise transcoded through ffmpeg.
		/// Returns null when the body is not a decodable image.
		/// </summary>
		public async UniTask<Texture2D> GetTextureAsync(CancellationToken cancellationToken = default) {
			DecodeNatively();
			if (_texture)
				return _texture;

			var png = await FFmpeg.TranscodeToPng(rawData, cancellationToken: cancellationToken);
			if (png == null)
				return null;

			_texture = ToTexture(png);
			_decoded = true;
			return _texture;
		}

		/// <summary>Downloads <paramref name="url"/> and returns the decoded image, or null.</summary>
		public static async UniTask<Texture2D> Fetch(string url, CancellationToken cancellationToken = default) {
			if (string.IsNullOrEmpty(url))
				return null;

			try {
				using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
				var       handler = new DownloadHandlerImage();
				request.downloadHandler = handler;
				request.SetRequestHeader("Accept", "image/webp, image/png, image/jpeg, image/*;q=0.8");

				await request.SendWebRequest();
				if (request.result != UnityWebRequest.Result.Success) {
					Logger.LogWarning($"Could not download '{url}': {request.error}");
					return null;
				}

				return await handler.GetTextureAsync(cancellationToken);
			} catch (Exception e) {
				Logger.LogWarning($"Could not download '{url}': {e.Message}");
				return null;
			}
		}

		private void DecodeNatively() {
			if (_decoded || _texture || _content.Count == 0)
				return;
			_decoded = true;
			_texture = ToTexture(_content.ToArray());
		}

		private static Texture2D ToTexture(byte[] bytes) {
			if (bytes == null || bytes.Length == 0)
				return null;

			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			try {
				if (ImageConversion.LoadImage(texture, bytes, true))
					return texture;
			} catch (Exception e) {
				Logger.LogWarning($"Could not decode an image: {e.Message}");
			}

			UnityEngine.Object.Destroy(texture);
			return null;
		}

		// ── DownloadHandlerScript ──────────────────────────────────────────

		protected override byte[] GetData()
			=> _content.ToArray();

		protected override string GetText()
			=> Encoding.UTF8.GetString(_content.ToArray());

		protected override bool ReceiveData(byte[] data, int dataLength) {
			if (data == null || dataLength <= 0)
				return false;

			for (var i = 0; i < dataLength; i++)
				_content.Add(data[i]);
			return true;
		}

		protected override void CompleteContent()
			=> DecodeNatively();
	}
}

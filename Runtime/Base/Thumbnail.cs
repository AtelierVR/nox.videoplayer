using System;
using Cysharp.Threading.Tasks;
using Nox.VideoPlayer.Runtime.Processors;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.VideoPlayer.Runtime.Base {
	public class Thumbnail : IThumbnail {
		public string     Url        { get; set; }
		public string     Language   { get; set; }
		public Vector2Int Resolution { get; set; }

		/// <summary>
		/// Downloads and decodes the thumbnail. Remote thumbnails are frequently served as
		/// webp (the signed YouTube urls are), which Unity cannot read by itself, so the
		/// download goes through <see cref="DownloadHandlerImage"/>.
		/// </summary>
		public async UniTask<Texture2D> Fetch() {
			if (string.IsNullOrEmpty(Url))
				return null;

			try {
				return await DownloadHandlerImage.Fetch(Url);
			} catch (Exception e) {
				Logger.LogWarning($"Could not fetch the thumbnail '{Url}': {e.Message}");
				return null;
			}
		}
	}
}
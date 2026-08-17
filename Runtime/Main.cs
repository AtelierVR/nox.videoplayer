using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Nox.CCK.Language;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;
using Nox.VideoPlayer.Runtime.Commands;
using Nox.VideoPlayer.Runtime.Handlers;
using Nox.VideoPlayer.Runtime.Helpers;
using Nox.VideoPlayer.Runtime.Processors;
using UnityEngine.Events;

namespace Nox.VideoPlayer.Runtime {
	public class Main : IMainModInitializer, IVideoPlayerAPI {
		static readonly internal List<IHandler>     Handlers = new();
		public static            Main               Instance;
		public                   IMainModCoreAPI     CoreAPI;
		private                  LanguagePack       _lang;

		static readonly internal UnityEvent<IHandler> OnHandlerAdded   = new();
		static readonly internal UnityEvent<IHandler> OnHandlerRemoved = new();

		private void InvokeHandlerAdded(IHandler handler) {
			OnHandlerAdded.Invoke(handler);
			CoreAPI.EventAPI.Emit("search_handler_added", handler);
		}

		private void InvokeHandlerRemoved(IHandler handler) {
			OnHandlerRemoved.Invoke(handler);
			CoreAPI.EventAPI.Emit("search_handler_removed", handler);
		}

		public IHandler Add(IHandler handler) {
			if (handler == null) {
				CoreAPI.LoggerAPI.LogError("Cannot register a null search handler");
				return null;
			}

			if (string.IsNullOrWhiteSpace(handler.Id)) {
				CoreAPI.LoggerAPI.LogError("Cannot register a search handler with an empty id");
				return null;
			}

			if (Handlers.Exists(b => b.Id == handler.Id)) {
				CoreAPI.LoggerAPI.LogError($"Search handler with id {handler.Id} already exists");
				return null;
			}

			Handlers.Add(handler);
			InvokeHandlerAdded(handler);
			return handler;
		}

		public void Remove(string id) {
			var handler = Get(id);
			if (handler == null) {
				CoreAPI.LoggerAPI.LogError($"Search handler with id {id} does not exist");
				return;
			}

			Handlers.Remove(handler);
			InvokeHandlerRemoved(handler);
		}

		public IHandler Get(string id)
			=> Handlers.Find(b => b.Id == id);

		public bool Has(string id)
			=> Handlers.Exists(b => b.Id == id);

		private IHandler[] _handlers = Array.Empty<IHandler>();
		private VideoPlayerCommands _commands;

		public void OnInitializeMain(IMainModCoreAPI api) {
			Instance = this;
			CoreAPI  = api;
			_lang    = CoreAPI.AssetAPI.GetAsset<LanguagePack>("lang.asset");
			LanguageManager.AddPack(_lang);
			VideoPlayerManager.Listen();
			VideoPlayerResolver.Listen();
			PrepareAsync().Forget();
			_handlers = new IHandler[] {
				new Youtube(), 
				new Twitch(), 
				new Global()
			};
			foreach (var handler in _handlers)
				Add(handler);

			_commands = new VideoPlayerCommands();
		}

		public void OnDisposeMain() {
			_commands?.Dispose();
			_commands = null;
			VideoPlayerResolver.UnListen();
			VideoPlayerManager.UnListen();
			if (!YtDl.IsDownloading)
				YtDl.CancelDownload();
			if (!FFmpeg.IsDownloading)
				FFmpeg.CancelDownload();
			foreach (var handler in Handlers.ToArray())
				Remove(handler.Id);
			Handlers.Clear();
			_handlers = Array.Empty<IHandler>();
			_prepareCts?.Cancel();
			LanguageManager.RemovePack(_lang);
			_lang       = null;
			_prepareCts = null;
			Instance    = null;
			CoreAPI     = null;
		}

		private CancellationTokenSource _prepareCts;

		private async UniTask PrepareAsync() {
			_prepareCts?.Cancel();
			_prepareCts = new CancellationTokenSource();

			// Download yt-dlp and FFmpeg in parallel
			var ytDlTask   = UniTask.CompletedTask;
			var ffmpegTask = UniTask.CompletedTask;

			if (!YtDl.IsDownloading) {
				CoreAPI.LoggerAPI.Log("Downloading yt-dlp...");
				if (YtDl.IsAvailable)
					ytDlTask = YtDl.Update();
				else ytDlTask = YtDl.Download();
			}

			if (!FFmpeg.IsDownloading) {
				CoreAPI.LoggerAPI.Log("Downloading FFmpeg...");
				if (FFmpeg.IsAvailable)
					ffmpegTask  = FFmpeg.Update();
				else ffmpegTask = FFmpeg.Download();
			}

			// Wait for both downloads to complete
			await UniTask.WhenAll(ytDlTask, ffmpegTask);

			// Log version information
			if (YtDl.IsAvailable) {
				var ytDlVersion = await YtDl.GetVersion(cancellationToken: _prepareCts.Token);
				CoreAPI.LoggerAPI.Log($"yt-dlp is ready (version: {ytDlVersion})");
			}

			if (FFmpeg.IsAvailable) {
				var ffmpegVersion = await FFmpeg.GetVersion(cancellationToken: _prepareCts.Token);
				CoreAPI.LoggerAPI.Log($"FFmpeg is ready (version: {ffmpegVersion})");
			}

			_prepareCts = null;
		}
	}
}
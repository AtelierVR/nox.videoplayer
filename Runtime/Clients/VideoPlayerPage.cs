using System;
using System.Collections.Generic;
using System.Linq;
using Nox.UI;
using Nox.VideoPlayer.Runtime.Helpers;
using UnityEngine;
using Logger = Nox.CCK.Utils.Logger;

namespace Nox.VideoPlayer.Runtime.Clients {
	public class VideoPlayerPage : IPage {
		readonly internal List<UiPlayer> Players = new();


		static internal string GetStaticKey()
			=> "video_player";

		public string GetKey()
			=> GetStaticKey();

		private static bool T<T>(object[] o, int index, out T value) {
			if (o.Length > index && o[index] is T t) {
				value = t;
				return true;
			}

			value = default;
			return false;
		}

		static internal IPage OnGotoAction(IMenu menu, object[] context)
			=> new VideoPlayerPage {
				_mId      = menu.Id,
				_context  = context,
				_selected = T(context, 0, out int id) ? id : 0
			};

		private int                  _mId;
		private object[]             _context;
		private GameObject           _content;
		private VideoPlayerComponent _component;
		private int                  _selected;

		public object[] GetContext()
			=> _context;

		public GameObject GetContent(RectTransform parent) {
			if (_content) return _content;
			(_content, _component) = VideoPlayerComponent.Generate(this, parent);
			return _content;
		}

		public IMenu GetMenu()
			=> Client.UiAPI.Get<IMenu>(_mId);

		public void OnOpen(IPage lastPage) {
			Logger.LogDebug($"Opened page in menu {_mId} with selected player {_selected}");
			VideoPlayerManager.OnRegistered.AddListener(OnRegistered);
			VideoPlayerManager.OnUnRegistered.AddListener(OnUnRegistered);
			foreach (var player in VideoPlayerManager.VideoPlayers)
				Add(player);
		}

		public void OnRemove() {
			VideoPlayerManager.OnRegistered.RemoveListener(OnRegistered);
			VideoPlayerManager.OnUnRegistered.RemoveListener(OnUnRegistered);
			foreach (var ui in Players)
				ui.Dispose();
		}

		public IVideoPlayer GetSelectedPlayer() {
			var player = VideoPlayerManager.VideoPlayers.FirstOrDefault(p => p.GetId() == _selected);
			if (player != null || VideoPlayerManager.VideoPlayers.Count <= 0) return player;
			player    = VideoPlayerManager.ActiveVideoPlayers.FirstOrDefault();
			_selected = player?.GetId() ?? 0;
			return player;
		}

		private void OnUnRegistered(IVideoPlayer player) {
			Remove(player);
			if (player == null || player.GetId() != _selected) return;
			var next = VideoPlayerManager.ActiveVideoPlayers.FirstOrDefault();
			_selected = next?.GetId() ?? 0;
			OnUpdate();
		}


		private void OnRegistered(IVideoPlayer player) {
			Add(player);
			OnUpdate();
		}

		public void OnDisplay(IPage lastPage)
			=> OnUpdate();

		internal void OnUpdate()
			=> _component?.UpdateUI();


		private void Remove(IVideoPlayer emitter) {
			var ui = Players.FirstOrDefault(p => p.Player == emitter);
			if (ui == null) return;
			ui.Dispose();
			Players.Remove(ui);
		}

		private void Add(IVideoPlayer emitter) {
			var ui = new UiPlayer(emitter, this);
			Players.Add(ui);
			Logger.LogDebug($"Added player {emitter.GetId()}");
		}

		public void OnProgress(IVideoPlayer player, double progress) {
			if (player == null || player.GetId() != _selected) return;
			_component?.UpdateProgress(player, progress);
		}

		public void OnStream(IVideoPlayer player) {
			if (player == null || player.GetId() != _selected) return;
			_component?.UpdateStream(player);
		}

		public void OnPlayStatusChanged(IVideoPlayer player, State state) {
			if (player == null || player.GetId() != _selected) return;
			_component?.UpdatePlayStatus(player, state);
		}

        public void OnTexture(IVideoPlayer player, Texture2D _) {
			if (player == null || player.GetId() != _selected) return;
			_component?.UpdateRender(player);
        }

        public void OnResolution(IVideoPlayer player, Vector2Int _) {
			if (player == null || player.GetId() != _selected) return;
			_component?.UpdateRender(player);
        }

		public void TogglePlayPause() {
			var player = GetSelectedPlayer();
			if (player == null) return;
			if (player.State == State.Playing)
				player.Pause();
			else player.Resume();
		}

    }

	public class UiPlayer : IDisposable {
		public IVideoPlayer    Player;
		public VideoPlayerPage Page;

		public UiPlayer(IVideoPlayer player, VideoPlayerPage page) {
			Player = player;
			Page   = page;
			Player.OnState.AddListener(OnState);
			Player.OnStream.AddListener(OnStream);
			if (Player is IVideoPlayerVideo pt)
				pt.OnTexture.AddListener(OnTexture);
			if (Player is IVideoPlayerResolution pr)
				pr.OnResolution.AddListener(OnResolution); 
			Logger.Log($"[VideoPlayerPage] Player {player.GetId()} added to UI");
		}

        public void Dispose() {
			Player.OnState.RemoveListener(OnState);
			Player.OnStream.RemoveListener(OnStream);
			if (Player is IVideoPlayerVideo pt)
				pt.OnTexture.RemoveListener(OnTexture);
			if (Player is IVideoPlayerResolution pr)
				pr.OnResolution.RemoveListener(OnResolution); 
			Logger.Log($"[VideoPlayerPage] Player {Player.GetId()} removed from UI");
		}

        private void OnState(IVideoPlayer _, State state)
			=> Page.OnPlayStatusChanged(Player, state);

		private void OnStream(IVideoPlayer _)
			=> Page.OnStream(Player);

		public void OnProgress(IVideoPlayer _, double progress)
			=> Page.OnProgress(Player, progress);

        private void OnTexture(IVideoPlayer _, Texture2D texture)
			=> Page.OnTexture(Player, texture);

        private void OnResolution(IVideoPlayer _, Vector2Int resolution)
			=> Page.OnResolution(Player, resolution);
	}
}
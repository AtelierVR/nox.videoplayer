using System.Linq;
using Nox.UI;
using Nox.VideoPlayer.Runtime.Helpers;
using UnityEngine;

namespace Nox.VideoPlayer.Runtime.Clients {
	/// <summary>
	/// Page used to search and set a new media on a video player: a search bar with the
	/// results on the left, the details of the selected result and a Play button on the
	/// right. Only reachable when the player grants the search permission
	/// (see <see cref="Nox.VideoPlayer.IVideoPlayerControl"/>).
	/// </summary>
	public class VideoSearchPage : IPage {
		internal static string GetStaticKey()
			=> "video_search";

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

		internal static IPage OnGotoAction(IMenu menu, object[] context)
			=> new VideoSearchPage {
				_mId      = menu.Id,
				_context  = context,
				_selected = T(context, 0, out int id) ? id : 0
			};

		private int          _mId;
		private object[]     _context;
		private GameObject   _content;
		private VideoSearchComponent _component;
		private int          _selected;

		public object[] GetContext()
			=> _context;

		public IMenu GetMenu()
			=> Client.UiAPI.Get<IMenu>(_mId);

		public GameObject GetContent(RectTransform parent) {
			if (_content) return _content;
			(_content, _component) = VideoSearchComponent.Generate(this, parent);
			return _content;
		}

		/// <summary>The player the page acts on (selected one, or the first active).</summary>
		internal IVideoPlayer GetPlayer() {
			var player = VideoPlayerManager.VideoPlayers.FirstOrDefault(p => p.GetId() == _selected);
			player ??= VideoPlayerManager.ActiveVideoPlayers.FirstOrDefault();
			if (player != null)
				_selected = player.GetId();
			return player;
		}

		/// <summary>
		/// Whether the local user is allowed to control the player (not read-only):
		/// they may search a new video and play the selected one.
		/// </summary>
		internal bool CanControl
			=> GetPlayer() is IVideoPlayerControl control && !control.IsReadonly;
	}
}

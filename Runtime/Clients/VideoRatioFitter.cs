using Nox.CCK.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Nox.VideoPlayer.Runtime.Clients {
	/// <summary>
	/// Sizes a container from the aspect ratio of the video it displays: it keeps the width
	/// given by its parent and derives its own height from that width and the ratio of the
	/// current frame (a fallback ratio is used until one arrives).
	///
	/// An <see cref="AspectRatioFitter"/> cannot do it here: it resizes its own rect, which a
	/// layout group overwrites, and it does not report the height to its parent. Same pattern
	/// as <see cref="VideoGridFitter"/>.
	/// </summary>
	[RequireComponent(typeof(LayoutElement))]
	public class VideoRatioFitter : MonoBehaviour, IUpdateLayout {
		[Tooltip("Element whose width drives the height of this container (the video).")]
		public RectTransform source;

		[Tooltip("Ratio (width / height) used until a frame arrives.")]
		public float fallbackRatio = 16f / 9f;

		[Tooltip("Bounds applied to the computed height.")]
		public float minHeight = 96f;

		[Tooltip("Bounds applied to the computed height.")]
		public float maxHeight = 720f;

		[Tooltip("When true, the container accepts being compressed below its ratio when the panel " +
			"is too small (the media is then letterboxed/clipped). When false, the container always " +
			"keeps its ratio and the other elements give way instead.")]
		public bool shrinkable = true;

		private LayoutElement _layout;
		private float         _ratio;
		private float         _width = -1f;
		private float         _applied = -1f;

		/// <summary>Width / height ratio of the media currently displayed.</summary>
		public float AspectRatio {
			get => _ratio;
			set {
				if (value <= 0f || Mathf.Approximately(_ratio, value))
					return;

				_ratio = value;
				Layout();
			}
		}

		/// <inheritdoc />
		public void UpdateLayout()
			=> Layout();

		private void Awake() {
			_layout = GetComponent<LayoutElement>();
			_ratio  = fallbackRatio;
		}

		/// <summary>
		/// The width of the video can change (panel resized, first layout pass) without a pass
		/// being requested on this subtree: detecting it costs a rect read, and the layout is
		/// only rebuilt on a real change.
		/// </summary>
		private void Update() {
			if (source && !Mathf.Approximately(source.rect.width, _width))
				Layout();
		}

		private void Layout() {
			if (!_layout || !source || _ratio <= 0f)
				return;

			var width = source.rect.width;
			_width = width;
			if (width <= 0f)
				return;

			var height = Mathf.Clamp(width / _ratio, minHeight, maxHeight);
			if (Mathf.Approximately(height, _applied))
				return;

			_applied = height;

			// The ratio drives the preferred size; the minimum is left to the policy: a
			// shrinkable container (the player) is the one that gives way when the panel is too
			// small, a non-shrinkable one (the poster of the selected media) never is.
			_layout.minHeight       = shrinkable ? 0f : height;
			_layout.preferredHeight = height;
			_layout.flexibleHeight  = 0f;

			if (transform.parent is RectTransform parent)
				LayoutRebuilder.MarkLayoutForRebuild(parent);
		}
	}
}

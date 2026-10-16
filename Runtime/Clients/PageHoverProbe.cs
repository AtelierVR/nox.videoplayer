using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Nox.VideoPlayer.Runtime.Clients {
	/// <summary>
	/// Relay for pointer enter/exit and movement, used to know whether a pointer (mouse, UI ray
	/// from an interactor…) is currently on the page and whether it is still active. Counts
	/// concurrent pointers so several sources (mouse + rays) can be on the page at the same time.
	/// </summary>
	public class PageHoverProbe : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler {
		private int _count;

		/// <summary>Raised when the page goes from hovered to not hovered (and the opposite).</summary>
		public UnityEvent<bool> HoverChanged { get; } = new();

		/// <summary>
		/// Raised every time a pointer moves over the page. Only sent on an actual movement, so a
		/// listener can time out when the events stop coming (idle pointer).
		/// </summary>
		public UnityEvent Moved { get; } = new();

		public bool Hovered
			=> _count > 0;

		public void OnPointerEnter(PointerEventData eventData) {
			if (_count++ == 0)
				HoverChanged.Invoke(true);
		}

		public void OnPointerExit(PointerEventData eventData) {
			if (_count > 0 && --_count == 0)
				HoverChanged.Invoke(false);
		}

		public void OnPointerMove(PointerEventData eventData)
			=> Moved.Invoke();
	}
}

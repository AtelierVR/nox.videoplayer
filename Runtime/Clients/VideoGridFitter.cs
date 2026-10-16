using Nox.CCK.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Nox.VideoPlayer.Runtime.Clients {
	/// <summary>
	/// Sizes the cells of a <see cref="GridLayoutGroup"/> from the available width, with a
	/// fixed aspect ratio and an adaptive column count. Same behaviour as the search
	/// results grid (no overflow: the spacing is accounted for before sizing the cells).
	/// </summary>
	[RequireComponent(typeof(GridLayoutGroup))]
	public class VideoGridFitter : MonoBehaviour, IUpdateLayout {
		[Tooltip("Gap between two cells.")]
		public Vector2 spacing = new(16f, 16f);

		[Tooltip("Width / height ratio of a cell.")]
		public float ratio = 16f / 9f;

		[Tooltip("Minimum cell width; drives the column count.")]
		public float minWidth = 220f;

		/// <inheritdoc />
		public void UpdateLayout() {
			var grid = GetComponent<GridLayoutGroup>();
			var rect = GetComponent<RectTransform>();
			if (!grid || !rect)
				return;

			grid.spacing        = spacing;
			grid.childAlignment = TextAnchor.UpperLeft;

			var available = rect.rect.width - grid.padding.left - grid.padding.right;
			if (available <= 0f)
				return;

			var step    = Mathf.Max(1f, minWidth + spacing.x);
			var columns = Mathf.Max(1, Mathf.FloorToInt((available + spacing.x) / step));
			var cell    = (available - spacing.x * (columns - 1)) / columns;

			grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
			grid.constraintCount = columns;
			grid.cellSize        = new Vector2(cell, cell / ratio);
		}

		private void OnValidate()
			=> UpdateLayout();
	}
}

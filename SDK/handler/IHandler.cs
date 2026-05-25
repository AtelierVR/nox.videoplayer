using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nox.VideoPlayer {
	public interface IHandler {
		public string GetId();

		public string GetTitleKey();

		public string[] GetTitleArguments();

		public int EstimatePriority(IFetchOptions options);

		public UniTask<IResult[]> Fetch(IFetchOptions options);
	}
}
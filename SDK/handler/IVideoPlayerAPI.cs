namespace Nox.VideoPlayer {
	public interface IVideoPlayerAPI {
		public IHandler Add(IHandler handler);

		public void Remove(string id);

		public IHandler Get(string id);

		public bool Has(string id);
	}
}
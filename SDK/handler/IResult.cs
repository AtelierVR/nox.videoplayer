namespace Nox.VideoPlayer {
	public interface IResult {
		public bool IsError();

		public string GetError();

		public bool HasNext();

		public IResolve[] GetData();
	}
}
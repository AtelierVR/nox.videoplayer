namespace Nox.VideoPlayer {
	public interface IResult {
		public bool IsError { get; }

		public string Message { get; }

		public bool HasNext();

		public IResolve[] Data { get; }
	}
}
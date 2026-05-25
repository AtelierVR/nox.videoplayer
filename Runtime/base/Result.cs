using System;
using System.Linq;
using Nox.VideoPlayer;

namespace api.nox.videoplayer {
	public class Result : IResult {
		public string       Error = null;
		public Resolve[] Data  = null;

		public bool IsError()
			=> !string.IsNullOrEmpty(Error);

		public string GetError()
			=> Error;

		public bool HasNext()
			=> false;

		public IResolve[] GetData()
			=> Data.Cast<IResolve>().ToArray();

		public static Result FromError(string error)
			=> new() {
				Error = error,
				Data  = Array.Empty<Resolve>()
			};

		public static Result FromData(Resolve[] data)
			=> new() {
				Error = null,
				Data  = data
			};
	}
}
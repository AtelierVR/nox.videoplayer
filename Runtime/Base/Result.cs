using System;
using System.Linq;
using Nox.VideoPlayer;

namespace Nox.VideoPlayer.Runtime.Base {
	public class Result : IResult {
		public string     Error { get; set; } = null;
		public IResolve[] Data  { get; set; } = null;

		public bool IsError
			=> !string.IsNullOrEmpty(Error);

		public string Message
			=> Error;

		public bool HasNext()
			=> false;

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
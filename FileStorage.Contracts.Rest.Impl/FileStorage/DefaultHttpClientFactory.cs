using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;

namespace FileStorage.Contracts.Rest.Impl.FileStorage
{
	/// <summary>
	/// <see cref="IHttpClientFactory"/> for clients created without a factory. Service.Registry 6.x leaves
	/// RestClientResponseBase.ClientFactory null in this case, so every request failed with NullReferenceException.
	/// One thread-safe <see cref="HttpClient"/> per name; pooled connections are recycled to pick up DNS changes.
	/// </summary>
	internal sealed class DefaultHttpClientFactory : IHttpClientFactory
	{
		private readonly ConcurrentDictionary<string, Lazy<HttpClient>> _clients =
			new ConcurrentDictionary<string, Lazy<HttpClient>>(StringComparer.Ordinal);

		private DefaultHttpClientFactory()
		{
		}

		/// <summary>
		/// Shared instance.
		/// </summary>
		public static DefaultHttpClientFactory Instance { get; } = new DefaultHttpClientFactory();

		/// <inheritdoc />
		public HttpClient CreateClient(string name)
		{
			return _clients.GetOrAdd(name ?? string.Empty,
				_ => new Lazy<HttpClient>(Create, LazyThreadSafetyMode.ExecutionAndPublication)).Value;
		}

		private static HttpClient Create()
		{
			var handler = new SocketsHttpHandler
			{
				PooledConnectionLifetime = TimeSpan.FromMinutes(2),
				PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1)
			};
			// files may be large: the default timeout of 100 seconds is too short
			return new HttpClient(handler, disposeHandler: true)
			{
				Timeout = TimeSpan.FromMinutes(10)
			};
		}
	}
}

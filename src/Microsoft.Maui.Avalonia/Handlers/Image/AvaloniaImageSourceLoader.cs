using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Microsoft.Maui.Avalonia.Handlers;

internal static class AvaloniaImageSourceLoader
{
	static readonly HttpClient HttpClient = new()
	{
		Timeout = TimeSpan.FromSeconds(30)
	};

	public static Task<Bitmap?> LoadAsync(IImageSource imageSource, IServiceProvider services, CancellationToken cancellationToken)
	{
		return imageSource switch
		{
			IFileImageSource fileImageSource => LoadFromFileAsync(fileImageSource.File, cancellationToken),
			IStreamImageSource streamImageSource => LoadFromStreamAsync(streamImageSource, cancellationToken),
			IUriImageSource uriImageSource => LoadFromUriAsync(uriImageSource, cancellationToken),
			_ => Task.FromResult<Bitmap?>(null)
		};
	}

	static async Task<Bitmap?> LoadFromFileAsync(string? path, CancellationToken token)
	{
		if (string.IsNullOrWhiteSpace(path))
			return null;

		token.ThrowIfCancellationRequested();

		if (TryOpenAsset(path, out var assetStream))
		{
			await using (assetStream.ConfigureAwait(false))
			{
				return await CreateBitmapAsync(assetStream, token).ConfigureAwait(false);
			}
		}

		var resolved = ResolveRelativePath(path);
		if (resolved is null || !File.Exists(resolved))
			return null;

		await using var fileStream = File.OpenRead(resolved);
		return await CreateBitmapAsync(fileStream, token).ConfigureAwait(false);
	}

	static async Task<Bitmap?> LoadFromStreamAsync(IStreamImageSource streamImageSource, CancellationToken token)
	{
		var stream = await streamImageSource.GetStreamAsync(token).ConfigureAwait(false);
		if (stream == null)
			return null;

		await using (stream.ConfigureAwait(false))
		{
			return await CreateBitmapAsync(stream, token).ConfigureAwait(false);
		}
	}

	static async Task<Bitmap?> LoadFromUriAsync(IUriImageSource uriImageSource, CancellationToken token)
	{
		var uri = uriImageSource.Uri;

		if (!uri.IsAbsoluteUri)
			return await LoadFromFileAsync(uri.OriginalString, token).ConfigureAwait(false);

		if (IsAssetUri(uri))
			return await LoadFromFileAsync(uri.ToString(), token).ConfigureAwait(false);

		if (uri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
			return await LoadFromFileAsync(uri.LocalPath, token).ConfigureAwait(false);

		if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
			uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, uri);

			if (!uriImageSource.CachingEnabled)
			{
				request.Headers.CacheControl = new CacheControlHeaderValue
				{
					NoCache = true,
					NoStore = true
				};
			}

			using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
			response.EnsureSuccessStatusCode();
			await using var networkStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
			return await CreateBitmapAsync(networkStream, token).ConfigureAwait(false);
		}

		return null;
	}

	static async Task<Bitmap?> CreateBitmapAsync(Stream source, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();

		using var buffer = new MemoryStream();
		await source.CopyToAsync(buffer, 81920, token).ConfigureAwait(false);
		buffer.Position = 0;
		return new Bitmap(buffer);
	}

	static bool TryOpenAsset(string path, out Stream stream)
	{
		stream = Stream.Null;

		if (!Uri.TryCreate(path, UriKind.Absolute, out var uri))
			return false;

		if (!IsAssetUri(uri) || !global::Avalonia.Platform.AssetLoader.Exists(uri))
			return false;

		stream = global::Avalonia.Platform.AssetLoader.Open(uri);
		return true;
	}

	static bool IsAssetUri(Uri uri)
	{
		if (!uri.IsAbsoluteUri)
			return false;

		return uri.Scheme.Equals("avares", StringComparison.OrdinalIgnoreCase) ||
			uri.Scheme.Equals("resm", StringComparison.OrdinalIgnoreCase) ||
			uri.Scheme.Equals("ms-appx", StringComparison.OrdinalIgnoreCase);
	}

	static string? ResolveRelativePath(string path)
	{
		if (Path.IsPathRooted(path))
			return path;

		var normalized = path.Replace('/', Path.DirectorySeparatorChar);
		var baseDir = AppContext.BaseDirectory;

		static bool TryResolve(string candidate, out string fullPath)
		{
			fullPath = candidate;
			return File.Exists(candidate);
		}

		if (TryResolve(Path.Combine(baseDir, normalized), out var direct))
			return direct;

		var resourceCandidates = new[]
		{
			Path.Combine(baseDir, Path.GetFileName(normalized) ?? normalized),
			Path.Combine(baseDir, "Resources", normalized),
			Path.Combine(baseDir, "Resources", "Images", normalized),
			Path.Combine(baseDir, "Resources", "Images", Path.GetFileName(normalized) ?? normalized)
		};

		foreach (var candidate in resourceCandidates)
		{
			if (TryResolve(candidate, out var resolved))
				return resolved;
		}

		return null;
	}
}

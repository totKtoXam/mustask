namespace Mustask.Application.Abstractions.Images;

/// <summary>
/// The request body of an upload, read under a cap by the Api (plan-backend § 6.2). The handler asks for it only after its
/// address checks, so the API reads no byte of the body of a refused
/// caller (a proxy in front may already have buffered it). The bytes are valid until the answer is disposed.
/// </summary>
public interface IUploadBody
{
    Task<UploadBodyAnswer> ReadAsync(int maxBytes, CancellationToken cancellationToken);
}

/// <summary>The bytes read, or <see cref="TooLarge"/> when the body (declared or counted) is over the cap.</summary>
public sealed class UploadBodyAnswer(ReadOnlyMemory<byte> content, bool tooLarge, IDisposable? lease) : IDisposable
{
    public ReadOnlyMemory<byte> Content { get; } = content;

    public bool TooLarge { get; } = tooLarge;

    public void Dispose() => lease?.Dispose();
}

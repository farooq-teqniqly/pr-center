namespace PrCenter.GitHub.Tests;

/// <summary>
/// Test handler for faking <see cref="HttpClient"/>. <see cref="SendAsync"/> is
/// protected and cannot be intercepted by NSubstitute, so it is sealed to
/// delegate to the public abstract <see cref="MockSendAsync"/>, which a
/// substitute can configure.
/// </summary>
internal abstract class FakeHttpMessageHandler : HttpMessageHandler
{
    /// <summary>Configurable stand-in for the sealed send.</summary>
    /// <param name="request">The outgoing request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The faked response.</returns>
    public abstract Task<HttpResponseMessage> MockSendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    );

    /// <inheritdoc />
    protected sealed override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    ) => MockSendAsync(request, cancellationToken);
}

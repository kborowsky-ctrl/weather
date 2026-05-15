namespace WeatherWizard.Services;

public sealed class HttpClientFactory : IDisposable
{
    public HttpClient Client { get; }

    public HttpClientFactory()
    {
        Client = new HttpClient();
        Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", AppConstants.HttpUserAgent);
        Client.Timeout = TimeSpan.FromSeconds(45);
    }

    public void Dispose() => Client.Dispose();
}

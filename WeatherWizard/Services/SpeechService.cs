using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace WeatherWizard.Services;

public sealed class SpeechService : IDisposable
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly MediaPlayer _player = new();
    private readonly SemaphoreSlim _speechGate = new(1, 1);

    public async Task SpeakAsync(string text, CancellationToken ct = default)
    {
        await _speechGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var stream = await _synthesizer.SynthesizeTextToStreamAsync(text);
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnEnded(MediaPlayer s, object o)
            {
                s.MediaEnded -= OnEnded;
                tcs.TrySetResult();
            }

            _player.MediaEnded += OnEnded;
            _player.AutoPlay = true;
            _player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
            _player.Play();

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(45), ct)).ConfigureAwait(false);
            if (completed != tcs.Task)
            {
                _player.MediaEnded -= OnEnded;
                _player.Pause();
            }
        }
        finally
        {
            _speechGate.Release();
        }
    }

    public void Dispose()
    {
        _player.Dispose();
        _synthesizer.Dispose();
        _speechGate.Dispose();
    }
}

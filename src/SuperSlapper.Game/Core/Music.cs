using Microsoft.Xna.Framework.Audio;

namespace SuperSlapper.Game.Core;

/// <summary>
/// Looping background music. Tracks are ordinary generated WAVs like every other
/// sound, so there is no content pipeline involved: the song is a request from the
/// screen on top of the stack, and a change of track crossfades over about three
/// quarters of a second rather than cutting.
/// </summary>
public sealed class Music
{
    private const float FadeSeconds = 0.75f;

    private readonly Assets _assets;
    private SoundEffectInstance? _active;
    private SoundEffectInstance? _fading;
    private string? _track;
    private float _fade = 1f;
    private float _volume = 0.35f;

    public Music(Assets assets) => _assets = assets;

    /// <summary>The track that has been asked for, or null when the game is silent.</summary>
    public string? Track => _track;

    /// <summary>
    /// Level the music plays at. Zero silences it without forgetting the request,
    /// so turning it back up resumes the same track.
    /// </summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Requests a track, or silence when <paramref name="track"/> is null. The
    /// screen stack calls this every frame, so nothing happens unless the request
    /// differs from the track already playing.
    /// </summary>
    public void Play(string? track)
    {
        if (track == _track)
        {
            return;
        }

        _track = track;
        _fade = 0f;

        if (_active is null)
        {
            return;
        }

        // The outgoing track keeps playing underneath the fade instead of being cut.
        _fading?.Dispose();
        _fading = _active;
        _active = null;
    }

    /// <summary>Advances the crossfade. Called every frame by the screen stack.</summary>
    public void Update(float elapsedSeconds)
    {
        _fade = Math.Min(1f, _fade + elapsedSeconds / FadeSeconds);

        if (_volume <= 0f)
        {
            // Muted: stop the tracks, but keep the request so raising the level
            // starts the same track again.
            _active?.Dispose();
            _active = null;
            _fading?.Dispose();
            _fading = null;
            return;
        }

        if (_active is null && _track is not null)
        {
            Start(_track);
        }

        if (_active is not null)
        {
            SetVolume(_active, _volume * FadeGain(_fade));
        }

        if (_fading is null)
        {
            return;
        }

        SetVolume(_fading, _volume * FadeGain(1f - _fade));

        if (_fade >= 1f)
        {
            _fading.Dispose();
            _fading = null;
        }
    }

    private void Start(string track)
    {
        var sound = _assets.Sound(track);
        if (sound is null)
        {
            return;
        }

        try
        {
            _active = sound.CreateInstance();
            _active.IsLooped = true;
            _active.Volume = 0f;
            _active.Play();
            _fade = 0f;
        }
        catch
        {
            // No audio device available (headless test runs); never take the game down for it.
            _active = null;
        }
    }

    /// <summary>
    /// The fade shape. The two gains are the sine and cosine of the same angle, so
    /// they always sum to a constant power: a crossfade through the middle keeps
    /// its loudness instead of dipping the way a straight linear pair does.
    /// </summary>
    private static float FadeGain(float position) => (float)Math.Sin(position * Math.PI / 2);

    private static void SetVolume(SoundEffectInstance instance, float volume)
    {
        try
        {
            instance.Volume = Math.Clamp(volume, 0f, 1f);
        }
        catch
        {
            // A device that goes away mid-session is not worth a crash.
        }
    }
}

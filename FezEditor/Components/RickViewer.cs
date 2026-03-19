using System.Diagnostics;
using FezEditor.Structure;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Vector2 = Microsoft.Xna.Framework.Vector2;

namespace FezEditor.Components;

public class RickViewer : EditorComponent
{
    private const float PlayerWidth = 250f;
    private const float PlayerHeight = 100f;
    private const float PlayerButtonSize = 32f;

    private RSoundEffect _soundEffectAsset;
    private VorbisSoundContainer? _oggSoundContainer;

    private SoundEffect? _soundEffect;
    private SoundEffectInstance? _soundEffectInstance;
    private readonly Stopwatch _playbackStopwatch = new();
    private TimeSpan _duration = TimeSpan.Zero;

    private float? _currentSeekRequest = null;

    public RickViewer(Game game, string title, RSoundEffect soundEffectAsset) : base(game, title)
    {
        _soundEffectAsset = soundEffectAsset;
        _oggSoundContainer = null;
    }

    public RickViewer(Game game, string title, VorbisSoundContainer soundContainer) : base(game, title)
    {
        _soundEffectAsset = new RSoundEffect();
        _oggSoundContainer = soundContainer;
    }

    public override void LoadContent()
    {
        if (_oggSoundContainer != null)
        {
            _oggSoundContainer.Load();
            _soundEffectAsset = _oggSoundContainer.CreateSoundEffectAsset();
            _oggSoundContainer.Dispose();
            _oggSoundContainer = null;
        }
        CreateInitialSoundEffectInstance();
    }

    public override void Draw()
    {
        var regionSize = ImGuiX.GetContentRegionAvail();
        var offsetX = Math.Max(0, (regionSize.X - PlayerWidth) / 2);
        var offsetY = Math.Max(0, (regionSize.Y - PlayerHeight) / 2);

        ImGuiX.SetCursorPos(ImGuiX.GetCursorPos() + new Vector2(offsetX, offsetY));
        ImGui.BeginGroup();

        DrawPlaybackButton();
        DrawPlaybackSlider();
        DrawSoundEffectInfo();

        ImGui.EndGroup();
    }

    private void DrawPlaybackButton()
    {
        var buttonIcon = _soundEffectInstance?.State == SoundState.Playing
            ? Icons.DebugPause
            : Icons.Play;

        if (ImGuiX.Button(buttonIcon, Vector2.One * PlayerButtonSize))
        {
            TogglePlaybackState();
        }
    }

    private void DrawPlaybackSlider()
    {
        ImGui.SetNextItemWidth(PlayerWidth - PlayerButtonSize);
        ImGui.SameLine();
        var sliderBumpOffset = (PlayerButtonSize - ImGui.GetFrameHeight()) * 0.5f;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + sliderBumpOffset);

        var playbackElapsed = _playbackStopwatch.Elapsed;
        var playbackDuration = _soundEffect?.Duration ?? TimeSpan.Zero;
        if (playbackElapsed > playbackDuration)
        {
            playbackElapsed = playbackDuration;
        }

        // if we did seeking, we have to include the duration of a sound that we skipped over
        var skippedDuration = _duration - playbackDuration;
        var totalPlaybackElapsed = playbackElapsed + skippedDuration;
        var playbackProgress = (float)(totalPlaybackElapsed.TotalSeconds / _duration.TotalSeconds);

        var sliderValue = playbackProgress;
        if (_currentSeekRequest != null)
        {
            sliderValue = _currentSeekRequest.Value;
            totalPlaybackElapsed = TimeSpan.FromSeconds(_duration.TotalSeconds * sliderValue);
        }
        var format = $"{totalPlaybackElapsed:mm\\:ss\\.ff} / {_duration:mm\\:ss\\.ff}";
        ImGui.SliderFloat("##playbackSlider", ref sliderValue, 0f, 1f, format);

        if (ImGui.IsItemActivated())
        {
            PausePlayback();
        }

        if (ImGui.IsItemActive())
        {
            _currentSeekRequest = sliderValue;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _currentSeekRequest = null;
            SeekPlayback(sliderValue);
        }
    }

    private void DrawSoundEffectInfo()
    {
        ImGui.NewLine();
        ImGui.Text("Channel count: " + _soundEffectAsset.ChannelCount);
        ImGui.Text("Sample frequency: " + _soundEffectAsset.SampleFrequency + " Hz");
    }

    private void TogglePlaybackState()
    {
        if (_soundEffectInstance!.State == SoundState.Playing)
        {
            PausePlayback();
        }
        else if (_soundEffectInstance!.State == SoundState.Stopped)
        {
            SeekPlayback(0.0f);
        }
        else if (_soundEffectInstance!.State == SoundState.Paused)
        {
            ResumePlayback();
        }
    }

    private void PausePlayback()
    {
        _soundEffectInstance!.Pause();
        _playbackStopwatch.Stop();
    }

    private void ResumePlayback()
    {
        _playbackStopwatch.Start();
        _soundEffectInstance!.Play();
    }

    private void SeekPlayback(float percentage)
    {
        // XNA's SoundEffectInstance doesn't support seeking. We're mimicking
        // this functionality by recreating the sound effect with requested offset
        RecreateSoundEffectInstance(percentage);
        _playbackStopwatch.Restart();
        _soundEffectInstance!.Play();
    }

    private void CreateInitialSoundEffectInstance()
    {
        RecreateSoundEffectInstance(0f);
        if (_soundEffect != null)
        {
            _duration = _soundEffect.Duration;
        }
    }

    private void RecreateSoundEffectInstance(float skipPercentage)
    {
        _soundEffectInstance?.Dispose();
        _soundEffect?.Dispose();

        if (_soundEffectAsset.DataChunk.Length == 0)
        {
            return;
        }

        var byteOffset = Math.Clamp(
            (int)(_soundEffectAsset.DataChunk.Length * skipPercentage),
            0,
            _soundEffectAsset.DataChunk.Length - 1
        );

        byteOffset -= byteOffset % (_soundEffectAsset.BitsPerSample / 8 * _soundEffectAsset.ChannelCount);

        _soundEffect = new SoundEffect(
            _soundEffectAsset.DataChunk,
            byteOffset,
            _soundEffectAsset.DataChunk.Length - byteOffset,
            _soundEffectAsset.SampleFrequency,
            (AudioChannels)_soundEffectAsset.ChannelCount,
            _soundEffectAsset.LoopStart,
            _soundEffectAsset.LoopLength
        );
        _soundEffectInstance = _soundEffect.CreateInstance();
    }

    public override void Dispose()
    {
        _soundEffect?.Dispose();
        _soundEffectInstance?.Dispose();

        base.Dispose();
    }
}
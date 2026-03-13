using Microsoft.Xna.Framework;

namespace FezEditor.Structure;

public class Clock
{
    public static readonly TimeSpan InitialTime = new(12, 0, 0);

    private const float DefaultTimeMultiplier = 260f;

    private const float HoursPerDay = 24f;

    public TimeSpan CurrentTime
    {
        get => _currentTime;
        set
        {
            if (_currentTime == value)
            {
                return;
            }

            _currentTime = value;

            // Day phases
            DawnContribution = Ease(DayFraction, DayPhase.Dawn.StartTime, DayPhase.Dawn.Duration);
            DuskContribution = Ease(DayFraction, DayPhase.Dusk.StartTime, DayPhase.Dusk.Duration);
            NightContribution = Ease(DayFraction, DayPhase.Night.StartTime, DayPhase.Night.Duration);

            // Handle the midnight wrap
            var nightContribution = Ease(DayFraction, DayPhase.Night.StartTime - 1f, DayPhase.Night.Duration);
            NightContribution = Math.Max(NightContribution, nightContribution);
        }
    }

    public float TimeFactor { get; set; } = 1f;

    public float DayFraction => (float)_currentTime.TotalDays % 1f; // A trick to express value as [0..1) range

    /// <summary>
    /// Shows how much "night" currently is within a day as [0..1) range.
    /// </summary>
    public float NightContribution { get; private set; }

    /// <summary>
    /// Shows how much "dawn" currently is within a day as [0..1) range.
    /// </summary>
    public float DawnContribution { get; private set; }

    /// <summary>
    /// Shows how much "dusk" currently is within a day as [0..1) range.
    /// </summary>
    public float DuskContribution { get; private set; }

    private TimeSpan _currentTime = InitialTime;

    public void Tick(GameTime gameTime)
    {
        var millis = gameTime.ElapsedGameTime.TotalMilliseconds * TimeFactor * DefaultTimeMultiplier;
        CurrentTime += TimeSpan.FromMilliseconds(millis);
    }

    private static float Ease(float value, float start, float duration)
    {
        var diff = value - start;
        var step = duration / 3f;

        if (diff < step)
        {
            // Fade in
            return MathHelper.Clamp(diff / step, 0f, 1f);
        }

        if (diff > 2f * step)
        {
            // Fade out
            return 1f - MathHelper.Clamp((diff - (2f * step)) / step, 0f, 1f);
        }

        if (diff < 0f || diff > duration)
        {
            // Outside window
            return 0f;
        }

        // Full contribution
        return 1f;
    }

    private record DayPhase(int StartHour, int EndHour)
    {
        public static readonly DayPhase Night = new(20, 4);

        public static readonly DayPhase Dawn = new(2, 6);

        public static readonly DayPhase Day = new(5, 20);

        public static readonly DayPhase Dusk = new(18, 22);

        public float StartTime => StartHour / HoursPerDay;

        public float Duration
        {
            get
            {
                var endFraction = EndHour / HoursPerDay;
                if (endFraction < StartTime)
                {
                    // Wrap around midnight
                    endFraction += 1f;
                }

                return endFraction - StartTime;
            }
        }
    }
}
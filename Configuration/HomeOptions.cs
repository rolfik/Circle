namespace CircleOfTruthAndLove.Configuration;

/// <summary>
/// Options for the Home (default) page presentation.
/// </summary>
public class HomeOptions
{
    /// <summary>
    /// Minimum scale of the default Home content during a curtain pulse cycle.
    /// 1.0 disables pulsing. Range typically 0.1–1.0.
    /// </summary>
    public double PulseMinScale { get; set; } = 1.0;
}

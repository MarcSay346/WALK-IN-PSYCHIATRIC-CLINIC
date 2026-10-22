using System.Collections.Generic;

namespace ClinicApi.Services;

/// <summary>Wait-Time Estimation module (A3 module 4): rule-based estimate.</summary>
public class WaitTimeService
{
    public const int MaxPosition = 1000;
    public const int MaxDurationMinutes = 480;

    /// <summary>Estimated minutes until a user at <paramref name="position"/> is served.</summary>
    public int EstimateMinutes(int? position, int? expectedDurationMinutes)
    {
        var errors = new Dictionary<string, string>();

        // TODO: validate the inputs with InputRules (already in the project), e.g.
        //   InputRules.IntInRange(errors, "position", "Queue position", position, 1, MaxPosition);
        //   ...same idea for expectedDurationMinutes (1..MaxDurationMinutes)...
        //   InputRules.ThrowIfInvalid(errors);

        // TODO: return the estimate. A3 suggests: (people ahead of you) x (expected duration).
        return 0;
    }
}

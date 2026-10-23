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

        // Validate the inputs (required, whole numbers, within range)
        InputRules.IntInRange(errors, "position", "Queue position", position, 1, MaxPosition);
        InputRules.IntInRange(errors, "expectedDurationMinutes", "Expected duration", expectedDurationMinutes, 1, MaxDurationMinutes);
        InputRules.ThrowIfInvalid(errors);

        // Rule-based estimate: (people ahead of you) x (expected duration per person).
        // Position 1 = next in line = 0 minutes.
        return (position!.Value - 1) * expectedDurationMinutes!.Value;
    }
}
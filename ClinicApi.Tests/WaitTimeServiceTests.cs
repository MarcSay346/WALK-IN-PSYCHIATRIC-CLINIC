using ClinicApi.Services;
using Xunit;

namespace ClinicApi.Tests;

public class WaitTimeServiceTests
{
    private readonly WaitTimeService _service = new();

    [Fact]
    public void Estimate_InvalidPosition_Throws()
    {
        Assert.Throws<ApiValidationException>(() => _service.EstimateMinutes(0, 15));
    }

    // TODO: test a normal estimate (e.g. position 3 with 15 minutes each)
    // TODO: test position 1 (next in line)
    // TODO: test a missing duration (null) and a too-large duration
    // TODO: test the endpoint: WaitTimeEndpoints.GetEstimate(...) returns 200 for good input, 400 for bad input
}
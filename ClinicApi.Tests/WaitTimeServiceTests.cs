using ClinicApi.Endpoints;
using ClinicApi.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ClinicApi.Tests;

public class WaitTimeServiceTests
{
    private readonly WaitTimeService _service = new();

    private static int StatusOf(IResult result) => ((IStatusCodeHttpResult)result).StatusCode ?? 0;

    [Fact]
    public void Estimate_InvalidPosition_Throws()
    {
        Assert.Throws<ApiValidationException>(() => _service.EstimateMinutes(0, 15));
    }

    [Fact]
    public void Estimate_Position3_With15MinuteVisits_Is30Minutes()
    {
        Assert.Equal(30, _service.EstimateMinutes(3, 15));
    }

    [Fact]
    public void Estimate_NextInLine_IsZero()
    {
        Assert.Equal(0, _service.EstimateMinutes(1, 45));
    }

    [Theory]
    [InlineData(null, 15, "position")]
    [InlineData(1001, 15, "position")]
    [InlineData(3, null, "expectedDurationMinutes")]
    [InlineData(3, 0, "expectedDurationMinutes")]
    [InlineData(3, 481, "expectedDurationMinutes")]
    public void Estimate_MissingOrOutOfRange_ThrowsWithFieldError(int? position, int? duration, string field)
    {
        var ex = Assert.Throws<ApiValidationException>(() => _service.EstimateMinutes(position, duration));
        Assert.True(ex.Errors.ContainsKey(field));
    }

    [Fact]
    public void Endpoint_GoodInput_Returns200()
    {
        Assert.Equal(200, StatusOf(WaitTimeEndpoints.GetEstimate(3, 15, _service)));
    }

    [Fact]
    public void Endpoint_BadInput_Returns400()
    {
        Assert.Equal(400, StatusOf(WaitTimeEndpoints.GetEstimate(0, 15, _service)));
    }
}

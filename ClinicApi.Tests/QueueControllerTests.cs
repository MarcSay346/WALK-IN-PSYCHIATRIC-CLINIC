using RadiantMindClinic.Controllers;
using RadiantMindClinic.Models;
using RadiantMindClinic.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ClinicApi.Tests;

/// <summary>Checks the queue API returns the right responses (200 / 400 / 404).</summary>
public class QueueControllerTests
{
    private readonly QueueService _service = new();
    private readonly QueueController _controller;

    public QueueControllerTests()
    {
        _controller = new QueueController(_service);
    }

    private QueueItem JoinDirect(string name = "Ana") =>
        _service.JoinQueue(new JoinQueueRequest { Name = name, Contact = "a@test.com", ServiceName = "Dr. Nate" });

    [Fact]
    public void JoinQueue_Valid_ReturnsOkWithTheQueueItem()
    {
        var result = _controller.JoinQueue(new JoinQueueRequest { Name = "Ana", Contact = "a@test.com", ServiceName = "Dr. Nate" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var item = Assert.IsType<QueueItem>(ok.Value);
        Assert.Equal(1, item.Position);
    }

    [Theory]
    [InlineData("", "a@test.com")]
    [InlineData("Ana", "")]
    [InlineData("   ", "   ")]
    public void JoinQueue_MissingNameOrContact_ReturnsBadRequest(string name, string contact)
    {
        var result = _controller.JoinQueue(new JoinQueueRequest { Name = name, Contact = contact, ServiceName = "Dr. Nate" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetQueue_ReturnsOk()
    {
        JoinDirect();

        Assert.IsType<OkObjectResult>(_controller.GetQueue("Dr. Nate"));
    }

    [Fact]
    public void LeaveQueue_KnownPatient_ReturnsOk()
    {
        var item = JoinDirect();

        Assert.IsType<OkObjectResult>(_controller.LeaveQueue(item.Id));
    }

    [Fact]
    public void LeaveQueue_UnknownPatient_ReturnsNotFound()
    {
        Assert.IsType<NotFoundObjectResult>(_controller.LeaveQueue("nope"));
    }

    [Fact]
    public void ServeNext_WithPatientWaiting_ReturnsOk()
    {
        JoinDirect();

        Assert.IsType<OkObjectResult>(_controller.ServeNext("Dr. Nate"));
    }

    [Fact]
    public void ServeNext_EmptyQueue_ReturnsNotFound()
    {
        Assert.IsType<NotFoundObjectResult>(_controller.ServeNext("Dr. Nate"));
    }

    [Fact]
    public void GetStatus_KnownPatient_ReturnsOk()
    {
        var item = JoinDirect();

        Assert.IsType<OkObjectResult>(_controller.GetStatus(item.Id));
    }

    [Fact]
    public void GetStatus_UnknownPatient_ReturnsNotFound()
    {
        Assert.IsType<NotFoundObjectResult>(_controller.GetStatus("nope"));
    }
}

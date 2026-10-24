using System.Linq;
using RadiantMindClinic.Models;
using RadiantMindClinic.Services;
using Xunit;

namespace ClinicApi.Tests;

/// <summary>Queue Management module (QueueService): join, leave, ordering, wait estimates, serve next.</summary>
public class QueueServiceTests
{
    private readonly QueueService _queue = new();

    private QueueItem Join(string name, string service = "Dr. Nate", int priority = 3) =>
        _queue.JoinQueue(new JoinQueueRequest { Name = name, Contact = name + "@test.com", ServiceName = service, Priority = priority });

    [Fact]
    public void JoinQueue_FirstPatient_IsPositionOne_WithNoWait()
    {
        var item = Join("Ana");

        Assert.Equal(1, item.Position);
        Assert.Equal(0, item.EstimatedWaitMinutes);
        Assert.False(string.IsNullOrEmpty(item.Id));
    }

    [Fact]
    public void JoinQueue_SecondPatient_WaitsForTheFirstVisit()
    {
        Join("Ana");                 // Dr. Nate visits take 60 minutes
        var second = Join("Ben");

        Assert.Equal(2, second.Position);
        Assert.Equal(60, second.EstimatedWaitMinutes);
    }

    [Fact]
    public void JoinQueue_WaitAddsUpForEveryoneAhead()
    {
        Join("Ana", "Dr. Cole");     // Dr. Cole visits take 30 minutes
        Join("Ben", "Dr. Cole");
        var third = Join("Cal", "Dr. Cole");

        Assert.Equal(3, third.Position);
        Assert.Equal(60, third.EstimatedWaitMinutes);
    }

    [Fact]
    public void JoinQueue_NoServiceName_UsesGeneralWalkIn()
    {
        var item = _queue.JoinQueue(new JoinQueueRequest { Name = "Ana", Contact = "a@test.com", ServiceName = null! });

        Assert.Equal("General Walk-In Consultation", item.ServiceName);
    }

    [Fact]
    public void JoinQueue_UnknownService_UsesDefaultDurationOf20()
    {
        Join("Ana", "Dr. Unknown");
        var second = Join("Ben", "Dr. Unknown");

        Assert.Equal(20, second.EstimatedWaitMinutes);
    }

    [Fact]
    public void Ordering_HigherPriorityGoesFirst_EvenIfTheyArriveLater()
    {
        var standard = Join("Ana", priority: 3);
        var urgent = Join("Ben", priority: 1);

        Assert.Equal(1, urgent.Position);
        Assert.Equal(2, standard.Position);
    }

    [Fact]
    public void Ordering_SamePriority_FollowsArrivalOrder()
    {
        var first = Join("Ana");
        var second = Join("Ben");

        var queue = _queue.GetCurrentQueue("Dr. Nate").ToList();

        Assert.Equal(first.Id, queue[0].Id);
        Assert.Equal(second.Id, queue[1].Id);
    }

    [Fact]
    public void EachService_HasItsOwnLine()
    {
        Join("Ana", "Dr. Nate");
        var other = Join("Ben", "Dr. Valdez");

        Assert.Equal(1, other.Position);
        Assert.Equal(0, other.EstimatedWaitMinutes);
    }

    [Fact]
    public void GetCurrentQueue_FiltersByService_AndListsAllWithoutFilter()
    {
        Join("Ana", "Dr. Nate");
        Join("Ben", "Dr. Valdez");

        Assert.Single(_queue.GetCurrentQueue("Dr. Nate"));
        Assert.Equal(2, _queue.GetCurrentQueue(null!).Count());
    }

    [Fact]
    public void LeaveQueue_RemovesPatient_AndMovesOthersUp()
    {
        var first = Join("Ana");
        var second = Join("Ben");

        Assert.True(_queue.LeaveQueue(first.Id));

        var status = _queue.GetPatientStatus(second.Id);
        Assert.Equal(1, status.Position);
        Assert.Equal(0, status.EstimatedWaitMinutes);
    }

    [Fact]
    public void LeaveQueue_UnknownId_ReturnsFalse()
    {
        Assert.False(_queue.LeaveQueue("nope"));
    }

    [Fact]
    public void ServeNext_ReturnsHighestPriority_AndRemovesThem()
    {
        Join("Ana", priority: 3);
        var urgent = Join("Ben", priority: 1);

        var served = _queue.ServeNext("Dr. Nate");

        Assert.Equal(urgent.Id, served.Id);
        Assert.Single(_queue.GetCurrentQueue("Dr. Nate"));
    }

    [Fact]
    public void ServeNext_OnlyServesTheChosenService()
    {
        Join("Ana", "Dr. Nate");
        var valdez = Join("Ben", "Dr. Valdez");

        var served = _queue.ServeNext("Dr. Valdez");

        Assert.Equal(valdez.Id, served.Id);
        Assert.Single(_queue.GetCurrentQueue("Dr. Nate"));
    }

    [Fact]
    public void ServeNext_EmptyQueue_ReturnsNull()
    {
        Assert.Null(_queue.ServeNext("Dr. Nate"));
    }

    [Fact]
    public void GetPatientStatus_UnknownId_ReturnsNull()
    {
        Assert.Null(_queue.GetPatientStatus("nope"));
    }
}

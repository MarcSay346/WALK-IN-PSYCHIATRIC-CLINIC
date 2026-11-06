using System;
using System.Collections.Generic;
using System.Linq;
using ClinicApi.Services;
using Microsoft.AspNetCore.Mvc;
using RadiantMindClinic.Controllers;
using RadiantMindClinic.Models;
using RadiantMindClinic.Services;
using Xunit;

namespace ClinicApi.Tests;

/// <summary>A4: Queue module stored in the Queue + QueueEntry tables (SQLite test copy of the schema).</summary>
public class SqlQueueServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly SqlQueueService _queue;
    private readonly int _ana, _ben, _cara;

    public SqlQueueServiceTests()
    {
        _queue = new SqlQueueService(_db);
        _db.AddService("Dr. Nate", durationMinutes: 60, priority: "High");
        _db.AddService("Dr. Cole", durationMinutes: 30, priority: "Low");
        _ana = _db.AddUser("ana@test.com");
        _ben = _db.AddUser("ben@test.com");
        _cara = _db.AddUser("cara@test.com");
    }

    public void Dispose() => _db.Dispose();

    private QueueItem Join(int userId, string service = "Dr. Nate", int priority = 3, string name = "Patient") =>
        _queue.JoinQueue(new JoinQueueRequest
        {
            UserId = userId.ToString(), Name = name, Contact = "713-555-0000", ServiceName = service, Priority = priority
        });

    // ---------- join ----------

    [Fact]
    public void Join_SavesEntry_AndCreatesOpenQueueForService()
    {
        var item = Join(_ana, name: "Ana");

        Assert.Equal(1, item.Position);
        Assert.Equal(0, item.EstimatedWaitMinutes);
        Assert.Equal(_ana.ToString(), item.UserId);
        Assert.Equal(1, _db.Scalar<int>("SELECT COUNT(*) FROM Queue WHERE Status = 'Open'"));
        Assert.Equal("Waiting", _db.Scalar<string>("SELECT Status FROM QueueEntry WHERE EntryId = @id", new { id = int.Parse(item.Id) }));
    }

    [Fact]
    public void Join_UsesServiceDurationForWaitEstimate_AndStoresPosition()
    {
        Join(_ana);
        var second = Join(_ben);

        Assert.Equal(2, second.Position);
        Assert.Equal(60, second.EstimatedWaitMinutes);
        Assert.Equal(2, _db.Scalar<int>("SELECT Position FROM QueueEntry WHERE EntryId = @id", new { id = int.Parse(second.Id) }));
    }

    [Fact]
    public void Join_HigherPriorityGoesFirst()
    {
        Join(_ana, priority: 3);
        var urgent = Join(_ben, priority: 1);

        Assert.Equal(1, urgent.Position);
        Assert.Equal(_ben.ToString(), _queue.GetCurrentQueue("Dr. Nate").First().UserId);
        Assert.Equal(2, _db.Scalar<int>("SELECT Position FROM QueueEntry WHERE UserId = @_ana", new { _ana }));
    }

    [Fact]
    public void Join_ByEmail_AndServiceNameIgnoresCase()
    {
        var item = _queue.JoinQueue(new JoinQueueRequest
        {
            UserId = "ANA@test.com", Name = "Ana", Contact = "a", ServiceName = "dr. cole", Priority = 3
        });
        Assert.Equal("Dr. Cole", item.ServiceName);
        Assert.Equal(_ana.ToString(), item.UserId);
    }

    [Fact]
    public void Join_SameUserTwice_IsRejected()
    {
        Join(_ana);
        Assert.Throws<InvalidOperationException>(() => Join(_ana));
        Join(_ana, service: "Dr. Cole");   // a different service is fine
    }

    [Fact]
    public void Join_InvalidInput_IsRejected()
    {
        Assert.Throws<ApiValidationException>(() => Join(_ana, service: "Dr. Nobody"));
        Assert.Throws<ApiValidationException>(() => Join(_ana, priority: 7));
        Assert.Throws<ApiValidationException>(() => Join(_ana, name: new string('x', 101)));
        Assert.Throws<ApiValidationException>(() => Join(9999));
        Assert.Throws<ApiValidationException>(() => _queue.JoinQueue(new JoinQueueRequest
        {
            Name = "No user", Contact = "c", ServiceName = "Dr. Nate", Priority = 3
        }));
        Assert.Throws<ApiValidationException>(() => _queue.JoinQueue(new JoinQueueRequest
        {
            UserId = _ana.ToString(), Name = "Ana", Contact = new string('c', 101), ServiceName = "Dr. Nate", Priority = 3
        }));
        Assert.Throws<ApiValidationException>(() => _queue.JoinQueue(null!));
        Assert.Equal(0, _db.Scalar<int>("SELECT COUNT(*) FROM QueueEntry"));
    }

    // ---------- view / status ----------

    [Fact]
    public void GetCurrentQueue_FiltersByService_AndPersistsAcrossInstances()
    {
        Join(_ana);
        Join(_ben, service: "Dr. Cole");
        Join(_cara);

        var fresh = new SqlQueueService(_db);
        Assert.Equal(3, fresh.GetCurrentQueue().Count());
        Assert.Equal(2, fresh.GetCurrentQueue("Dr. Nate").Count());
        Assert.Single(fresh.GetCurrentQueue("dr. cole"));
    }

    [Fact]
    public void GetPatientStatus_ReturnsCurrentPosition_OrNull()
    {
        Join(_ana);
        var ben = Join(_ben);

        Assert.Equal(2, _queue.GetPatientStatus(ben.Id).Position);
        Assert.Null(_queue.GetPatientStatus("9999"));
        Assert.Null(_queue.GetPatientStatus("not-a-number"));
    }

    // ---------- leave / serve ----------

    [Fact]
    public void Leave_MarksCanceled_AndMovesOthersUp()
    {
        var ana = Join(_ana);
        var ben = Join(_ben);

        Assert.True(_queue.LeaveQueue(ana.Id));

        Assert.Equal("Canceled", _db.Scalar<string>("SELECT Status FROM QueueEntry WHERE EntryId = @id", new { id = int.Parse(ana.Id) }));
        Assert.Equal(1, _queue.GetPatientStatus(ben.Id).Position);
        Assert.Equal(1, _db.Scalar<int>("SELECT Position FROM QueueEntry WHERE EntryId = @id", new { id = int.Parse(ben.Id) }));
        Assert.Null(_queue.GetPatientStatus(ana.Id));
    }

    [Fact]
    public void Leave_UnknownOrAlreadyLeft_ReturnsFalse()
    {
        var ana = Join(_ana);
        Assert.True(_queue.LeaveQueue(ana.Id));
        Assert.False(_queue.LeaveQueue(ana.Id));
        Assert.False(_queue.LeaveQueue("9999"));
        Assert.False(_queue.LeaveQueue("abc"));
    }

    [Fact]
    public void ServeNext_ServesHighestPriorityThenEarliest()
    {
        Join(_ana, priority: 3);
        Join(_ben, priority: 2);
        Join(_cara, priority: 2);

        var first = _queue.ServeNext("Dr. Nate");
        var second = _queue.ServeNext("Dr. Nate");

        Assert.Equal(_ben.ToString(), first.UserId);
        Assert.Equal(_cara.ToString(), second.UserId);
        Assert.Equal(2, _db.Scalar<int>("SELECT COUNT(*) FROM QueueEntry WHERE Status = 'Served'"));
        Assert.Equal(1, _queue.GetCurrentQueue("Dr. Nate").Single().Position);
    }

    [Fact]
    public void ServeNext_EmptyQueue_ReturnsNull()
    {
        Assert.Null(_queue.ServeNext("Dr. Nate"));
        Assert.Null(_queue.ServeNext());
    }

    [Fact]
    public void ServeNext_WithoutService_ServesAcrossAllQueues()
    {
        Join(_ana, service: "Dr. Cole", priority: 1);
        Join(_ben, service: "Dr. Nate", priority: 3);

        Assert.Equal(_ana.ToString(), _queue.ServeNext().UserId);
    }

    // ---------- open / closed ----------

    [Fact]
    public void ClosedQueue_RejectsNewPeople_UntilReopened()
    {
        Join(_ana);
        Assert.Equal("Closed", _queue.SetQueueStatus("Dr. Nate", open: false));
        Assert.Equal("Closed", _queue.GetQueueStatus("Dr. Nate"));

        Assert.Throws<InvalidOperationException>(() => Join(_ben));

        _queue.SetQueueStatus("Dr. Nate", open: true);
        Assert.Equal(2, Join(_ben).Position);   // ana is still waiting in the reopened queue
    }

    [Fact]
    public void SetQueueStatus_CreatesQueue_AndValidates()
    {
        Assert.Equal("Open", _queue.GetQueueStatus("Dr. Cole"));     // no queue yet
        _queue.SetQueueStatus("Dr. Cole", open: false);
        Assert.Equal(1, _db.Scalar<int>("SELECT COUNT(*) FROM Queue WHERE Status = 'Closed'"));

        Assert.Throws<ApiValidationException>(() => _queue.SetQueueStatus(" ", true));
        Assert.Throws<KeyNotFoundException>(() => _queue.SetQueueStatus("Dr. Nobody", true));
    }

    // ---------- controller with the database version ----------

    [Fact]
    public void Controller_ReturnsRightStatusCodes()
    {
        var controller = new QueueController(_queue);
        var ok = new JoinQueueRequest { UserId = _ana.ToString(), Name = "Ana", Contact = "c", ServiceName = "Dr. Nate", Priority = 3 };

        Assert.IsType<OkObjectResult>(controller.JoinQueue(ok));
        Assert.IsType<ConflictObjectResult>(controller.JoinQueue(ok));
        Assert.IsType<BadRequestObjectResult>(controller.JoinQueue(new JoinQueueRequest
        {
            UserId = _ben.ToString(), Name = "Ben", Contact = "c", ServiceName = "Dr. Nobody", Priority = 3
        }));

        Assert.IsType<OkObjectResult>(controller.GetQueueState("Dr. Nate"));
        Assert.IsType<OkObjectResult>(controller.SetQueueState("Dr. Nate", false));
        Assert.IsType<NotFoundObjectResult>(controller.SetQueueState("Dr. Nobody", false));
    }

    [Fact]
    public void Controller_QueueState_WithInMemoryQueue()
    {
        var controller = new QueueController(new QueueService());
        Assert.IsType<OkObjectResult>(controller.GetQueueState("Dr. Nate"));
        Assert.IsType<BadRequestObjectResult>(controller.SetQueueState("Dr. Nate", false));
    }
}

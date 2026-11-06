using System;
using System.Collections.Generic;
using ClinicApi.Data;
using ClinicApi.Endpoints;
using ClinicApi.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ClinicApi.Tests;

/// <summary>A4: Service Management module stored in the Service table (SQLite test copy of the schema).</summary>
public class SqlServiceCatalogTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly ServiceCatalog _catalog;

    public SqlServiceCatalogTests() => _catalog = new ServiceCatalog(new SqlServiceRepository(_db));

    public void Dispose() => _db.Dispose();

    private static ServiceRequest Valid(string name = "Dr. Kim", int duration = 20, string priority = "Medium") =>
        new(name, "Medication review", duration, priority);

    private static int StatusOf(IResult result) => ((IStatusCodeHttpResult)result).StatusCode ?? 0;

    [Fact]
    public void List_StartsEmpty_WithoutSeedData()
    {
        Assert.Empty(_catalog.List());   // in the real database the seed rows come from schema.sql
    }

    [Fact]
    public void Create_SavesRow_AndReturnsNewId()
    {
        var created = _catalog.Create(Valid());

        Assert.True(created.Id > 0);
        Assert.Equal(1, _db.Scalar<int>("SELECT COUNT(*) FROM Service WHERE Name = 'Dr. Kim'"));
        Assert.Equal(20, _db.Scalar<int>("SELECT DurationMinutes FROM Service WHERE ServiceId = @Id", new { created.Id }));
    }

    [Fact]
    public void Create_TrimsAndNormalizesPriority()
    {
        var created = _catalog.Create(new ServiceRequest("  Dr. Kim  ", "  Review  ", 20, "high"));

        Assert.Equal("Dr. Kim", created.Name);
        Assert.Equal("Review", created.Description);
        Assert.Equal("High", _db.Scalar<string>("SELECT Priority FROM Service WHERE ServiceId = @Id", new { created.Id }));
    }

    [Fact]
    public void Services_PersistAcrossInstances_InOrder()
    {
        _catalog.Create(Valid("Dr. Kim"));
        _catalog.Create(Valid("Dr. Lee"));

        var fresh = new ServiceCatalog(new SqlServiceRepository(_db));
        var list = fresh.List();

        Assert.Equal(2, list.Count);
        Assert.Equal("Dr. Kim", list[0].Name);
        Assert.Equal("Dr. Lee", fresh.Get(list[1].Id).Name);
    }

    [Fact]
    public void Create_DuplicateName_IgnoringCase_IsRejected()
    {
        _catalog.Create(Valid("Dr. Kim"));
        Assert.Throws<InvalidOperationException>(() => _catalog.Create(Valid("dr. kim")));
        Assert.Equal(1, _db.Scalar<int>("SELECT COUNT(*) FROM Service"));
    }

    [Fact]
    public void Create_InvalidInput_SavesNothing()
    {
        Assert.Throws<ApiValidationException>(() => _catalog.Create(Valid(name: "")));
        Assert.Throws<ApiValidationException>(() => _catalog.Create(Valid(duration: 0)));
        Assert.Throws<ApiValidationException>(() => _catalog.Create(Valid(duration: 481)));
        Assert.Throws<ApiValidationException>(() => _catalog.Create(Valid(priority: "Urgent")));
        Assert.Throws<ApiValidationException>(() => _catalog.Create(Valid(name: new string('x', 101))));
        Assert.Equal(0, _db.Scalar<int>("SELECT COUNT(*) FROM Service"));
    }

    [Fact]
    public void Update_ChangesRow()
    {
        var created = _catalog.Create(Valid());

        var updated = _catalog.Update(created.Id, new ServiceRequest("Dr. Kim", "Therapy", 50, "Low"));

        Assert.Equal(50, updated.DurationMinutes);
        Assert.Equal("Therapy", _db.Scalar<string>("SELECT Description FROM Service WHERE ServiceId = @Id", new { created.Id }));
        Assert.Equal("Low", _catalog.Get(created.Id).Priority);
    }

    [Fact]
    public void Update_CanKeepOwnName_ButNotTakeAnother()
    {
        var kim = _catalog.Create(Valid("Dr. Kim"));
        _catalog.Create(Valid("Dr. Lee"));

        _catalog.Update(kim.Id, Valid("DR. KIM", duration: 25));   // same service, different case: fine
        Assert.Throws<InvalidOperationException>(() => _catalog.Update(kim.Id, Valid("Dr. Lee")));
    }

    [Fact]
    public void GetOrUpdate_Missing_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => _catalog.Get(9999));
        Assert.Throws<KeyNotFoundException>(() => _catalog.Update(9999, Valid()));
    }

    [Fact]
    public void Database_RejectsBadRows_EvenWithoutTheBackendChecks()
    {
        var repo = new SqlServiceRepository(_db);
        Assert.ThrowsAny<Exception>(() => repo.Add(new ClinicService(0, "Bad", "x", 0, "Medium")));      // duration CHECK
        Assert.ThrowsAny<Exception>(() => repo.Add(new ClinicService(0, "Bad", "x", 10, "Urgent")));     // priority CHECK
        repo.Add(new ClinicService(0, "Same", "x", 10, "Low"));
        Assert.ThrowsAny<Exception>(() => repo.Add(new ClinicService(0, "Same", "x", 10, "Low")));       // UNIQUE name
    }

    [Fact]
    public void Endpoints_UseDatabaseCatalog()
    {
        Assert.Equal(200, StatusOf(ServiceEndpointsTestHelper.Create(_catalog, Valid())));
        Assert.Equal(409, StatusOf(ServiceEndpointsTestHelper.Create(_catalog, Valid())));
        Assert.Single(_catalog.List());
    }
}

/// <summary>Calls the same code the POST /api/services endpoint runs.</summary>
internal static class ServiceEndpointsTestHelper
{
    public static IResult Create(ServiceCatalog catalog, ServiceRequest request) =>
        NotificationHistoryEndpoints.Handle(() => Results.Ok(catalog.Create(request)));
}

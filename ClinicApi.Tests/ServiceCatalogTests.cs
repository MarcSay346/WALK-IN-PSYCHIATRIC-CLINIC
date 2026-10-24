using ClinicApi.Services;
using Xunit;

namespace ClinicApi.Tests;

public class ServiceCatalogTests
{
    private readonly ServiceCatalog _catalog = new();

    private static ServiceRequest Valid(string name = "Dr. Kim") =>
        new(name, "Medication review", 20, "Medium");

    [Fact]
    public void List_StartsWithSeededServices()
    {
        Assert.Equal(3, _catalog.List().Count);
    }

    [Fact]
    public void Create_ValidService_IsAddedWithNewId()
    {
        var created = _catalog.Create(Valid());

        Assert.True(created.Id > 0);
        Assert.Equal("Dr. Kim", created.Name);
        Assert.Equal(20, created.DurationMinutes);
        Assert.Contains(_catalog.List(), s => s.Id == created.Id);
    }

    [Fact]
    public void Create_TrimsTextAndNormalizesPriority()
    {
        var created = _catalog.Create(new ServiceRequest("  Dr. Kim  ", "  Visit  ", 15, "high"));

        Assert.Equal("Dr. Kim", created.Name);
        Assert.Equal("Visit", created.Description);
        Assert.Equal("High", created.Priority);
    }

    [Fact]
    public void Create_MissingName_Throws()
    {
        var ex = Assert.Throws<ApiValidationException>(() =>
            _catalog.Create(new ServiceRequest("", "Visit", 20, "Low")));
        Assert.True(ex.Errors.ContainsKey("name"));
    }

    [Fact]
    public void Create_NameTooLong_Throws()
    {
        var ex = Assert.Throws<ApiValidationException>(() =>
            _catalog.Create(new ServiceRequest(new string('a', 101), "Visit", 20, "Low")));
        Assert.True(ex.Errors.ContainsKey("name"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(481)]
    public void Create_BadDuration_Throws(int? duration)
    {
        var ex = Assert.Throws<ApiValidationException>(() =>
            _catalog.Create(new ServiceRequest("Dr. Kim", "Visit", duration, "Low")));
        Assert.True(ex.Errors.ContainsKey("durationMinutes"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Urgent")]
    public void Create_BadPriority_Throws(string? priority)
    {
        var ex = Assert.Throws<ApiValidationException>(() =>
            _catalog.Create(new ServiceRequest("Dr. Kim", "Visit", 20, priority)));
        Assert.True(ex.Errors.ContainsKey("priority"));
    }

    [Fact]
    public void Create_NullBody_Throws()
    {
        Assert.Throws<ApiValidationException>(() => _catalog.Create(null));
    }

    [Fact]
    public void Create_DuplicateName_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _catalog.Create(new ServiceRequest("dr. nate", "Visit", 20, "Low")));
    }

    [Fact]
    public void Update_ChangesFields()
    {
        var first = _catalog.List()[0];

        var updated = _catalog.Update(first.Id, new ServiceRequest(first.Name, "Longer visit", 90, "Low"));

        Assert.Equal(90, updated.DurationMinutes);
        Assert.Equal("Low", _catalog.Get(first.Id).Priority);
    }

    [Fact]
    public void Update_UnknownId_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => _catalog.Update(999, Valid()));
    }

    [Fact]
    public void Update_ToAnotherServicesName_Throws()
    {
        var services = _catalog.List();
        Assert.Throws<InvalidOperationException>(() =>
            _catalog.Update(services[0].Id, new ServiceRequest(services[1].Name, "Visit", 20, "Low")));
    }

    [Fact]
    public void Get_UnknownId_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => _catalog.Get(999));
    }
}

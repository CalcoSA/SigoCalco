using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SigoCalco.Authentication.Test;

public partial class PointOfSaleAssignmentTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreatingUserPersistsTheExplicitStatusIncludingInactive(bool status)
    {
        await using var fixture = new Fixture();
        await fixture.Initialize();
        var request = Request(OtherRole);
        request.StatusUser = status;

        var result = Response<OkObjectResult>(await fixture.Controller.Create(request));

        Assert.Equal(status, Assert.IsType<UserAdministrationDto>(result.Result).StatusUser);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(status, (await fixture.Context.Users.SingleAsync()).StatusUser);
    }

    [Fact]
    public void MysqlStatusUserModelDoesNotEmitTheBooleanDefaultWarning()
    {
        using var context = new SigoCalcoContext(new DbContextOptionsBuilder<SigoCalcoContext>()
            .UseMySql("Server=localhost;Database=bdcalco_sigocalco;",
                new MySqlServerVersion(new Version(8, 0, 36)))
            .ConfigureWarnings(warnings => warnings.Throw(RelationalEventId.BoolWithDefaultWarning))
            .Options);
        // Inspect provider metadata without opening a connection.
        var property = context.Model.FindEntityType(typeof(User))!.FindProperty(nameof(User.StatusUser))!;
        Assert.False(property.IsNullable);
    }
}

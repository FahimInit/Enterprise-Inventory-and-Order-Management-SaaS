using Inventory.Application.Common;
using Inventory.Domain.Common;
using Inventory.Infrastructure.Tenancy;
using NetArchTest.Rules;

namespace Inventory.ArchitectureTests;

public sealed class CleanArchitectureDependencyTests
{
    private const string DomainNamespace = "Inventory.Domain";
    private const string ApplicationNamespace = "Inventory.Application";
    private const string InfrastructureNamespace = "Inventory.Infrastructure";
    private const string ApiNamespace = "Inventory.API";

    [Fact]
    public void Domain_Should_Not_Depend_On_Application_Infrastructure_Or_Api()
    {
        var domainAssembly = typeof(EntityBase).Assembly;

        var result = Types.InAssembly(domainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                ApplicationNamespace,
                InfrastructureNamespace,
                ApiNamespace,
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain layer must remain pure and independent of application, persistence, and web frameworks.");
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Api()
    {
        var applicationAssembly = typeof(ITenantContext).Assembly;

        var result = Types.InAssembly(applicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application layer must depend only on Domain and abstractions, never on Infrastructure or API.");
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Api()
    {
        var infrastructureAssembly = typeof(TenantContext).Assembly;

        var result = Types.InAssembly(infrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Infrastructure layer must not depend on the API composition root.");
    }
}

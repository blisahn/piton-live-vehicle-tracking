using NetArchTest.Rules;
using System.Reflection;
using VehicleTracking.Api.Controllers;
using VehicleTracking.Application.Ingestion;
using VehicleTracking.Domain.Vehicles;
using VehicleTracking.Infrastructure.Spool;

namespace VehicleTracking.ArchitectureTests;

public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(VehicleKey).Assembly;
    private static readonly Assembly Application = typeof(SnapshotNormalizer).Assembly;
    private static readonly Assembly Infrastructure = typeof(SpoolOptions).Assembly;
    private static readonly Assembly Api = typeof(VehiclesController).Assembly;

    private const string ApplicationNs = "VehicleTracking.Application";
    private const string InfrastructureNs = "VehicleTracking.Infrastructure";
    private const string ApiNs = "VehicleTracking.Api";

    [Fact]
    public void Domain_does_not_depend_on_application_infrastructure_or_api() =>
        AssertNoViolations(Types.InAssembly(Domain).ShouldNot()
            .HaveDependencyOnAny(ApplicationNs, InfrastructureNs, ApiNs));

    [Fact]
    public void Application_does_not_depend_on_infrastructure_or_api() =>
        AssertNoViolations(Types.InAssembly(Application).ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, ApiNs));

    [Fact]
    public void Application_does_not_depend_on_aspnet_core_or_identity() =>
        AssertNoViolations(Types.InAssembly(Application).ShouldNot().HaveDependencyOnAny(
            "Microsoft.AspNetCore", "Microsoft.IdentityModel", "System.IdentityModel"));

    [Fact]
    public void Infrastructure_does_not_depend_on_api() =>
        AssertNoViolations(Types.InAssembly(Infrastructure).ShouldNot().HaveDependencyOn(ApiNs));

    [Fact]
    public void Controllers_do_not_depend_on_infrastructure_or_entity_framework() =>
        AssertNoViolations(Types.InAssembly(Api).That()
            .ResideInNamespace("VehicleTracking.Api.Controllers").ShouldNot()
            .HaveDependencyOnAny(InfrastructureNs, "Microsoft.EntityFrameworkCore"));

    private static void AssertNoViolations(ConditionList rule)
    {
        var result = rule.GetResult();
        var violators = string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>());
        Assert.True(result.IsSuccessful, "Violating types: " + violators);
    }
}

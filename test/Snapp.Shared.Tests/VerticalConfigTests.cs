using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using FluentAssertions;
using Snapp.Shared.Configuration;
using Xunit;

namespace Snapp.Shared.Tests;

public class VerticalConfigTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string GetTestDataPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "TestData", fileName);
    }

    [Fact]
    public void DentalConfig_Deserializes_Successfully()
    {
        var json = File.ReadAllText(GetTestDataPath("dental.json"));
        var config = JsonSerializer.Deserialize<VerticalConfig>(json, JsonOptions);

        config.Should().NotBeNull();
        config!.Vertical.Should().Be("dental");
        config.DisplayName.Should().Be("Dental Practice");
    }

    [Fact]
    public void DentalConfig_Has_Seven_Dimensions()
    {
        var config = LoadDentalConfig();

        config.Dimensions.Should().HaveCount(7);
        config.Dimensions.Select(d => d.Name).Should().BeEquivalentTo(
            "FinancialHealth", "OwnerRisk", "Operations",
            "ClientBase", "RevenueDiversification", "MarketPosition",
            "Workforce");
    }

    [Fact]
    public void DentalConfig_Dimension_Weights_Sum_To_One()
    {
        var config = LoadDentalConfig();

        var totalWeight = config.Dimensions.Sum(d => d.Weight);
        totalWeight.Should().Be(1.0m);
    }

    [Fact]
    public void DentalConfig_Each_Dimension_Has_Thresholds()
    {
        var config = LoadDentalConfig();

        // Inverse-scored dimensions where lower values are better (e.g., workforce pressure)
        var inverseDimensions = new HashSet<string> { "Workforce" };

        foreach (var dim in config.Dimensions)
        {
            dim.Thresholds.Should().NotBeNull($"dimension {dim.Name} should have thresholds");

            if (inverseDimensions.Contains(dim.Name))
            {
                dim.Thresholds.Strong.Should().BeLessThan(dim.Thresholds.Acceptable,
                    $"strong < acceptable for inverse-scored {dim.Name}");
                dim.Thresholds.Acceptable.Should().BeLessThan(dim.Thresholds.Weak,
                    $"acceptable < weak for inverse-scored {dim.Name}");
            }
            else
            {
                dim.Thresholds.Strong.Should().BeGreaterThan(dim.Thresholds.Acceptable,
                    $"strong > acceptable for {dim.Name}");
                dim.Thresholds.Acceptable.Should().BeGreaterThan(dim.Thresholds.Weak,
                    $"acceptable > weak for {dim.Name}");
            }
        }
    }

    [Fact]
    public void DentalConfig_Each_Dimension_Has_Kpis()
    {
        var config = LoadDentalConfig();

        foreach (var dim in config.Dimensions)
        {
            dim.Kpis.Should().NotBeEmpty($"dimension {dim.Name} should have KPIs");
        }
    }

    [Fact]
    public void DentalConfig_Has_Nine_ContributionCategories()
    {
        var config = LoadDentalConfig();

        config.ContributionCategories.Should().HaveCount(9);
        config.ContributionCategories.Select(c => c.Category).Should().BeEquivalentTo(
            "Revenue", "StaffCount", "Utilization", "ClientVolume",
            "RevenueMix", "OwnerProduction", "EquipmentAge", "FacilityType",
            "WorkforceSignals");
    }

    [Fact]
    public void DentalConfig_Each_ContributionCategory_Has_Fields()
    {
        var config = LoadDentalConfig();

        foreach (var cat in config.ContributionCategories)
        {
            cat.Fields.Should().NotBeEmpty($"category {cat.Category} should have fields");
            foreach (var field in cat.Fields)
            {
                field.Name.Should().NotBeNullOrEmpty();
                field.Label.Should().NotBeNullOrEmpty();
                field.Type.Should().NotBeNullOrEmpty();
            }
        }
    }

    [Fact]
    public void DentalConfig_Has_Six_CareerStageRules()
    {
        var config = LoadDentalConfig();

        config.CareerStageRules.Should().HaveCount(6);
        config.CareerStageRules.Select(r => r.Stage).Should().BeEquivalentTo(
            "PreExit", "Mature", "Growth", "AcquisitionLaunch", "Associate", "TrainingEntry");
    }

    [Fact]
    public void DentalConfig_CareerStageRules_Have_Signals_And_RiskFlags()
    {
        var config = LoadDentalConfig();

        foreach (var rule in config.CareerStageRules)
        {
            rule.Signals.Should().NotBeEmpty($"stage {rule.Stage} should have signals");
            rule.RiskFlags.Should().NotBeEmpty($"stage {rule.Stage} should have risk flags");
        }
    }

    [Fact]
    public void DentalConfig_Has_BenchmarkMetrics()
    {
        var config = LoadDentalConfig();

        config.BenchmarkMetrics.Should().NotBeEmpty();
        foreach (var metric in config.BenchmarkMetrics)
        {
            metric.Name.Should().NotBeNullOrEmpty();
            metric.GeographicLevels.Should().NotBeEmpty(
                $"benchmark {metric.Name} should have geographic levels");
        }
    }

    [Fact]
    public void DentalConfig_Has_ValuationDrivers()
    {
        var config = LoadDentalConfig();

        config.ValuationDrivers.Should().NotBeEmpty();
        foreach (var driver in config.ValuationDrivers)
        {
            driver.Name.Should().NotBeNullOrEmpty();
            driver.ImpactWeight.Should().NotBe(0m,
                $"driver {driver.Name} should have non-zero impact weight");
        }
    }

    [Fact]
    public void DentalConfig_Has_RegistrySource_With_TaxonomyCodes()
    {
        var config = LoadDentalConfig();

        config.RegistrySource.Should().NotBeNull();
        config.RegistrySource.Type.Should().Be("NPI_NPPES");
        config.RegistrySource.TaxonomyCodes.Should().Contain("122300000X");
        config.RegistrySource.TaxonomyCodes.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void DentalConfig_Has_RoleDefinitions()
    {
        var config = LoadDentalConfig();

        config.RoleDefinitions.Should().NotBeEmpty();
        config.RoleDefinitions.Select(r => r.Role).Should().Contain("hygienist");
        config.RoleDefinitions.Select(r => r.Role).Should().Contain("assistant");
        config.RoleDefinitions.Select(r => r.Role).Should().Contain("front_desk");
        config.RoleDefinitions.Select(r => r.Role).Should().Contain("office_manager");

        config.RoleDefinitions.Should().Contain(r => r.IsProvider && r.IsRequired,
            "at least one role should be a required provider");
    }

    [Fact]
    public void DentalConfig_Passes_DataAnnotation_Validation()
    {
        var config = LoadDentalConfig();

        var results = new List<ValidationResult>();
        var context = new ValidationContext(config);
        var isValid = Validator.TryValidateObject(config, context, results, validateAllProperties: true);

        isValid.Should().BeTrue(
            $"config should pass validation but got: {string.Join("; ", results.Select(r => r.ErrorMessage))}");
    }

    [Fact]
    public void MissingRequiredFields_Fails_Validation()
    {
        var config = new VerticalConfig(); // all defaults, missing required fields

        var results = new List<ValidationResult>();
        var context = new ValidationContext(config);
        var isValid = Validator.TryValidateObject(config, context, results, validateAllProperties: true);

        isValid.Should().BeFalse("empty config should fail validation");
        results.Should().NotBeEmpty();
    }

    [Fact]
    public void InvalidConfidence_Fails_Validation()
    {
        var config = LoadDentalConfig();
        config.BaseConfidence = 150m; // out of range

        var results = new List<ValidationResult>();
        var context = new ValidationContext(config);
        var isValid = Validator.TryValidateObject(config, context, results, validateAllProperties: true);

        isValid.Should().BeFalse("confidence > 100 should fail range validation");
    }

    [Fact]
    public void SchemaFile_Exists_And_Is_Valid_Json()
    {
        var schemaPath = GetTestDataPath("schema.json");
        File.Exists(schemaPath).Should().BeTrue("schema.json should exist");

        var json = File.ReadAllText(schemaPath);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("$schema").GetString()
            .Should().Contain("json-schema.org");
        doc.RootElement.GetProperty("required").GetArrayLength()
            .Should().BeGreaterThan(0);
    }

    [Fact]
    public void SchemaFile_Requires_All_Top_Level_Fields()
    {
        var schemaPath = GetTestDataPath("schema.json");
        var json = File.ReadAllText(schemaPath);
        var doc = JsonDocument.Parse(json);

        var required = doc.RootElement.GetProperty("required")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        required.Should().Contain("vertical");
        required.Should().Contain("displayName");
        required.Should().Contain("dimensions");
        required.Should().Contain("contributionCategories");
        required.Should().Contain("careerStageRules");
        required.Should().Contain("benchmarkMetrics");
        required.Should().Contain("valuationDrivers");
        required.Should().Contain("registrySource");
        required.Should().Contain("roleDefinitions");
    }

    private static VerticalConfig LoadDentalConfig()
    {
        var json = File.ReadAllText(GetTestDataPath("dental.json"));
        return JsonSerializer.Deserialize<VerticalConfig>(json, JsonOptions)!;
    }
}

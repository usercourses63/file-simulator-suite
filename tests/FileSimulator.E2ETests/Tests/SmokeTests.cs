using FileSimulator.E2ETests.Fixtures;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace FileSimulator.E2ETests.Tests;

[Collection("Simulator")]
public class SmokeTests
{
    private readonly SimulatorTestFixture _fixture;

    public SmokeTests(SimulatorTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Dashboard_Loads_Successfully()
    {
        var page = await _fixture.Context.NewPageAsync();
        await page.GotoAsync(_fixture.DashboardUrl);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var title = await page.TitleAsync();
        title.Should().Contain("File Simulator");

        await page.CloseAsync();
    }

    [Fact]
    public async Task Api_Health_Returns_Success()
    {
        using var client = new HttpClient();
        var response = await client.GetAsync($"{_fixture.ApiUrl}/api/health");
        response.IsSuccessStatusCode.Should().BeTrue();
    }
}

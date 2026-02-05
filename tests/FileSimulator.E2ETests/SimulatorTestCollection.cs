using FileSimulator.E2ETests.Fixtures;
using Xunit;

namespace FileSimulator.E2ETests;

[CollectionDefinition("Simulator")]
public class SimulatorTestCollection : ICollectionFixture<SimulatorTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

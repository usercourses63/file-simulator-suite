using Xunit;

namespace FileSimulator.IntegrationTests.Fixtures;

/// <summary>
/// Collection definition for tests that share the SimulatorCollectionFixture.
/// Tests marked with [Collection("Simulator")] will share a single fixture instance.
/// </summary>
[CollectionDefinition("Simulator")]
public class SimulatorCollection : ICollectionFixture<SimulatorCollectionFixture>
{
    // This class has no code, it is never instantiated.
    // Its purpose is to be the marker that applies [CollectionDefinition]
    // and associates the SimulatorCollectionFixture with the "Simulator" collection name.
}

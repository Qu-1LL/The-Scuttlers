namespace TriloGame.Tests.Performance;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceBenchmarkCollection
{
    public const string Name = "PerformanceBenchmarks";
}

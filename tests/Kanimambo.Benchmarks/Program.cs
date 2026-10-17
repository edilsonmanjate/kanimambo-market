using BenchmarkDotNet.Running;
using Kanimambo.Benchmarks.Flyweight;

// dotnet run -c Release --project tests/Kanimambo.Benchmarks -- --filter *Flyweight*
// dotnet run -c Release --project tests/Kanimambo.Benchmarks -- --retained
if (args.Contains("--retained"))
{
    RetainedMemory.Report(100_000);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(FlyweightBenchmarks).Assembly).Run(args);

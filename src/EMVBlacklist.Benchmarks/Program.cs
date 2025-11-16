using BenchmarkDotNet.Running;
using EMVBlacklist.Benchmarks;

Console.WriteLine("===========================================");
Console.WriteLine("EMV Blacklist Filter Benchmarks");
Console.WriteLine("===========================================\n");

// Run detailed comparison first
var comparison = new FilterComparison();
await comparison.RunComparison();

Console.WriteLine("\n\n===========================================");
Console.WriteLine("Running BenchmarkDotNet benchmarks...");
Console.WriteLine("===========================================\n");

// Run BenchmarkDotNet benchmarks
var summary = BenchmarkRunner.Run<FilterBenchmark>();

Console.WriteLine("\nBenchmarks complete!");

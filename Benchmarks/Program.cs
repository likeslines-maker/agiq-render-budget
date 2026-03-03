using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using BenchmarkDotNet.Columns;

using RB = global::AgiqRenderBudget;

namespace RenderBudgetBenchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<RenderBudgetBench>();
    }
}

public sealed class BenchConfig : ManualConfig
{
    public BenchConfig()
    {
        AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
        AddLogger(ConsoleLogger.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(HtmlExporter.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);
    }
}

[Config(typeof(BenchConfig))]
[MemoryDiagnoser]
public class RenderBudgetBench
{
    private RB.AgiqRenderBudgetOptimizer _opt = null!;
    private RB.RenderBudgetContext _ctx = null!;
    private RB.RenderBudgetState _state = null!;
    private List<RB.RenderItem> _items = null!;

    private RB.BudgetSettings _settingsGreedyOnly;
    private RB.BudgetSettings _settingsImprove;

    // "sink" to prevent dead-code elimination
    private float _sink;

    // shared options:do not allocate per item
    private static readonly RB.QualityOption[] SharedOptions =
    {
 new(0.005f,0.20f),
 new(0.012f,0.45f),
 new(0.025f,0.75f),
 new(0.045f,1.00f),
 };

    private static readonly RB.GroupBudget[] SharedGroups =
    {
 new(GroupId:1,BudgetMs:0.45f),
 new(GroupId:2,BudgetMs:0.35f),
 new(GroupId:3,BudgetMs:0.55f),
 };

    [Params(1000, 5000, 10000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        _opt = new RB.AgiqRenderBudgetOptimizer(seed: 123);
        _state = new RB.RenderBudgetState();

        _items = GenerateScene(ItemCount, seed: 123);

        _ctx = _opt.Prepare(_items, SharedGroups);

        // Greedy-only:no improve loop,no time checks
        _settingsGreedyOnly = RB.PlatformProfile.PC60.ToSettings(seed: 123, improveCap: 0, randomMoves: 0)
        with
        { TimeLimitMs = 0f };

        // Improve enabled:your typical settings
        _settingsImprove = RB.PlatformProfile.PC60.ToSettings(seed: 123, improveCap: 2500, randomMoves: 2);

        // Warm-up once to allocate state buffers
        _sink = _opt.OptimizeFrame(_ctx, _settingsImprove, _state).TotalScore;
    }

    [Benchmark(Description = "OptimizeFrame (greedy only)")]
    public void OptimizeFrame_GreedyOnly()
    {
        _sink = _opt.OptimizeFrame(_ctx, _settingsGreedyOnly, _state).TotalScore;
    }

    [Benchmark(Description = "OptimizeFrame (greedy + improve)")]
    public void OptimizeFrame_Improve()
    {
        _sink = _opt.OptimizeFrame(_ctx, _settingsImprove, _state).TotalScore;
    }

    private static List<RB.RenderItem> GenerateScene(int n, int seed)
    {
        var rng = new Random(seed);
        var items = new List<RB.RenderItem>(n);

        for (int i = 0; i < n; i++)
        {
            float imp = (float)(rng.NextDouble() * rng.NextDouble() * 10.0 + 0.2);

            int g = rng.Next(0, 10);
            int groupId = g switch
            {
                0 or 1 => 1,// shadows
                2 => 2,// vfx
                3 or 4 => 3,// enemies
                _ => -1
            };

            int pinned = -1;
            int min = 0;
            if (groupId == 3 && rng.NextDouble() < 0.05) min = 2; // some enemies never below high
            if (groupId == 1 && rng.NextDouble() < 0.02) pinned = 1; // some shadows pinned

            items.Add(new RB.RenderItem
            {
                Id = i,
                Importance = imp,
                GroupId = groupId,
                MinLevel = min,
                PinnedLevel = pinned,
                Options = SharedOptions,// IMPORTANT:shared array
                QualityExponent = 1.0f
            });
        }

        return items;
    }
}

AgiqRenderBudget

Smart real-time rendering budget allocation for games:stable FPS + better visuals where it matters.

Key idea: instead of local heuristics (distance-only LOD),allocate frame budget globally across thousands of objects using a fast budget optimizer with:
importance-aware scoring (gameplay > background)
group budgets (shadows / VFX / enemies / UI,etc.)
smoothing & hysteresis (no flicker / no level thrashing)
zero allocations per frame (after warm-up)

This library is a practical “global optimization” brick designed for per-frame or every-N-frames use.


---

The Problem

Every game hits the same wall:

You have 16.67 ms per frame (60 FPS)
You have 10,000+ renderables
You have limited budgets:polygons,shading,textures,shadows,post FX…

Question: How do you spend the budget so the player sees the most important content at the highest quality?

Typical solutions:
Distance-based LOD (ignores gameplay importance)
Global quality presets (all-or-nothing)
Manual tuning (doesn’t scale)
Static rules (break on dynamic camera/gameplay)

Result:unstable FPS,visual “mush”,and endless performance firefighting.

---

What AgiqRenderBudget Does

AgiqRenderBudget selects a quality level for each object (LOD / shadow quality / texture mip / shader variant) under:
a global time budget (ms)
optional group budgets (e.g. shadows <= 0.45ms)
per-item constraints (min level,pinned level)
smoothing across frames (penalty for changing levels + hold frames)

It runs fast enough to be used frequently (per-frame or every few frames) without GC spikes.

---

Benchmarks

CPU: Intel Core i5-11400F
Runtime: .NET 8,BenchmarkDotNet (InProcess)
Allocator: 0 B per call (after warm-up)

| Objects | Time (Mean) | Allocations |
|--------:|------------:|------------:|
| 1,000 | ~6.37 µs | 0 B |
| 5,000 | ~31.87 µs | 0 B |
| 10,000 | ~83.62 µs | 0 B |

10,000 objects in ~0.083 ms is roughly 0.5% of a 60 FPS frame.

---

Install

NuGet
```bash
dotnet add package AgiqRenderBudget
```

---

Quick Start

1) Create optimizer + state (once)
```csharp
using AgiqRenderBudget;

var optimizer = new AgiqRenderBudgetOptimizer(seed:123);
var state = new RenderBudgetState();
```

2) Build items + prepare context (on scene load / when options change)
```csharp
var items = new List<RenderItem>();

foreach (var obj in scene.Objects)
{
 items.Add(new RenderItem
 {
 Id = obj.Id,
 Importance = obj.Importance,// your metric:gameplay,distance,screen size,visibility...
 GroupId = obj.GroupId,// e.g. 1=Shadows,2=VFX,3=Enemies
 MinLevel = obj.MinLevel,// never go below
 PinnedLevel = obj.IsBoss ? 3 :-1,// boss always high
 Options = new[]
 {
 new QualityOption(0.005f,0.20f),
 new QualityOption(0.012f,0.45f),
 new QualityOption(0.025f,0.75f),
 new QualityOption(0.045f,1.00f),
 }
 });
}

var groups = new[]
{
 new GroupBudget(1,0.45f),// shadows budget (ms)
 new GroupBudget(2,0.35f),// vfx budget
 new GroupBudget(3,0.55f),// enemies budget
};

var ctx = optimizer.Prepare(items,groups);
```

3) Optimize each frame (or every N frames)
```csharp
var settings = PlatformProfile.PC60.ToSettings(
 seed:123,
 improveCap:2500,
 randomMoves:2
);

var allocation = optimizer.OptimizeFrame(ctx,settings,state);

for (int i = 0; i < allocation.Levels.Length; i++)
{
 scene.Objects[i].SetQualityLevel(allocation.Levels[i]);
}
```

---

API Overview

RenderItem
Importance:how valuable quality is for this item (gameplay first)
Options[]:discrete quality options (cost,quality)
GroupId:optional group id for group budgets
MinLevel:hard minimum level
PinnedLevel:fixed level (e.g. boss/UI)

PlatformProfile
Presets:
PlatformProfile.PC60
PlatformProfile.Console60
PlatformProfile.Mobile60

BudgetSettings
BudgetMs:total global budget
TimeLimitMs:optional CPU time budget for improvement loop
ImproveIterationsCap:improvement iterations
ChangePenalty,MaxStepPerFrame,MinHoldFrames:smoothing

---

Licensing & Pricing (Commercial)

This is commercial software.

Individual (1 developer):$100 / year
Studio / Large developer:$500 / year

Contact:support@agiq.tech

See LICENSE.txt for terms.

---

FAQ

Q:Is this “optimal”?
A:It’s a fast global budget optimizer designed for real-time use. It aims for high-quality allocations under hard budgets and constraints,and typically outperforms simple local heuristics.

Q:Does it allocate memory each frame?
A:No. After warm-up,per-frame calls are designed to be zero-allocation.

Q:Unity/IL2CPP?
A:The package targets netstandard2.1 + net8.0. Unity support depends on your Unity version and scripting runtime.

---

Support

Email:vipvodu@yandex.ru
Telegram: @vivpodu

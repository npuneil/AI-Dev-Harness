using System.Collections.Generic;

namespace LocalAiDemos.Shared.AI;

/// <summary>
/// Default Foundry Local model aliases per detected silicon. Override in
/// <see cref="Settings.AppSettings"/> if the user has cached a different model.
/// Aliases match the Foundry Model Catalog naming (the SDK resolves them
/// automatically to the right hardware-optimised variant).
/// </summary>
public static class ModelCatalog
{
    public const string DefaultSmallAlias = "phi-4-mini";
    public const string DefaultCoderAlias = "qwen2.5-coder-1.5b";

    private static readonly Dictionary<Silicon, string> _smallBySilicon = new()
    {
        [Silicon.IntelCoreUltra] = "phi-4-mini",
        [Silicon.QualcommSnapdragonX] = "qwen2.5-7b",
        [Silicon.AmdRyzenAi] = "phi-4-mini",
        [Silicon.IntelGeneric] = "phi-4-mini",
        [Silicon.AmdGeneric] = "phi-4-mini",
        [Silicon.Unknown] = "phi-4-mini",
    };

    private static readonly Dictionary<Silicon, string> _coderBySilicon = new()
    {
        [Silicon.IntelCoreUltra] = "qwen2.5-coder-1.5b",
        [Silicon.QualcommSnapdragonX] = "qwen2.5-coder-1.5b",
        [Silicon.AmdRyzenAi] = "qwen2.5-coder-1.5b",
        [Silicon.IntelGeneric] = "qwen2.5-coder-1.5b",
        [Silicon.AmdGeneric] = "qwen2.5-coder-1.5b",
        [Silicon.Unknown] = "qwen2.5-coder-0.5b",
    };

    public static string DefaultSmallFor(Silicon silicon) =>
        _smallBySilicon.TryGetValue(silicon, out var alias) ? alias : DefaultSmallAlias;

    public static string DefaultCoderFor(Silicon silicon) =>
        _coderBySilicon.TryGetValue(silicon, out var alias) ? alias : DefaultCoderAlias;
}

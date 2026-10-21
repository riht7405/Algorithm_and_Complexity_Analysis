using FractalPlatform.Domain.Abstractions;

namespace FractalPlatform.Application.Registry;

public class FractalRegistry : IFractalRegistry
{
    private readonly Dictionary<string, IFractal> _map;

    public FractalRegistry(IEnumerable<IFractal> fractals)
    {
        _map = fractals.ToDictionary(f => f.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IFractal> All => _map.Values.ToList();

    public IFractal? Get(string id) => _map.GetValueOrDefault(id);

    public bool TryGet(string id, out IFractal fractal)
    {
        var found = _map.TryGetValue(id, out var f);
        fractal = f!;
        return found;
    }
}
using FractalPlatform.Domain.Abstractions;

namespace FractalPlatform.Application.Registry;

public interface IFractalRegistry
{
    IReadOnlyList<IFractal> All { get; }
    IFractal? Get(string id);
    bool TryGet(string id, out IFractal fractal);
}
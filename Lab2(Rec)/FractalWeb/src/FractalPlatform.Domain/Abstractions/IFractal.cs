namespace FractalPlatform.Domain.Abstractions;

public interface IFractal
{
    string Id { get; }
    FractalMetadata Metadata { get; }
    Task<FractalResult> ComputeAsync(FractalRequest request, CancellationToken ct = default);
}
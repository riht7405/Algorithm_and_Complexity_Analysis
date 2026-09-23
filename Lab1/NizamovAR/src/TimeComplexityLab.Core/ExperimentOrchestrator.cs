using System.Diagnostics;
using TimeComplexityLab.Domain;

namespace TimeComplexityLab.Core;

public class ExperimentOrchestrator
{
    private readonly IExperimentRepository _repository;

    public ExperimentOrchestrator(IExperimentRepository repository)
    {
        _repository = repository;
    }

    public async Task<ExperimentSession> RunExperimentAsync(
        IAlgorithm algorithm,
        int nMax, int nStep,
        int mMax, int mStep,
        int runsCount,
        bool forceRecalc = false)
    {
        await _repository.InitializeAsync();

        var session = new ExperimentSession
        {
            AlgorithmId = algorithm.Id,
            DisplayName = algorithm.DisplayName,
            Dimension = algorithm.Dimension,
            MeasureUnit = algorithm.MeasureUnit,
            NMax = nMax,
            Step = nStep,
            MMax = algorithm.Dimension == ExperimentDimension.TwoDimensional ? mMax : 0,
            MStep = algorithm.Dimension == ExperimentDimension.TwoDimensional ? mStep : 0,
            RunsPerPoint = runsCount
        };

        await _repository.SaveSessionAsync(session);

        var maxPoint = new ExperimentPoint(
            nMax,
            algorithm.Dimension == ExperimentDimension.TwoDimensional ? mMax : null);

        var sourceInput = await Task.Run(() =>
            algorithm.GenerateSource(maxPoint, new Random(42)));

        if (algorithm.Dimension == ExperimentDimension.OneDimensional)
        {
            for (int n = nStep; n <= nMax; n += nStep)
            {
                await RunPointAsync(algorithm, session, sourceInput,
                    new ExperimentPoint(n), runsCount, forceRecalc);
            }
        }
        else
        {
            for (int n = nStep; n <= nMax; n += nStep)
            {
                for (int m = mStep; m <= mMax; m += mStep)
                {
                    await RunPointAsync(algorithm, session, sourceInput,
                        new ExperimentPoint(n, m), runsCount, forceRecalc);
                }
            }
        }

        return session;
    }

    private async Task RunPointAsync(
        IAlgorithm algorithm,
        ExperimentSession session,
        object sourceInput,
        ExperimentPoint point,
        int runsCount,
        bool forceRecalc)
    {
        // === Попадание в кэш ===
        if (!forceRecalc)
        {
            var cached = await _repository.GetCachedMeasurementsAsync(
                algorithm.Id, point.N, point.M ?? 0, runsCount);

            if (cached.Count == runsCount)
            {
                // Создаём НОВЫЕ измерения, привязанные к ТЕКУЩЕЙ сессии
                var copied = cached.Select(c => new Measurement
                {
                    SessionId = session.Id,   // ← важно: привязка к текущей сессии
                    N = point.N,
                    M = point.M ?? 0,
                    RunIndex = c.RunIndex,
                    ElapsedMilliseconds = c.ElapsedMilliseconds,
                    Steps = c.Steps
                }).ToList();

                foreach (var m in copied)
                    session.Measurements.Add(m);

                // ← ГЛАВНОЕ: сохраняем в БД, чтобы сессия не осталась пустой
                await _repository.SaveMeasurementsAsync(copied);

                return;
            }
        }

        // === Реальные замеры ===
        var newMeasurements = new List<Measurement>();

        await Task.Run(() =>
        {
            // Warm-up
            for (int w = 0; w < 10; w++)
            {
                var slice = algorithm.Slice(sourceInput, point);
                var input = algorithm.Clone(slice);
                algorithm.Execute(input, new ExecutionTracker());
            }

            // Замеры
            for (int run = 1; run <= runsCount; run++)
            {
                var slice = algorithm.Slice(sourceInput, point);
                var input = algorithm.Clone(slice);
                var tracker = new ExecutionTracker();

                var sw = Stopwatch.StartNew();
                algorithm.Execute(input, tracker);
                sw.Stop();

                newMeasurements.Add(new Measurement
                {
                    SessionId = session.Id,
                    N = point.N,
                    M = point.M ?? 0,
                    RunIndex = run,
                    ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                    Steps = tracker.Steps
                });
            }
        });

        foreach (var m in newMeasurements)
            session.Measurements.Add(m);

        await _repository.SaveMeasurementsAsync(newMeasurements);

        if (point.N >= 5_000_000)
        {
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
        }
    }
}
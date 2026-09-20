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
        int nMax,
        int step,
        int runsCount,
        bool forceRecalc = false)
    {
        await _repository.InitializeAsync();

        var session = new ExperimentSession
        {
            AlgorithmId = algorithm.Id,
            DisplayName = algorithm.DisplayName,
            NMax = nMax,
            Step = step,
            RunsPerPoint = runsCount
        };

        await _repository.SaveSessionAsync(session);

        for (int n = step; n <= nMax; n += step)
        {
            // 1. Проверка кэша
            if (!forceRecalc)
            {
                var cached = await _repository.GetCachedMeasurementsAsync(
                    algorithm.Id, n, runsCount);

                if (cached.Count == runsCount)
                {
                    foreach (var c in cached)
                    {
                        session.Measurements.Add(new Measurement
                        {
                            N = n,
                            RunIndex = c.RunIndex,
                            ElapsedMilliseconds = c.ElapsedMilliseconds
                        });
                    }
                    continue;
                }
            }

            // 2. Warm-up на фиксированном seed — вне зачётных запусков
            var warmupInput = algorithm.GenerateInput(n, new Random(42));
            algorithm.Execute(warmupInput);

            // 3. Основные замеры — тот же seed, тот же вектор
            var newMeasurements = new List<Measurement>();

            for (int run = 1; run <= runsCount; run++)
            {
                var input = algorithm.GenerateInput(n, new Random(42));

                var sw = Stopwatch.StartNew();
                algorithm.Execute(input);
                sw.Stop();

                var m = new Measurement
                {
                    SessionId = session.Id,
                    N = n,
                    RunIndex = run,
                    ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds
                };

                newMeasurements.Add(m);
                session.Measurements.Add(m);
            }

            await _repository.SaveMeasurementsAsync(newMeasurements);
        }

        return session;
    }
}
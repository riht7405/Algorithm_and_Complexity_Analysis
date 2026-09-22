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

        if (algorithm.Dimension == ExperimentDimension.OneDimensional)
        {
            for (int n = nStep; n <= nMax; n += nStep)
            {
                await RunPointAsync(algorithm, session, new ExperimentPoint(n), runsCount, forceRecalc);
            }
        }
        else
        {
            for (int n = nStep; n <= nMax; n += nStep)
            {
                for (int m = mStep; m <= mMax; m += mStep)
                {
                    await RunPointAsync(algorithm, session, new ExperimentPoint(n, m), runsCount, forceRecalc);
                }
            }
        }

        return session;
    }

    private async Task RunPointAsync(
        IAlgorithm algorithm,
        ExperimentSession session,
        ExperimentPoint point,
        int runsCount,
        bool forceRecalc)
    {
        // Проверка кэша
        if (!forceRecalc)
        {
            var cached = await _repository.GetCachedMeasurementsAsync(
                algorithm.Id, point.N, point.M ?? 0, runsCount);

            if (cached.Count == runsCount)
            {
                foreach (var c in cached)
                {
                    session.Measurements.Add(new Measurement
                    {
                        N = point.N,
                        M = point.M ?? 0,
                        RunIndex = c.RunIndex,
                        ElapsedMilliseconds = c.ElapsedMilliseconds,
                        Steps = c.Steps
                    });
                }
                return;
            }
        }

        // Усиленный warm-up: 10 прогонов вместо 1,
        // чтобы JIT успел скомпилировать все ветки
        for (int w = 0; w < 10; w++)
        {
            var warmupInput = algorithm.GenerateInput(point, new Random(42));
            algorithm.Execute(warmupInput, new ExecutionTracker());
        }

        // Основные замеры — тот же seed, тот же вход
        var newMeasurements = new List<Measurement>();

        for (int run = 1; run <= runsCount; run++)
        {
            var tracker = new ExecutionTracker();
            var input = algorithm.GenerateInput(point, new Random(42));

            var sw = Stopwatch.StartNew();
            algorithm.Execute(input, tracker);
            sw.Stop();

            var m = new Measurement
            {
                SessionId = session.Id,
                N = point.N,
                M = point.M ?? 0,
                RunIndex = run,
                ElapsedMilliseconds = sw.Elapsed.TotalMilliseconds,
                Steps = tracker.Steps
            };

            newMeasurements.Add(m);
            session.Measurements.Add(m);
        }

        await _repository.SaveMeasurementsAsync(newMeasurements);
    }
}
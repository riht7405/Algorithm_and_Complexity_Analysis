using System.Windows;
using System.Windows.Controls;
using TimeComplexityLab.Core;
using TimeComplexityLab.Core.Approximation;
using TimeComplexityLab.Domain;
using TimeComplexityLab.Infrastructure;
using TimeComplexityLab.Presentation.Wpf.Services;

namespace TimeComplexityLab.Presentation.Wpf.Views;

public partial class AutoRunView : UserControl
{
    private readonly ExperimentOrchestrator _orchestrator;
    private readonly ApproximationService _approximationService = new();
    private CancellationTokenSource? _cts;

    private static readonly ScottPlot.Color[] Palette = new[]
    {
        ScottPlot.Color.FromHex("#0D6EFD"),
        ScottPlot.Color.FromHex("#198754"),
        ScottPlot.Color.FromHex("#DC3545"),
        ScottPlot.Color.FromHex("#6F42C1"),
        ScottPlot.Color.FromHex("#FD7E14"),
        ScottPlot.Color.FromHex("#20C997"),
        ScottPlot.Color.FromHex("#D63384"),
        ScottPlot.Color.FromHex("#0DCAF0"),
    };

    private static readonly ScottPlot.Color ApproximationColor =
        ScottPlot.Color.FromHex("#FD7E14");

    public AutoRunView()
    {
        InitializeComponent();
        var repository = new SqliteExperimentRepository();
        _orchestrator = new ExperimentOrchestrator(repository);
    }

    private async void BtnRun_Click(object sender, RoutedEventArgs e)
    {
        var algorithms = AlgorithmRegistry.CreateAll();

        // Чистим вкладки от предыдущего прогона
        ResultsTabControl.Items.Clear();

        _cts = new CancellationTokenSource();
        BtnRun.IsEnabled = false;
        BtnStop.IsEnabled = true;
        ProgressBar.Value = 0;

        var token = _cts.Token;

        for (int i = 0; i < algorithms.Count; i++)
        {
            if (token.IsCancellationRequested)
            {
                StatusText.Text = $"Остановлено пользователем. Завершено {i} из {algorithms.Count}.";
                break;
            }

            var algo = algorithms[i];
            StatusText.Text = $"Сейчас: {algo.DisplayName} ({i + 1}/{algorithms.Count})...";

            try
            {
                var d = algo.Defaults;

                var session = await _orchestrator.RunExperimentAsync(
                    algo,
                    d.NMax, d.NStep,
                    d.MMax, d.MStep,
                    d.RunsPerPoint,
                    forceRecalc: ChkForceRecalc.IsChecked == true);

                AddResultTab(algo, session, d.Hypothesis);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка в «{algo.DisplayName}»: {ex.Message}";
                AddErrorTab(algo, ex.Message);
            }

            ProgressBar.Value = (i + 1) * 100.0 / algorithms.Count;
        }

        if (!token.IsCancellationRequested)
        {
            StatusText.Text = $"Готово. Прогнано {algorithms.Count} алгоритмов.";
            ProgressBar.Value = 100;
        }

        BtnRun.IsEnabled = true;
        BtnStop.IsEnabled = false;
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        BtnStop.IsEnabled = false;
    }

    private void AddResultTab(IAlgorithm algo, ExperimentSession session, string hypothesisTag)
    {
        var plot = new ScottPlot.WPF.WpfPlot();
        plot.Plot.XLabel("Размер входных данных (N)");
        plot.Plot.ShowLegend();

        var color = Palette[ResultsTabControl.Items.Count % Palette.Length];

        // === STEP-режим ===
        if (algo.MeasureUnit == MeasureUnit.Steps)
        {
            plot.Plot.YLabel("Количество шагов");

            var points = session.Measurements
                .GroupBy(m => m.N)
                .Select(g => new { N = g.Key, Avg = g.Average(m => (double)m.Steps) })
                .OrderBy(p => p.N)
                .ToList();

            double[] xs = points.Select(p => (double)p.N).ToArray();
            double[] ys = points.Select(p => p.Avg).ToArray();

            var scatter = plot.Plot.Add.Scatter(xs, ys);
            scatter.LegendText = $"{algo.DisplayName} (шаги)";
            scatter.MarkerSize = 5;
            scatter.Color = color;
        }
        // === 2D-режим ===
        else if (algo.Dimension == ExperimentDimension.TwoDimensional)
        {
            plot.Plot.YLabel("Время (мс)");

            var groups = session.Measurements
                .GroupBy(m => m.M)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var group in groups)
            {
                var points = group
                    .GroupBy(m => m.N)
                    .Select(g => new { N = g.Key, Avg = g.Average(m => m.ElapsedMilliseconds) })
                    .OrderBy(p => p.N)
                    .ToList();

                double[] xs = points.Select(p => (double)p.N).ToArray();
                double[] ys = points.Select(p => p.Avg).ToArray();

                var scatter = plot.Plot.Add.Scatter(xs, ys);
                scatter.LegendText = $"m={group.Key}";
                scatter.MarkerSize = 3;
            }
        }
        // === 1D-режим ===
        else
        {
            plot.Plot.YLabel("Время (мс)");

            var points = session.Measurements
                .GroupBy(m => m.N)
                .Select(g => new { N = g.Key, Avg = g.Average(m => m.ElapsedMilliseconds) })
                .OrderBy(p => p.N)
                .ToList();

            double[] xs = points.Select(p => (double)p.N).ToArray();
            double[] ys = points.Select(p => p.Avg).ToArray();

            var scatter = plot.Plot.Add.Scatter(xs, ys);
            scatter.LegendText = $"{algo.DisplayName} (эмпирика)";
            scatter.MarkerSize = 5;
            scatter.Color = color;

            // Аппроксимация — если удаётся распарсить гипотезу
            if (Enum.TryParse<ComplexityFunction>(hypothesisTag, out var hyp))
            {
                var tuples = points.Select(p => (N: p.N, T: p.Avg)).ToList();
                var approx = _approximationService.Fit(tuples, hyp);
                var curve = _approximationService.BuildCurve(
                    points.Select(p => p.N).ToList(), approx);

                double[] cx = curve.Select(c => (double)c.N).ToArray();
                double[] cy = curve.Select(c => c.T).ToArray();

                var line = plot.Plot.Add.Scatter(cx, cy);
                line.LegendText = $"Аппроксимация {hyp.ToDisplayString()}";
                line.MarkerSize = 0;
                line.LineWidth = 2;
                line.Color = ApproximationColor;
            }
        }

        plot.Plot.Axes.AutoScale();

        var tab = new TabItem
        {
            Header = algo.DisplayName,
            Content = plot
        };
        ResultsTabControl.Items.Add(tab);

        // Автоматически переключаемся на новую вкладку
        ResultsTabControl.SelectedItem = tab;
    }

    private void AddErrorTab(IAlgorithm algo, string message)
    {
        var tab = new TabItem
        {
            Header = $"⚠ {algo.DisplayName}",
            Content = new TextBlock
            {
                Text = $"Ошибка: {message}",
                Margin = new Thickness(20),
                FontSize = 14,
                Foreground = System.Windows.Media.Brushes.DarkRed,
                TextWrapping = TextWrapping.Wrap
            }
        };
        ResultsTabControl.Items.Add(tab);
    }
}
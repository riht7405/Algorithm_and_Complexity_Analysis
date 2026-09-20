using System.Windows;
using System.Windows.Controls;
using TimeComplexityLab.Core;
using TimeComplexityLab.Core.Approximation;
using TimeComplexityLab.Domain;
using TimeComplexityLab.Infrastructure;

namespace TimeComplexityLab.Presentation.Wpf;

public partial class MainWindow : Window
{
    private readonly ExperimentOrchestrator _orchestrator;
    private readonly ApproximationService _approximationService = new();
    private readonly List<IAlgorithm> _algorithms = new();

    public MainWindow()
    {
        InitializeComponent();

        // Сборка зависимостей — здесь, в Presentation.
        // Application не знает про Infrastructure, Infrastructure реализует
        // интерфейс Application, Presentation соединяет их вместе.
        var repository = new SqliteExperimentRepository();
        _orchestrator = new ExperimentOrchestrator(repository);

        // Регистрация алгоритмов.
        // Позже это заменим на DI-контейнер или реестр.
        _algorithms.Add(new VectorSumAlgorithm());

        AlgoComboBox.ItemsSource = _algorithms;
        AlgoComboBox.SelectedIndex = 0;

        // Настройка внешнего вида графика
        MainPlot.Plot.XLabel("Размер входных данных (N)");
        MainPlot.Plot.YLabel("Время выполнения (мс)");
        MainPlot.Plot.ShowLegend();
    }

    /// <summary>
    /// Кнопка «Запустить бенчмарк». Очищает график и строит одну кривую.
    /// </summary>
    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        MainPlot.Plot.Clear();
        await RunAndPlotAsync();
    }

    /// <summary>
    /// Кнопка «Наложить на график». НЕ очищает график —
    /// новая кривая добавляется поверх существующих.
    /// </summary>
    private async void BtnCompare_Click(object sender, RoutedEventArgs e)
    {
        await RunAndPlotAsync();
    }

    /// <summary>
    /// Кнопка «Очистить график». Убирает все кривые.
    /// </summary>
    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        MainPlot.Plot.Clear();
        MainPlot.Refresh();
    }

    /// <summary>
    /// Общая логика: валидация параметров → запуск эксперимента →
    /// группировка по N → отрисовка на графике.
    /// </summary>
    private async Task RunAndPlotAsync()
    {
        if (AlgoComboBox.SelectedItem is not IAlgorithm algo)
        {
            MessageBox.Show("Выбери алгоритм.");
            return;
        }

        if (!int.TryParse(TxtNMax.Text, out int nMax) ||
            !int.TryParse(TxtStep.Text, out int step) ||
            !int.TryParse(TxtRuns.Text, out int runs) ||
            nMax <= 0 || step <= 0 || runs <= 0)
        {
            MessageBox.Show("Проверь параметры.");
            return;
        }

        // Определяем гипотезу f(n)
        var selectedItem = (ComboBoxItem)HypothesisComboBox.SelectedItem;
        var hypothesis = (ComplexityFunction)Enum.Parse(
            typeof(ComplexityFunction), (string)selectedItem.Tag);

        IsEnabled = false;
        try
        {
            var session = await _orchestrator.RunExperimentAsync(
                algo, nMax, step, runs);

            var points = session.Measurements
                .GroupBy(m => m.N)
                .Select(g => new { N = g.Key, Avg = g.Average(m => m.ElapsedMilliseconds) })
                .OrderBy(p => p.N)
                .ToList();

            double[] xs = points.Select(p => (double)p.N).ToArray();
            double[] ys = points.Select(p => p.Avg).ToArray();

            // Эмпирическая кривая
            var scatter = MainPlot.Plot.Add.Scatter(xs, ys);
            scatter.LegendText = $"{algo.DisplayName} (эмпирика)";
            scatter.MarkerSize = 5;
            scatter.Color = ScottPlot.Color.FromHex("#0D6EFD");

            // Аппроксимация
            var pointTuples = points
                .Select(p => (N: p.N, T: p.Avg))
                .ToList();
            var approximation = _approximationService.Fit(pointTuples, hypothesis);
            var nValues = points.Select(p => p.N).ToList();
            var curve = _approximationService.BuildCurve(nValues, approximation);

            double[] curveX = curve.Select(c => (double)c.N).ToArray();
            double[] curveY = curve.Select(c => c.T).ToArray();

            var line = MainPlot.Plot.Add.Scatter(curveX, curveY);
            line.LegendText = $"Аппроксимация {hypothesis.ToDisplayString()}";
            line.MarkerSize = 0;
            line.LineWidth = 2;
            line.Color = ScottPlot.Color.FromHex("#FD7E14");

            // Вывод C и MSE в панель
            ApproxInfoText.Text =
                $"f(n) = {hypothesis.ToDisplayString()}\n" +
                $"C = {approximation.C:E3}\n" +
                $"MSE = {approximation.MSE:E3}\n" +
                $"Точек: {approximation.PointsCount}";

            MainPlot.Plot.Axes.AutoScale();
            MainPlot.Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}\n\n{ex.StackTrace}");
        }
        finally
        {
            IsEnabled = true;
        }
    }
}
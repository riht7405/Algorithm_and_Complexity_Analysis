using System.Windows;
using System.Windows.Controls;
using TimeComplexityLab.Core;
using TimeComplexityLab.Core.Algorithms;
using TimeComplexityLab.Core.Approximation;
using TimeComplexityLab.Domain;
using TimeComplexityLab.Infrastructure;

namespace TimeComplexityLab.Presentation.Wpf;

public partial class MainWindow : Window
{
    private readonly ExperimentOrchestrator _orchestrator;
    private readonly ApproximationService _approximationService = new();
    private readonly List<IAlgorithm> _algorithms = new();

    // Палитра для эмпирик. Цвет аппроксимации всегда оранжевый.
    private static readonly ScottPlot.Color[] Palette = new[]
    {
        ScottPlot.Color.FromHex("#0D6EFD"), // синий
        ScottPlot.Color.FromHex("#198754"), // зелёный
        ScottPlot.Color.FromHex("#DC3545"), // красный
        ScottPlot.Color.FromHex("#6F42C1"), // фиолетовый
        ScottPlot.Color.FromHex("#20C997"), // бирюзовый
        ScottPlot.Color.FromHex("#D63384"), // розовый
        ScottPlot.Color.FromHex("#6C757D"), // серый
        ScottPlot.Color.FromHex("#0DCAF0"), // циан
    };

    private static readonly ScottPlot.Color ApproximationColor =
        ScottPlot.Color.FromHex("#FD7E14"); // оранжевый

    public MainWindow()
    {
        InitializeComponent();

        var repository = new SqliteExperimentRepository();
        _orchestrator = new ExperimentOrchestrator(repository);

        // Регистрация алгоритмов. Порядок определяет цвет эмпирики.
        _algorithms.Add(new ConstantFunctionAlgorithm());
        _algorithms.Add(new VectorSumAlgorithm());
        _algorithms.Add(new VectorProductAlgorithm());
        _algorithms.Add(new PolynomialNaiveAlgorithm());
        _algorithms.Add(new PolynomialHornerAlgorithm());
        _algorithms.Add(new BubbleSortAlgorithm());
        _algorithms.Add(new QuickSortAlgorithm());
        _algorithms.Add(new TimsortAlgorithm());

        AlgoComboBox.ItemsSource = _algorithms;
        AlgoComboBox.SelectedIndex = 0;

        MainPlot.Plot.XLabel("Размер входных данных (N)");
        MainPlot.Plot.YLabel("Время выполнения (мс)");
        MainPlot.Plot.ShowLegend();
    }

    private ScottPlot.Color GetColorForAlgorithm(IAlgorithm algo)
    {
        int index = _algorithms.IndexOf(algo);
        if (index < 0) index = 0;
        return Palette[index % Palette.Length];
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        MainPlot.Plot.Clear();
        await RunAndPlotAsync();
    }

    private async void BtnCompare_Click(object sender, RoutedEventArgs e)
    {
        await RunAndPlotAsync();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        MainPlot.Plot.Clear();
        MainPlot.Refresh();
    }

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

        if (HypothesisComboBox.SelectedItem is not ComboBoxItem hypItem ||
            hypItem.Tag is not string hypTag ||
            !Enum.TryParse<ComplexityFunction>(hypTag, out var hypothesis))
        {
            MessageBox.Show("Выбери гипотезу f(n).");
            return;
        }

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

            var color = GetColorForAlgorithm(algo);

            // Эмпирика — цвет алгоритма, сплошная
            var scatter = MainPlot.Plot.Add.Scatter(xs, ys);
            scatter.LegendText = $"{algo.DisplayName} (эмпирика)";
            scatter.MarkerSize = 5;
            scatter.Color = color;

            // Аппроксимация — всегда оранжевая, пунктирная
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
            line.Color = ApproximationColor;

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
            MessageBox.Show(
                $"Ошибка эксперимента:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }
}
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TimeComplexityLab.Domain;
using TimeComplexityLab.Infrastructure;
using TimeComplexityLab.Presentation.Wpf.Services;

namespace TimeComplexityLab.Presentation.Wpf.Views;

public partial class HistoryView : UserControl
{
    private readonly SqliteExperimentRepository _repository = new();
    private List<SessionRow> _rows = new();

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

    public HistoryView()
    {
        InitializeComponent();
        HistoryPlot.Plot.XLabel("Размер входных данных (N)");
        HistoryPlot.Plot.YLabel("Время (мс) / Шаги");
        HistoryPlot.Plot.ShowLegend();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _repository.InitializeAsync();

            // Одноразовая очистка «мусорных» сессий из прошлых запусков
            int cleaned = await _repository.CleanupEmptySessionsAsync();
            if (cleaned > 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Очищено пустых сессий: {cleaned}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка очистки: {ex.Message}");
        }

        await RefreshAsync();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            await _repository.InitializeAsync();
            var sessions = await _repository.GetAllSessionsAsync();

            _rows = sessions.Select(s => new SessionRow
            {
                Id = s.Id,
                StartedAtUtc = s.StartedAtUtc,
                DisplayName = s.DisplayName,
                AlgorithmId = s.AlgorithmId,
                NMax = s.NMax,
                Step = s.Step,
                MMax = s.MMax,
                MStep = s.MStep,
                RunsPerPoint = s.RunsPerPoint,
                Dimension = s.Dimension,
                MeasureUnit = s.MeasureUnit,
                PointCount = (s.MMax > 0 && s.MStep > 0
                    ? (s.NMax / s.Step) * (s.MMax / s.MStep)
                    : s.Step > 0 ? s.NMax / s.Step : 0) + 1
            }).ToList();

            SessionsGrid.ItemsSource = _rows;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка загрузки истории: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SessionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Оставлено для будущих подсказок; сейчас логики нет
    }

    private async void SessionsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SessionsGrid.SelectedItem is not SessionRow row) return;

        // Двойной клик — строим график только для этой сессии
        await PlotSessionsAsync(new List<SessionRow> { row });
    }

    private async void BtnPlotSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = SessionsGrid.SelectedItems
            .OfType<SessionRow>()
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show("Выбери хотя бы одну сессию (Ctrl+клик для нескольких).");
            return;
        }

        await PlotSessionsAsync(selected);
    }

    private async Task PlotSessionsAsync(List<SessionRow> selected)
    {
        var sessionIds = selected.Select(s => s.Id).ToList();

        try
        {
            var measurements = await _repository.GetMeasurementsBySessionsAsync(sessionIds);

            HistoryPlot.Plot.Clear();

            // Считаем, сколько раз встречается комбинация (алгоритм + размеры + режим)
            var dupGroups = selected
                .GroupBy(s => $"{s.AlgorithmId}|{s.NMax}|{s.MMax}|{s.Step}|{s.MStep}|{s.MeasureUnit}")
                .ToDictionary(g => g.Key, g => g.Count());

            var dupCounter = new Dictionary<string, int>();
            int colorIndex = 0;

            foreach (var session in selected)
            {
                var key = $"{session.AlgorithmId}|{session.NMax}|{session.MMax}|{session.Step}|{session.MStep}|{session.MeasureUnit}";
                int dupIndex = dupCounter.GetValueOrDefault(key, 0);
                dupCounter[key] = dupIndex + 1;

                var baseColor = Palette[colorIndex % Palette.Length];

                // Дубли: уменьшаем прозрачность, чтобы основная кривая была ярче
                double alpha = Math.Max(0.35, 0.9 - dupIndex * 0.18);
                var color = baseColor.WithAlpha(alpha);

                var timeLabel = session.StartedAtUtc.ToString("HH:mm:ss");
                var dupLabel = dupGroups[key] > 1
                    ? $" [{dupIndex + 1}/{dupGroups[key]}]"
                    : "";

                var sessionMeasurements = measurements
                    .Where(m => m.SessionId == session.Id)
                    .ToList();

                if (sessionMeasurements.Count == 0)
                {
                    colorIndex++;
                    continue;
                }

                if (session.Dimension == ExperimentDimension.TwoDimensional)
                {
                    // 2D — серия кривых по m
                    var mGroups = sessionMeasurements
                        .GroupBy(m => m.M)
                        .OrderBy(g => g.Key)
                        .ToList();

                    foreach (var mGroup in mGroups)
                    {
                        var points = mGroup
                            .GroupBy(m => m.N)
                            .Select(g => new
                            {
                                N = g.Key,
                                Avg = session.MeasureUnit == MeasureUnit.Steps
                                    ? g.Average(m => (double)m.Steps)
                                    : g.Average(m => m.ElapsedMilliseconds)
                            })
                            .OrderBy(p => p.N).ToList();

                        double[] xs = points.Select(p => (double)p.N).ToArray();
                        double[] ys = points.Select(p => p.Avg).ToArray();

                        var scatter = HistoryPlot.Plot.Add.Scatter(xs, ys);
                        scatter.LegendText = $"{session.DisplayName} m={mGroup.Key}{dupLabel} ({timeLabel})";
                        scatter.MarkerSize = 3;
                        scatter.Color = color;
                    }
                }
                else
                {
                    // 1D — одна кривая
                    var points = sessionMeasurements
                        .GroupBy(m => m.N)
                        .Select(g => new
                        {
                            N = g.Key,
                            Avg = session.MeasureUnit == MeasureUnit.Steps
                                ? g.Average(m => (double)m.Steps)
                                : g.Average(m => m.ElapsedMilliseconds)
                        })
                        .OrderBy(p => p.N).ToList();

                    double[] xs = points.Select(p => (double)p.N).ToArray();
                    double[] ys = points.Select(p => p.Avg).ToArray();

                    var scatter = HistoryPlot.Plot.Add.Scatter(xs, ys);
                    scatter.LegendText = $"{session.DisplayName}{dupLabel} ({timeLabel})";
                    scatter.MarkerSize = 5;
                    scatter.Color = color;
                }

                colorIndex++;
            }

            HistoryPlot.Plot.Axes.AutoScale();
            HistoryPlot.Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка построения графика: {ex.Message}\n\n{ex.StackTrace}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnShow3D_Click(object sender, RoutedEventArgs e)
    {
        var selected = SessionsGrid.SelectedItems
            .OfType<SessionRow>()
            .ToList();

        if (selected.Count != 1)
        {
            MessageBox.Show("Выбери ровно одну 2D-сессию для 3D-визуализации.");
            return;
        }

        var row = selected[0];

        if (row.Dimension != ExperimentDimension.TwoDimensional)
        {
            MessageBox.Show(
                $"Сессия «{row.DisplayName}» — 1D. 3D-поверхность доступна только " +
                "для 2D-алгоритмов (Матричное умножение, DTW, Хиршберг).",
                "Не 2D", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var measurements = await _repository
                .GetMeasurementsBySessionsAsync(new[] { row.Id });

            if (measurements.Count == 0)
            {
                MessageBox.Show("У этой сессии нет замеров.");
                return;
            }

            var session = new ExperimentSession
            {
                Id = row.Id,
                AlgorithmId = row.AlgorithmId,
                DisplayName = row.DisplayName,
                Dimension = row.Dimension,
                MeasureUnit = row.MeasureUnit,
                NMax = row.NMax,
                MMax = row.MMax,
                RunsPerPoint = row.RunsPerPoint,
                Measurements = measurements
            };

            SessionStore.SetFromHistory(session);

            var axisLabel = row.MeasureUnit == MeasureUnit.Steps ? "шаги" : "время, мс";
            var window = new SurfacePlotWindow();
            window.BuildSurface(
                measurements,
                row.DisplayName,
                axisLabel,
                row.NMax,
                row.MMax);
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка 3D-визуализации: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (SessionsGrid.SelectedItem is not SessionRow row)
        {
            MessageBox.Show("Выбери сессию для удаления.");
            return;
        }

        var result = MessageBox.Show(
            $"Удалить сессию «{row.DisplayName}» от {row.StartedAtUtc:dd.MM.yyyy HH:mm:ss}?",
            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            await _repository.DeleteSessionAsync(row.Id);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Ошибка удаления: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// DTO для отображения в DataGrid.
    /// </summary>
    public class SessionRow
    {
        public Guid Id { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public string DisplayName { get; set; } = "";
        public string AlgorithmId { get; set; } = "";
        public int NMax { get; set; }
        public int Step { get; set; }
        public int MMax { get; set; }
        public int MStep { get; set; }
        public int RunsPerPoint { get; set; }
        public ExperimentDimension Dimension { get; set; }
        public MeasureUnit MeasureUnit { get; set; }
        public int PointCount { get; set; }
    }
}
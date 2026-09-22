using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using TimeComplexityLab.Domain;
using WpfMesh = System.Windows.Media.Media3D.MeshGeometry3D;
using WpfPoint = System.Windows.Media.Media3D.Point3D;

namespace TimeComplexityLab.Presentation.Wpf.Views;

public partial class SurfacePlotWindow : Window
{
    public SurfacePlotWindow()
    {
        InitializeComponent();
    }

    public void BuildSurface(
        IReadOnlyList<Measurement> measurements,
        string algorithmName,
        string axisLabel,
        int nMax,
        int mMax)
    {
        var isSteps = axisLabel == "шаги";

        // 1. Усредняем значения по точкам
        var points = measurements
            .GroupBy(x => (x.N, x.M))
            .Select(g => new
            {
                N = g.Key.N,
                M = g.Key.M,
                Value = g.Average(x => isSteps ? (double)x.Steps : x.ElapsedMilliseconds)
            })
            .OrderBy(p => p.N).ThenBy(p => p.M)
            .ToList();

        if (points.Count < 4)
        {
            MessageBox.Show(
                "Недостаточно точек. Увеличь количество значений N и M.",
                "Мало данных", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var nValues = points.Select(p => p.N).Distinct().OrderBy(x => x).ToList();
        var mValues = points.Select(p => p.M).Distinct().OrderBy(x => x).ToList();

        if (nValues.Count < 3 || mValues.Count < 3)
        {
            MessageBox.Show(
                $"Мало уникальных значений: N = {nValues.Count}, M = {mValues.Count}. " +
                "Уменьши шаги N и M.",
                "Мало данных", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var z = new double[nValues.Count, mValues.Count];
        var nIndex = new Dictionary<int, int>();
        var mIndex = new Dictionary<int, int>();
        for (int i = 0; i < nValues.Count; i++) nIndex[nValues[i]] = i;
        for (int j = 0; j < mValues.Count; j++) mIndex[mValues[j]] = j;

        foreach (var p in points)
            z[nIndex[p.N], mIndex[p.M]] = p.Value;

        double zMin = double.MaxValue, zMaxVal = double.MinValue;
        for (int i = 0; i < nValues.Count; i++)
            for (int j = 0; j < mValues.Count; j++)
            {
                var v = z[i, j];
                if (v < zMin) zMin = v;
                if (v > zMaxVal) zMaxVal = v;
            }
        if (zMaxVal - zMin < 1e-12) zMaxVal = zMin + 1e-12;

        double maxN = nValues.Max();
        double maxM = mValues.Max();

        // Координаты: X в [-1,1], Y в [-1,1], Z в [0.05, 1.5] (высота)
        WpfPoint MakePoint(int n, int m, double val)
        {
            double x = maxN > 0 ? (double)n / maxN * 2 - 1 : 0;
            double y = maxM > 0 ? (double)m / maxM * 2 - 1 : 0;
            double zNorm = (val - zMin) / (zMaxVal - zMin);
            return new WpfPoint(x, y, 0.05 + zNorm * 1.45);
        }

        // 2. Очищаем всё перед построением
        Viewport.Children.Clear();

        // 3. Точки замеров — яркие сферы
        var sphereMaterial = new MaterialGroup();
        sphereMaterial.Children.Add(new DiffuseMaterial(
            new SolidColorBrush(Color.FromRgb(220, 40, 40))));
        sphereMaterial.Children.Add(new EmissiveMaterial(
            new SolidColorBrush(Color.FromRgb(120, 0, 0))));

        double sphereRadius = Math.Max(0.012, 0.4 / Math.Max(nValues.Count, mValues.Count));

        foreach (var p in points)
        {
            var sphere = new SphereVisual3D
            {
                Center = MakePoint(p.N, p.M, p.Value),
                Radius = sphereRadius,
                Material = sphereMaterial
            };
            Viewport.Children.Add(sphere);
        }

        // 4. Строим поверхность — полупрозрачный синий
        var mesh = new WpfMesh();

        for (int i = 0; i < nValues.Count - 1; i++)
        {
            for (int j = 0; j < mValues.Count - 1; j++)
            {
                var p00 = MakePoint(nValues[i], mValues[j], z[i, j]);
                var p10 = MakePoint(nValues[i + 1], mValues[j], z[i + 1, j]);
                var p11 = MakePoint(nValues[i + 1], mValues[j + 1], z[i + 1, j + 1]);
                var p01 = MakePoint(nValues[i], mValues[j + 1], z[i, j + 1]);

                int i0 = mesh.Positions.Count;
                mesh.Positions.Add(p00);
                mesh.Positions.Add(p10);
                mesh.Positions.Add(p11);
                mesh.TriangleIndices.Add(i0);
                mesh.TriangleIndices.Add(i0 + 1);
                mesh.TriangleIndices.Add(i0 + 2);

                int i1 = mesh.Positions.Count;
                mesh.Positions.Add(p00);
                mesh.Positions.Add(p11);
                mesh.Positions.Add(p01);
                mesh.TriangleIndices.Add(i1);
                mesh.TriangleIndices.Add(i1 + 1);
                mesh.TriangleIndices.Add(i1 + 2);
            }
        }

        var surfaceMaterial = new DiffuseMaterial(
            new SolidColorBrush(Color.FromArgb(130, 60, 120, 220)));

        var surfaceModel = new GeometryModel3D(mesh, surfaceMaterial)
        {
            BackMaterial = surfaceMaterial
        };

        Viewport.Children.Add(new ModelVisual3D { Content = surfaceModel });

        // 4.5. Сетка поверх поверхности — линии вдоль N и M
        var grid = new LinesVisual3D
        {
            Color = Color.FromArgb(180, 60, 60, 60),
            Thickness = 1
        };

        // Небольшое смещение по Z, чтобы линии не мерцали с поверхностью
        WpfPoint Offset(WpfPoint p) => new WpfPoint(p.X, p.Y, p.Z + 0.008);

        // Линии вдоль N (для каждой позиции m)
        for (int j = 0; j < mValues.Count; j++)
        {
            for (int i = 0; i < nValues.Count - 1; i++)
            {
                grid.Points.Add(Offset(MakePoint(nValues[i], mValues[j], z[i, j])));
                grid.Points.Add(Offset(MakePoint(nValues[i + 1], mValues[j], z[i + 1, j])));
            }
        }

        // Линии вдоль M (для каждой позиции n)
        for (int i = 0; i < nValues.Count; i++)
        {
            for (int j = 0; j < mValues.Count - 1; j++)
            {
                grid.Points.Add(Offset(MakePoint(nValues[i], mValues[j], z[i, j])));
                grid.Points.Add(Offset(MakePoint(nValues[i], mValues[j + 1], z[i, j + 1])));
            }
        }

        Viewport.Children.Add(grid);

        // 4.7. Координатная сетка на стенках
        AddCoordinateGrids(nValues, mValues);

        // 4.8. Подписи на сетке
        AddGridLabels(nValues, mValues, zMin, zMaxVal);

        // 5. Оси с числами
        AddAxesWithLabels(nValues, mValues, zMin, zMaxVal, axisLabel);

        // 6. Смотрим на сцену
        Viewport.ZoomExtents(600);

        // 7. Подвал
        InfoText.Text =
            $"{algorithmName}   |   " +
            $"N: {nValues.Min()}..{nValues.Max()} ({nValues.Count} знач.)   |   " +
            $"M: {mValues.Min()}..{mValues.Max()} ({mValues.Count} знач.)   |   " +
            $"{axisLabel}: {zMin:F3}..{zMaxVal:F3}   |   " +
            $"точек: {points.Count}";

        Title = $"3D-поверхность — {algorithmName}";
    }

    private void AddCoordinateGrids(List<int> nValues, List<int> mValues)
    {
        const double zTop = 1.5;

        var grid = new LinesVisual3D
        {
            Color = Color.FromArgb(30, 200, 200, 200),
            Thickness = 0.5
        };

        double maxN = nValues.Max();
        double maxM = mValues.Max();

        double NormN(int n) => maxN > 0 ? (double)n / maxN * 2 - 1 : 0;
        double NormM(int m) => maxM > 0 ? (double)m / maxM * 2 - 1 : 0;

        // ===== Пол (Z=0) =====
        foreach (var n in nValues)
        {
            double x = NormN(n);
            grid.Points.Add(new WpfPoint(x, -1, 0));
            grid.Points.Add(new WpfPoint(x, 1, 0));
        }
        foreach (var m in mValues)
        {
            double y = NormM(m);
            grid.Points.Add(new WpfPoint(-1, y, 0));
            grid.Points.Add(new WpfPoint(1, y, 0));
        }

        // ===== Задняя стенка (Y = 1) =====
        // Вертикали по N
        foreach (var n in nValues)
        {
            double x = NormN(n);
            grid.Points.Add(new WpfPoint(x, 1, 0));
            grid.Points.Add(new WpfPoint(x, 1, zTop));
        }
        // Горизонтали по Z (6 линий)
        for (int i = 0; i <= 6; i++)
        {
            double z = zTop * i / 6.0;
            grid.Points.Add(new WpfPoint(-1, 1, z));
            grid.Points.Add(new WpfPoint(1, 1, z));
        }

        // ===== Боковая стенка (X = -1) =====
        // Вертикали по M
        foreach (var m in mValues)
        {
            double y = NormM(m);
            grid.Points.Add(new WpfPoint(-1, y, 0));
            grid.Points.Add(new WpfPoint(-1, y, zTop));
        }
        // Горизонтали по Z (6 линий)
        for (int i = 0; i <= 6; i++)
        {
            double z = zTop * i / 6.0;
            grid.Points.Add(new WpfPoint(-1, -1, z));
            grid.Points.Add(new WpfPoint(-1, 1, z));
        }

        Viewport.Children.Add(grid);
    }

    private void AddGridLabels(
    List<int> nValues,
    List<int> mValues,
    double zMin,
    double zMax)
    {
        const double zTop = 1.5;
        const int zLevels = 6;

        double maxN = nValues.Max();
        double maxM = mValues.Max();

        double NormN(int n) => maxN > 0 ? (double)n / maxN * 2 - 1 : 0;
        double NormM(int m) => maxM > 0 ? (double)m / maxM * 2 - 1 : 0;

        // ===== Z-метки на задней стенке (Y=1, справа от поверхности) =====
        for (int i = 0; i <= zLevels; i++)
        {
            double t = (double)i / zLevels;
            double z = zTop * t;
            double value = zMin + (zMax - zMin) * t;
            AddLabel(value.ToString("F2"),
                     new WpfPoint(1.08, 1, z),
                     11, false);
        }

        // ===== N-метки на полу (Y=-1, вдоль переднего края) =====
        // Если значений много — показываем каждое второе, чтобы не сливались
        int nStep = nValues.Count > 15 ? 2 : 1;
        for (int i = 0; i < nValues.Count; i += nStep)
        {
            double x = NormN(nValues[i]);
            AddLabel(nValues[i].ToString(),
                     new WpfPoint(x, -1.08, 0),
                     11, false);
        }

        // ===== M-метки на полу (X=-1, вдоль левого края) =====
        int mStep = mValues.Count > 15 ? 2 : 1;
        for (int i = 0; i < mValues.Count; i += mStep)
        {
            double y = NormM(mValues[i]);
            AddLabel(mValues[i].ToString(),
                     new WpfPoint(-1.08, y, 0),
                     11, false);
        }
    }

    private void AddAxesWithLabels(
        List<int> nValues,
        List<int> mValues,
        double zMin,
        double zMax,
        string axisLabel)
    {
        // Оси — три линии
        var axes = new LinesVisual3D
        {
            Color = Color.FromRgb(40, 40, 40),
            Thickness = 2
        };
        axes.Points.Add(new WpfPoint(-1, -1, 0));
        axes.Points.Add(new WpfPoint(1.3, -1, 0));
        axes.Points.Add(new WpfPoint(-1, -1, 0));
        axes.Points.Add(new WpfPoint(-1, 1.3, 0));
        axes.Points.Add(new WpfPoint(-1, -1, 0));
        axes.Points.Add(new WpfPoint(-1, -1, 1.8));
        Viewport.Children.Add(axes);

        // Подписи осей (буквы)
        AddLabel("N", new WpfPoint(1.4, -1, 0), 22, true);
        AddLabel("M", new WpfPoint(-1, 1.4, 0), 22, true);
        AddLabel(axisLabel, new WpfPoint(-1, -1, 1.95), 18, true);

        // Метки с числами на осях N
        AddLabel(nValues.Min().ToString(),
                 new WpfPoint(-1, -1.1, -0.05), 14, false);
        AddLabel(nValues.Max().ToString(),
                 new WpfPoint(1, -1.1, -0.05), 14, false);
        AddLabel(((nValues.Min() + nValues.Max()) / 2).ToString(),
                 new WpfPoint(0, -1.1, -0.05), 14, false);

        // Метки с числами на осях M
        AddLabel(mValues.Min().ToString(),
                 new WpfPoint(-1.1, -1, -0.05), 14, false);
        AddLabel(mValues.Max().ToString(),
                 new WpfPoint(-1.1, 1, -0.05), 14, false);

        // Метки с числами на оси Z
        AddLabel(zMin.ToString("F2"),
                 new WpfPoint(-1.15, -1, 0.05), 14, false);
        AddLabel(zMax.ToString("F2"),
                 new WpfPoint(-1.15, -1, 1.5), 14, false);
    }

    private void AddLabel(string text, WpfPoint position, int fontSize, bool bold)
    {
        var label = new BillboardTextVisual3D
        {
            Text = text,
            Position = position,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
            FontSize = fontSize,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal
        };
        Viewport.Children.Add(label);
    }
}
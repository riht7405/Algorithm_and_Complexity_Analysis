using System.Windows;
using System.Windows.Controls;
using TimeComplexityLab.Domain;
using TimeComplexityLab.Presentation.Wpf.Services;

namespace TimeComplexityLab.Presentation.Wpf.Views;

public partial class SurfaceView : UserControl
{
    public SurfaceView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshStatus();
        SessionStore.CurrentChanged += RefreshStatus;
    }

    private void RefreshStatus()
    {
        var snap = SessionStore.Current;

        if (snap is null)
        {
            StatusText.Text = "Сначала запусти 2D-эксперимент на вкладке «Эксперимент» " +
                              "или выбери сессию в «Истории».";
            BtnBuild3D.IsEnabled = false;
            return;
        }

        if (snap.Dimension != ExperimentDimension.TwoDimensional)
        {
            StatusText.Text = $"Последняя сессия — «{snap.DisplayName}». " +
                              "3D доступно только для 2D-алгоритмов.";
            BtnBuild3D.IsEnabled = false;
            return;
        }

        var source = snap.FromHistory ? "из истории" : "из эксперимента";
        StatusText.Text = $"Готова сессия ({source}): {snap.DisplayName} " +
                          $"({snap.Session.Measurements.Count} измерений).";
        BtnBuild3D.IsEnabled = true;
    }

    private void BtnBuild3D_Click(object sender, RoutedEventArgs e)
    {
        var snap = SessionStore.Current;
        if (snap is null) return;

        var axisLabel = snap.MeasureUnit == MeasureUnit.Steps ? "шаги" : "время, мс";

        var window = new SurfacePlotWindow();
        window.BuildSurface(
            snap.Session.Measurements,
            snap.DisplayName,
            axisLabel,
            snap.Session.NMax,
            snap.Session.MMax);
        window.Show();
    }
}
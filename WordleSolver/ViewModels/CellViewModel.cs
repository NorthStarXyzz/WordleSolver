using CommunityToolkit.Mvvm.ComponentModel;

namespace WordleSolver.ViewModels;

public partial class CellViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _letter = null!;

    [ObservableProperty]
    private int _colorIndex;

    public void CycleColor()
    {
        ColorIndex = (ColorIndex + 1) % 3;
    }
}
using Microsoft.Maui.Controls;
using ResignerTool.ViewModels;

namespace ResignerTool.Views;

public partial class ApkResignPage : ContentPage
{
    public ApkResignPage()
    {
        InitializeComponent();
        BindingContext = new ApkResignViewModel();
    }
}
using ResignerTool.Views;

namespace ResignerTool;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(ApkResignPage), typeof(ApkResignPage));
        Routing.RegisterRoute(nameof(IpaResignPage), typeof(IpaResignPage));
    }
}

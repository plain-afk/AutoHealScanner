using System.Windows;

namespace AutoHealScanner
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // MAGIC FIX: Pipilitin natin ang system na i-load ang Material Design DLL sa memory bago mag-run ang UI para hindi niya sabihing "Cannot locate resource".
            var forceLoad = typeof(MaterialDesignThemes.Wpf.PaletteHelper);

            base.OnStartup(e);
        }
    }
}
using ObjLoader.Plugin;
using ObjLoader.Views.Windows;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels.Camera
{
    public class CameraWindowButtonViewModel : Bindable, IDisposable
    {
        private readonly ItemProperty[] _properties;
        private CameraWindow? _window;
        private bool _isDisposed;

        public ActionCommand OpenWindowCommand { get; }

        public CameraWindowButtonViewModel(ItemProperty[] properties)
        {
            _properties = properties;
            OpenWindowCommand = new ActionCommand(_ => true, _ => OpenWindow());
        }

        private void OpenWindow()
        {
            if (_isDisposed || _window != null) return;

            var param = _properties.FirstOrDefault()?.PropertyOwner as ObjLoaderParameter;
            if (param != null)
            {
                var vm = new CameraWindowViewModel(param);
                _window = new CameraWindow { DataContext = vm };
                _window.Closed += OnWindowClosed;
                _window.Show();
            }
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            if (sender is not Window win) return;
            win.Closed -= OnWindowClosed;
            if (win.DataContext is IDisposable vm) vm.Dispose();
            _window = null;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_window == null) return;
            var win = _window;
            _window = null;

            if (win.Dispatcher.CheckAccess())
            {
                win.Close();
            }
            else
            {
                win.Dispatcher.Invoke(win.Close);
            }
        }
    }
}
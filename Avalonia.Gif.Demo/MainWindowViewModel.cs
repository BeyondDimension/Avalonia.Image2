using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using ReactiveUI;

namespace Avalonia.Gif.Demo
{
    public partial class MainWindowViewModel : ReactiveObject
    {
        public MainWindowViewModel()
        {
            Stretches = new List<Stretch>
            {
                Stretch.None,
                Stretch.Fill,
                Stretch.Uniform,
                Stretch.UniformToFill
            };

            var list = AssetLoader.GetAssets(new Uri("avares://Avalonia.Gif.Demo/Images/"), null)
                  .Select(x => x.AbsoluteUri).ToList();
            list.Add("https://6tse5sb49lk6hk0n5edb56hibm4pc0iqom0ocsi2orftnim6hd5vuass.qc.dolfincdnx.net:5147/xdispatch2a304e1874a31533/media.st.dl.eccdnx.com/steamcommunity/public/images/items/1629910/045c57ebb6946fdf7e57a53d5768117dd8543862.gif?bsreqid=f301830fb4dd3faaaa6a682f1482045a&bsxdisp=se");
            list.Add("https://image.mossimo.net:5996/images/ys_900x350_0620.jpg");
            AvailableGifs = list;
        }

        private IReadOnlyList<string> _availableGifs;

        public IReadOnlyList<string> AvailableGifs
        {
            get => _availableGifs;
            set => this.RaiseAndSetIfChanged(ref _availableGifs, value);
        }

        private string _selectedGif;

        public string SelectedGif
        {
            get => _selectedGif;
            set => this.RaiseAndSetIfChanged(ref _selectedGif, value);
        }

        private IReadOnlyList<Stretch> _stretches;

        public IReadOnlyList<Stretch> Stretches
        {
            get => _stretches;
            set => this.RaiseAndSetIfChanged(ref _stretches, value);
        }

        private Stretch _stretch = Stretch.None;

        public Stretch Stretch
        {
            get => _stretch;
            set => this.RaiseAndSetIfChanged(ref _stretch, value);
        }

        public ICommand HangUiThreadCommand { get; } = ReactiveCommand.Create(() => Dispatcher.UIThread.InvokeAsync(() => Thread.Sleep(5000)));
    }
}
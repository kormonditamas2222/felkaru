using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace gambling
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<BitmapImage> symbols = new();
        private int ActiveSymbolCount = 10;
        private int activeCount = 0;
        private Random _rnd = new();
        private int credits = 100;
        private int spinCost = 10;
        private DispatcherTimer frameTimer = new();
        private const double ImageHeight = 200.0;
        private ReelState[] reels = new ReelState[3];

        public MainWindow()
        {
            InitializeComponent();

            LoadSymbols();
            activeCount = Math.Min(Math.Max(1, ActiveSymbolCount), symbols.Count);
            if (symbols.Count == 0)
            {
                lb_eredmeny.Content = "No symbol images found in Images\\";
                return;
            }
            reels[0] = new ReelState(spReel1);
            reels[1] = new ReelState(spReel2);
            reels[2] = new ReelState(spReel3);

            for (int i = 0; i < 3; i++)
            {
                reels[i].CenterIndex = 0;
                reels[i].OffsetFraction = 0.0;
                UpdateReelImages(i);
            }

            frameTimer.Interval = TimeSpan.FromMilliseconds(16);
            frameTimer.Tick += FrameTimer_Tick;
            frameTimer.Start();

            UpdateCreditsDisplay();
        }

        private void LoadSymbols()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string imagesDir = Path.Combine(baseDir, "Images");
                if (!Directory.Exists(imagesDir))
                {
                    lb_eredmeny.Content = "Images folder not found.";
                    return;
                }

                var supported = new[] { ".png", ".jpg", ".jpeg", ".gif" };
                var files = Directory.EnumerateFiles(imagesDir)
                                     .Where(f => supported.Contains(Path.GetExtension(f).ToLowerInvariant()))
                                     .OrderBy(f => f)
                                     .ToList();

                foreach (var file in files)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(file, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    symbols.Add(bmp);
                }
            }
            catch (Exception ex)
            {
                lb_eredmeny.Content = $"Error loading images: {ex.Message}";
            }
        }

        private void UpdateReelImages(int reel)
        {
            if (activeCount == 0) return;
            int center = Mod(reels[reel].CenterIndex, activeCount);
            int top = Mod(center - 1, activeCount);
            int bottom = Mod(center + 1, activeCount);

            var panel = reels[reel].Panel;

            ((System.Windows.Controls.Image)panel.Children[0]).Source = symbols[top];
            ((System.Windows.Controls.Image)panel.Children[1]).Source = symbols[center];
            ((System.Windows.Controls.Image)panel.Children[2]).Source = symbols[bottom];
        }

        private static int Mod(int x, int m) => ((x % m) + m) % m;

        private double _lastTick = Environment.TickCount / 1000.0;

        private void FrameTimer_Tick(object? sender, EventArgs e)
        {
            double now = Environment.TickCount / 1000.0;
            double dt = now - _lastTick;
            if (dt <= 0) dt = 0.016;
            _lastTick = now;

            for (int i = 0; i < 3; i++)
            {
                var rs = reels[i];
                if (!rs.Spinning) continue;

                double deltaSymbols = rs.Rate * dt;
                rs.OffsetFraction += deltaSymbols;

                while (rs.OffsetFraction >= 1.0)
                {
                    rs.OffsetFraction -= 1.0;
                    rs.CenterIndex = Mod(rs.CenterIndex + 1, activeCount);
                    UpdateReelImages(i);
                }

                if (rs.Transform != null)
                {
                    rs.Transform.Y = -rs.OffsetFraction * ImageHeight;
                }

                if (rs.StopRequested)
                {
                    rs.Rate = Math.Max(0.5, rs.Rate * 0.97);
                    if (rs.Rate <= 1.0)
                    {
                        if (rs.CenterIndex == rs.TargetIndex && rs.OffsetFraction < 0.03)
                        {
                            rs.Spinning = false;
                            rs.StopRequested = false;
                            rs.Rate = 0.0;
                            rs.OffsetFraction = 0.0;
                            if (rs.Transform != null) rs.Transform.Y = 0.0;
                            UpdateReelImages(i);
                        }
                    }
                }
            }
        }

        private void UpdateCreditsDisplay()
        {
            lb_kredit.Content = $"Kreditek: {credits}";
        }

        private void StartSpin()
        {
            for (int i = 0; i < 3; i++)
            {
                var rs = reels[i];
                rs.Spinning = true;
                rs.StopRequested = false;
                rs.Rate = 10.0 + i * 1.5; 
            }
        }

        private void RequestStop(int reel)
        {
            var rs = reels[reel];
            rs.StopRequested = true;
            rs.TargetIndex = _rnd.Next(activeCount);
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (activeCount == 0)
            {
                lb_eredmeny.Content = "Nincs mit pörgetni.";
                return;
            }

            if (credits < spinCost)
            {
                lb_eredmeny.Content = "Nincs elég kredit.";
                return;
            }

            btnSpin.IsEnabled = false;
            credits -= spinCost;
            UpdateCreditsDisplay();
            lb_eredmeny.Content = "Pörgetés...";

            StartSpin();

            await Task.Delay(800);
            RequestStop(0);
            await Task.Delay(600);
            RequestStop(1);
            await Task.Delay(600);
            RequestStop(2);

            while (reels.Any(r => r.Spinning))
            {
                await Task.Delay(50);
            }

            int a = reels[0].CenterIndex;
            int b = reels[1].CenterIndex;
            int c = reels[2].CenterIndex;

            if (a == b && b == c)
            {
                int win = spinCost * 10;
                credits += win;
                lb_eredmeny.Content = $"Jackpot! Három egyforma szimbólum. +{win} kredit";
            }
            else if (a == b || a == c || b == c)
            {
                int win = spinCost * 3;
                credits += win;
                lb_eredmeny.Content = $"Két egyforma szimbólum! +{win} kredit";
            }
            else
            {
                lb_eredmeny.Content = "Nem nyertél. Próbáld újra.";
            }

            UpdateCreditsDisplay();
            btnSpin.IsEnabled = true;
        }

        private class ReelState
        {
            public System.Windows.Controls.StackPanel Panel { get; }
            public TranslateTransform? Transform { get; }
            public double OffsetFraction { get; set; } = 0.0;
            public int CenterIndex { get; set; } = 0;
            public double Rate { get; set; } = 0.0;
            public bool Spinning { get; set; } = false;
            public bool StopRequested { get; set; } = false;
            public int TargetIndex { get; set; } = 0;

            public ReelState(System.Windows.Controls.StackPanel panel)
            {
                Panel = panel;
                Transform = panel.RenderTransform as TranslateTransform;
            }
        }
    }
}
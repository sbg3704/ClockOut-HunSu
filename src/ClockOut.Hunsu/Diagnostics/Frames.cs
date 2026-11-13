// 기능: 프레임 시각 도우미. 다음 프레임 시각 한 번 알리기, 움직임 동안 프레임 간격(가장 긴 간격, 16.7ms 넘은 수) 재기, 창 그림 PNG 저장.
// 사용법: Frames.Next(t => ...);  var probe = Frames.StartProbe(); ... var (max, over) = probe.Stop();  Frames.Snapshot(visual, path)
// 의존: WPF CompositionTarget.Rendering(그리기 직전에 화면 스레드에서 불림). 측정용이며 보통 사용 때도 해가 없다.
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClockOut.Hunsu.Diagnostics;

internal static class Frames
{
    public static void Next(Action<long> callback)
    {
        void Handler(object? sender, EventArgs e)
        {
            CompositionTarget.Rendering -= Handler;
            callback(Stopwatch.GetTimestamp());
        }
        CompositionTarget.Rendering += Handler;
    }

    public static Probe StartProbe() => new();

    /// <summary>움직임 동안 프레임 사이 간격을 잰다. 60fps 기준 16.7ms를 넘은 간격 수와 가장 긴 간격.</summary>
    public sealed class Probe
    {
        private long _last;
        private double _max;
        private double _maxSteady; // 첫 간격(새로 보이는 요소의 배치·첫 그리기)을 뺀 가장 긴 간격
        private int _over;
        private int _frames;
        private long _firstAt;

        internal Probe() => CompositionTarget.Rendering += OnFrame;

        private void OnFrame(object? sender, EventArgs e)
        {
            long now = Stopwatch.GetTimestamp();
            if (_last != 0)
            {
                double ms = (now - _last) * 1000.0 / Stopwatch.Frequency;
                if (ms > _max) _max = ms;
                if (_frames >= 2 && ms > _maxSteady) _maxSteady = ms;
                if (_frames >= 2 && ms > 1000.0 / 60 * 1.5) _over++; // 첫 간격 뒤에 한 프레임 반을 넘으면 건너뛴 것으로 센다
            }
            if (_firstAt == 0) _firstAt = now;
            _last = now;
            _frames++;
        }

        /// <summary>첫 프레임부터 마지막 프레임까지 평균 간격(ms).</summary>
        public double MeanGapMs => _frames > 1 ? (_last - _firstAt) * 1000.0 / Stopwatch.Frequency / (_frames - 1) : double.NaN;

        public (double MaxGapMs, double SteadyGapMs, int Skipped, int Frames) Stop()
        {
            CompositionTarget.Rendering -= OnFrame;
            return (Math.Round(_max, 2), Math.Round(_maxSteady, 2), _over, _frames);
        }
    }

    /// <summary>창이 스스로 그린 그림을 PNG로 저장한다(사용자 화면을 찍지 않음, docs/05 6장).</summary>
    public static void Snapshot(FrameworkElement root, string path)
    {
        var src = PresentationSource.FromVisual(root);
        double scale = src?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        int w = Math.Max(1, (int)(root.ActualWidth * scale)), h = Math.Max(1, (int)(root.ActualHeight * scale));
        var bmp = new RenderTargetBitmap(w, h, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bmp.Render(root);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var fs = File.Create(path);
        enc.Save(fs);
    }
}

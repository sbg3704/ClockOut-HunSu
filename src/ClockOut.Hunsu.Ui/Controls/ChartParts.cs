// 기능: 막대(bar), 추세선(spark), 진행(progress) 부품. 숫자를 한눈에 보이는 모양으로 그린다. Parts의 일부. 계산은 Core/View/ChartMath.cs.
// 사용법: view.json에서
//   {"type":"bar","items":"a.rows","label":"{item.name}","value":"{item.count}","tone":"info","max":8,"emptyText":"없음"}   배열 없이 "value"+"total"이면 한 줄(비율)
//   {"type":"spark","values":"selection.summary.history","field":"count","label":"최근","format":"n0","tone":"info"}   field는 객체 배열일 때만
//   {"type":"progress","label":"처리","value":"{a.done}","total":"{a.all}","tone":"good"}   total이 없으면 값을 0~1 비율로 본다
//   숫자가 아니면 "-"와 빈 막대. 예외를 내지 않는다.
// 의존: Core/View(Template, DataScope, ChartMath, CommandArgs), Theme. 모양 크기와 색은 Theme 이름으로만 쓴다. 움직임은 폭만 바꾼다(Hunsu.Fast).
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ClockOut.Hunsu.Core.View;
using ClockOut.Hunsu.Ui.Theme;

namespace ClockOut.Hunsu.Ui.Controls;

public static partial class Parts
{
    private const double BarHeight = 6;

    /// <summary>글 틀 하나를 숫자로. "{경로}" 한 덩어리면 JSON 숫자를 그대로 쓰고, 아니면 글로 채운 뒤 읽는다.</summary>
    private static bool Num(string template, DataScope scope, out double value)
    {
        value = 0;
        string t = template.Trim();
        if (t.Length > 2 && t[0] == '{' && t[^1] == '}' && t.IndexOf('{', 1) < 0)
        {
            string expr = t[1..^1];
            int colon = expr.IndexOf(':');
            if (scope.TryGet((colon < 0 ? expr : expr[..colon]).Trim(), out var el))
            {
                if (el.ValueKind == JsonValueKind.Number) { value = el.GetDouble(); return double.IsFinite(value); }
                if (el.ValueKind == JsonValueKind.String) return ChartMath.TryParse(el.GetString(), out value);
                return false;
            }
            return false;
        }
        return ChartMath.TryParse(Template.Render(template, scope), out value);
    }

    /// <summary>둥근 바탕 줄 + 채움 한 쌍. 채움 폭은 바탕 폭 × 비율.</summary>
    private sealed class BarTrack
    {
        public Border Track { get; } = new() { Height = BarHeight };
        public Border Fill { get; } = new() { HorizontalAlignment = HorizontalAlignment.Left, Height = BarHeight };
        private double _ratio;
        private bool _themed;

        public BarTrack()
        {
            Track.Child = Fill;
            Track.SizeChanged += (_, _) => Apply(false);
            Track.Loaded += (_, _) =>
            {
                if (_themed) return;
                _themed = true;
                var pill = (CornerRadius)Track.FindResource("Hunsu.PillRadius");
                Track.CornerRadius = pill;
                Fill.CornerRadius = pill;
                Track.Background = (Brush)Track.FindResource("Hunsu.GlassAlt");
                Apply(false);
            };
        }

        public void Set(double ratio, Brush tone, bool animate)
        {
            _ratio = ratio;
            Fill.Background = tone;
            Apply(animate);
        }

        private void Apply(bool animate)
        {
            double w = Math.Max(0, Track.ActualWidth * _ratio);
            double from = double.IsNaN(Fill.Width) ? 0 : Fill.Width;
            Fill.BeginAnimation(FrameworkElement.WidthProperty, null);
            Fill.Width = w; // 최종 값을 먼저 두고, 애니메이션은 이전 폭에서 따라간다(끝나면 이 값으로 남음)
            if (animate && Track.IsLoaded && Math.Abs(from - w) > 0.5)
                Fill.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(from, w, ThemeKeys.Get<Duration>(Track, ThemeKeys.Fast)) { FillBehavior = FillBehavior.Stop });
        }
    }

    /// <summary>"라벨 ........ 숫자" 윗줄 + 막대 아랫줄 한 칸.</summary>
    private static (FrameworkElement Element, TextBlock Label, TextBlock Number, BarTrack Bar) LabeledBar()
    {
        var label = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var number = new TextBlock { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(number, 1);
        head.Children.Add(label);
        head.Children.Add(number);
        var bar = new BarTrack();
        bar.Track.Margin = new Thickness(0, 3, 0, 0);
        var box = new StackPanel { Margin = new Thickness(0, 3, 0, 3) };
        box.Children.Add(head);
        box.Children.Add(bar.Track);
        bool themed = false;
        box.Loaded += (_, _) =>
        {
            if (themed) return;
            themed = true;
            label.FontSize = number.FontSize = (double)box.FindResource("Hunsu.FontSmall");
            label.Foreground = (Brush)box.FindResource(ThemeKeys.Text);
            number.Foreground = (Brush)box.FindResource(ThemeKeys.Text);
            number.FontFamily = (FontFamily)box.FindResource("Hunsu.FontNumber");
            System.Windows.Documents.Typography.SetNumeralAlignment(number, FontNumeralAlignment.Tabular);
        };
        return (box, label, number, bar);
    }

    // ---- bar ----
    private static (FrameworkElement, Action<DataScope>) BarPart(ViewItem item)
    {
        var host = new StackPanel { Margin = new Thickness(0, 2, 0, 2) };
        string itemsPath = item.Get("items");
        double? declaredMax = Num(item.Get("max"), new DataScope(), out var mx) && mx > 0 ? mx : null;
        var single = itemsPath.Length == 0 ? LabeledBar() : default;
        if (itemsPath.Length == 0) host.Children.Add(single.Element);
        return (host, scope =>
        {
            if (itemsPath.Length == 0)
            {
                bool ok = Num(item.Get("value"), scope, out var v);
                bool hasTotal = Num(item.Get("total"), scope, out var total);
                single.Label.Text = Template.Render(item.Get("label"), scope);
                single.Number.Text = ok ? Template.FormatNumber(v, "n0") : "-";
                single.Bar.Set(ok ? (hasTotal ? ChartMath.Ratio(v, total) : ChartMath.Clamp01(v)) : 0, ThemeKeys.Tone(host, Template.Render(item.Get("tone", "info"), scope)), true);
                return;
            }
            host.Children.Clear();
            if (!scope.TryGet(itemsPath, out var arr) || arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            {
                host.Children.Add(Placeholder(Template.Render(item.Get("emptyText", "표시할 항목이 없습니다."), scope)));
                return;
            }
            var rows = new List<(string Label, bool Ok, double Value, string Tone)>();
            foreach (var el in arr.EnumerateArray())
            {
                if (rows.Count >= ListMaxRows) break;
                var row = CommandArgs.RowScope(el);
                rows.Add((Template.Render(item.Get("label"), row), Num(item.Get("value"), row, out var v), v, Template.Render(item.Get("tone", "info"), row)));
            }
            double max = ChartMath.MaxOf(rows.Where(r => r.Ok).Select(r => r.Value), declaredMax);
            foreach (var r in rows)
            {
                var (el, label, number, bar) = LabeledBar();
                label.Text = r.Label;
                number.Text = r.Ok ? Template.FormatNumber(r.Value, "n0") : "-";
                bar.Set(r.Ok ? ChartMath.Ratio(r.Value, max) : 0, ThemeKeys.Tone(host, r.Tone), false);
                host.Children.Add(el);
            }
        });
    }

    // ---- progress ----
    private static (FrameworkElement, Action<DataScope>) ProgressPart(ViewItem item)
    {
        var (box, label, number, bar) = LabeledBar();
        return (box, scope =>
        {
            bool ok = Num(item.Get("value"), scope, out var v);
            bool hasTotal = Num(item.Get("total"), scope, out var total);
            label.Text = Template.Render(item.Get("label"), scope);
            number.Text = !ok ? "-" : hasTotal ? $"{Template.FormatNumber(v, "n0")} / {Template.FormatNumber(total, "n0")}" : Template.FormatNumber(ChartMath.Clamp01(v), "p");
            bar.Set(ok ? (hasTotal ? ChartMath.Ratio(v, total) : ChartMath.Clamp01(v)) : 0, ThemeKeys.Tone(box, Template.Render(item.Get("tone", "good"), scope)), true);
        });
    }

    // ---- spark ----
    private static (FrameworkElement, Action<DataScope>) SparkPart(ViewItem item)
    {
        const double H = 28, Pad = 3, Dot = 5;
        var label = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var last = new TextBlock { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(last, 1);
        head.Children.Add(label);
        head.Children.Add(last);
        var line = new Polyline { StrokeThickness = 1.5, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        var dot = new Ellipse { Width = Dot, Height = Dot, Visibility = Visibility.Collapsed };
        var canvas = new Canvas { Height = H, ClipToBounds = false, Margin = new Thickness(0, 3, 0, 0) };
        canvas.Children.Add(line);
        canvas.Children.Add(dot);
        var box = new StackPanel { Margin = new Thickness(0, 3, 0, 3) };
        box.Children.Add(head); // 라벨이 없어도 오른쪽 마지막 값은 보인다
        box.Children.Add(canvas);
        IReadOnlyList<double> norm = [];
        void Draw()
        {
            double w = canvas.ActualWidth;
            line.Points.Clear();
            if (norm.Count == 0 || w <= 0) { dot.Visibility = Visibility.Collapsed; return; }
            double step = norm.Count > 1 ? (w - Dot) / (norm.Count - 1) : 0;
            double x0 = Dot / 2, usable = H - 2 * Pad;
            for (int i = 0; i < norm.Count; i++) line.Points.Add(new Point(x0 + i * step, Pad + (1 - norm[i]) * usable));
            if (norm.Count == 1) line.Points.Add(new Point(x0 + 1, line.Points[0].Y)); // 점 하나도 짧은 선으로 보이게
            var p = line.Points[norm.Count == 1 ? 0 : ^1];
            Canvas.SetLeft(dot, p.X - Dot / 2);
            Canvas.SetTop(dot, p.Y - Dot / 2);
            dot.Visibility = Visibility.Visible;
        }
        canvas.SizeChanged += (_, _) => Draw();
        bool themed = false;
        box.Loaded += (_, _) =>
        {
            if (themed) return;
            themed = true;
            label.FontSize = last.FontSize = (double)box.FindResource("Hunsu.FontSmall");
            label.Foreground = (Brush)box.FindResource(ThemeKeys.Muted);
            last.Foreground = (Brush)box.FindResource(ThemeKeys.Text);
            last.FontFamily = (FontFamily)box.FindResource("Hunsu.FontNumber");
            System.Windows.Documents.Typography.SetNumeralAlignment(last, FontNumeralAlignment.Tabular);
        };
        string valuesPath = item.Get("values"), field = item.Get("field"), format = item.Get("format", "n0");
        return (box, scope =>
        {
            var raw = new List<double>();
            if (scope.TryGet(valuesPath, out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in arr.EnumerateArray())
                {
                    var v = el;
                    if (field.Length > 0 && (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(field, out v))) continue;
                    if (v.ValueKind == JsonValueKind.Number) raw.Add(v.GetDouble());
                    else if (v.ValueKind == JsonValueKind.String && ChartMath.TryParse(v.GetString(), out var d)) raw.Add(d);
                }
            }
            var good = raw.Where(double.IsFinite).ToList();
            norm = ChartMath.Normalize(good);
            label.Text = Template.Render(item.Get("label"), scope);
            last.Text = good.Count > 0 ? Template.FormatNumber(good[^1], format) : "-";
            var tone = ThemeKeys.Tone(box, Template.Render(item.Get("tone", "info"), scope));
            line.Stroke = tone;
            dot.Fill = tone;
            Draw();
        });
    }
}

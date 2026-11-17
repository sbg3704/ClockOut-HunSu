// 기능: 셀 위 표시층의 "그리기 방식(style)" 등록처. 모듈은 view.json marks[]에 style 이름만 적고, 그 이름을 맡은 방식이 칸 자리를 모아 그림 계획을 만든다.
//       새 방식은 IMarkStyle 하나를 만들어 MarkStyles.All에 더하면 되고, 셸(ShellController.Marks)은 고치지 않는다. outline·fill·underline처럼 단순한 상자는 여기 없이 셸이 공용으로 그린다.
// 사용법: foreach 표시 선언: var s = MarkStyles.Find(spec.Style); s?.Add(spec, arr, panes, ctx);  끝나고 s.Build(ctx)로 계획을 받아 MarkWindow에 넘긴다.
// 의존: Core/Marks, Core/View(MarkSpec, DataScope), Core/Settings. 화면 그리기는 Overlay/MarkWindow가 한다.
using ClockOut.Hunsu.Core;
using ClockOut.Hunsu.Core.Marks;
using ClockOut.Hunsu.Core.Settings;
using ClockOut.Hunsu.Core.View;

namespace ClockOut.Hunsu.Shell;

/// <summary>한 번 그릴 때 방식들이 같이 쓰는 값: 조회 결과 모음, 엑셀 창 자리, 화면 배율, 지금 설정.</summary>
internal sealed record MarkContext(DataScope Scope, PixelRect Excel, double Scale, HunsuSettings Settings);

internal interface IMarkStyle
{
    string Name { get; }

    /// <summary>그릴 때마다 처음에 불러 모아 둔 칸을 비운다.</summary>
    void Begin();

    /// <summary>표시 선언 하나의 칸 자리(arr)를 받는다. panes는 엑셀 창의 칸 영역들.</summary>
    void Add(MarkSpec spec, System.Text.Json.JsonElement arr, IReadOnlyList<PixelRect> panes, MarkContext ctx);

    /// <summary>모은 칸으로 그림 계획을 만든다. 그릴 것이 없으면 null.</summary>
    SketchPlan? Build(MarkContext ctx);
}

internal static class MarkStyles
{
    public static readonly IReadOnlyList<IMarkStyle> All = [new PencilMarkStyle()];

    public static IMarkStyle? Find(string style) => All.FirstOrDefault(s => s.Name == style);
}

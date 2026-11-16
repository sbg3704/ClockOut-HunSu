// 기능: 수식 흐름 스케치의 "보이는 형태" 묶음(프로필). 같은 계획(모양·번호·순서)을 색연필, 형광 마커, 펜 세 가지 느낌으로 그린다. 셋 다 손글씨 캘리그라피 강약(처음 가늘게, 끝 날렵하게, 펜촉 방향)이 있다.
// 사용법: var look = SketchLook.ByName("marker");  FlowSketch.Plan(..., look)   이름 목록은 SketchLook.All(설정 창 선택지)
// 의존: 없음(순수 값). 새 프로필은 아래 표에 한 줄 더하면 된다.
namespace ClockOut.Hunsu.Core.Marks;

/// <param name="Name">설정에 저장되는 이름. Label은 설정 창에 보이는 이름.</param>
/// <param name="Width">획 굵기 배율. Alpha는 진하기 배율.</param>
/// <param name="Wobble">떨림(px). Bow는 화살표 휘어짐 배율.</param>
/// <param name="Retrace">같은 획을 얇게 한 번 더 덧그림.</param>
/// <param name="ShadeWidth">이웃 칸 칠하기 선 굵기(0이면 칠하지 않음). ShadeAlpha는 그 진하기.</param>
/// <param name="Grain">종이 결 무늬 붓을 쓰는지.</param>
/// <param name="Taper">캘리그라피 강약: 획 처음은 가늘게 시작해 굵어지고 끝은 날렵하게 빠지는 정도(0~1).</param>
/// <param name="Nib">납작한 펜촉 효과: 한 방향 획은 굵고 다른 방향 획은 가늘게(0~1).</param>
public sealed record SketchLook(string Name, string Label, double Width, double Alpha, double Wobble, double Bow, bool Retrace, double ShadeWidth, double ShadeAlpha, bool Grain, double Taper, double Nib)
{
    public static readonly SketchLook Pencil = new("pencil", "색연필", 1.0, 1.0, 1.6, 1.0, true, 1.5, 0.5, true, 0.55, 0.15);
    public static readonly SketchLook Marker = new("marker", "형광 마커", 2.3, 0.55, 0.8, 0.8, false, 8, 0.28, false, 0.3, 0.4);
    public static readonly SketchLook Ink = new("ink", "펜", 0.7, 1.0, 0.45, 0.6, false, 0, 0, false, 0.85, 0.6);

    public static readonly IReadOnlyList<SketchLook> All = [Pencil, Marker, Ink];
    public static IReadOnlyList<string> Names => [.. All.Select(l => l.Name)];

    /// <summary>이름으로 찾는다. 모르는 이름은 색연필.</summary>
    public static SketchLook ByName(string? name) => All.FirstOrDefault(l => l.Name == name) ?? Pencil;
}

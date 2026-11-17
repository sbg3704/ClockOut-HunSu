// 기능: 패널 룩(손그림 프로필) 이름과 값 한 벌. 세 가지(펜과 형광펜, 색연필 노트, 먹빛 유리와 분필) + 손그림 없는 기본. 화면과 레지스트리 없이 시험된다.
// 사용법: var spec = SkinChoice.ByName(settings.Skin);  spec.Kind(손그림 종류), spec.ForcedTheme(밝기를 룩이 정하면 "light"/"dark"), spec.Dictionary(Ui가 합칠 XAML 이름)
//   새 룩은 아래 표에 한 줄 + Ui/Theme/Skin<이름>.xaml 하나. 모듈은 건드리지 않는다(색은 테마 이름으로만 쓰므로 자동으로 따라간다).
// 의존: 없음(순수 값). 2026-11-17 사용자 결정: 세 룩 모두 프로필로 넣고 기본은 B.
namespace ClockOut.Hunsu.Core.Settings;

/// <summary>손그림 테두리·제목줄의 그리는 방식. None이면 손그림 없이 기존 모양.</summary>
public enum SkinKind { None, Pen, Pencil, Chalk }

/// <param name="Name">설정에 저장되는 이름. Label·Note는 설정 창 글.</param>
/// <param name="ForcedTheme">룩이 밝기를 정하면 "light"·"dark", 설정 theme을 따르면 null.</param>
/// <param name="Dictionary">Ui/Theme 안의 XAML 파일 이름(없으면 null).</param>
/// <param name="HandTitle">제목·머리글에 손글씨 글꼴(Nanum Pen Script)을 쓰는지.</param>
public sealed record SkinSpec(string Name, string Label, string Note, SkinKind Kind, string? ForcedTheme, string? Dictionary, bool HandTitle);

public static class SkinChoice
{
    public const string PenMarker = "penmarker";
    public const string PencilNote = "pencilnote";
    public const string Chalk = "chalk";
    public const string Plain = "plain";

    /// <summary>기본 룩(2026-11-17 사용자 결정: B).</summary>
    public const string Default = PenMarker;

    public static readonly IReadOnlyList<SkinSpec> All =
    [
        new(PenMarker, "B 펜과 형광펜", "흰 패널, 잉크 펜 선, 제목 밑 형광펜 칠", SkinKind.Pen, "light", "SkinPenMarker.xaml", true),
        new(PencilNote, "A 색연필 노트", "종이 질감, 연필 테두리, 색연필 빗금 제목줄", SkinKind.Pencil, "light", "SkinPencilNote.xaml", true),
        new(Chalk, "C 먹빛 유리와 분필", "어두운 반투명, 분필 선, 네온 형광펜", SkinKind.Chalk, "dark", "SkinChalk.xaml", true),
        new(Plain, "기본(손그림 없음)", "이전 모양. 화면 테마(밝게·어둡게)를 따름", SkinKind.None, null, null, false),
    ];

    public static IReadOnlyList<string> Names => [.. All.Select(s => s.Name)];

    /// <summary>모르는 이름은 기본 룩.</summary>
    public static string Normalize(string? name)
    {
        string n = (name ?? "").Trim().ToLowerInvariant();
        return All.Any(s => s.Name == n) ? n : Default;
    }

    public static SkinSpec ByName(string? name) => All.First(s => s.Name == Normalize(name));
}

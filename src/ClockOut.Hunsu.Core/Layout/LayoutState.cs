// 기능: 사용자가 바꾼 배치(자리, 접힘, 투명도, 핀 창 위치)를 담고 JSON으로 읽고 쓴다. 모듈 선언과 합칠 때 사용자 값이 먼저다(docs/01 6.1장).
// 사용법: var s = LayoutState.FromJson(text); var eff = s.Effective(manifestId, manifestPlace, manifestOrder); s.Update(id, p => p with {...}); s.ToJson()
// 의존: System.Text.Json, Places, PixelRect. 파일 읽기·쓰기는 앱이 맡는다(이 클래스는 글만 다룸). 셀 값이나 경로는 담지 않는다.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClockOut.Hunsu.Core.Layout;

/// <summary>모듈 하나의 사용자 배치. null은 "사용자가 정하지 않음 = 선언 값을 따름".</summary>
public sealed record ModulePlacement
{
    public string? Place { get; init; }
    public int? Order { get; init; }
    public bool Collapsed { get; init; }
    public int Opacity { get; init; }
    public PixelRect? PinRect { get; init; }
    public bool Hidden { get; init; }

    /// <summary>사용자가 모듈 관리자에서 켜고 끈 값. null은 "아직 정하지 않음 = 설정(settings.json)의 기본값을 따름".</summary>
    public bool? Enabled { get; init; }
}

/// <summary>선언과 사용자 값을 합친 결과.</summary>
public sealed record EffectivePlacement(string Place, int Order, bool Collapsed, int Opacity, PixelRect? PinRect, bool Hidden);

public sealed class LayoutState
{
    public const int CurrentSpec = 0;
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public int Spec { get; init; } = CurrentSpec;
    public Dictionary<string, ModulePlacement> Modules { get; init; } = new(StringComparer.Ordinal);

    /// <summary>글을 읽는다. 읽을 수 없으면 빈 배치(기본값)로 시작하고 ok=false.</summary>
    public static (LayoutState State, bool Ok) FromJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (new LayoutState(), true);
        try
        {
            var s = JsonSerializer.Deserialize<LayoutState>(text, Json);
            if (s == null || s.Spec > CurrentSpec) return (new LayoutState(), false);
            return (new LayoutState { Spec = s.Spec, Modules = new Dictionary<string, ModulePlacement>(s.Modules, StringComparer.Ordinal) }, true);
        }
        catch (JsonException)
        {
            return (new LayoutState(), false);
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public ModulePlacement Get(string id) => Modules.TryGetValue(id, out var p) ? p : new ModulePlacement();

    public void Update(string id, Func<ModulePlacement, ModulePlacement> change) => Modules[id] = change(Get(id));

    /// <summary>사용자 값 > 선언 값. Hidden은 "꺼짐"(관리자에서 끈 것, 또는 아직 정하지 않았고 기본값이 꺼짐). 저장된 자리가 지금 선언의 모양으로 놓을 수 없는 자리면 선언 자리로 되돌린다.</summary>
    public EffectivePlacement Effective(string id, string declaredPlace, int declaredOrder, IReadOnlyCollection<string> shapes, bool enabledByDefault = true)
    {
        var p = Get(id);
        string place = p.Place != null && Places.CanPlace(p.Place, shapes) ? p.Place : declaredPlace;
        return new EffectivePlacement(place, p.Order ?? declaredOrder, p.Collapsed, Math.Clamp(p.Opacity, 0, OpacityLevels.Rest.Length - 1), p.PinRect, !(p.Enabled ?? enabledByDefault));
    }
}

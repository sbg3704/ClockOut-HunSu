// 기능: 모듈이 선언한 단축키 글("Ctrl+Alt+D")을 전역 단축키 값(수식키 비트 + 가상 키 코드)으로 읽는다. 훈수꾼 자체 단축키(Ctrl+Alt+H, Ctrl+Alt+Shift+H)와 겹치면 거절한다.
// 사용법: if (HotkeyChord.TryParse("Ctrl+Alt+D", out var c)) RegisterHotKey(c.Mods, c.Key);   c.Text는 보기 좋게 다듬은 글.
// 의존: HotkeyPolicy(수식키 비트). 순수 계산이라 화면 없이 시험된다. 키는 영문 A~Z, 0~9, F1~F12만(키보드 배열에 덜 흔들리는 것).
namespace ClockOut.Hunsu.Core;

public readonly record struct HotkeyChord(int Mods, int Key, string Text)
{
    public static bool TryParse(string? text, out HotkeyChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        int mods = 0, key = 0;
        bool ctrl = false, alt = false, shift = false;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": ctrl = true; break;
                case "alt": alt = true; break;
                case "shift": shift = true; break;
                default:
                    if (key != 0) return false; // 키는 하나만
                    key = VirtualKey(raw);
                    if (key == 0) return false;
                    break;
            }
        }
        if (key == 0 || !(ctrl || alt)) return false; // 수식키 없이 글자 하나만 가로채면 타자를 막는다
        if (ctrl) mods |= HotkeyPolicy.ModControl;
        if (alt) mods |= HotkeyPolicy.ModAlt;
        if (shift) mods |= HotkeyPolicy.ModShift;
        if (key == HotkeyPolicy.Key && ctrl && alt) return false; // 부르기·전부 숨김과 겹침
        string name = key is >= 0x70 and <= 0x7B ? "F" + (key - 0x6F) : ((char)key).ToString();
        chord = new HotkeyChord(mods | HotkeyPolicy.ModNoRepeat, key, string.Join("+", new[] { ctrl ? "Ctrl" : "", alt ? "Alt" : "", shift ? "Shift" : "", name }.Where(s => s.Length > 0)));
        return true;
    }

    private static int VirtualKey(string s)
    {
        string u = s.ToUpperInvariant();
        if (u.Length == 1 && (u[0] is >= 'A' and <= 'Z' || u[0] is >= '0' and <= '9')) return u[0];
        if (u[0] == 'F' && int.TryParse(u.AsSpan(1), out int n) && n is >= 1 and <= 12) return 0x6F + n;
        return 0;
    }
}

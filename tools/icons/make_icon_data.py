# 기능: vendor/icons의 아이콘 세트 압축 파일(통째로 받아 둔 SVG)에서 profiles.json에 짝지은 그림만 꺼내
#       WPF가 읽는 경로 문자열로 바꿔 src/ClockOut.Hunsu.Ui/Icons/IconData.g.cs를 만든다(실행 중 SVG를 해석하지 않게, docs/09 3.1장).
# 사용법: python tools/icons/make_icon_data.py   (저장소 맨 위에서. 결과 파일을 커밋한다. 세트 판을 바꾸면 vendor/icons와 SOURCES.md도 함께)
# 의존: 파이썬 3.10+ 표준 라이브러리만. 입력: vendor/icons/profiles.json(뜻 이름 → 세트별 그림 이름), vendor/icons/<세트>/*.zip
import json, os, re, sys, zipfile
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
VENDOR = os.path.join(ROOT, 'vendor', 'icons')
OUT = os.path.join(ROOT, 'src', 'ClockOut.Hunsu.Ui', 'Icons', 'IconData.g.cs')
# 세트: (압축 파일 이름, 그림 이름 → 압축 안 파일 이름, 선 그림인가)
SETS = {
    'fluent':   ('fluentui-svg-icons-1.1.345-24-regular.zip', '{}_24_regular.svg', False),
    'lucide':   ('lucide-static-1.66.0.zip', '{}.svg', True),
    'phosphor': ('phosphor-core-2.1.1-regular.zip', '{}.svg', False),
    'tabler':   ('tabler-icons-3.50.0-outline.zip', '{}.svg', True),
    'material': ('material-symbols-svg-400-0.48.2-outlined.zip', '{}.svg', False),
    'iconoir':  ('iconoir-7.12.1-regular.zip', '{}.svg', True),
}
NUM = re.compile(r'[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?')

def fmt(v):
    s = f'{float(v):.3f}'.rstrip('0').rstrip('.')
    return '0' if s in ('-0', '') else s

def norm_path(d):
    """SVG 경로를 WPF 경로 문법으로: 숫자 사이를 띄우고 호(A)의 붙은 깃발(011)을 나눈다."""
    out, i, cmd, argi = [], 0, None, 0
    while i < len(d):
        c = d[i]
        if c in ' ,\t\r\n': i += 1; continue
        if c.isalpha():
            cmd, argi = c, 0; out.append(c); i += 1; continue
        if cmd in ('A', 'a') and argi % 7 in (3, 4):
            if c not in '01': raise ValueError('호 깃발 틀림: ' + d[i:i + 10])
            out.append(c); i += 1; argi += 1; continue
        m = NUM.match(d, i)
        if not m: raise ValueError('경로를 못 읽음: ' + d[i:i + 10])
        out.append(fmt(m.group())); i = m.end(); argi += 1
    return ' '.join(out)

def f(e, k, dflt=0.0):
    return float(e.get(k, dflt))

def shape_path(e):
    tag = e.tag.split('}')[-1]
    if tag == 'path': return norm_path(e.get('d', ''))
    if tag in ('circle', 'ellipse'):
        cx, cy = f(e, 'cx'), f(e, 'cy')
        rx = f(e, 'r') if tag == 'circle' else f(e, 'rx'); ry = f(e, 'r') if tag == 'circle' else f(e, 'ry')
        return f'M {fmt(cx - rx)} {fmt(cy)} A {fmt(rx)} {fmt(ry)} 0 1 0 {fmt(cx + rx)} {fmt(cy)} A {fmt(rx)} {fmt(ry)} 0 1 0 {fmt(cx - rx)} {fmt(cy)} Z'
    if tag == 'rect':
        x, y, w, h = f(e, 'x'), f(e, 'y'), f(e, 'width'), f(e, 'height')
        rx = float(e.get('rx', e.get('ry', 0))); ry = float(e.get('ry', e.get('rx', 0)))
        rx, ry = min(rx, w / 2), min(ry, h / 2)
        if rx <= 0: return f'M {fmt(x)} {fmt(y)} H {fmt(x + w)} V {fmt(y + h)} H {fmt(x)} Z'
        return (f'M {fmt(x + rx)} {fmt(y)} H {fmt(x + w - rx)} A {fmt(rx)} {fmt(ry)} 0 0 1 {fmt(x + w)} {fmt(y + ry)} V {fmt(y + h - ry)} '
                f'A {fmt(rx)} {fmt(ry)} 0 0 1 {fmt(x + w - rx)} {fmt(y + h)} H {fmt(x + rx)} A {fmt(rx)} {fmt(ry)} 0 0 1 {fmt(x)} {fmt(y + h - ry)} '
                f'V {fmt(y + ry)} A {fmt(rx)} {fmt(ry)} 0 0 1 {fmt(x + rx)} {fmt(y)} Z')
    if tag == 'line':
        return f'M {fmt(f(e, "x1"))} {fmt(f(e, "y1"))} L {fmt(f(e, "x2"))} {fmt(f(e, "y2"))}'
    if tag in ('polyline', 'polygon'):
        pts = [fmt(v) for v in NUM.findall(e.get('points', ''))]
        s = 'M ' + ' '.join(pts[:2]) + ' L ' + ' '.join(pts[2:])
        return s + (' Z' if tag == 'polygon' else '')
    return None

def convert(svg, stroke):
    root = ET.fromstring(svg)
    vb = [float(v) for v in NUM.findall(root.get('viewBox', '0 0 24 24'))]
    parts, evenodd, width = [], False, float(root.get('stroke-width', 2 if stroke else 0) or 0)
    for e in root.iter():
        if e is root: continue
        tag = e.tag.split('}')[-1]
        if tag in ('g', 'title', 'desc', 'defs'):
            if e.get('transform'): raise ValueError('transform은 아직 못 다룸')
            continue
        if e.get('transform'): raise ValueError('transform은 아직 못 다룸')
        if stroke and e.get('stroke') == 'none': continue
        if not stroke and e.get('fill') == 'none': continue
        p = shape_path(e)
        if p is None: raise ValueError('모르는 모양: ' + tag)
        parts.append(p)
        if e.get('fill-rule') == 'evenodd' or e.get('clip-rule') == 'evenodd': evenodd = True
        if e.get('stroke-width'): width = float(e.get('stroke-width'))
    data = ('F0 ' if evenodd else 'F1 ') + ' '.join(parts)
    return data, vb, width

def main():
    profiles = json.load(open(os.path.join(VENDOR, 'profiles.json'), encoding='utf-8'))
    lines = []
    for set_name, (zip_name, pattern, stroke) in SETS.items():
        z = zipfile.ZipFile(os.path.join(VENDOR, set_name, zip_name))
        lines.append(f'        ["{set_name}"] = new Dictionary<string, IconGlyph>(StringComparer.Ordinal)')
        lines.append('        {')
        for key in sorted(profiles):
            name = profiles[key].get(set_name)
            if not name: continue
            data, vb, width = convert(z.read(pattern.format(name)).decode('utf-8'), stroke)
            lines.append(f'            ["{key}"] = new("{name}", "{data}", {fmt(vb[0])}, {fmt(vb[1])}, {fmt(vb[2])}, {fmt(vb[3])}, {"true" if stroke else "false"}, {fmt(width)}),')
        lines.append('        },')
    head = '''// 기능: 아이콘 프로필별 그림 데이터(자동 생성, 손으로 고치지 않는다). 뜻 이름 → 세트의 그림 경로·보기 상자·선 그림 여부.
// 사용법: IconFactory가 읽는다. 다시 만들기: python tools/icons/make_icon_data.py (vendor/icons/profiles.json과 세트 압축 파일에서)
// 의존: 없음. 출처와 라이선스는 vendor/SOURCES.md, vendor/icons/<세트>/LICENSE.
namespace ClockOut.Hunsu.Ui.Icons;

internal static class IconData
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, IconGlyph>> Profiles = new Dictionary<string, IReadOnlyDictionary<string, IconGlyph>>(StringComparer.Ordinal)
    {
'''
    with open(OUT, 'w', encoding='utf-8', newline='\r\n') as fo:
        fo.write(head + '\n'.join(lines) + '\n    };\n}\n')
    print('wrote', OUT, sum(len(v) for v in profiles.values()), 'icons')

if __name__ == '__main__':
    main()

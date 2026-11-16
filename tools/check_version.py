# 기능: 프로그램을 바꾼 PR이 판 번호(Directory.Build.props의 Version)를 올렸는지 확인한다. 안 올렸거나 모양이 X.Y.ZZZ가 아니거나 main보다 낮으면 실패(종료 코드 1).
# 사용법: python3 tools/check_version.py [기준 브랜치=origin/main]    (tools\build.ps1이 부른다. 기준을 못 찾으면 모양만 보고 통과)
# 의존: git, python3. 파일을 바꾸지 않는다. "프로그램"은 src/, modules/, addins/ 아래를 말한다(문서·협업·tools만 바꾼 PR은 올리지 않아도 된다).
import re, subprocess, sys

base = sys.argv[1] if len(sys.argv) > 1 else "origin/main"
PROGRAM = ("src/", "modules/", "addins/", "Directory.Build.props")

def git(*a):
    r = subprocess.run(["git", *a], capture_output=True, text=True)
    return r.returncode, r.stdout

def version(text, strict=False):
    m = re.search(r"<Version>(\d+)\.(\d+)\.(\d{3,})</Version>" if strict else r"<Version>(\d+)\.(\d+)\.(\d+)</Version>", text or "")
    return tuple(int(x) for x in m.groups()) if m else None

def show(v):
    return f"{v[0]}.{v[1]}.{v[2]:03d}" if v else "?"

cur = version(open("Directory.Build.props", encoding="utf-8-sig").read(), strict=True)
if cur is None:
    print("[실패] Directory.Build.props의 <Version>이 X.Y.ZZZ(끝 세 자리) 모양이 아닙니다."); sys.exit(1)
rc, old_text = git("show", f"{base}:Directory.Build.props")
if rc != 0:
    print(f"[알림] 기준 {base}를 찾지 못해 모양만 확인했습니다(판 {show(cur)})."); sys.exit(0)
old = version(old_text)
rc, names = git("diff", "--name-only", f"{base}...HEAD")
changed = [n for n in names.splitlines() if n.startswith(PROGRAM)] if rc == 0 else []
if changed and old is not None and cur <= old:
    print(f"[실패] 프로그램 파일이 바뀌었는데({len(changed)}개) 판 번호가 그대로입니다: {show(cur)} (main {show(old)}).")
    print("  Directory.Build.props의 <Version>을 올리세요(+1 고침, +10 기능, +100 큰 변화). 다른 계정의 열린 PR과 같은 번호를 고르지 않게 협업 노트를 보세요.")
    sys.exit(1)
print(f"판 번호 확인 통과: {show(cur)} (main {show(old)})")

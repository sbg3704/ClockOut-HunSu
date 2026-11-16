#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""
기능: 훈수꾼 시나리오 통합 문서(tools/scenarios/훈수꾼_시나리오.xlsx)를 만든다. 모듈마다 "어디를 보면 무엇이 나와야 하는지"가 시트와 안내 표로 들어 있다.
사용법(클라우드/개발자 PC, 엑셀 불필요): python3 tools/make_scenarios.py   (XlsxWriter 필요: pip install xlsxwriter)
      모듈을 더하면 아래 SCENARIOS 표에 한 줄, 필요하면 시트 함수 하나를 더하고 다시 만들어 xlsx를 커밋한다(docs/07 4장 규칙).
      PC 빌드(co build hunsu)는 이 파일을 out\ 로 복사하고, 큰 시트(큰 표, 대용량)는 tools\make_example_xlsx.ps1이 엑셀로 더한다.
의존: xlsxwriter. 값은 전부 시험용 가짜 자료다(회사 자료 없음). 외부 링크는 xlsx 안의 XML을 직접 더해 만든다(XlsxWriter에 없음).
"""
import re
import shutil
import sys
import zipfile
from pathlib import Path

import xlsxwriter

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "scenarios" / "훈수꾼_시나리오.xlsx"

# 모듈 id, 이름, 시트, 해볼 것, 나와야 하는 것  (모든 modules/ 폴더가 여기 있어야 한다: ModuleToolsTests가 검사)
SCENARIOS = [
    ("hunsu-selection", "선택 요약", "매출", "B3:F14를 마우스로 고른다", "개수 60과 합계·평균·최소·최대가 나온다. 범위를 바꾸면 바로 바뀐다."),
    ("hunsu-selection", "선택 요약", "섞임", "A2:A12를 고른다", "숫자·글자·수식 개수가 나오고 오류 3개 배지가 보인다."),
    ("hunsu-selection", "선택 요약", "큰 표 / 대용량", "A열 머리글을 눌러 열 전체를 고른다. 큰 범위를 마우스로 끌어 본다", "104만 칸이어도 바로 나오고 패널이 떨리지 않는다(docs/06)."),
    ("hunsu-errors", "오류 셀", "오류", "시트를 연다(아무 칸이나)", "오류 셀 7개(종류별 1개씩). 목록 줄을 누르면 그 칸으로 이동한다."),
    ("hunsu-sheets", "시트 이동", "아무 시트", "패널의 시트 이름을 누른다", "보이는 시트만 나열되고(숨김_시트, 아주숨김_시트는 없음) 누르면 그 시트로 이동, 지금 시트가 강조된다."),
    ("hunsu-flow", "수식 흐름", "흐름", "B1, D1, E1을 차례로 고른다", "B1: 쓰는 칸 A1 1개, 쓰는 칸을 쓰는 C1·D1 2개. D1: 쓰는 칸 3개. E1: '다른 시트도 사용합니다' 배지. 목록을 누르면 이동."),
    ("hunsu-book", "책 정보", "아무 시트", "아무 칸에 값을 쓴다. 수식 탭 > 계산 옵션 > 수동으로 바꿔 본다(끝나면 자동으로 되돌린다)", "저장 안 된 변경 배지, 수동 계산 경고(빨강)가 나온다. 시트 수는 17개(큰 시트를 더하면 19)."),
    ("hunsu-audit", "책 점검", "아무 시트", "패널을 본다", "숨은 시트 1(숨김_시트), 아주 숨김 1, 보호된 시트 1(보호됨), 외부 링크 1개."),
    ("hunsu-names", "이름 목록", "이름", "패널의 이름을 누른다", "보이는 이름 5개(엑셀 이름 관리자 기준. Names.Count는 숨은 이름 2개가 더해 7). 세율·합계범위는 눌러 이동, 버전표시·오늘·깨진이름은 '수식·상수'라 이동하지 않는다."),
    ("hunsu-keys", "단축키 표", "아무 시트", "패널을 본다", "다리 없이 단축키 표가 나온다."),
    ("hunsu-cell", "셀 정보", "셀정보", "A2부터 A12까지 한 칸씩 고른다", "각 칸 옆(C열)에 적힌 기대와 같다: 수식·종류(숫자/날짜/텍스트/참거짓/오류/빈 칸)·서식·병합·메모·잠금."),
    ("hunsu-sheetinfo", "시트 정보", "필터 / 보호됨 / 매출", "필터 시트: 필터 적용 중. 보호됨: 시트 보호됨. 매출: 틀 고정", "사용 범위 크기, 보호, 필터, 틀 고정, 조건부 서식 수가 맞게 나온다."),
    ("hunsu-composition", "시트 구성", "매출", "시트를 연다", "수식 24 · 숫자 60 · 글자 22 · 그 밖 6(사용 범위 112칸)."),
    ("hunsu-comments", "메모와 댓글", "메모", "시트를 연다", "메모 4개 목록. 줄을 누르면 그 칸으로 이동. 스레드 댓글은 직접 하나 달아 보면 '댓글'로 늘어난다."),
    ("hunsu-links", "외부 링크", "링크", "파일을 열 때 링크 보안 경고가 뜨면 '업데이트 안 함'", "원본_연결용.xlsx 1개(파일 이름만, 경로 없음)."),
    ("hunsu-tables", "표 목록", "표", "시트를 연다", "표 3개(매출표, 직원표, 재고표)와 줄 수. 누르면 그 표로 이동."),
    ("hunsu-validation", "유효성 영역", "유효성", "시트를 연다", "영역 4곳 · 40칸(목록, 정수, 날짜, 사용자 수식). 누르면 이동."),
    ("hunsu-condrules", "조건부 서식", "조건부", "시트를 연다", "규칙 10개(셀 값, 수식, 색조, 데이터 막대, 아이콘, 상위, 중복, 텍스트, 빈 칸, 오류). 누르면 이동."),
    ("hunsu-hidden", "숨은 행·열", "숨김행열", "시트를 연다", "숨은 행 3개(5, 6, 10행)·숨은 열 2개(D, F열). 누르면 그곳으로 이동."),
]

SHEETS = ["안내", "매출", "섞임", "오류", "흐름", "이름", "메모", "표", "유효성", "조건부", "숨김행열", "필터", "셀정보", "보호됨", "링크", "숨김_시트", "아주숨김_시트"]


def build(path: Path) -> None:
    wb = xlsxwriter.Workbook(str(path))
    wb.set_properties({"title": "훈수꾼 시나리오", "comments": "시험용 가짜 자료입니다. 훈수꾼은 셀 값을 바꾸지 않습니다."})
    bold = wb.add_format({"bold": True})
    title = wb.add_format({"bold": True, "font_size": 16})
    head = wb.add_format({"bold": True, "bg_color": "#DDEBF7", "border": 1, "text_wrap": True, "valign": "top"})
    cell = wb.add_format({"border": 1, "text_wrap": True, "valign": "top"})
    muted = wb.add_format({"font_color": "#808080"})
    num1 = wb.add_format({"num_format": "#,##0.0"})
    numb = wb.add_format({"num_format": "#,##0.0", "bold": True})
    datef = wb.add_format({"num_format": "yyyy-mm-dd"})
    ws = {n: wb.add_worksheet(n) for n in SHEETS}

    # --- 안내 ---
    a = ws["안내"]
    a.set_column("A:A", 18); a.set_column("B:B", 16); a.set_column("C:C", 44); a.set_column("D:D", 70)
    lines = [
        ("훈수꾼 시나리오 통합 문서", title),
        ("이 파일 하나로 모든 모듈을 시험합니다. 값은 전부 시험용 가짜 자료이고, 훈수꾼은 셀 값을 바꾸지 않습니다.", None),
        ("", None),
        ("0. 모든 모듈 켜기: 훈수꾼 알림 영역 아이콘 → '모듈 관리…' → [모두 켜기]. (또는 settings.json의 enabledByDefault에 [\"*\"])", None),
        ("1. 엑셀 창 오른쪽(또는 왼쪽)에 모듈 알약이 붙습니다. 아래 표의 '해볼 것'을 하고 '나와야 하는 것'과 같은지 보세요.", None),
        ("2. 다르거나 화면이 떨리거나 엑셀이 버벅이면, 어느 모듈·어느 칸인지 적어서 알려 주세요(빌드 기록은 co copy).", None),
        ("3. 파일을 열 때 '링크 업데이트' 보안 경고가 뜨는 것은 정상입니다(링크 시나리오). [업데이트 안 함]을 누르세요.", None),
        ("4. 큰 시트(큰 표 2만 줄, 대용량 10만 줄)는 co build 중에 엑셀로 더합니다. 없으면 빌드 기록의 경고를 알려 주세요.", None),
        ("", None),
    ]
    for i, (t, f) in enumerate(lines):
        a.write(i, 0, t, f)
    r0 = len(lines)
    for c, h in enumerate(["모듈 id", "이름 · 시트", "해볼 것", "나와야 하는 것"]):
        a.write(r0, c, h, head)
    for i, (mid, name, sheet, todo, expect) in enumerate(SCENARIOS, start=1):
        a.write(r0 + i, 0, mid, cell)
        a.write(r0 + i, 1, f"{name} · {sheet}", cell)
        a.write(r0 + i, 2, todo, cell)
        a.write(r0 + i, 3, expect, cell)
    a.write(r0 + len(SCENARIOS) + 2, 0, "새 모듈을 더하면 이 표(tools/make_scenarios.py의 SCENARIOS)에 시나리오를 더하고 파일을 다시 만듭니다.", muted)

    # --- 매출: 12달 x 5팀 (선택 요약, 구성, 틀 고정) ---
    s = ws["매출"]
    s.write("A1", "월별 매출(만 원)", bold)
    s.write("A2", "월")
    teams = ["영업1팀", "영업2팀", "기술팀", "지원팀", "해외팀"]
    for t, name in enumerate(teams):
        s.write(1, 1 + t, name)
    s.write("G2", "합계")
    for m in range(12):
        s.write(2 + m, 0, f"{m + 1}월")
        row_sum = 0.0
        for t in range(5):
            v = round(800 + ((m + 3) * 131 + (t + 2) * 57) % 1700 + (((m + 3) * (t + 2)) % 10) / 10, 1)
            s.write_number(2 + m, 1 + t, v, num1)
            row_sum += v
        s.write_formula(2 + m, 6, f"=SUM(B{3 + m}:F{3 + m})", num1, round(row_sum, 1))
    s.write("A15", "합계", bold); s.write("A16", "평균", bold)
    for c in "BCDEFG":
        s.write_formula(f"{c}15", f"=SUM({c}3:{c}14)", numb)
        s.write_formula(f"{c}16", f"=AVERAGE({c}3:{c}14)", numb)
    s.freeze_panes(2, 1)
    s.set_column("A:G", 11)

    # --- 섞임 ---
    s = ws["섞임"]
    s.write("A1", "여러 종류(숫자·글자·날짜·빈칸·수식·오류)", bold)
    s.write_number("A2", 120); s.write_number("A3", 45.5); s.write("A4", "글자")
    s.write_formula("A5", "=DATE(2026,11,12)", datef, 46338)
    s.write_formula("A7", "=A2*2", None, 240)
    s.write_formula("A8", "=A2/0", None, "#DIV/0!")
    s.write_formula("A9", "=NA()", None, "#N/A")
    s.write_formula("A10", '=VLOOKUP("없음",A2:A4,1,FALSE)', None, "#N/A")
    s.write_number("A11", -30); s.write("A12", "메모")
    s.write("B2", "← A2:A12를 골라 보세요: 오류 3개, 수식 5개", muted)
    s.set_column("A:A", 14)

    # --- 오류: 종류별 7개 ---
    s = ws["오류"]
    s.write("A1", "오류 셀 7개(종류별)", bold)
    s.write("B2", "입력"); s.write_number("A2", 1); s.write_number("A3", 2)
    errs = [("C2", "=1/0", "#DIV/0!"), ("C3", "=NA()", "#N/A"), ("C4", "=#REF!", "#REF!"), ("C5", "=FOO()", "#NAME?"),
            ("C6", '="가"+1', "#VALUE!"), ("C7", "=SQRT(-1)", "#NUM!"), ("C8", "=A2:A3 B2:B3", "#NULL!")]
    for addr, f, v in errs:
        s.write_formula(addr, f, None, v)
    s.write("D2", "← 각 줄: #DIV/0!, #N/A, #REF!, #NAME?, #VALUE!, #NUM!, #NULL!", muted)

    # --- 흐름 ---
    s = ws["흐름"]
    s.write("A1", 10)
    s.write_formula("B1", "=A1*2", None, 20); s.write_formula("C1", "=B1+A1", None, 30)
    s.write_formula("D1", "=SUM(A1:C1)", None, 60); s.write_formula("E1", "=D1+매출!G15", None, 0)
    s.write("A3", "B1: 쓰는 칸 A1, 쓰는 칸을 쓰는 C1·D1 / D1: 쓰는 칸 3개 / E1: 다른 시트 배지", muted)

    # --- 이름 ---
    s = ws["이름"]
    s.write("A1", "이름(Name) 5개", bold)
    s.write("A2", "세율"); s.write_number("B2", 0.1)
    wb.define_name("세율", "=이름!$B$2")
    wb.define_name("합계범위", "=매출!$B$3:$B$14")
    wb.define_name("버전표시", '="v1.0"')
    wb.define_name("오늘", "=TODAY()")
    wb.define_name("깨진이름", "=#REF!")
    s.write("A4", "← 세율, 합계범위는 눌러 이동 / 버전표시, 오늘, 깨진이름은 수식·상수", muted)

    # --- 메모: 노트 4개 ---
    s = ws["메모"]
    s.write("A1", "메모(노트) 4개", bold)
    for i, (addr, txt) in enumerate([("B3", "확인 필요: 합계가 안 맞음"), ("D5", "이 값은 분기 말에 갱신"), ("B8", "담당자에게 물어볼 것"), ("E10", "삭제 예정")]):
        s.write(addr, f"값{i + 1}")
        s.write_comment(addr, txt, {"author": "시험", "x_scale": 1.5})

    # --- 표(ListObject) 3개 ---
    s = ws["표"]
    s.write("A1", "표 3개", bold)
    s.add_table("B3:E8", {"name": "매출표", "columns": [{"header": h} for h in ["월", "영업", "기술", "지원"]],
                         "data": [[f"{m}월", 100 + m * 3, 80 + m * 2, 60 + m] for m in range(1, 6)]})
    s.add_table("B11:D14", {"name": "직원표", "columns": [{"header": h} for h in ["이름", "팀", "나이"]],
                           "data": [["김가", "영업", 31], ["이나", "기술", 27], ["박다", "지원", 35]]})
    s.add_table("G3:H12", {"name": "재고표", "columns": [{"header": "품목"}, {"header": "수량"}],
                          "data": [[f"품목{i}", i * 5] for i in range(1, 10)]})

    # --- 유효성 4영역 ---
    s = ws["유효성"]
    s.write("A1", "데이터 유효성 4영역(각 10칸)", bold)
    s.write("B1", "목록"); s.write("D1", "정수 1~100"); s.write("F1", "2026년 날짜"); s.write("H1", "사용자 수식(짝수만)")
    s.data_validation("B2:B11", {"validate": "list", "source": ["예", "아니오", "보류"]})
    s.data_validation("D2:D11", {"validate": "integer", "criteria": "between", "minimum": 1, "maximum": 100})
    s.data_validation("F2:F11", {"validate": "date", "criteria": "between", "minimum": __import__("datetime").date(2026, 1, 1), "maximum": __import__("datetime").date(2026, 12, 31)})
    s.data_validation("H2:H11", {"validate": "custom", "value": "=MOD(H2,2)=0"})

    # --- 조건부 서식 10규칙 ---
    s = ws["조건부"]
    s.write("A1", "조건부 서식 10규칙(열마다 하나)", bold)
    red = wb.add_format({"bg_color": "#FFC7CE"})
    for c in range(10):
        for r in range(2, 12):
            s.write_number(r, c, (r * 7 + c * 13) % 20)
    s.conditional_format("A3:A12", {"type": "cell", "criteria": ">", "value": 10, "format": red})
    s.conditional_format("B3:B12", {"type": "formula", "criteria": "=B3<5", "format": red})
    s.conditional_format("C3:C12", {"type": "3_color_scale"})
    s.conditional_format("D3:D12", {"type": "data_bar"})
    s.conditional_format("E3:E12", {"type": "icon_set", "icon_style": "3_arrows"})
    s.conditional_format("F3:F12", {"type": "top", "value": 3, "format": red})
    s.conditional_format("G3:G12", {"type": "duplicate", "format": red})
    s.conditional_format("H3:H12", {"type": "text", "criteria": "containing", "value": "1", "format": red})
    s.conditional_format("I3:I12", {"type": "blanks", "format": red})
    s.conditional_format("J3:J12", {"type": "errors", "format": red})

    # --- 숨김행열: 행 5,6,10 / 열 D,F ---
    s = ws["숨김행열"]
    s.write("A1", "숨은 행 3개(5, 6, 10행), 숨은 열 2개(D, F열)", bold)
    for r in range(1, 14):
        for c in range(7):
            s.write_number(r, c, r * 10 + c)
    for r in (4, 5, 9):
        s.set_row(r, None, None, {"hidden": True})
    s.set_column("D:D", None, None, {"hidden": True})
    s.set_column("F:F", None, None, {"hidden": True})

    # --- 필터: 자동 필터 + 필터 적용 중 ---
    s = ws["필터"]
    s.write_row("A1", ["이름", "점수", "팀"])
    data = [(f"사람{i}", (i * 17) % 100, ["가", "나", "다"][i % 3]) for i in range(1, 21)]
    for i, row in enumerate(data, start=1):
        s.write_row(i, 0, row)
    s.autofilter("A1:C21")
    s.filter_column(1, "x >= 50")
    for i, row in enumerate(data, start=1):
        if row[1] < 50:
            s.set_row(i, None, None, {"hidden": True})

    # --- 셀정보: 칸 종류별 ---
    s = ws["셀정보"]
    s.write("A1", "칸 하나씩 골라 보세요(기대는 C열)", bold)
    unl = wb.add_format({"locked": False})
    pct = wb.add_format({"num_format": "0.0%"})
    s.write_number("A2", 1234.5, num1); s.write("C2", "숫자, 서식 #,##0.0", muted)
    s.write("A3", "글자"); s.write("C3", "텍스트", muted)
    s.write_number("A4", 46338, datef); s.write("C4", "날짜, 서식 yyyy-mm-dd", muted)
    s.write_boolean("A5", True); s.write("C5", "참/거짓", muted)
    s.write_formula("A6", "=A2*2", num1, 2469); s.write("C6", "수식 =A2*2, 숫자", muted)
    long_f = "=" + "+".join(["A2"] * 80)
    s.write_formula("A7", long_f, None, 98760); s.write("C7", "긴 수식(200글자 넘으면 앞부분만 …)", muted)
    s.write("C8", "A8은 빈 칸 → 빈 칸", muted)
    s.write_formula("A9", "=1/0", None, "#DIV/0!"); s.write("C9", "오류 값(수식)", muted)
    s.merge_range("A10:B10", "병합된 칸", cell); s.write("C10", "병합됨", muted)
    s.write("A11", "메모 있음"); s.write_comment("A11", "셀 정보 시험용 메모", {"author": "시험"}); s.write("C11", "메모 있음", muted)
    s.write_number("A12", 0.256, pct); s.write("A13", "잠금 해제", unl); s.write("C12", "숫자, 서식 0.0%", muted); s.write("C13", "잠금 해제(보호 시 수정 가능)", muted)
    s.set_column("A:A", 20); s.set_column("C:C", 44)

    # --- 보호됨 ---
    s = ws["보호됨"]
    s.write("A1", "이 시트는 보호되어 있습니다(암호 없음)", bold)
    s.write("A2", "수정 불가"); s.write("B3", "수정 가능", unl)
    s.protect()

    # --- 링크: 외부 링크 1개(뒤에서 XML로 더함) ---
    s = ws["링크"]
    s.write("A1", "외부 링크 1개(원본_연결용.xlsx)", bold)
    s.write_formula("B2", "=[1]원본!$B$2", None, 7)
    s.write_formula("B3", "=[1]원본!$B$3*2", None, 14)
    s.write("C2", "← 다른 파일을 가리키는 수식(원본 파일은 없어도 됩니다)", muted)

    # --- 숨김/아주숨김 ---
    ws["숨김_시트"].write("A1", "숨겨진 시트(사용자가 다시 표시 가능)")
    ws["숨김_시트"].hide()
    ws["아주숨김_시트"].write("A1", "아주 숨김 시트(VBA로만 표시 가능)")
    ws["아주숨김_시트"].hide()

    ws["안내"].activate()
    wb.close()


EXT_XML = ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n'
           '<externalLink xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
           'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
           '<externalBook r:id="rId1"><sheetNames><sheetName val="원본"/></sheetNames>'
           '<sheetDataSet><sheetData sheetId="0"><row r="2"><cell r="B2"><v>7</v></cell></row>'
           '<row r="3"><cell r="B3"><v>7</v></cell></row></sheetData></sheetDataSet></externalBook></externalLink>')
EXT_RELS = ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLinkPath" '
            'Target="원본_연결용.xlsx" TargetMode="External"/></Relationships>')


def patch(src: Path, dst: Path) -> None:
    """외부 링크 부품을 더하고 '아주숨김_시트'를 veryHidden으로 바꾼다."""
    with zipfile.ZipFile(src) as zin, zipfile.ZipFile(dst, "w", zipfile.ZIP_DEFLATED) as zout:
        for item in zin.infolist():
            data = zin.read(item.filename)
            if item.filename == "[Content_Types].xml":
                t = data.decode("utf-8")
                t = t.replace("</Types>", '<Override PartName="/xl/externalLinks/externalLink1.xml" '
                              'ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.externalLink+xml"/></Types>')
                data = t.encode("utf-8")
            elif item.filename == "xl/workbook.xml":
                t = data.decode("utf-8")
                t, n = re.subn(r'(<sheet name="아주숨김_시트"[^>]*?)state="hidden"', r'\1state="veryHidden"', t)
                assert n == 1, "veryHidden 치환 실패"
                assert "</sheets>" in t
                t = t.replace("</sheets>", '</sheets><externalReferences><externalReference r:id="rIdExt1"/></externalReferences>', 1)
                data = t.encode("utf-8")
            elif item.filename == "xl/_rels/workbook.xml.rels":
                t = data.decode("utf-8")
                t = t.replace("</Relationships>", '<Relationship Id="rIdExt1" '
                              'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLink" '
                              'Target="externalLinks/externalLink1.xml"/></Relationships>')
                data = t.encode("utf-8")
            zout.writestr(item, data)
        zout.writestr("xl/externalLinks/externalLink1.xml", EXT_XML.encode("utf-8"))
        zout.writestr("xl/externalLinks/_rels/externalLink1.xml.rels", EXT_RELS.encode("utf-8"))


def main() -> int:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    tmp = OUT.with_suffix(".tmp.xlsx")
    build(tmp)
    patch(tmp, OUT)
    tmp.unlink()
    print("만들었습니다:", OUT, OUT.stat().st_size, "bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())

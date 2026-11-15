"""Keeps Content/Hitmen/Localization/HitmenText.csv (the translation sheet, edited in Excel) in step with the code.

    python Tools/Localization/hitmen_extract_text.py            # add new HITMEN_TEXT keys to the sheet
    python Tools/Localization/hitmen_extract_text.py --convert  # one-off: LOCTEXT / NSLOCTEXT -> HITMEN_TEXT
    python Tools/Localization/hitmen_extract_text.py --check    # list keys with an empty cell per language

The sheet: one row per text, columns Key;en;fr;... (UTF-8 with a BOM, ';' separated so French Excel opens it as
columns). Existing translations are kept; a key whose English changed in the code gets the new English and its other
cells are kept (check them). Rows no longer in the code are kept at the end (the game ignores them).
Adding a language: add a column (its two-letter code, e.g. "de") and fill it; the "_LanguageName" row names it in the
Options. Data-asset texts (weapon, ability names) are rows whose key starts with "Asset." (their English is matched).
"""
import csv
import glob
import io
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SOURCE = os.path.join(ROOT, "Source", "Hitmen")
SHEET = os.path.join(ROOT, "Content", "Hitmen", "Localization", "HitmenText.csv")

STRING = r'"((?:[^"\\]|\\.)*)"'
NSLOCTEXT = re.compile(r'NSLOCTEXT\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*' + STRING + r'\s*\)')
LOCTEXT = re.compile(r'(?<![A-Z_])LOCTEXT\(\s*"([^"]+)"\s*,\s*' + STRING + r'\s*\)')
HITMEN = re.compile(r'HITMEN_TEXT\(\s*"([^"]+)"\s*,\s*' + STRING + r'\s*\)')
NAMESPACE = re.compile(r'#define\s+LOCTEXT_NAMESPACE\s+"([^"]+)"')


def unescape(text):
    return bytes(text, "utf-8").decode("unicode_escape").encode("latin-1").decode("utf-8")


def source_files():
    return sorted(glob.glob(os.path.join(SOURCE, "**", "*.cpp"), recursive=True) + glob.glob(os.path.join(SOURCE, "**", "*.h"), recursive=True))


def convert():
    """LOCTEXT / NSLOCTEXT -> HITMEN_TEXT("<namespace>.<key>", "<English>") in every source file."""
    changed = 0
    for path in source_files():
        text = open(path, encoding="utf-8").read()
        match = NAMESPACE.search(text)
        namespace = match.group(1) if match else "Hitmen"
        new = NSLOCTEXT.sub(lambda m: 'HITMEN_TEXT("%s.%s", "%s")' % (m.group(1), m.group(2), m.group(3)), text)
        new = LOCTEXT.sub(lambda m: 'HITMEN_TEXT("%s.%s", "%s")' % (namespace, m.group(1), m.group(2)), new)
        if new != text:
            if '#include "Core/HitmenText.h"' not in new and not path.endswith("HitmenText.h"):
                first = re.search(r'^#include\s+"[^"]+"\s*$', new, re.M)
                if first:
                    new = new[:first.end()] + '\n#include "Core/HitmenText.h"' + new[first.end():]
            open(path, "w", encoding="utf-8", newline="").write(new)
            changed += 1
    print("converted", changed, "files")


def scan():
    keys = {}
    for path in source_files():
        for key, english in HITMEN.findall(open(path, encoding="utf-8").read()):
            keys.setdefault(key, unescape(english))
    return keys


def read_sheet():
    if not os.path.exists(SHEET):
        return ["en", "fr"], {}
    raw = open(SHEET, encoding="utf-8-sig").read()
    delimiter = ";" if raw.splitlines()[0].count(";") >= raw.splitlines()[0].count(",") else ","
    rows = list(csv.reader(io.StringIO(raw), delimiter=delimiter))
    columns = [c.strip().lower() for c in rows[0][1:]]
    table = {}
    for row in rows[1:]:
        if row and row[0].strip():
            table[row[0].strip()] = (row[1:] + [""] * len(columns))[:len(columns)]
    return columns, table


def write_sheet(columns, table, order):
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    with open(SHEET, "w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.writer(handle, delimiter=";", quoting=csv.QUOTE_MINIMAL)
        writer.writerow(["Key"] + columns)
        for key in order:
            writer.writerow([key] + table[key])


def update(extra=None):
    columns, table = read_sheet()
    if "en" not in columns:
        columns.insert(0, "en")
        table = {k: [""] + v for k, v in table.items()}
    english = columns.index("en")
    code = scan()
    if extra:
        code.update(extra)
    added = 0
    for key, text in code.items():
        if key not in table:
            table[key] = [""] * len(columns)
            added += 1
        table[key][english] = text
    if "_LanguageName" not in table:
        table["_LanguageName"] = [{"en": "English", "fr": "Français"}.get(c, c.upper()) for c in columns]
    order = ["_LanguageName"] + sorted(k for k in table if k in code) + sorted(k for k in table if k not in code and k != "_LanguageName")
    write_sheet(columns, table, order)
    print("sheet:", len(order), "rows,", added, "new, languages", " ".join(columns))
    return columns, table


def check():
    columns, table = read_sheet()
    for column in columns:
        missing = [k for k, v in table.items() if not v[columns.index(column)].strip()]
        print(column, "missing", len(missing))
        for key in missing[:40]:
            print("   ", key)


if __name__ == "__main__":
    if "--convert" in sys.argv:
        convert()
    if "--check" in sys.argv:
        check()
    else:
        update()

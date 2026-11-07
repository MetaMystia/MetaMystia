"""仓库一致性检查：文档链接、本地化键、网络消息登记。用法：python tools/check.py"""
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MOD = ROOT / "src/MetaMystia.Mod"
NETWORK = ROOT / "src/MetaMystia.Network"
LANGUAGES = ("zh-CN", "zh-TW", "en", "ja", "ko")
errors = []


def read(path):
    return path.read_text(encoding="utf-8-sig")


def tracked(*patterns):
    out = subprocess.run(["git", "ls-files", "-z", "--", *patterns], cwd=ROOT, capture_output=True, check=True)
    return [ROOT / p for p in out.stdout.decode().split("\0") if p and (ROOT / p).exists()]


def check_links():
    link = re.compile(r"\]\(([^)\s]+)\)")
    for md in tracked("*.md"):
        body = re.sub(r"```.*?```", "", read(md), flags=re.S)
        for target in link.findall(body):
            path = target.split("#")[0]
            if path and not re.match(r"[a-z][a-z0-9+.-]*:", path) and not (md.parent / path).exists():
                errors.append(f"{md.relative_to(ROOT)}: 链接目标不存在 {target}")
    mention = re.compile(r"\bdocs/[\w./-]+\.md")
    for source in tracked("*.cs", "*.py", "*.csx"):
        for target in mention.findall(read(source)):
            if not (ROOT / target).exists():
                errors.append(f"{source.relative_to(ROOT)}: 引用的文档不存在 {target}")


def placeholders(value):
    return set(re.findall(r"\{(\d+)[^}]*\}", value.replace("{{", "").replace("}}", "")))


def check_l10n():
    body = re.search(r"enum TextId\s*\{(.*?)\}", read(MOD / "UI/L10n.cs"), re.S).group(1)
    body = re.sub(r"//[^\n]*|/\*.*?\*/", "", body, flags=re.S)
    keys = {m.split("=")[0].strip() for m in body.split(",") if m.strip()}
    tables = {lang: json.loads(read(MOD / f"UI/Locales/{lang}.json")) for lang in LANGUAGES}
    for lang, table in tables.items():
        for key in sorted(keys - table.keys()):
            errors.append(f"Locales/{lang}.json: 缺少 {key}")
        for key in sorted(table.keys() - keys):
            errors.append(f"Locales/{lang}.json: TextId 中没有 {key}")
    for key in sorted(keys):
        sets = {lang: placeholders(t[key]) for lang, t in tables.items() if isinstance(t.get(key), str)}
        if len({frozenset(s) for s in sets.values()}) > 1:
            errors.append(f"Locales: {key} 各语言占位符不一致 {sets}")


def check_messages():
    source = read(NETWORK / "GameMessageType.cs")
    enum = set(re.findall(r"^\s*(\w+)\s*=\s*\d+\s*,?", re.search(r"enum GameMessageType[^{]*\{(.*?)\}", source, re.S).group(1), re.M))
    rules = set(re.findall(r"new\(\(ushort\)GameMessageType\.(\w+)", source))
    union = dict(re.findall(r"MemoryPackUnion\(\(ushort\)GameMessageType\.(\w+),\s*typeof\((\w+)\)\)", read(MOD / "Multiplayer/Messages/MultiplayerMessage.cs")))
    type_of = {name: cls for cls, name in re.findall(r"^\s*(\w+)\s*=>\s*GameMessageType\.(\w+)\s*,", read(MOD / "Multiplayer/GameMessages.cs"), re.M)}
    places = {"GameMessageRules": rules, "MemoryPackUnion": set(union), "GameMessages.TypeOf": set(type_of)}
    for place, names in places.items():
        for name in sorted(enum - names):
            errors.append(f"{place}: 未登记 GameMessageType.{name}")
        for name in sorted(names - enum):
            errors.append(f"{place}: GameMessageType 中没有 {name}")
    for name in sorted(set(union) & set(type_of)):
        if union[name] != type_of[name]:
            errors.append(f"消息 {name}: MemoryPackUnion 为 {union[name]}，TypeOf 为 {type_of[name]}")


sys.stdout.reconfigure(encoding="utf-8")
for check in (check_links, check_l10n, check_messages):
    check()
for error in errors:
    print(error)
print(f"{len(errors)} 个问题" if errors else "检查通过")
sys.exit(1 if errors else 0)

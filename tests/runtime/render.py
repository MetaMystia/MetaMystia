"""填入参数后输出 actions/ 下的脚本，交给 UnityRuntimeDebugger 的 urdbg 以 program 模式执行。

    python tests/runtime/render.py night | python urdbg.py eval --program -f -
    python tests/runtime/render.py serve MODE=open DESK=3 | python urdbg.py eval --program -f -
"""
import re
import sys
from pathlib import Path

if len(sys.argv) < 2:
    sys.exit(__doc__)
path = Path(sys.argv[1])
if not path.is_file():
    path = Path(__file__).resolve().parent / "actions" / path.with_suffix(".cs").name
code = path.read_text(encoding="utf-8")
for pair in sys.argv[2:]:
    key, sep, value = pair.partition("=")
    if not sep:
        sys.exit(f"参数格式为 KEY=值：{pair}")
    code = code.replace(f"@@{key}@@", value)
missing = sorted(set(re.findall(r"@@(\w+)@@", code)))
if missing:
    sys.exit("缺少参数：" + "、".join(missing))
sys.stdout.reconfigure(encoding="utf-8")
sys.stdout.write(code)

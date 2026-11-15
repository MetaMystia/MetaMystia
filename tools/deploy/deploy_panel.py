#!/usr/bin/env python3
"""从标准输入读取 tar.gz，替换 Nginx 托管的管理面板静态文件；不重启任何服务。"""
import fcntl
import os
from pathlib import Path, PurePosixPath
import shutil
import sys
import tarfile
import tempfile

PANEL = Path("/var/www/metamystia-admin")
ALLOWED = {".html", ".css", ".js", ".svg", ".png", ".ico", ".woff2"}
MAX_BYTES = 20 * 1024 * 1024


def install(stream, panel=PANEL):
    parent = panel.parent
    parent.mkdir(parents=True, exist_ok=True)
    with (parent / (panel.name + ".lock")).open("w") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        staging = Path(tempfile.mkdtemp(prefix=panel.name + ".upload-", dir=parent))
        try:
            total = 0
            with tarfile.open(fileobj=stream, mode="r|gz") as archive:
                for member in archive:
                    name = PurePosixPath(member.name)
                    if name.is_absolute() or ".." in name.parts or not (member.isfile() or member.isdir()):
                        raise ValueError("面板包路径或类型无效")
                    if member.isfile():
                        if name.suffix.lower() not in ALLOWED:
                            raise ValueError("面板包包含不允许的文件：" + member.name)
                        total += member.size
                        if total > MAX_BYTES:
                            raise ValueError("面板包过大")
                    archive.extract(member, staging, filter="data")
            if not (staging / "index.html").is_file():
                raise ValueError("面板包缺少 index.html")
            for path in [staging, *staging.rglob("*")]:
                if os.geteuid() == 0:
                    os.chown(path, 0, 0)
                path.chmod(0o755 if path.is_dir() else 0o644)
            previous = parent / (panel.name + ".previous")
            if previous.exists():
                shutil.rmtree(previous)
            if panel.exists():
                panel.rename(previous)
            staging.rename(panel)
        except BaseException:
            shutil.rmtree(staging, ignore_errors=True)
            raise
    print("管理面板已更新。")


if __name__ == "__main__":
    install(sys.stdin.buffer)

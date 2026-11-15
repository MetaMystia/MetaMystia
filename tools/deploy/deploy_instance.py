#!/usr/bin/env python3
import errno
import fcntl
import grp
import json
import os
from pathlib import Path, PurePosixPath
import pwd
import re
import socket
import subprocess
import sys
import tarfile
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path("/opt/metamystia-hosting")
CONFIG = Path("/etc/metamystia-hosting/deploy.json")


def request(url, token, method="GET", body=None):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(url, data=data, method=method, headers={
        "Authorization": "Bearer " + token, "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=10) as response:
        content = response.read()
        return json.loads(content) if content else None


def select_port(value, first, last, reserved):
    ports = range(first, last + 1) if value == "auto" else [int(value)]
    for port in ports:
        if not first <= port <= last:
            raise ValueError(f"端口须为 {first}–{last}")
        if port in reserved:
            if value != "auto":
                raise ValueError("端口已被未退役实例登记")
            continue
        try:
            with socket.socket() as probe:
                probe.bind(("0.0.0.0", port))
        except OSError as error:
            if value != "auto" or error.errno != errno.EADDRINUSE:
                raise
        else:
            return port
    raise ValueError(f"{first}–{last} 没有可用端口")


def replacement_ids(value, records, metadata):
    if value != "auto":
        ids = [] if value == "-" else value.split(",")
        if any(not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,100}", item) for item in ids):
            raise ValueError("退役实例列表无效")
        return ids
    active = [record for record in records if record["state"] == "active"]
    if any(record["registration"]["gameVersion"] == metadata["gameVersion"] and
           record["registration"]["modVersion"] == metadata["modVersion"] for record in active):
        raise ValueError("相同游戏和模组版本已有活跃实例，请发布新版本")
    return [record["id"] for record in active]


def main():
    os.umask(0o027)
    instance_id, game_port, admin_port, replaces = sys.argv[1:]
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,100}", instance_id):
        raise ValueError("实例 ID 无效")
    config = json.loads(CONFIG.read_text())
    gateway = config["gatewayUrl"].rstrip("/")
    token = config["gatewayToken"]
    instance_token = config["instanceToken"]
    group = grp.getgrnam("metamystia").gr_gid
    user = pwd.getpwnam("metamystia").pw_uid
    instances = ROOT / "instances"
    instances.mkdir(parents=True, exist_ok=True)
    os.chown(instances, 0, group)
    with (ROOT / "deploy.lock").open("w") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        destination = instances / instance_id
        unit = "metamystia-instance@" + instance_id + ".service"
        if destination.exists():
            raise ValueError("实例目录已存在；请检查已有实例，不覆盖运行文件")
        records = request(gateway + "/admin/instances", token)
        reserved = set()
        for record in records:
            if record["state"] != "stopped":
                reserved.add(record["registration"]["port"])
                reserved.add(urllib.parse.urlsplit(record["registration"]["adminUrl"]).port)
        game_port = select_port(game_port, 41000, 41099, reserved)
        admin_port = select_port(admin_port, 42000, 42099, reserved)
        staging = Path(tempfile.mkdtemp(prefix="upload-", dir=instances))
        with tarfile.open(fileobj=sys.stdin.buffer, mode="r|gz") as archive:
            for member in archive:
                name = PurePosixPath(member.name)
                if name.is_absolute() or ".." in name.parts or not (member.isfile() or member.isdir()):
                    raise ValueError("部署包路径或类型无效")
                if name.parts and name.parts[0] in ("logs", "instance.env", "server.json", "chat-filter.json"):
                    raise ValueError("部署包不能覆盖实例配置")
                archive.extract(member, staging, filter="data")
        metadata = json.loads((staging / "deployment.json").read_text())
        old_ids = replacement_ids(replaces, records, metadata)
        executable = staging / "MetaMystia.Server"
        if not executable.is_file() or not (staging / "MetaMystia.Server.dll").is_file():
            raise ValueError("部署包缺少服务端")
        for path in [staging, *staging.rglob("*")]:
            os.chown(path, 0, group)
            path.chmod(0o750 if path.is_dir() else 0o640)
        executable.chmod(0o750)
        logs = staging / "logs"
        logs.mkdir(mode=0o750)
        os.chown(logs, user, group)
        for name in ("server.json", "chat-filter.json"):
            if (ROOT / name).exists():
                (staging / name).symlink_to(ROOT / name)
        environment = {
            "MM_INSTANCE_ID": instance_id, "MM_GAME_PORT": str(game_port),
            "MM_GAME_ADDRESS": "127.0.0.1",
            "MM_ADMIN_URL": "http://127.0.0.1:" + str(admin_port),
            "MM_INSTANCE_TOKEN": instance_token,
        }
        env_file = staging / "instance.env"
        env_file.write_text("".join(key + "=" + json.dumps(value) + "\n" for key, value in environment.items()))
        env_file.chmod(0o600)
        registration = {
            "gameVersion": metadata["gameVersion"], "modVersion": metadata["modVersion"],
            "host": "127.0.0.1", "port": game_port, "adminUrl": environment["MM_ADMIN_URL"]}
        (staging / "registration.json").write_text(json.dumps(registration))
        staging.rename(destination)
        subprocess.run(["systemctl", "enable", "--now", unit], check=True)
        status = None
        for _ in range(30):
            try:
                status = request(environment["MM_ADMIN_URL"] + "/admin/status", instance_token)
                break
            except (urllib.error.URLError, TimeoutError):
                time.sleep(1)
        if status is None or status["instanceId"] != instance_id or status["modVersion"] != metadata["modVersion"]:
            raise RuntimeError("新实例未就绪；旧实例保持不变")
        subprocess.run(["runuser", "-u", "metamystia", "--", str(destination / "MetaMystia.Server"),
                        "--probe", "127.0.0.1", str(game_port)], check=True, timeout=15, cwd=destination)
        request(gateway + "/admin/instances/" + instance_id, token, "PUT", registration)
        for old_id in old_ids:
            request(gateway + "/admin/instances/" + old_id + "/drain", token, "POST",
                    {"noticeId": instance_id, "targetModVersion": metadata["modVersion"]})
        print("新版已激活，旧实例已收到退役请求：" + instance_id)


if __name__ == "__main__":
    main()

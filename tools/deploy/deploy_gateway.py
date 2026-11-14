#!/usr/bin/env python3
import fcntl
import grp
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import tarfile
import tempfile
import time

from deploy_instance import CONFIG, ROOT, request


def main():
    os.umask(0o027)
    config = json.loads(CONFIG.read_text())
    with (ROOT / 'deploy.lock').open('w') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        staging = Path(tempfile.mkdtemp(prefix='gateway-upload-', dir=ROOT))
        with tarfile.open(fileobj=sys.stdin.buffer, mode='r|gz') as archive:
            for member in archive:
                name = PurePosixPath(member.name)
                if name.is_absolute() or '..' in name.parts or not (member.isfile() or member.isdir()):
                    raise ValueError('部署包路径或类型无效')
                archive.extract(member, staging, filter='data')
        if not (staging / 'MetaMystia.Gateway').is_file() or not (staging / 'MetaMystia.Gateway.dll').is_file():
            raise ValueError('部署包缺少 Gateway')
        group = grp.getgrnam('metamystia').gr_gid
        for path in [staging, *staging.rglob('*')]:
            os.chown(path, 0, group)
            path.chmod(0o750 if path.is_dir() else 0o640)
        (staging / 'MetaMystia.Gateway').chmod(0o750)
        previous = ROOT / 'gateway.previous'
        if previous.exists():
            if previous.resolve().parent != ROOT.resolve() or previous.is_symlink():
                raise ValueError('旧备份路径无效')
            shutil.rmtree(previous)
        subprocess.run(['systemctl', 'stop', 'metamystia-gateway.service'], check=True)
        (ROOT / 'gateway').rename(previous)
        staging.rename(ROOT / 'gateway')
        subprocess.run(['systemctl', 'start', 'metamystia-gateway.service'], check=False)
        for _ in range(15):
            try:
                request(config['gatewayUrl'].rstrip('/') + '/admin/status', config['gatewayToken'])
                print('Gateway 已更新；路由文件保留。')
                return
            except OSError:
                time.sleep(1)
        subprocess.run(['systemctl', 'stop', 'metamystia-gateway.service'], check=True)
        (ROOT / 'gateway').rename(ROOT / ('gateway-failed-' + str(time.time_ns())))
        previous.rename(ROOT / 'gateway')
        subprocess.run(['systemctl', 'start', 'metamystia-gateway.service'], check=True)
        raise RuntimeError('Gateway 启动失败，已恢复上一版')


if __name__ == '__main__':
    main()

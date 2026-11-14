import errno
import io
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import tarfile
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

import deploy_instance as deploy
import deploy_gateway


def instance(name, state, version="0.29.4", game="game"):
    return {"id": name, "state": state, "registration": {
        "gameVersion": game, "modVersion": version, "port": 41000,
        "adminUrl": "http://127.0.0.1:42000"}}


class PortTests(unittest.TestCase):
    def test_occupied_port_is_skipped_only_in_auto_mode(self):
        with socket.socket() as listener:
            listener.bind(("0.0.0.0", 0))
            port = listener.getsockname()[1]
            selected = deploy.select_port("auto", port, port + 100, set())
            self.assertGreater(selected, port)
            self.assertLessEqual(selected, port + 100)
            with self.assertRaises(OSError):
                deploy.select_port(str(port), port, port + 100, set())
            with self.assertRaises(ValueError):
                deploy.select_port("auto", port, port, set())

    def test_registered_port_cannot_be_reused_even_when_offline(self):
        with patch.object(deploy.socket, "socket") as socket_factory:
            with self.assertRaises(ValueError):
                deploy.select_port("auto", 41000, 41000, {41000})
            with self.assertRaises(ValueError):
                deploy.select_port("41000", 41000, 41099, {41000})
            socket_factory.assert_not_called()

    def test_explicit_port_must_be_in_range(self):
        with self.assertRaises(ValueError):
            deploy.select_port("40999", 41000, 41099, set())
        with self.assertRaises(ValueError):
            deploy.select_port("41100", 41000, 41099, set())

    def test_unexpected_socket_errors_are_reported(self):
        with patch.object(deploy.socket, "socket") as socket_factory:
            socket_factory.return_value.__enter__.return_value.bind.side_effect = PermissionError(errno.EACCES, "denied")
            with self.assertRaises(PermissionError):
                deploy.select_port("auto", 41000, 41099, set())


class ReplacementTests(unittest.TestCase):
    metadata = {"gameVersion": "game", "modVersion": "0.29.5"}

    def test_auto_selects_only_active_instances(self):
        records = [instance("old", "active"), instance("other-game", "active", game="older-game"),
                   instance("draining", "draining"), instance("staged", "staged"), instance("stopped", "stopped")]
        self.assertEqual(deploy.replacement_ids("auto", records, self.metadata), ["old", "other-game"])

    def test_first_deployment_has_no_replacements(self):
        self.assertEqual(deploy.replacement_ids("auto", [], self.metadata), [])

    def test_same_active_version_is_rejected(self):
        with self.assertRaises(ValueError):
            deploy.replacement_ids("auto", [instance("same", "active", "0.29.5")], self.metadata)

    def test_manual_replacements_are_preserved(self):
        self.assertEqual(deploy.replacement_ids("-", [], self.metadata), [])
        self.assertEqual(deploy.replacement_ids("old1,old2", [], self.metadata), ["old1", "old2"])
        with self.assertRaises(ValueError):
            deploy.replacement_ids("../old", [], self.metadata)


class DeploymentTests(unittest.TestCase):
    def run_deployment(self, probe_fails=False):
        archive_data = io.BytesIO()
        metadata = {"gameVersion": "game", "modVersion": "0.29.5"}
        with tarfile.open(fileobj=archive_data, mode="w:gz") as archive:
            for name, data in {"deployment.json": json.dumps(metadata).encode(),
                               "MetaMystia.Server": b"server", "MetaMystia.Server.dll": b"dll"}.items():
                entry = tarfile.TarInfo(name)
                entry.size = len(data)
                archive.addfile(entry, io.BytesIO(data))
        archive_data.seek(0)
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            config = root / "config.json"
            config.write_text(json.dumps({"gatewayUrl": "http://127.0.0.1:40900",
                "gatewayToken": "directory-test", "instanceToken": "instance-test"}))
            calls = Mock()
            records = [instance("old", "active"), instance("draining", "draining")]
            request = Mock(side_effect=[records, {"instanceId": "new", "modVersion": "0.29.5"}, None, None])
            run = Mock(side_effect=[None, subprocess.CalledProcessError(1, "probe")] if probe_fails else None)
            calls.attach_mock(request, "request")
            calls.attach_mock(run, "run")
            previous_umask = os.umask(0o027)
            self.addCleanup(os.umask, previous_umask)
            with patch.object(deploy, "ROOT", root), patch.object(deploy, "CONFIG", config), \
                 patch.object(sys, "argv", ["deploy_instance.py", "new", "auto", "auto", "auto"]), \
                 patch.object(sys, "stdin", SimpleNamespace(buffer=archive_data)), \
                 patch.object(deploy.grp, "getgrnam", return_value=SimpleNamespace(gr_gid=os.getgid())), \
                 patch.object(deploy.pwd, "getpwnam", return_value=SimpleNamespace(pw_uid=os.getuid())), \
                 patch.object(deploy.os, "chown"), patch.object(deploy, "request", request), \
                 patch.object(deploy.subprocess, "run", run):
                if probe_fails:
                    with self.assertRaises(subprocess.CalledProcessError):
                        deploy.main()
                    self.assertEqual(request.call_count, 2)
                    return
                deploy.main()
            registration = json.loads((root / "instances/new/registration.json").read_text())
            self.assertEqual(registration["host"], "127.0.0.1")
            self.assertIn('MM_GAME_ADDRESS="127.0.0.1"', (root / "instances/new/instance.env").read_text())
            self.assertTrue(41001 <= registration["port"] <= 41099)
            self.assertNotEqual(registration["adminUrl"], "http://127.0.0.1:42000")
            self.assertEqual([call[0] for call in calls.mock_calls], ["request", "run", "request", "run", "request", "request"])
            self.assertEqual(request.call_args.args[2:], ("POST", {"noticeId": "new", "targetModVersion": "0.29.5"}))

    def test_auto_deployment_probes_before_registering_and_retiring(self):
        self.run_deployment()

    def test_failed_probe_does_not_register_or_retire(self):
        self.run_deployment(probe_fails=True)


class GatewayDeploymentTests(unittest.TestCase):
    def update(self, healthy):
        archive_data = io.BytesIO()
        with tarfile.open(fileobj=archive_data, mode='w:gz') as archive:
            for name in ('MetaMystia.Gateway', 'MetaMystia.Gateway.dll'):
                entry = tarfile.TarInfo(name)
                entry.size = 3
                archive.addfile(entry, io.BytesIO(b'new'))
        archive_data.seek(0)
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / 'gateway').mkdir()
            (root / 'gateway/old').write_text('old')
            (root / 'data').mkdir()
            (root / 'data/instances.json').write_text('routes')
            config = root / 'config.json'
            config.write_text(json.dumps({'gatewayUrl': 'http://127.0.0.1:40900', 'gatewayToken': 'test'}))
            previous_umask = os.umask(0o027)
            self.addCleanup(os.umask, previous_umask)
            with patch.object(deploy_gateway, 'ROOT', root), patch.object(deploy_gateway, 'CONFIG', config), \
                 patch.object(sys, 'stdin', SimpleNamespace(buffer=archive_data)), \
                 patch.object(deploy_gateway.grp, 'getgrnam', return_value=SimpleNamespace(gr_gid=os.getgid())), \
                 patch.object(deploy_gateway.os, 'chown'), patch.object(deploy_gateway.time, 'sleep'), \
                 patch.object(deploy_gateway.subprocess, 'run') as run, \
                 patch.object(deploy_gateway, 'request', side_effect=None if healthy else OSError('offline')):
                if healthy:
                    deploy_gateway.main()
                    self.assertTrue((root / 'gateway/MetaMystia.Gateway').is_file())
                    self.assertTrue((root / 'gateway.previous/old').is_file())
                    self.assertEqual(run.call_count, 2)
                else:
                    with self.assertRaises(RuntimeError):
                        deploy_gateway.main()
                    self.assertTrue((root / 'gateway/old').is_file())
                    self.assertEqual(run.call_count, 4)
            self.assertEqual((root / 'data/instances.json').read_text(), 'routes')

    def test_manual_update_preserves_routes(self):
        self.update(True)

    def test_unhealthy_update_restores_previous_gateway(self):
        self.update(False)


if __name__ == "__main__":
    unittest.main()

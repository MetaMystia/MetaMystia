#!/usr/bin/env python3
import json
import re
import sys

from deploy_instance import CONFIG, ROOT, request


instance_id, replaces = sys.argv[1:]
if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,100}", instance_id):
    raise ValueError("实例 ID 无效")
config = json.loads(CONFIG.read_text())
registration = json.loads((ROOT / "instances" / instance_id / "registration.json").read_text())
directory = config["directoryUrl"].rstrip("/")
request(directory + "/admin/instances/" + instance_id, config["directoryToken"], "PUT", registration)
request(directory + "/admin/instances/" + instance_id + "/activate", config["directoryToken"], "POST",
        {"replaces": [] if replaces == "-" else replaces.split(",")})
print("实例登记与激活已完成：" + instance_id)

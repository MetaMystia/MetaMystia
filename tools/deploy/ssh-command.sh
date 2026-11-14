#!/bin/bash
set -euo pipefail
read -r -a command <<< "${SSH_ORIGINAL_COMMAND:-}"
case "${command[0]:-}" in
    deploy-instance)
        test "${#command[@]}" = 5
        exec sudo -n /usr/bin/python3 /opt/metamystia-hosting/tools/deploy_instance.py "${command[@]:1}"
        ;;
    deploy-gateway)
        test "${#command[@]}" = 1
        exec sudo -n /usr/bin/python3 /opt/metamystia-hosting/tools/deploy_gateway.py
        ;;
    *) exit 1 ;;
esac

#!/bin/bash
set -euo pipefail
read -r -a command <<< "${SSH_ORIGINAL_COMMAND:-}"
case "${command[0]:-}" in
    deploy-instance)
        test "${#command[@]}" = 5
        exec sudo -n /usr/bin/python3 /opt/metamystia-hosting/tools/deploy_instance.py "${command[@]:1}"
        ;;
    activate-instance)
        test "${#command[@]}" = 3
        exec sudo -n /usr/bin/python3 /opt/metamystia-hosting/tools/activate_instance.py "${command[@]:1}"
        ;;
    *) exit 1 ;;
esac

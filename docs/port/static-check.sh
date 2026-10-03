#!/usr/bin/env bash
# 迁移静态检查（在仓库根目录运行）
set -u
MOD=src/MetaMystia.Mod
fail=0
check_empty() {
  local name="$1"; shift
  local out
  out=$(eval "$@" 2>/dev/null)
  if [ -n "$out" ]; then
    echo "FAIL: $name"
    echo "$out" | head -20
    fail=1
  else
    echo "ok: $name"
  fi
}

check_empty "无 BepInEx（缺口文件与 CompatPatches 除外）" \
  "grep -rn --include=*.cs -e 'using BepInEx' -e 'BepInEx\.' $MOD | grep -v '/obj/' | grep -v 'Patches/Compat/' | grep -v 'CompatPatches.cs'"
check_empty "无 HarmonyLib（缺口文件与 CompatPatches 除外）" \
  "grep -rl --include=*.cs -e HarmonyLib -e HarmonyPatch $MOD | grep -v '/obj/' | grep -v 'Patches/Compat/' | grep -v 'CompatPatches.cs'"
check_empty "无 BasePlugin" "grep -rn --include=*.cs BasePlugin $MOD | grep -v '/obj/'"
check_empty "无 IGuestDirector 实现" "grep -rn --include=*.cs IGuestDirector $MOD | grep -v '/obj/'"
check_empty "无 HarmonyReversePatch（缺口文件除外）" \
  "grep -rn --include=*.cs HarmonyReversePatch $MOD | grep -v '/obj/' | grep -v 'Patches/Compat/'"
check_empty "无 UnityEngine.Debug" "grep -rn --include=*.cs 'UnityEngine.Debug' $MOD | grep -v '/obj/'"
check_empty "无 ManualLogSource" "grep -rn --include=*.cs ManualLogSource $MOD | grep -v '/obj/'"
check_empty "无 MyPluginInfo" "grep -rn --include=*.cs MyPluginInfo $MOD | grep -v '/obj/'"

exit $fail

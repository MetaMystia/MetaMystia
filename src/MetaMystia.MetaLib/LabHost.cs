using System;

using UnityEngine;

using MetaMystia.MetaLib.Storage;

namespace MetaMystia.MetaLib;

public sealed class LabHost : MonoBehaviour
{
    public LabHost(IntPtr pointer) : base(pointer) { }

    private void Update()
    {
        if (!Application.isFocused)
            return;
        for (var i = 0; i < 9; i++)
        {
            if (!Input.GetKeyDown((KeyCode)((int)KeyCode.F1 + i)))
                continue;
            LabChecks.Run(i + 1);
            break;
        }
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(12, 60, 760, 90),
            "MetaLib  F1 状态 / F2 写入 A / F3 校验 A / F4 更新 A\n" +
            "F5 写入 B / F6 隔离检查 / F7 大数据 / F8 删除样例 / F9 往返检查\n" + LabChecks.Status);
    }

    private void OnDestroy() => ModSaveData.Close();
}

# 存档数据

单机，读同一份存档，存档只写入空槽位 90。脚本为 `saving`，未写明的参数填 `NAME=a VERSION=1 VALUE=0`。

## 1 读档前

- 操作：标题界面 `MODE=get`、`MODE=set VALUE=5`、`MODE=raw`。
- 期望：`n=default`；`written=False n=5`；`not loaded`。

## 2 写入与存档

- 操作：读档后 `MODE=get`、`MODE=set VALUE=5`、`NAME=b MODE=set VALUE=6`、`MODE=save VALUE=90`，再 `MODE=set VALUE=100`。
- 期望：`n=default`，之后均为 `written=True`；槽位 90 的 `schedulerPartialDLC.MystiaLib.finishedEvents` 为 a=5、b=6 两条记录，其余集合为空。

## 3 换存档

- 操作：用例 2 之后依次回标题读槽位 90、原存档、槽位 90，每次 `MODE=get`。
- 期望：依次为 `n=5`、`n=default`、`n=5`。

## 4 不可用的记录

- 操作：读槽位 90，`MODE=inject` 把 a 换成 `version` 为 2 的记录，再换成 `data.n` 为字符串的记录；每次 `MODE=get`、`MODE=set VALUE=8`、`MODE=raw`。期间 `NAME=b MODE=set VALUE=9`。
- 期望：两次均为 `n=default`、`written=False n=8`，`raw` 中 a 的记录不变；b 为 `written=True`。
- 依据：日志各有一条 `Save record 'RuntimeTest/a' is kept but not used`。

## 5 移除模组后往返

- 操作：槽位 90 存档后退出，移除模组启动，读槽位 90，推进一天并在结算面板覆盖槽位 90，重启再读存一次；装回模组读槽位 90。
- 期望：每次存档后文件中 `MystiaLib` 键只有一个，内容与移除前逐字一致；装回后 `MODE=get` 与移除前相同。读档列表没有无法读取的存档。

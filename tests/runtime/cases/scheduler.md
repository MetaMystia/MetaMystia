# 资源包排程存档

单机。测试包 `I123Test`：新闻 `_I123_News_A`（`rewards` 发 1001 金钱）、`_I123_News_B`；事件 `_I123_Event_Day3` 与限时任务 `_I123_Mission_Day3`，触发均为 `OnWorkEnd`、`Relative` 3 天（任务完成条件 `BillRepayment`）。同时加载示例包。脚本为 `scheduler`，只引用游戏类型，未安装模组时也能执行；`FILTER=_I123_`。存档写入空槽位，核对存档 JSON 的 `schedulerPartialDLC`。推进日期用 `MODE=addday` 后存档、读档。用例 1–6 在 0.31.4（#123 修复）通过（运行时实测，单机一实例）。

## 1 卸载模组期间不计时

- 操作：第 D 天 `MODE=news LABEL=_I123_News_A VALUE=D+3`、`MODE=event`、`MODE=mission`，存档。移除模组读档，`MODE=addday VALUE=5` 后存档。装回模组读档。
- 期望：移除期间存档的 `ResourceEx` 分区 `dlcSaveDate` 仍为 D、key 仍为 D+3；装回后三项 key 均为 D+8。
- 依据：日志 `Reloaded Scheduler data ResourceEx (saved on day D, today D+5, shift +5)`。

## 2 顺延后发布

- 操作：用例 1 之后推进到 D+8，存档后读档；当晚营业一轮。
- 期望：当天报纸有「I123 新闻 A」，资金 +1001；营业结束后 `finishedEvents`、`finishedMissions` 含该事件和任务。

## 3 只卸载资源包

- 操作：移除两个资源包，读用例 1 移除模组期间的存档（D+5），`MODE=addday VALUE=2` 后存档；装回资源包读档。
- 期望：存档有 `MetaMystia.ResourceEx` 分区，`dlcSaveDate` 为 D+5，三项 key 为 D+8，示例包的完成记录在其中；装回后 key 为 D+10，完成记录恢复。
- 依据：装回后的读档日志没有 `Activated kizuna event node`。

## 4 留存分区不重复改动

- 操作：用例 3 移除资源包期间，推进两天后再次执行 `SchedulerDataRecovery.CheckAndReloadSchedulerData`（同一存档再次进入白天）。
- 期望：未加载数据仍是同一个字典，`MetaMystia.ResourceEx` 的 `dlcSaveDate` 不变，没有恢复日志。

## 5 未注册的新闻

- 操作：`MODE=news LABEL=_I123_News_Missing VALUE=<今天>`；`MODE=news LABEL=_I123_News_A,_I123_News_Missing VALUE=<今天>`；`MODE=apply`。
- 期望：第一次不排程；第二次只排新闻 A；发布后资金 +1001，没有异常。
- 依据：日志 `ScheduleNews reward skipped, news not registered` 与 `ScheduleNews reward ignores unregistered news`。

## 6 多个分区与异常日期

- 操作：改存档 JSON：`ResourceEx` 的 `dlcSaveDate` 设为今天 +6，另加 key 3 与 key 今天+11 的新闻 B；把原分区复制为 `ResourceEx1` 并设 `dlcSaveDate` 为 -1；加 `UNDEFINED2`，含一条未注册新闻。读档。
- 期望：`ResourceEx` 的 key 各减 6，key 3 变为 1（不落进 0、-1），-1、0 不变；`ResourceEx1` 不平移，同名事件、任务各只有一份，新闻 A 另有一条在原 key；未注册新闻移入 `MetaMystia.ResourceEx`。不再清除任何分区。

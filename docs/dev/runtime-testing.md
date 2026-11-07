# 游戏实测

只在用户要求时进行。需要用户启动游戏并提供调试器 Token。

## 工具

UnityRuntimeDebugger，位置见 `AGENTS.local.md`。接口说明从运行中的调试器 `GET /help` 读取。Token 每次启动都会变化，只作为本次请求参数，不写入文件、文档或输出。调试请求须经用户批准。用户关闭游戏后只整理已有材料，不重连或重启。

## 方法

- 接手时重新读取场景、地图、面板和状态，不沿用上次的对象、坐标或句柄。
- 先读逆向代码再操作。
- 优先使用玩家互动、按钮提交和游戏原有流程。直接调用业务方法时，记录跳过了哪些 UI 检查，不能据此宣称完整玩家操作已通过。
- 每次执行一个可核验的动作，等对话、协程或切图结束后比较前后状态。购买、邀请、委托等消费性动作结果不明时先查状态，不重复提交。
- 需要实时响应的操作（如夜间上菜）放在游戏内协程中执行；逐条请求的往返延迟赶不上客人的耐心窗口。
- 显示名不唯一，隐藏的缓存对象和重复文本不能当作多个实体或多次收益；以标签、资源 ID 和状态差值为准。
- 汇报方式见 [verification](verification.md)。

## 运行时经验

### Il2Cpp 与调试

- 访问可能已销毁的对象前，用 Unity 的 `obj != null` 判断。
- 判断 `MonoSingleton` 单例是否存在用 `hasInstance`；读取 `Instance` 会在实例不存在时创建空对象并执行 `Awake`。
- Il2Cpp 对象向子类转换用 `TryCast<T>()`。C# 的 `as` 对 `OrderBase` → `SpecialOrder` 实测恒为 `null`。
- Il2Cpp 集合不能直接使用 LINQ，先遍历复制为托管集合。
- `default(InputAction.CallbackContext)` 触发空对象转换错误，`new InputAction.CallbackContext()` 可用。只有确认回调不读取参数时才能这样调用；选店的 `ConfirmChoiceStart` 会调用 `ReadValue<float>()`。
- 托管协程用 `BepInEx.Unity.IL2CPP.Utils` 的 `StartCoroutine(IEnumerator)` 扩展挂到存活的 `MonoBehaviour` 上。宿主随场景销毁时协程一并结束，例如 `GuestsManager` 在夜间结束时销毁。
- 截图在 `WaitForEndOfFrame` 后调用 `ScreenCapture.CaptureScreenshotAsTexture()`，编码 PNG 后释放纹理。
- 调试器读取存档中的嵌套值类型曾得到异常数值。存档状态以原版 UI、实际 JSON 文件和重载结果交叉核对。

### UI

- 激活不等于可见：大量缓存面板处于激活状态，需要结合 Canvas、透明度和裁剪状态筛选。
- 同名控件（如多个 `Selection(Clone)`）用实例 ID 或唯一文本定位；实例 ID 只在对象生命周期内有效。父控件的文本包含子节点文本。
- `UIButtonBase.CallSubmitAction()` 对 `PointerClickAsSelect` 的按钮只执行选择。选择会触发列表滚动，等滚动完成后再提交。
- `UILogicalUnit.TryInvokeCurrentCancel()` 对电话菜单、产物汇总面板无效，改用面板的返回按钮或 `ClosePanel()`。
- 快进、前往开店等长按按钮使用 `TryExecuteHold` 或 `ExternalStartHold()`。

### 白天

- 普通白天 10:00–18:00，共 16 行动点，每点 30 分钟。行走和现实时间等待不推进时钟，也不降低采集冷却。
- 采集（`CollectTrackedCollectable`）先设置冷却再扣 1 点；扣点会同时降低所有地图上已追踪采集点的冷却。可采条件：已开放、冷却为 0、处于采集时段。
- 出口是否扣点由 `shouldCostAction` 决定；有的出口走入即触发，家门需要互动。互动范围以 Collider 为准，`Transform.position` 和 `bounds` 都不可靠。
- 移动用 `UpdateInputDirection`，由游戏物理推进，结束时调用 `ExternalStop`。直线追踪不会绕开障碍；停滞时截图诊断，增加中间点。切图完成后重新读取地图和出生点。
- 邀请：开始即记录 `attempted`；整段对话结束后才登记受邀并扣点。
- 委托：提交时登记，对话结束扣点，日终 `ReceiveCommissionsAsync()` 入库。
- 赛钱：回应对话结束后扣款，每天一次，不扣行动点。源码推断：至少 500 円时安排灵梦夜间正面符卡，至少 1000 円时另安排“实惠”料理制作时间减少 20%。
- 快捷传送入口为 `DaySceneSustainedPannel.OpenFastTravelPanelParameterless()`；八云传送扣钱，不扣行动点。
- 原版角色与资源包角色可以同名（如 `Koakuma` 与 `_ResourceExample_Koakuma`），以标签区分。

### 夜间

- 普通伙伴只处理 `NormalOrder`，`SpecialOrder` 只由 `PartnerBossBehaviour` 处理；稀客订单通常需要玩家上菜。
- 上菜面板：`Send(料理)` → `Send(酒水)` → `ClosePanel()`，经 `OnPanelClose` 判定并评价。`CloseExternPanel` 是取消路径，会回收待上菜物品。
- 稀客入座到首单约 15 秒，此前 `AllOrdersCount == 0`，调用 `PeekOrders` 会抛出异常。
- 营业倒计时归零（`TryCloseIzakaya`）后，已入座的客人在耐心耗尽前仍可服务，还可能继续追加订单。稀客的耐心窗口约 1–2 分钟。
- 订单只需命中标签；直接放入托盘的料理不受当天菜单限制。
- 按 `SpecialGuest.Id` 遍历桌位曾找不到已入座的客人，用固定桌号调用 `ExcuteEventAtCorodinate(n)` 可以打开面板，原因未查明。

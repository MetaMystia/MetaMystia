# 审计与调试

阅读限制见 [README.md](README.md)。

## 审计

- 每条涉及游戏运行逻辑的结论，都要对应到逆向代码中的具体方法。逆向代码可能不准确，关键结论还要用运行时数据交叉验证。
- 找原型：先找行为相近的原版符卡，再追查它调用的接口，以及这些接口的所有调用方。例如核对上菜竞态时，就要检索 `ServedBeverageInAir` 的所有读写位置。
- 找并发路径：玩家（上菜面板）、伙伴（`PartnerWaitressBehaviour` 等）、其他符卡、客人离开（`GuestGroupController` 清理订单）、场景切换。
- 找项目补丁：同一入口可能已经被项目补丁改写，尤其是联机相关的补丁（`Patches/NightScene/`、`Managers/GuestFSM.cs`）。
- 汇报结论时，标注证据类型："运行时实测"、"源码推断"、"待验证"。按钮返回成功、状态已登记、实际效果生效，这三者不能互相替代。

## 运行时探针

使用 Il2cppConsoleMod（位置见 `paths.local.md`，用法见其 README）：
- token 每次启动都会变化，从游戏的 `BepInEx/LogOutput.log` 读取；token 只作为本次请求参数，不要写入脚本、文档或记录。
- 所有调试 HTTP 请求都要经执行工具审批。
- 需要保留的变量、对象或协程用 `/script`。脚本里可以写托管 `IEnumerator`，经 `WrapToIl2Cpp()` 交给 `EventManager.StartCoroutine`。
- 访问已销毁的 Il2Cpp 对象的成员会抛异常，先用 `obj != null`（Unity 的空判断）过滤。
- 判断 `MonoSingleton` 单例（如 `EventManager`）是否存在时用 `hasInstance`，不要读 `Instance`：实例不存在时 `Instance` 会在当前场景中创建一个空对象并执行其 `Awake`。

| 目的 | 方法 |
|---|---|
| 资源是否仍然存活 | `AssetBundle.GetAllLoadedAssetBundles_Native()`、`Resources.FindObjectsOfTypeAll(Il2CppType.Of<GameObject>())` |
| 验证资源卸载 | 给一部分对象设置 `DontUnloadUnusedAsset`，调用 `Resources.UnloadUnusedAssets()` 后比较两组对象的存活情况 |
| 特效状态 | 读取 `ParticleSystem` 的 `isPlaying`、`particleCount`，以及渲染器的排序层、材质和 `shader.isSupported` |
| 暂停时查看粒子 | `ParticleSystem.Simulate(t, false, true)` |
| UI 层级 | 根 `Canvas` 的 `renderMode`、`sortingOrder`，以及 `overrideSorting` 的子画布 |
| 画面对比 | 在协程中 `WaitForEndOfFrame` 后调用 `ScreenCapture.CaptureScreenshotAsTexture()`，把结果保存为 PNG 再查看 |
| 导出原版贴图或图集精灵图 | 在临时图层放一个 `SpriteRenderer`，用临时正交相机按精灵图矩形 1:1 渲染到 `RenderTexture`，再 `ReadPixels`。直接按 UV 裁剪图集会错位 |
| 触发演出 | 直接调用对应接口，例如 `EventManager.CallSpellDeclaration(sprite, languageBase, false)`，不必真的触发符卡 |
| 预览新资源 | 热替换数据库条目，例如替换 `BuffDescription` 中的图标 |

## 对照实验

表现和预期不符时，一次只改一个变量，并保留同一时刻的截图或数值：
- 确认不是动画状态的问题：调换播放顺序后重复截图。
- 确认问题出在资源属性上：把原版资源按我们的方式重建，看它是否也出现同样的问题。
- 确认根因：只改一个属性（例如 pivot），看结果能不能和原版一致（可以对截图做像素差比较）。

## 离线检查

- Unity 批处理模式可以运行编辑器脚本检查 prefab（`ParticleProbe`）。
- UnityPy 可以读取资源包中的对象、引用和排序层，也可以比较两次构建的产物。

## 崩溃

- 游戏静默退出时，先看 Windows 自动保存的转储（`%LOCALAPPDATA%\CrashDumps`），再用 cdb 分析：`cdb -z <dmp> -c ".ecxr; kn 40; q"`。
- 如果调用栈是 `ntdll!RtlReportCriticalFailure ← RtlFreeHeap`，说明进程堆已损坏。崩溃位置只是**发现**损坏的地方，不是写坏堆的地方，不能据此判断是最近触发的功能导致的。
- 要定位写入点，可以对游戏进程开启 PageHeap（`gflags.exe` 与 cdb 位于同一目录；`gflags /p /enable <exe> /full`，需要管理员权限，调试完用 `/disable` 关闭），越界写发生时会当场崩溃。
- 崩溃点落在托管代码时，用 SOS 解析：`dotnet tool install -g dotnet-sos`、`dotnet-sos install`，然后在 cdb 中执行 `.load <sos.dll>; !ip2md <地址>; !clrstack -f; !pe`。
- 定位到第三方库内部时，用 `ilspycmd -t <类型全名> <dll>` 反编译**本机实际加载的**版本来核对，不要凭记忆判断。
- 案例：符卡开发期间的随机崩溃，按"PageHeap 当场崩溃 → SOS 定位到 `ClassInjector.RegisterTypeInIl2Cpp` → 反编译确认虚表越界写"的顺序定位，详见 [`il2cppinterop-defects.md`](../il2cppinterop-defects.md)。
- 按模块二分：临时加入环境变量开关跳过可疑模块，定位后删除。偶发问题每组要启动多次才能得出结论。
- 游戏的 `Player.log`（`%USERPROFILE%\AppData\LocalLow\Epicomic\Touhou Mystia Izakaya\`）记录原版流程日志，例如 `Timed Buff [id] registered`、`Spell Queue Execution Finish`，可以用来核对符卡队列是否正常结束。

## 部署

- 构建会把 DLL 复制到游戏插件目录，并改名为带版本号的文件名。游戏运行时 DLL 被锁，改名失败会留下新旧两份 DLL，必须关闭游戏后重新构建。
- 资源包更新后需要重启游戏。

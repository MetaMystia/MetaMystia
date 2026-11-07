# 文档

| 目录 | 内容 | 维护 |
|---|---|---|
| `dev/` | 环境、验证、提交流程、游戏实测 | 流程变化时更新 |
| `rules/` | 编码规范 | 必须遵守 |
| `reference/` | 架构、配置约定、已知缺陷 | 随代码同步更新 |
| `research/` | 带基线（游戏版本、提交）的审计、调研 | 不随代码更新；结论实现或失效后改写或删除 |
| `spell-creation/`、`map-creation/` | 符卡、白天地图制作 | 有阅读限制，见各自 README |

## 开发流程

- [setup](dev/setup.md)：依赖、构建、部署
- [verification](dev/verification.md)：验证等级、离线测试、汇报
- [workflow](dev/workflow.md)：分支、提交、PR、CI、发布
- [runtime-testing](dev/runtime-testing.md)：游戏实测方法与运行时经验

## 编码规范

- [code-style](rules/code-style.md)：代码风格、`using` 顺序
- [harmony-hook](rules/harmony-hook.md)：Patch 组织与 Hook 选择
- [network-message](rules/network-message.md)：联机消息定义、路由与接收约束
- [threading](rules/threading.md)：线程边界、主线程切换、异步 IO
- [coroutine](rules/coroutine.md)：协程
- [il2cpp-interop](rules/il2cpp-interop.md)：Interop 类型边界与异常排查
- [logging](rules/logging.md)：AutoLog、日志级别、TracePatch
- [localization](rules/localization.md)：TextId、语言文件、游戏术语
- [console-command](rules/console-command.md)：控制台命令

## 参考

- [glossary](reference/glossary.md)：游戏名词与类型对照
- [il2cppinterop-defects](reference/il2cppinterop-defects.md)：已确认的 Interop 缺陷与绕过
- 联机：[architecture](reference/multiplayer/architecture.md)、[business-sync](reference/multiplayer/business-sync.md)（开发方法）、[scene-flow](reference/multiplayer/scene-flow.md)、[yuyuko-challenge](reference/multiplayer/yuyuko-challenge.md)
- ResourceEx：[package-contract](reference/resourceex/package-contract.md)、[module-structure](reference/resourceex/module-structure.md)、[day-maps](reference/resourceex/day-maps.md)、[RuntimeAddressables](../src/MetaMystia.Mod/ResourceEx/Addressables/README.md)
- 服务端：[console](reference/server/console.md)、[chat-filter](reference/server/chat-filter.md)

## 调研

- [guest-fsm-audit](research/guest-fsm-audit.md)：普通营业顾客同步问题清单
- [day-map-native](research/day-map-native.md)：原版白天地图结构、NPC 与出口
- [native-keybinding](research/native-keybinding.md)：接入原版按键设置（未实现）

## 编写规则

- 主体使用中文；专有名词、类名、API、命令、路径和代码保留英文。
- 简洁、中立，不包装或宣传。只写约束、事实和难以从代码看出的经验，不复述代码。
- 按同级文档的粒度写：同类功能没有单独说明的，新功能也不单独加。根目录 `README.md` 只做总览，不写具体功能。
- 结论标注“运行时实测”“源码推断”“待验证”。
- 不写本机路径：仓库内用相对路径，仓库外的写在 `*.local.md`。
- 新增、移动或删除文档后更新本索引，并运行 `python tools/check.py`。

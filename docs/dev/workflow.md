# 提交与发布

## 分支

新开发使用独立分支和工作树，不直接在 `main` 上修改。分支名如 `feat/…`、`fix/…`、`docs/…`、`spell/…`。工作树位置见 `AGENTS.local.md`。

## 提交

- 英文 Conventional Commits：`type(scope): summary`。常用 type：`feat`、`fix`、`refactor`、`docs`、`test`、`ci`、`chore`。
- scope 使用模块名：`multiplayer`、`network`、`server`、`resourceex`、`spell`、`console`、`ui`、`locales`、`tools`。
- 一次提交只做一件事；格式化、重命名与行为修改分开。
- 不把 Agent 自己加入作者或 `Co-authored-by`；用户的署名可以添加。

## PR

- 标题同提交格式。正文用中文：概要、改动、验证。验证部分写明达到的等级和未覆盖的范围，见 [verification](verification.md)。
- 关联 issue 时写 `Fixes #<编号>`。
- 推送前确认 CI 中的编译、测试与 `tools/check.py` 在本地通过。

## 关联仓库

ResourceEx 配置字段、校验或加载规则变化时，同步更新：

- [`package-contract`](../reference/resourceex/package-contract.md) 等本仓库文档；
- ResourceEx Editor（`MetaMystia/MetaMystia-ResourceEx-Editor`）；
- 示例资源包（`MetaMystia/MetaMystia-ResourceExample`）；
- 用户文档（`doc.meta-mystia.izakaya.cc`）。

## 版本与发布

- 版本号在 `Versions.props`，单独提交：`chore: bump mod version to <版本>`。
- 推送标签 `v<版本>` 后，CI 校验标签与 `Versions.props` 一致，编译测试通过后创建 Release 草稿并上传模组 DLL 与 SHA-256。
- 正式发布 Release 后，`Deploy server` 自动部署到 `production` 环境；草稿和预发布不部署，执行时已不是最新正式 Release 则拒绝部署。保留手动入口，端口和退役实例默认自动选择。

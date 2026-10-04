# dsh-newpet

Mars 的深蓝色女仆鲸鱼娘桌宠，适用于 Windows 上的 DeepSeek Harness。

## 功能

- 独立透明置顶桌宠，桌面端退到后台后仍可操作。
- 悬停快捷按钮：唤起桌面端、查询 DeepSeek 余额。
- 任务跟随：显示任务、草稿、工具、步数和工作时长；任务气泡可折叠。
- 任务结束后显示「知道了」「查看」；出错显示独立表情。
- 拖动、贴边、缩放和位置保存；右键菜单可开关开机自启。
- 单击抚摸并唤起桌面端，双击庆祝，三击转圈；呼吸、摇摆、交叉淡入和粒子效果。
- 设置 → 看板娘：启停桌宠，修改用户称呼和宠物自称。
- 8 张关键立绘：待机、工作、思考、成功、出错、庆祝、睡觉、互动。

## 安装

```powershell
dsh plugin --profile desktop add github:Mars-bbt/dsh-newpet
```

重启 DeepSeek Harness。插件加载时启动桌宠，卸载时结束桌宠。

本地开发也可以安装源码目录。关闭桌宠后编译，重新启动即可更新程序：

```powershell
./desktop-pet/build.ps1
npm test
```

源码无需额外下载 NuGet 包，使用 Windows .NET Framework 编译器和 WPF。
唤起桌面端时清理从插件宿主继承的 Electron/Node 模式变量，通过桌面端的单实例入口恢复窗口。

## 数据

插件读取当前 `DSH_HOME` 中的本机会话缓存，查询余额时调用 DeepSeek 官方接口。
称呼和位置保存在 `%APPDATA%\MarsNewPet`，不随安装包或仓库发布。
源码仓库不包含 API Key、会话记录或用户任务内容。

## 作者和许可

Copyright (c) 2026 Mars。采用 [MIT License](LICENSE)。
素材生成方式与来源记录见 [ASSET_PROVENANCE.md](ASSET_PROVENANCE.md)。

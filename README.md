# PhantomGo

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)
[![C#](https://img.shields.io/badge/C%23-100%25-blue.svg)](https://dotnet.microsoft.com/)
[![WinUI3](https://img.shields.io/badge/WinUI3-Supported-green.svg)](https://docs.microsoft.com/en-us/windows/apps/design/patterns/winui/)

## 项目简介
PhantomGo 是一个基于 C# + WinUI3 的幻影围棋博弈系统，集成了 minigo 神经网络和蒙特卡洛树搜索算法。

## 项目架构

```
PhantomGo/
├── PhantomGo.Core/          # 核心逻辑库
│   ├── Agents/              # AI 算法实现
│   ├── Assets/              # minigo 神经网络模型
│   ├── Helpers/             # 辅助类
│   ├── Models/              # 围棋底层实体类
│   └── Logic/               # 围棋底层逻辑层
├── PhantomGo/               # WinUI3 界面库
│   ├── MainWindow.xaml      # 主窗口
│   ├── ContestWindow.xaml   # 竞赛窗口
│   ├── Models               # 实体类
│   └── Services/            # 逻辑层
└── PhantomGo.sln            # 解决方案文件
```

## 快速开始
系统要求
- 操作系统: Win10/11(2004及以上)

<img width="1116" height="793" alt="图片" src="https://github.com/user-attachments/assets/98789056-6227-4421-8385-9b4f5f3344b4" />


<img width="1116" height="793" alt="图片" src="https://github.com/user-attachments/assets/acbb889c-d906-4eb7-897b-7efa76595c17" />

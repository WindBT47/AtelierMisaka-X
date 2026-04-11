# AtelierMisaka Reforge

Current version: `v1.6.9`

AtelierMisaka Reforge is a Windows desktop tool for browsing and downloading creator content from Fanbox, Fantia, and Patreon.

Reforge is the continued maintenance line after the original branch stopped active development. It keeps the original use case and continues with UI consolidation, Fantia polling support, and ongoing fixes.

## Overview

AtelierMisaka Reforge focuses on three workflows:

* Browse creator posts
* Queue and download supported content
* Manage downloads inside a unified main window

The current UI uses three main sections:

* `下载`
* `文章`
* `设置`

Fantia also supports a built-in polling mode that checks for new posts every 90 seconds.

## Main Features

* Support Fanbox, Fantia, and Patreon
* Load supported creator lists
* Browse creator post lists
* Filter by publish date and support tier or price range
* Download files, images, and attached content
* View active downloads and completed items in the main window
* Fantia polling mode with 90-second checks
* Proxy settings
* Multi-language UI: `zh-CN`, `ja-JP`, `en-US`
* Fanbox like support

## Notes

* This tool is for content you can already access, including supported and public content.
* Please support the creators whose work you enjoy.
* Some site flows still rely on your own session or cookie setup.

## Quick Start

1. Open `设置`.
2. Choose `Fanbox`, `Fantia`, or `Patreon`.
3. Set the creator URL, save path, and account or proxy options if needed.
4. Click `确定`.
5. Use `文章` to browse posts.
6. Use `下载` to watch active and completed tasks.
7. On Fantia, enable `轮询模式` if you want automatic checks every 90 seconds.

## Runtime Requirements

* Windows 10 or later
* [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)
* [Microsoft Visual C++ Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)

The packaged release includes the CefSharp runtime used by the application.

---

# 欢迎来到 AtelierMisaka Reforge

当前版本：`v1.6.9`

AtelierMisaka Reforge 是一个 Windows 桌面工具，用于查看和下载 Fanbox、Fantia、Patreon 上创作者已公开或已支援可访问的内容。

Reforge 这条线是原分支停止活跃维护后的继续维护版本。它保留原来的使用目标，并持续推进界面整合、Fantia 轮询支持和后续修正。

## 项目简介

当前版本围绕三个核心操作展开：

* 查看文章
* 管理下载
* 在主界面内完成设置

当前主界面已经统一为三个主区：

* `下载`
* `文章`
* `设置`

Fantia 额外支持 `90 秒轮询模式`，可自动检查新 post。

## 主要功能

* 支持 Fanbox、Fantia、Patreon
* 获取支援列表
* 显示作者文章列表
* 按发表时间和支援等级或价格区间筛选
* 下载文件、图片和附件内容
* 在主窗口内查看下载中和已完成列表
* Fantia 轮询模式，每 90 秒检查一次
* 代理设置
* 多语言支持：`简体中文`、`日文`、`英文`
* Fanbox 点赞功能

## 使用说明

* 这个工具只面向您已经有权限访问的内容，包括已支援内容和公开内容。
* 如果有喜欢的作者，请尽量支援。
* 部分站点仍然需要您自己准备会话信息或 Cookies。

## 快速开始

1. 打开 `设置`。
2. 选择 `Fanbox`、`Fantia` 或 `Patreon`。
3. 填写作者链接、保存路径，以及需要的账号或代理配置。
4. 点击 `确定`。
5. 在 `文章` 页面查看文章列表。
6. 在 `下载` 页面查看当前任务和已完成项目。
7. 如果是 Fantia，可以开启 `轮询模式`，程序会每 90 秒自动检查新 post。

## 运行环境

* Windows 10 及以上
* [.NET Framework 4.8](https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/net48)
* [Microsoft Visual C++ Redistributable](https://learn.microsoft.com/zh-cn/cpp/windows/latest-supported-vc-redist)

发布包内已包含程序运行所需的 CefSharp 运行库。

---

# AtelierMisaka Reforge へようこそ

現在のバージョン：`v1.6.9`

AtelierMisaka Reforge は、Fanbox、Fantia、Patreon の投稿とコンテンツを閲覧し、ダウンロードするための Windows デスクトップツールです。

Reforge は、元のブランチの更新が止まったあとに続けて保守しているラインです。元の用途を引き継ぎつつ、UI の統合、Fantia のポーリング対応、継続的な修正を進めています。

## プロジェクト概要

現在のバージョンは次の 3 つの作業を中心にしています。

* 投稿一覧を確認する
* ダウンロードを管理する
* メイン画面内で設定を完結させる

メイン画面は次の 3 つのセクションで構成されています。

* `下载`
* `文章`
* `设置`

Fantia では `90 秒ごとのポーリングモード` に対応しています。

## 主な機能

* Fanbox、Fantia、Patreon に対応
* 支援中クリエイター一覧の取得
* 投稿一覧の表示
* 公開日時と支援プランまたは価格帯でのフィルタ
* ファイル、画像、添付コンテンツのダウンロード
* メインウィンドウ内での進行中一覧と完了一覧の表示
* Fantia の 90 秒ポーリングモード
* Proxy 設定
* 多言語 UI：`中国語`、`日本語`、`英語`
* Fanbox の Like 対応

## 利用上の注意

* このツールは、すでにアクセス権のある公開コンテンツと支援済みコンテンツを対象にしています。
* 好きなクリエイターがいれば、ぜひ支援してください。
* 一部サイトでは、自分のセッション情報や Cookies の準備が必要です。

## クイックスタート

1. `设置` を開きます。
2. `Fanbox`、`Fantia`、`Patreon` のいずれかを選びます。
3. クリエイター URL、保存先、必要なアカウント情報や Proxy を設定します。
4. `确定` を押します。
5. `文章` で投稿を確認します。
6. `下载` で進行中と完了済みのタスクを確認します。
7. Fantia では `轮询模式` を有効にすると、90 秒ごとに新しい post を確認します。

## 動作環境

* Windows 10 以降
* [.NET Framework 4.8](https://dotnet.microsoft.com/ja-jp/download/dotnet-framework/net48)
* [Microsoft Visual C++ Redistributable](https://learn.microsoft.com/ja-jp/cpp/windows/latest-supported-vc-redist)

配布パッケージには CefSharp ランタイムが含まれています。

---

## Libraries

* [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json)
* [CefSharp](https://github.com/cefsharp/CefSharp)
* [SQLite.NET](https://github.com/praeclarum/sqlite-net)

# AtelierMisaka Reforge

Current version: `v1.6.10`

AtelierMisaka Reforge is a Windows desktop tool for browsing and downloading creator content from Fanbox, Fantia, Patreon, and X.

Reforge is the continued maintenance line after the original branch stopped active development. It keeps the original use case and continues with UI consolidation, Fantia polling support, X media capture, and ongoing fixes.

## Overview

AtelierMisaka Reforge focuses on three workflows:

* Browse creator posts
* Scan, search, and monitor X media from your logged-in browser session
* Queue and download supported content
* Manage downloads inside a unified main window

The current UI uses three main sections:

* `下载`
* `文章`
* `设置`

Fantia also supports a built-in polling mode that checks for new posts every 20-30 seconds randomly.

X support is split into three modes:

* `Media Scan`: quickly scans the creator's recent media timeline.
* `History Backfill`: uses X search syntax such as `from:username since:2017-01-01 until:2018-01-01 filter:media` to backfill older media that may no longer appear reliably in the normal timeline.
* `Notification Watch`: checks the logged-in account's notifications for new media posts after the watch starts.

### Why X Support Is Different

Many X download tools mainly depend on timeline back-scrolling, page DOM parsing, or a single `/media` view. That is fast for recent posts, but it can miss older posts once the timeline hits practical backfill limits or when X stops showing old indexed content reliably.

AtelierMisaka Reforge uses a browser-session based capture pipeline instead:

* It captures the X Web JSON responses that the logged-in browser actually receives.
* It keeps the fast recent media scan for day-to-day use.
* It adds history search backfill for older media ranges that timeline-only tools often miss.
* It adds notification watching for new media after you start monitoring.
* It keeps retweet media and quote media as separate switches, so you can avoid pulling unrelated reposted content by default.

This does not mean every old post is guaranteed to appear, because X search and timeline indexing are not guaranteed to be complete. The point is practical coverage: Reforge combines multiple X Web paths instead of betting everything on one timeline.

## Main Features

* Support Fanbox, Fantia, Patreon, and X
* Load supported creator lists
* Browse creator post lists
* Filter by publish date and support tier or price range
* Download files, images, and attached content
* View active downloads and completed items in the main window
* Fantia polling mode with random 20-30 second checks
* X recent media scan
* X history search backfill for older media
* X notification watch with random 45-75 second checks
* X retweet media and quote media switches
* Proxy settings
* Multi-language UI: `zh-CN`, `ja-JP`, `en-US`
* Fanbox like support

## Notes

* This tool is for content you can already access, including supported and public content.
* Please support the creators whose work you enjoy.
* Some site flows still rely on your own session or cookie setup.
* X history backfill improves coverage for older posts, but X search and timeline results are not guaranteed to be complete.
* X Web structures can change, so X parsing may require future maintenance.

## Quick Start

1. Open `设置`.
2. Choose `Fanbox`, `Fantia`, `Patreon`, or `X`.
3. Set the creator URL, save path, and account or proxy options if needed.
4. Click `确定`.
5. Use `文章` to browse posts.
6. Use `下载` to watch active and completed tasks.
7. On Fantia, enable `轮询模式` if you want automatic checks every 20-30 seconds.
8. On X, log in with `打开浏览器`, then choose `媒体扫描`, `历史回填`, or `通知监听`.

## Runtime Requirements

* Windows 10 or later
* [.NET Framework 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)
* [Microsoft Visual C++ Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)

The packaged release includes the CefSharp runtime used by the application.

---

# 欢迎来到 AtelierMisaka Reforge

当前版本：`v1.6.10`

AtelierMisaka Reforge 是一个 Windows 桌面工具，用于查看和下载 Fanbox、Fantia、Patreon、X 上创作者已公开或已支援可访问的内容。

Reforge 这条线是原分支停止活跃维护后的继续维护版本。它保留原来的使用目标，并持续推进界面整合、Fantia 轮询、X 媒体抓取和后续修正。

## 项目简介

当前版本围绕三个核心操作展开：

* 查看文章
* 抓取、搜索和监听 X 媒体
* 管理下载
* 在主界面内完成设置

当前主界面已经统一为三个主区：

* `下载`
* `文章`
* `设置`

Fantia 额外支持 `20-30 秒随机轮询模式`，可自动检查新 post。

X 支持三种运行方式：

* `媒体扫描`：快速扫描作者近期媒体时间线。
* `历史回填`：使用 X 搜索语法补抓旧媒体，例如 `from:用户名 since:2017-01-01 until:2018-01-01 filter:media`，适合处理普通时间线无法稳定回溯的旧帖。
* `通知监听`：监听当前登录账号启动后的新通知，发现通知关联媒体后加入下载队列。

### X 抓取为什么不一样

很多 X 下载工具主要依赖时间线滚动、页面 DOM 解析，或者单一的 `/media` 页面。这种方式抓近期内容很快，但遇到高产账号、旧年份内容、时间线回溯上限、搜索索引不稳定时，很容易漏掉旧帖。

AtelierMisaka Reforge 的 X 抓取不是只赌一条路径：

* 捕获已登录内嵌浏览器实际收到的 X Web JSON 响应。
* 保留近期媒体扫描，用来快速处理最近内容。
* 增加历史搜索回填，用时间范围和搜索语法补抓时间线拿不到的旧媒体。
* 增加通知监听，用来处理启动后出现的新通知媒体。
* 转发媒体和引用媒体是独立开关，默认可以避免把作者转发流里的大量外部内容一起拉下来。

这不等于保证所有旧帖 100% 都能出现，因为 X 搜索和时间线索引本身不保证完整。它的优势是实用覆盖率：不是只翻一条时间线，而是组合多条 X Web 路径处理不同场景。

## 主要功能

* 支持 Fanbox、Fantia、Patreon、X
* 获取支援列表
* 显示作者文章列表
* 按发表时间和支援等级或价格区间筛选
* 下载文件、图片和附件内容
* 在主窗口内查看下载中和已完成列表
* Fantia 轮询模式，每 20-30 秒随机检查一次
* X 近期媒体扫描
* X 历史搜索回填，用于补抓旧媒体
* X 通知监听，每 45-75 秒随机检查一次
* X 转发媒体和引用媒体独立开关
* 代理设置
* 多语言支持：`简体中文`、`日文`、`英文`
* Fanbox 点赞功能

## 使用说明

* 这个工具只面向您已经有权限访问的内容，包括已支援内容和公开内容。
* 如果有喜欢的作者，请尽量支援。
* 部分站点仍然需要您自己准备会话信息或 Cookies。
* X 历史回填能改善旧帖覆盖率，但 X 搜索和时间线结果本身不保证完整。
* X Web 结构可能变化，相关解析逻辑后续可能需要维护。

## 快速开始

1. 打开 `设置`。
2. 选择 `Fanbox`、`Fantia`、`Patreon` 或 `X`。
3. 填写作者链接、保存路径，以及需要的账号或代理配置。
4. 点击 `确定`。
5. 在 `文章` 页面查看文章列表。
6. 在 `下载` 页面查看当前任务和已完成项目。
7. 如果是 Fantia，可以开启 `轮询模式`，程序会每 20-30 秒随机检查新 post。
8. 如果是 X，先用 `打开浏览器` 登录，然后选择 `媒体扫描`、`历史回填` 或 `通知监听`。

## 运行环境

* Windows 10 及以上
* [.NET Framework 4.8](https://dotnet.microsoft.com/zh-cn/download/dotnet-framework/net48)
* [Microsoft Visual C++ Redistributable](https://learn.microsoft.com/zh-cn/cpp/windows/latest-supported-vc-redist)

发布包内已包含程序运行所需的 CefSharp 运行库。

---

# AtelierMisaka Reforge へようこそ

現在のバージョン：`v1.6.10`

AtelierMisaka Reforge は、Fanbox、Fantia、Patreon、X の投稿とコンテンツを閲覧し、ダウンロードするための Windows デスクトップツールです。

Reforge は、元のブランチの更新が止まったあとに続けて保守しているラインです。元の用途を引き継ぎつつ、UI の統合、Fantia のポーリング対応、X メディア取得、継続的な修正を進めています。

## プロジェクト概要

現在のバージョンは次の 3 つの作業を中心にしています。

* 投稿一覧を確認する
* X のメディアを取得、検索、監視する
* ダウンロードを管理する
* メイン画面内で設定を完結させる

メイン画面は次の 3 つのセクションで構成されています。

* `下载`
* `文章`
* `设置`

Fantia では `20-30 秒ごとのランダムポーリングモード` に対応しています。

X は次の 3 つの動作モードに対応しています。

* `メディア取得`：クリエイターの最近のメディアタイムラインを素早く取得します。
* `履歴補完`：`from:username since:2017-01-01 until:2018-01-01 filter:media` のような X 検索構文を使い、通常のタイムラインで安定して遡れない古いメディアを補完します。
* `通知監視`：ログイン中アカウントの通知を確認し、開始後に出現した通知関連のメディアをダウンロードキューへ追加します。

### X 取得がほかと違う点

多くの X ダウンロードツールは、タイムラインのスクロール、ページ DOM の解析、または単一の `/media` 画面に依存します。最近の投稿には速い一方で、投稿数の多いアカウント、古い年の投稿、タイムラインの遡り制限、検索インデックスの不安定さに弱くなります。

AtelierMisaka Reforge の X 取得は、1 つの経路だけに依存しません。

* ログイン済みの内蔵ブラウザが実際に受け取った X Web JSON レスポンスを取得します。
* 最近のメディア取得を残し、普段の取得を素早く処理します。
* 履歴検索補完で、時間範囲と検索構文を使ってタイムラインだけでは届かない古いメディアを補完します。
* 通知監視で、開始後に出現した新しい通知関連メディアを処理します。
* リポストメディアと引用メディアは個別スイッチにし、不要な外部コンテンツを既定で避けられます。

X の検索結果やタイムラインインデックスは完全とは限らないため、古い投稿を 100% 保証するものではありません。強みは実用上のカバー率です。1 本のタイムラインだけに頼らず、複数の X Web 経路を組み合わせています。

## 主な機能

* Fanbox、Fantia、Patreon、X に対応
* 支援中クリエイター一覧の取得
* 投稿一覧の表示
* 公開日時と支援プランまたは価格帯でのフィルタ
* ファイル、画像、添付コンテンツのダウンロード
* メインウィンドウ内での進行中一覧と完了一覧の表示
* Fantia の 20-30 秒ランダムポーリングモード
* X の最近メディア取得
* X の履歴検索補完
* X の通知監視、45-75 秒ごとのランダム確認
* X のリポストメディアと引用メディアの個別スイッチ
* Proxy 設定
* 多言語 UI：`中国語`、`日本語`、`英語`
* Fanbox の Like 対応

## 利用上の注意

* このツールは、すでにアクセス権のある公開コンテンツと支援済みコンテンツを対象にしています。
* 好きなクリエイターがいれば、ぜひ支援してください。
* 一部サイトでは、自分のセッション情報や Cookies の準備が必要です。
* X の履歴補完は古い投稿のカバー率を改善しますが、X の検索結果やタイムライン結果は完全とは限りません。
* X Web の構造は変わる可能性があるため、X 解析部分は今後も保守が必要になる場合があります。

## クイックスタート

1. `设置` を開きます。
2. `Fanbox`、`Fantia`、`Patreon`、`X` のいずれかを選びます。
3. クリエイター URL、保存先、必要なアカウント情報や Proxy を設定します。
4. `确定` を押します。
5. `文章` で投稿を確認します。
6. `下载` で進行中と完了済みのタスクを確認します。
7. Fantia では `轮询模式` を有効にすると、20-30 秒ごとにランダムで新しい post を確認します。
8. X では、まず `打开浏览器` でログインし、`媒体扫描`、`历史回填`、`通知监听` のいずれかを選びます。

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

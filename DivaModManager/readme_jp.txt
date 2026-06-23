
DivaModManager by Enomoto v1.3.1.35 (beta5)


----- 注意(必ず読んでください！！) -------------------------

- 一部Windows版DivaModManager by Enomoto v1.3.1.34から機能を制限しているものや動作不安定な機能を含みます

- 自己責任で利用してください
- 下に記載した[動作確認済の環境]以外での動作は未確認です
- 特にLinux系統でもディストリビューションが異なる場合やMac OSなどの環境では、OSが動作不可能になる可能性があります

- .NETのインストールは不要です(代わりに実行ファイルのサイズが非常に大きいです)
- まずはWindows版と極力同じ機能の実装を優先するため、機能リクエスト等は基本的に受け付けられません


----- 不具合や機能制限など -------------------------

本ツールのLinuxやSteam Deck上の起動は、"既にMod動作環境が構築完了している方々向けのMod管理ツール"を前提といます
具体的には、Windows版DivaModManager by Enomoto v1.3.1.34から以下の変更があります

### Windows・Linux共通
- UI変更
- Moduleタブ、Songタブの仮実装
  使用方法はGitHubをご覧ください
- キャッシュ機能の実装
  一部回線が遅い方やGameBananaタブにおいて、過去バージョンよりも初回動作が遅くなる可能性があります
  またDivaModManager/cacheフォルダにキャッシュが蓄積されます(容量にご注意ください)
  一度キャッシュを生成した状態であれば読み込み時間の短縮やAPIへの過剰なアクセス制御が期待されます
  現在以下の時間がプログラムによって設定されています。
    GameBanana、DivaModArchiveのAPIキャッシュ : 3時間
    GameBanana、DivaModArchiveの画像キャッシュ : 72時間
- エラー報告用テンプレートを同梱

### Linuxでの制限
- WineまたはProton(Steam)上での動作を確認しております
  (Linuxネイティブではないことをご了承ください)
- 起動時に自動でDivaModLoaderをダウンロード・配置する機能の停止
- Update Coreボタンの機能の停止
- ポップアップウインドウがアクティブにならない(仕様化するかもしれません)
- ファイルの削除時に原則ゴミ箱に入るようになっています
  DivaModManager/Downloadsフォルダに一時ファイルが展開されますので、空き容量にご注意ください


----- 動作確認済の環境 -------------------------

### beta5
- Windows 11 Home 25H2
- Ubuntu 26.04 (on VMware Workstation) + Proton Experimental 11.0


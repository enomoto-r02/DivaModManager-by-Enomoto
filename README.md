
## 注意事項
自己責任で使用してください。

## 概要
本ソフトウェアは[TekkaGB氏のDivaModManager](https://github.com/TekkaGB/DivaModManager)をベースにパフォーマンスの向上や機能追加等を行ったものです。</br>
オリジナルのDivaModManagerを上書きして動くようにしているつもりですが、上書き前にバックアップを取るようお願いします。

#### 動作について
ネイティブなWindows 11のみを動作対象としています。</br>
Mac、Linux、SteamDeck、WineやProton、他の環境は動作対象外です。

#### その他
- 依存している外部サービス等の影響で動作しなくなる場合があります。
- 個人開発のため、プルリクエスト等については対応できない場合があります。
- ソースの流用等はライセンスに基づく形でご使用ください。

## よくある質問

#### [質問 1]
メッセージ「[WINDOW-0011] Extraction may have failed (SIZE_UNMATCH)」が表示されます。

#### [回答 1]
以下の手順を行ってみてください。

1 DivaModManagerの配置を、より浅い階層に移動してください。</br
以下のようなディレクトリに配置されている場合、このエラーメッセージが表示されやすくなります。</br>
> C:\Program Files (x86)\Steam\steamapps\common\Hatsune Miku Project DIVA Mega Mix Plus\DivaModManager

その場合、DivaModManagerフォルダを以下のような場所へ移動してください</br>
> C:\DivaModManager

2 上記で問題が解決しない場合は、DivaModManagerを経由せずにブラウザからModをダウンロードし、手動で解凍して配置してください。

#### [質問 2]
Windows 以外の環境、例えば Linux、Steam Deck、Mac、仮想環境（Wine または Proton）では動作しますか？</br>
それらのサポートの予定はありますか？

#### [回答 2]
動作しません。サポートする予定はありません。

## Notice
Use at your own risk.

## Overview
This software is based on [TekkaGB's DivaModManager]((https://github.com/TekkaGB/DivaModManager)) and has been enhanced for performance improvements and additional features.</br>
We intend for this version to overwrite and run in place of the original; however, please back up your data before overwriting.

#### System Requirements
This software is designed to run only on native Windows 11.</br>
Mac, Linux, SteamDeck, Wine, Proton, or any other environments are not supported.

#### Notes  
- The software may stop working due to dependencies on external services.
- Because this is a personal project, we may not be able to respond to pull requests or similar requests.
- Please use the source code in accordance with its license terms.

## Troubleshooting & Compatibility

#### [Question 1]
The message "[WINDOW-0011] Extraction may have failed (SIZE_UNMATCH)" appears.

#### [Answer 1]
Please follow the steps below.

1 Move the DivaModManager directory to a shallower folder hierarchy.</br>
For example, if it is located in a directory such as the one shown below, this error message is more likely to appear.
> C:\Program Files (x86)\Steam\steamapps\common\Hatsune Miku Project DIVA Mega Mix Plus\DivaModManager

In that case, move the DivaModManager folder to a location like the following
> C:\DivaModManager

2 If moving the folder as described in step 1 does not resolve the issue, please download the Mod from the browser instead of using DivaModManager, then manually extract and place it.

#### [Question 2]
Will it work on environments other than Windows, such as Linux, Steam Deck, Mac, or virtual environments (Wine or Proton)?</br>
Is there any plan to support them?

#### [Answer 2]
It does not work. There are no plans to support it.

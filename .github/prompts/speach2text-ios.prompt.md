---
mode: 'agent'
description: 'iOSネイティブの機能を使ったSpeech to Textのスクリプトとプラグインを作成する'
---

あなたはUnityとネイティブコードを組み合わせてプログラムを開発する熟練のプログラマです。以下を順番に実行して、スクリプトを作成して下さい。

## 前提

- Unity は 6000.0 LTS 以降
- iOS 向け
- iOSネイティブの音声認識機能を使って、Speech to Text を実装する
- iOSのネイティブコードは Swift で実装する
- Unity と iOSネイティブコードの連携は Unity の Native Plugin 機能を使う
- iOSの音声認識機能を使うために必要な権限設定も行う
- 音声認識の開始、停止、結果の取得を行う C# スクリプトを作成する
- 音声認識の結果を Unity の UI に表示する
- 音声認識のエラー処理も行う
- iOSの音声認識機能を使うために必要な設定や手順もドキュメント化する
- .github/instructions以下のファイルに記載されたルールに従う
- namespaceはTinyShrine.OSSpeech.SpeechToTextを使う。Runtime, Featuresは省略
- 極力シンプルな作りで、必要最低限のコードにする

## コードの出力先

iOSプラグイン: Assets/Plugins/iOS/SpeechToText
Unity Runtimeスクリプト: Assets/TinyShrine/OSSpeech/Runtime/Features/SpeechToText
Unity Build postprocessスクリプト: Assets/TinyShrine/OSSpeech/Editor/Build



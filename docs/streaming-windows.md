# Windows TTS ストリーミング対応

ご要望の「ファイル出力ではなく、Unityへストリーミング接続」に対応するため、Windows TTS プラグインのヘッダー/実装と Unity ブリッジを拡張し、PCM チャンクをコールバックで受け取れるようにしました。

## 変更点

- TtsPlugin.h
  - ストリーミング用APIを追加
    - `TTS_GetOutputPcmFormat(int*, int*, int*)` 16kHz/16bit/mono 固定を返却
    - `TTS_SetStreamCallbacks(TTS_AudioChunkCallback, TTS_CompleteCallback, void*)`
    - `TTS_SpeakAsync(const char* textUtf8)` 非同期合成開始（チャンクを逐次コールバック送出）
    - `TTS_Cancel()` 合成キャンセル
  - 既存の `TTS_SynthesizeToWav` は残置しつつ [Deprecated] コメントを付与

- TtsPlugin.cpp
  - ストリーミング実装を追加
    - SAPI の出力先にメモリ IStream（`MemoryWavSink`）を実装し、WAVヘッダを剥いでPCMを100ms（約3200bytes）単位でコールバック
    - 合成はバックグラウンドスレッドで COM STA を初期化し、スレッドローカルの `ISpVoice` を生成
    - `TTS_Init/TTS_SetRate/TTS_SetVolume` で選択ボイス/話速/音量を保持し、ワーカー内に反映
    - `TTS_Cancel()` はキャンセルフラグを立ててワーカー終了を待機、完了通知に負値を返却

- TtsBridge.cs
  - 新しいストリーミングAPIを P/Invoke で追加
    - イベント: `OnPcmChunk(byte[])`, `OnSynthesisComplete(int)`
    - メソッド: `EnsureStreamingCallbacksRegistered()`, `GetOutputPcmFormat()`, `SpeakAsync(string)`, `Cancel()`
  - 既存の `SpeakToClip` はそのまま（非ストリーミング用途向け）

- 追加: TtsStreamingPlayer.cs
  - 簡易プレイヤー（`AudioSource` と組み合わせ）
  - 受信チャンクをリングバッファに格納し `OnAudioFilterRead` で再生

## 使い方

- 初期化と設定（従来と同じ）
  - `TtsBridge.Init(voiceName, rate, volume);`
- ストリーミング再生
  - シーンに `AudioSource` を持つ `TtsStreamingPlayer` を配置（自動で `OnPcmChunk` を購読し再生）
  - または独自に:
    - `TtsBridge.EnsureStreamingCallbacksRegistered();`
    - `TtsBridge.OnPcmChunk += bytes => { /* bytes: 16kHz/16bit/mono PCM */ };`
    - `TtsBridge.OnSynthesisComplete += status => { /* 0=成功, <0=キャンセル, >0=HRESULT */ };`
  - 再生開始: `TtsBridge.SpeakAsync("こんにちは");`
  - キャンセル: `TtsBridge.Cancel();`

注意:

- 現状のフォーマットは 16kHz/16bit/mono 固定です（`TTS_GetOutputPcmFormat` で取得可能）。
- `TtsStreamingPlayer` は簡易実装のため、Unity の出力サンプルレートと16kHzが異なる場合はピッチが変わります（必要に応じてリサンプルをご検討ください）。

## 動作概要（内部）

- 合成はバックグラウンドスレッドで COM 初期化しローカル `ISpVoice` を生成、選択ボイス/話速/音量を反映。
- 出力は `IStream` に WAV として書き込ませ、終了後に WAV ヘッダを解析し PCM 部分だけを 100ms チャンクでコールバック。
- キャンセル時は早期終了し、完了コールバックに負値を返却。

## 品質ゲート

- Lint/Typecheck: PASS（変更ファイルに構文エラーなし、C# スタイル指摘は修正済み）
- Build: 未実行（Unity プロジェクトのため本環境ではビルド未実行）
- Tests: なし（本変更に伴う新規テストは未追加）

## 次の改善候補

- 出力サンプルレートに合わせたリサンプル導入（オンザフライ）
- ステレオ/サンプルレート可変のフォーマット指定 API 拡張
- チャンクサイズやスロットリング（現在 Sleep 50ms）の調整・除去（より低レイテンシ化）
- 既存 API `TTS_SynthesizeToWav` の正式非推奨化アナウンス（ドキュメント更新）

このまま `TtsStreamingPlayer` をシーンに置いて `TtsBridge.SpeakAsync` を呼べば、Unity 側でリアルタイム再生できます。必要があれば、リングバッファサイズやリサンプル周りの最適化も進めます。

変更を行いました。

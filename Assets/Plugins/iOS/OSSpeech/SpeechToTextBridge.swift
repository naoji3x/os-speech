import AVFoundation
import Foundation
import Speech

// C#へ返すコールバック（UTF-8, 最終結果フラグ）
public typealias SttCallback = @convention(c) (_ utf8Text: UnsafePointer<CChar>?, _ isFinal: Bool)
  -> Void

private var gCallback: SttCallback?
private var gLocale = "ja-JP"

private var recognizer: SFSpeechRecognizer?
private var engine: AVAudioEngine?
private var request: SFSpeechAudioBufferRecognitionRequest?
private var task: SFSpeechRecognitionTask?

// Stop/Final の堅牢化用の状態
private var lastNonEmptyText: String = ""
private var hasSentFinal: Bool = false
private var stopRequested: Bool = false
private var finalFallbackWorkItem: DispatchWorkItem?

@_cdecl("stt_set_callback")
public func stt_set_callback(_ cb: @escaping SttCallback) { gCallback = cb }

@_cdecl("stt_set_locale")
public func stt_set_locale(_ localeUtf8: UnsafePointer<CChar>?) -> Int32 {
  guard task == nil else { return -1 }  // 実行中は変更不可
  gLocale = (localeUtf8 != nil) ? String(cString: localeUtf8!) : "ja-JP"
  return 0
}

// 0 NotDetermined, 1 Denied, 2 Restricted, 3 Authorized
@_cdecl("stt_request_authorization")
public func stt_request_authorization() -> Int32 {
  var result: Int32 = -1
  let sem = DispatchSemaphore(value: 0)
  SFSpeechRecognizer.requestAuthorization { s in
    result = Int32(s.rawValue)
    sem.signal()
  }
  // マイク権限（iOSとmacOSの両方でTCC対象）
  AVCaptureDevice.requestAccess(for: .audio) { _ in }
  _ = sem.wait(timeout: .now() + 5)
  return result
}

@_cdecl("stt_start")
public func stt_start() -> Int32 {
  if task != nil { return -2 }

  recognizer = SFSpeechRecognizer(locale: Locale(identifier: gLocale))
  guard let rec = recognizer, rec.isAvailable else { return -3 }

  // === iOS と macOS の違いはここで吸収 ===
  do {
    #if os(iOS)
      let session = AVAudioSession.sharedInstance()
      try session.setCategory(.record, options: [.duckOthers])
      try session.setMode(.measurement)
      try session.setActive(true)
    #endif
  } catch { return -4 }

  engine = AVAudioEngine()
  request = SFSpeechAudioBufferRecognitionRequest()
  request?.shouldReportPartialResults = true
  guard let eng = engine, let req = request else { return -5 }

  // 状態初期化
  lastNonEmptyText = ""
  hasSentFinal = false
  stopRequested = false
  finalFallbackWorkItem?.cancel()
  finalFallbackWorkItem = nil

  let node = eng.inputNode
  let fmt = node.outputFormat(forBus: 0)
  node.removeTap(onBus: 0)
  node.installTap(onBus: 0, bufferSize: 1024, format: fmt) { buf, _ in req.append(buf) }

  eng.prepare()
  do { try eng.start() } catch {
    cleanup()
    return -6
  }

  task = recognizer?.recognitionTask(with: req) { result, error in
    if let r = result {
      let currentText = r.bestTranscription.formattedString
      if !currentText.isEmpty { lastNonEmptyText = currentText }

      // Final は空なら最後の非空をフォールバック
      let textToSend: String = (r.isFinal && currentText.isEmpty) ? lastNonEmptyText : currentText
      textToSend.withCString { cstr in
        gCallback?(cstr, r.isFinal)
      }

      if r.isFinal {
        hasSentFinal = true
        finalFallbackWorkItem?.cancel()
        finalFallbackWorkItem = nil
        cleanup()
        return
      }
    }

    if let _ = error {
      // Stop 要求でエラー経路になった場合でも最終を補完（空でも必ずFinalを送る）
      if stopRequested && !hasSentFinal {
        lastNonEmptyText.withCString { cstr in gCallback?(cstr, true) }
        hasSentFinal = true
      }
      finalFallbackWorkItem?.cancel()
      finalFallbackWorkItem = nil
      cleanup()
    }
  }
  return 0
}

@_cdecl("stt_stop")
public func stt_stop() {
  stopRequested = true

  // 入力を止め、追加のオーディオが来ないことを知らせる（グレースフルクローズ）
  if let e = engine {
    e.inputNode.removeTap(onBus: 0)
    e.stop()
  }
  request?.endAudio()

  // しばらく待っても recognitionTask から Final が来ない場合のフォールバック
  if finalFallbackWorkItem == nil {
    let work = DispatchWorkItem {
      if !hasSentFinal {
        // 空文字でもFinalを必ず送ることでC#側の完了待ちを解放
        lastNonEmptyText.withCString { cstr in gCallback?(cstr, true) }
        hasSentFinal = true
      }
      cleanup()
    }
    finalFallbackWorkItem = work
    // 体感と安定性のバランスを取った短い待機（必要に応じて調整）
    DispatchQueue.main.asyncAfter(deadline: .now() + 0.8, execute: work)
  }
}

private func cleanup() {
  // タイマーを必ず止める
  finalFallbackWorkItem?.cancel()
  finalFallbackWorkItem = nil

  // 認識タスクの終了。Stop 要求のケースでは既に endAudio 済み
  task?.cancel()
  task = nil

  // リクエストとエンジンの後始末（冪等）
  request?.endAudio()
  request = nil
  if let e = engine {
    e.inputNode.removeTap(onBus: 0)
    e.stop()
  }
  engine = nil
}

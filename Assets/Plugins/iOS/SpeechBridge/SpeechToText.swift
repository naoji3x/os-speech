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
      r.bestTranscription.formattedString.withCString { cstr in
        gCallback?(cstr, r.isFinal)
      }
      if r.isFinal { cleanup() }
    }
    if error != nil { cleanup() }
  }
  return 0
}

@_cdecl("stt_stop")
public func stt_stop() { cleanup() }

private func cleanup() {
  task?.cancel()
  task = nil
  request?.endAudio()
  request = nil
  if let e = engine {
    e.inputNode.removeTap(onBus: 0)
    e.stop()
  }
  engine = nil
}

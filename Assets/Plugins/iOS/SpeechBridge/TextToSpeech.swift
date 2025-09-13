import AVFoundation
import Foundation

// C# 側へイベント通知: 0=Started, 1=Finished, 2=Cancelled, 5=Error
public typealias TtsEventCallback = @convention(c) (_ event: Int32) -> Void

private var gEventCb: TtsEventCallback?
private let synth = AVSpeechSynthesizer()
private var gVoiceId: String?
private var gLang = "ja-JP"

private final class SynthDelegate: NSObject, AVSpeechSynthesizerDelegate {
  func speechSynthesizer(_ s: AVSpeechSynthesizer, didStart utterance: AVSpeechUtterance) {
    gEventCb?(0)
  }
  func speechSynthesizer(_ s: AVSpeechSynthesizer, didFinish utterance: AVSpeechUtterance) {
    gEventCb?(1)
  }
  func speechSynthesizer(_ s: AVSpeechSynthesizer, didCancel utterance: AVSpeechUtterance) {
    gEventCb?(2)
  }
}
private let delegateObj = SynthDelegate()

@_cdecl("tts_set_event_callback")
public func tts_set_event_callback(_ cb: @escaping TtsEventCallback) {
  gEventCb = cb
  synth.delegate = delegateObj
}

@_cdecl("tts_set_language")
public func tts_set_language(_ langUtf8: UnsafePointer<CChar>?) -> Int32 {
  gLang = (langUtf8 != nil) ? String(cString: langUtf8!) : "ja-JP"
  return 0
}

@_cdecl("tts_set_voice_id")
public func tts_set_voice_id(_ idUtf8: UnsafePointer<CChar>?) -> Int32 {
  gVoiceId = idUtf8 != nil ? String(cString: idUtf8!) : nil
  return 0
}

@_cdecl("tts_is_speaking")
public func tts_is_speaking() -> Int32 {
  return synth.isSpeaking ? 1 : 0
}

@_cdecl("tts_stop")
public func tts_stop() {
  synth.stopSpeaking(at: .immediate)
}

@_cdecl("tts_speak")
public func tts_speak(
  _ textUtf8: UnsafePointer<CChar>?,
  _ voiceOrNull: UnsafePointer<CChar>?,  // nullなら gVoiceId/lang を使用
  _ rate01: Float,  // 0.5..1.5 推奨
  _ pitch: Float,  // 0.5..2.0
  _ volume01: Float,  // 0..1
  _ queue: Bool  // false ならキューをクリアしてから発話
) -> Int32 {
  guard let tptr = textUtf8 else { return -1 }
  let text = String(cString: tptr)

  // キュー動作
  if !queue && synth.isSpeaking {
    synth.stopSpeaking(at: .immediate)
  }

  // できるだけメインスレッドで
  DispatchQueue.main.async {
    let u = AVSpeechUtterance(string: text)

    // 声の決定: 明示の voiceOrNull > gVoiceId > 言語コード
    var voice: AVSpeechSynthesisVoice?
    if let vp = voiceOrNull {
      let v = String(cString: vp)
      voice = AVSpeechSynthesisVoice(identifier: v) ?? AVSpeechSynthesisVoice(language: v)
    } else if let id = gVoiceId {
      voice = AVSpeechSynthesisVoice(identifier: id) ?? AVSpeechSynthesisVoice(language: id)
    } else {
      voice = AVSpeechSynthesisVoice(language: gLang)
    }
    if let v = voice { u.voice = v }

    // パラメータ
    let r = max(0.2, min(1.8, rate01))
    u.rate = AVSpeechUtteranceDefaultSpeechRate * r
    u.pitchMultiplier = max(0.5, min(2.0, pitch))
    u.volume = max(0.0, min(1.0, volume01))

    // iOSのみオーディオセッションを軽く設定（macは不要）
    #if os(iOS)
      do {
        let session = AVAudioSession.sharedInstance()
        try session.setCategory(.playback, options: [.duckOthers])
        try session.setActive(true, options: [])
      } catch {
        gEventCb?(5)
      }
    #endif

    synth.speak(u)
  }
  return 0
}

// 便利: 利用可能な音声一覧を JSON で返す（identifier, language, name*）
// C# 側で受け取ったら Marshal.PtrToStringUTF8 → tts_free() で解放
@_cdecl("tts_list_voices_json")
public func tts_list_voices_json() -> UnsafeMutablePointer<CChar>? {
  var arr: [[String: String]] = []
  for v in AVSpeechSynthesisVoice.speechVoices() {
    var item: [String: String] = ["identifier": v.identifier, "language": v.language]
    // name はiOSでもmacOSでも存在します（将来の互換のために optional扱い）
    if let n = v.name as String? { item["name"] = n }
    arr.append(item)
  }
  if let data = try? JSONSerialization.data(withJSONObject: arr, options: []),
    let str = String(data: data, encoding: .utf8)
  {
    return strdup(str)
  }
  return nil
}

@_cdecl("tts_free")
public func tts_free(_ p: UnsafeMutableRawPointer?) {
  if let p = p { free(p) }
}

// ==== PCM をメモリで取得して返す ====
// 返り値: 0=OK / <0 エラー
// outSamples: float32(モノラル) の連続配列（malloc で確保）→ C# 側で tts_free() を呼んで解放
// outFrameCount: サンプル数（フレーム数）
// outSampleRate: サンプルレート（Hz）
@_cdecl("tts_synthesize_pcm")
public func tts_synthesize_pcm(
  _ textUtf8: UnsafePointer<CChar>?,
  _ voiceOrNull: UnsafePointer<CChar>?,  // nullなら gVoiceId/lang を使用
  _ rate01: Float,
  _ pitch: Float,
  _ volume01: Float,
  _ outSamples: UnsafeMutablePointer<UnsafeMutablePointer<Float>?>?,
  _ outFrameCount: UnsafeMutablePointer<Int32>?,
  _ outSampleRate: UnsafeMutablePointer<Int32>?
) -> Int32 {
  guard let tptr = textUtf8 else { return -1 }
  let text = String(cString: tptr)

  // Utterance 準備（Speak と同じロジック）
  let u = AVSpeechUtterance(string: text)
  var voice: AVSpeechSynthesisVoice?
  if let vp = voiceOrNull {
    let v = String(cString: vp)
    voice = AVSpeechSynthesisVoice(identifier: v) ?? AVSpeechSynthesisVoice(language: v)
  } else if let id = gVoiceId {
    voice = AVSpeechSynthesisVoice(identifier: id) ?? AVSpeechSynthesisVoice(language: id)
  } else {
    voice = AVSpeechSynthesisVoice(language: gLang)
  }
  if let v = voice { u.voice = v }
  let r = max(0.2, min(1.8, rate01))
  u.rate = AVSpeechUtteranceDefaultSpeechRate * r
  u.pitchMultiplier = max(0.5, min(2.0, pitch))
  u.volume = max(0.0, min(1.0, volume01))

  var floats = [Float]()
  var sr: Double = 0.0

  // write は基本「同期」的に全バッファをコールバックして戻ってきます
  synth.write(u) { buffer in
    guard let pcm = buffer as? AVAudioPCMBuffer else { return }
    sr = pcm.format.sampleRate

    let ch = Int(pcm.format.channelCount)
    let frames = Int(pcm.frameLength)
    if frames == 0 { return }  // 終端やメタ情報の可能性はスキップ

    switch pcm.format.commonFormat {
    case .pcmFormatFloat32:
      guard let base = pcm.floatChannelData else { return }
      if ch == 1 {
        let p = base[0]
        floats.append(contentsOf: UnsafeBufferPointer(start: p, count: frames))
      } else {
        // 多chの場合は簡易モノラル化（平均）
        var tmp = [Float](repeating: 0, count: frames)
        for c in 0..<ch {
          let p = base[c]
          for i in 0..<frames { tmp[i] += p[i] }
        }
        let inv = 1.0 / Float(ch)
        for i in 0..<frames { tmp[i] *= inv }
        floats.append(contentsOf: tmp)
      }

    case .pcmFormatInt16:
      guard let base = pcm.int16ChannelData else { return }
      let scale: Float = 1.0 / 32768.0
      if ch == 1 {
        let p = base[0]
        for i in 0..<frames { floats.append(Float(p[i]) * scale) }
      } else {
        for i in 0..<frames {
          var acc: Int = 0
          for c in 0..<ch { acc += Int(base[c][i]) }
          floats.append(Float(acc) * scale / Float(ch))
        }
      }
    default:
      // それ以外のフォーマットは今回は未対応
      break
    }
  }

  // 結果を C# 側へ返す（malloc → tts_free で解放可能に）
  let count = Int32(floats.count)
  let bytes = floats.count * MemoryLayout<Float>.size
  guard bytes > 0 else { return -2 }

  let raw = malloc(bytes)
  if raw == nil { return -3 }
  floats.withUnsafeBytes { src in
    memcpy(raw, src.baseAddress, bytes)
  }

  if let outSamples = outSamples { outSamples.pointee = raw?.assumingMemoryBound(to: Float.self) }
  if let outFrameCount = outFrameCount { outFrameCount.pointee = count }
  if let outSampleRate = outSampleRate { outSampleRate.pointee = Int32(sr.rounded()) }
  return 0
}

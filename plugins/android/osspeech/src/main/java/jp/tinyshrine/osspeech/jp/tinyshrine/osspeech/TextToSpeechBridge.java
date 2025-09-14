package jp.tinyshrine.osspeech;

import android.content.Context;
import android.os.Bundle;
import android.speech.tts.TextToSpeech;
import android.speech.tts.Voice;
import android.speech.tts.UtteranceProgressListener;

import java.io.File;
import java.io.IOException;
import java.util.Locale;
import java.util.Set;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CountDownLatch;

import org.json.JSONArray;
import org.json.JSONObject;

public class TextToSpeechBridge {

    public interface Callback {
        void onEvent(int ev); // 0=Start,1=Finish,2=Cancel,5=Error
    }

    private static TextToSpeech tts;
    private static Callback cb;
    private static volatile boolean ready = false;
    private static String currentVoiceId = null;
    private static Locale currentLocale = Locale.forLanguageTag("ja-JP");

    // synthesizeToFile の完了を同期で待つためのマップ
    private static final ConcurrentHashMap<String, CountDownLatch> waitMap = new ConcurrentHashMap<>();

    public static void init(Context ctx, Callback callback) {
        cb = callback;
        if (tts != null)
            return;

        tts = new TextToSpeech(ctx.getApplicationContext(), status -> {
            ready = (status == TextToSpeech.SUCCESS);
            if (ready) {
                // 既定を日本語に
                tts.setLanguage(currentLocale);
            }
        });

        tts.setOnUtteranceProgressListener(new UtteranceProgressListener() {
            @Override
            public void onStart(String utteranceId) {
                if (cb != null)
                    cb.onEvent(0);
            }

            @Override
            public void onDone(String utteranceId) {
                CountDownLatch l = waitMap.remove(utteranceId);
                if (l != null)
                    l.countDown();
                if (cb != null)
                    cb.onEvent(1);
            }

            @Override
            public void onError(String utteranceId) {
                CountDownLatch l = waitMap.remove(utteranceId);
                if (l != null)
                    l.countDown();
                if (cb != null)
                    cb.onEvent(5);
            }

            @Override
            public void onStop(String utteranceId, boolean interrupted) {
                if (cb != null)
                    cb.onEvent(2);
            }
        });
    }

    public static void setLanguage(String langTag) {
        currentLocale = (langTag == null || langTag.isEmpty()) ? Locale.forLanguageTag("ja-JP")
                : Locale.forLanguageTag(langTag);
        if (tts != null)
            tts.setLanguage(currentLocale);
    }

    public static void setVoiceId(String idOrNull) {
        currentVoiceId = idOrNull;
        if (tts == null)
            return;
        if (idOrNull == null || idOrNull.isEmpty())
            return;
        Set<Voice> voices = tts.getVoices();
        if (voices == null)
            return;
        for (Voice v : voices) {
            if (idOrNull.equals(v.getName())) { // Voice#getName() を ID とみなす
                tts.setVoice(v);
                return;
            }
        }
        // 見つからなければロケールで落とす
        tts.setLanguage(currentLocale);
    }

    public static boolean isSpeaking() {
        return tts != null && tts.isSpeaking();
    }

    public static void stop() {
        if (tts != null)
            tts.stop();
    }

    public static int speak(String text, String voiceOrLocale, float rate01, float pitch, float volume01,
            boolean queue) {
        if (tts == null || !ready || text == null)
            return -1;

        // 音量は Bundle の KEY_PARAM_VOLUME（0..1）
        Bundle params = new Bundle();
        params.putFloat(TextToSpeech.Engine.KEY_PARAM_VOLUME, clamp01(volume01));

        // 速度・ピッチ
        tts.setSpeechRate(clamp(rate01, 0.5f, 2.0f)); // Android の 1.0=等速
        tts.setPitch(clamp(pitch, 0.5f, 2.0f));

        // 声の選択（優先度: 指定 > 既定ID > 言語）
        if (voiceOrLocale != null && !voiceOrLocale.isEmpty()) {
            if (!applyVoiceByName(voiceOrLocale)) {
                tts.setLanguage(Locale.forLanguageTag(voiceOrLocale));
            }
        } else if (currentVoiceId != null) {
            applyVoiceByName(currentVoiceId);
        } else {
            tts.setLanguage(currentLocale);
        }

        String utterId = UUID.randomUUID().toString();
        int mode = queue ? TextToSpeech.QUEUE_ADD : TextToSpeech.QUEUE_FLUSH;
        int r = tts.speak(text, mode, params, utterId);
        return (r == TextToSpeech.SUCCESS) ? 0 : -2;
    }

    public static String listVoicesJson() {
        try {
            JSONArray arr = new JSONArray();
            if (tts != null) {
                Set<Voice> vs = tts.getVoices();
                if (vs != null) {
                    for (Voice v : vs) {
                        JSONObject o = new JSONObject();
                        o.put("identifier", v.getName()); // C# 側の SetVoiceId と一致させる
                        o.put("language", v.getLocale().toLanguageTag());
                        o.put("quality", v.getQuality());
                        o.put("latency", v.getLatency());
                        o.put("name", v.getName()); // 便宜上
                        arr.put(o);
                    }
                }
            }
            return arr.toString();
        } catch (Exception e) {
            return "[]";
        }
    }

    // PCMファイル（WAVを想定）を同期生成してパスを返す（null=失敗）
    public static String synthesizeToFile(String text, String voiceOrLocale, float rate01, float pitch, float volume01,
            Context ctx) {
        if (tts == null || !ready || text == null)
            return null;

        // 一時ファイル
        File out;
        try {
            out = File.createTempFile("tts_", ".wav", ctx.getCacheDir());
        } catch (IOException e) {
            return null;
        }

        // パラメータ
        Bundle params = new Bundle();
        params.putFloat(TextToSpeech.Engine.KEY_PARAM_VOLUME, clamp01(volume01));
        tts.setSpeechRate(clamp(rate01, 0.5f, 2.0f));
        tts.setPitch(clamp(pitch, 0.5f, 2.0f));

        if (voiceOrLocale != null && !voiceOrLocale.isEmpty()) {
            if (!applyVoiceByName(voiceOrLocale)) {
                tts.setLanguage(Locale.forLanguageTag(voiceOrLocale));
            }
        } else if (currentVoiceId != null) {
            applyVoiceByName(currentVoiceId);
        } else {
            tts.setLanguage(currentLocale);
        }

        String utterId = UUID.randomUUID().toString();
        CountDownLatch latch = new CountDownLatch(1);
        waitMap.put(utterId, latch);

        int r = tts.synthesizeToFile(text, params, out, utterId);
        if (r != TextToSpeech.SUCCESS) {
            waitMap.remove(utterId);
            return null;
        }

        try {
            // 完了待ち（最大 15 秒）
            latch.await();
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }
        return out.getAbsolutePath();
    }

    // ---------- helpers ----------
    private static boolean applyVoiceByName(String name) {
        if (tts == null)
            return false;
        Set<Voice> voices = tts.getVoices();
        if (voices == null)
            return false;
        for (Voice v : voices) {
            if (name.equals(v.getName())) {
                tts.setVoice(v);
                return true;
            }
        }
        return false;
    }

    private static float clamp(float v, float a, float b) {
        return Math.max(a, Math.min(b, v));
    }

    private static float clamp01(float v) {
        return clamp(v, 0f, 1f);
    }
}

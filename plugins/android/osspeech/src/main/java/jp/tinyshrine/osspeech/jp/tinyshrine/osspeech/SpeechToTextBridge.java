package jp.tinyshrine.osspeech;

import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.speech.RecognitionListener;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;

import java.util.ArrayList;
import java.util.Locale;

public class SpeechToTextBridge {

    // C# 側から AndroidJavaProxy で実装するIF
    public interface Callback {
        void onReady(); // マイク準備完了

        void onBegin(); // 音声入力開始

        void onPartial(String text); // 部分結果

        void onFinal(String text); // 最終結果

        void onError(int code, String message); // エラー

        void onEnd(); // セッション終了（結果/エラー後）
    }

    private static SpeechRecognizer recognizer;
    private static Intent intent;
    private static Callback cb;
    private static Context app;
    private static final Handler main = new Handler(Looper.getMainLooper());

    private static String languageTag = "ja-JP";
    private static boolean partial = true;
    private static boolean preferOffline = false;
    private static volatile boolean listening = false;

    // ---- public API ----

    public static void init(Context ctx, Callback callback) {
        app = ctx.getApplicationContext();
        cb = callback;
        main.post(() -> {
            destroyInternal();
            recognizer = SpeechRecognizer.createSpeechRecognizer(app);
            recognizer.setRecognitionListener(listener);
            buildIntent();
        });
    }

    public static boolean isRecognitionAvailable(Context ctx) {
        return SpeechRecognizer.isRecognitionAvailable(ctx);
    }

    public static void setLanguage(String langTag) {
        languageTag = (langTag == null || langTag.isEmpty()) ? "ja-JP" : langTag;
        main.post(SpeechToTextBridge::buildIntent);
    }

    public static void setPreferOffline(boolean v) {
        preferOffline = v;
        main.post(SpeechToTextBridge::buildIntent);
    }

    public static void setPartialResults(boolean v) {
        partial = v;
        main.post(SpeechToTextBridge::buildIntent);
    }

    public static boolean isListening() {
        return listening;
    }

    public static void start() {
        if (recognizer == null) {
            if (cb != null)
                cb.onError(-1, "Recognizer not initialized");
            return;
        }
        main.post(() -> {
            try {
                listening = true;
                recognizer.startListening(intent);
            } catch (Exception e) {
                listening = false;
                if (cb != null)
                    cb.onError(-2, e.getMessage());
                if (cb != null)
                    cb.onEnd();
            }
        });
    }

    public static void stop() {
        if (recognizer == null)
            return;
        main.post(() -> {
            try {
                recognizer.stopListening();
            } catch (Exception e) {
                if (cb != null)
                    cb.onError(-3, e.getMessage());
            }
        });
    }

    public static void cancel() {
        if (recognizer == null)
            return;
        main.post(() -> {
            try {
                recognizer.cancel();
            } catch (Exception e) {
                if (cb != null)
                    cb.onError(-4, e.getMessage());
            } finally {
                listening = false;
                if (cb != null)
                    cb.onEnd();
            }
        });
    }

    public static void destroy() {
        main.post(SpeechToTextBridge::destroyInternal);
    }

    // ---- internal ----

    private static void buildIntent() {
        intent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL,
                RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, languageTag);
        intent.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, partial);
        intent.putExtra(RecognizerIntent.EXTRA_PREFER_OFFLINE, preferOffline);
        intent.putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 3);
    }

    private static void destroyInternal() {
        if (recognizer != null) {
            try {
                recognizer.destroy();
            } catch (Exception ignored) {
            }
            recognizer = null;
        }
        listening = false;
    }

    private static final RecognitionListener listener = new RecognitionListener() {
        @Override
        public void onReadyForSpeech(Bundle params) {
            if (cb != null)
                cb.onReady();
        }

        @Override
        public void onBeginningOfSpeech() {
            if (cb != null)
                cb.onBegin();
        }

        @Override
        public void onRmsChanged(float rmsdB) {
        }

        @Override
        public void onBufferReceived(byte[] buffer) {
        }

        @Override
        public void onEndOfSpeech() {
            /* 結果 or エラーを待つ */ }

        @Override
        public void onError(int error) {
            listening = false;
            if (cb != null)
                cb.onError(error, mapError(error));
            if (cb != null)
                cb.onEnd();
        }

        @Override
        public void onResults(Bundle results) {
            listening = false;
            String text = first(results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION));
            if (cb != null)
                cb.onFinal(text == null ? "" : text);
            if (cb != null)
                cb.onEnd();
        }

        @Override
        public void onPartialResults(Bundle partialResults) {
            if (!partial || cb == null)
                return;
            String text = first(partialResults.getStringArrayList(
                    SpeechRecognizer.RESULTS_RECOGNITION));
            if (text != null)
                cb.onPartial(text);
        }

        @Override
        public void onEvent(int eventType, Bundle params) {
        }

        private String first(ArrayList<String> list) {
            return (list != null && !list.isEmpty()) ? list.get(0) : null;
        }

        private String mapError(int code) {
            switch (code) {
                case SpeechRecognizer.ERROR_NETWORK:
                    return "NETWORK";
                case SpeechRecognizer.ERROR_AUDIO:
                    return "AUDIO";
                case SpeechRecognizer.ERROR_SERVER:
                    return "SERVER";
                case SpeechRecognizer.ERROR_CLIENT:
                    return "CLIENT";
                case SpeechRecognizer.ERROR_SPEECH_TIMEOUT:
                    return "TIMEOUT";
                case SpeechRecognizer.ERROR_NO_MATCH:
                    return "NO_MATCH";
                case SpeechRecognizer.ERROR_RECOGNIZER_BUSY:
                    return "BUSY";
                case SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS:
                    return "PERMISSION";
                default:
                    return "ERROR_" + code;
            }
        }
    };
}

package jp.tinyshrine.osspeech;

import java.util.ArrayList;

import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.speech.RecognitionListener;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;
import android.util.Log;

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
    private static Callback callback;
    private static Context context;
    private static final Handler main = new Handler(Looper.getMainLooper());

    private static String languageTag = "ja-JP";
    private static boolean partial = true;
    private static boolean preferOffline = false;
    private static volatile boolean listening = false;
    private static volatile boolean keepAlive = false; // ★ stop まで連続入力するか

    // ---- public API ----

    public static void init(Context ctx, Callback cb) {
        context = ctx.getApplicationContext();
        SpeechToTextBridge.callback = cb;
        main.post(() -> {
            destroyInternal();
            recognizer = SpeechRecognizer.createSpeechRecognizer(context);
            recognizer.setRecognitionListener(listener);
            buildIntent();
        });
    }

    public static boolean isRecognitionAvailable(Context ctx) {
        return SpeechRecognizer.isRecognitionAvailable(ctx);
    }

    public static void setLanguage(String langTag) {
        if (langTag == null) {
            langTag = "ja-JP";
        }

        languageTag = (langTag.isEmpty()) ? "ja-JP" : langTag;
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
            if (callback != null) {
                callback.onError(-1, "Recognizer not initialized");
            }
            return;
        }
        main.post(() -> {
            keepAlive = true; // 連続ON
            startListeningSafely();
        });
    }

    public static void stop() {
        if (recognizer == null || !listening) {
            return;
        }
        main.post(() -> {
            keepAlive = false; // 連続OFF
            try {
                recognizer.stopListening();
            } catch (Exception e) {
                if (callback != null) {
                    callback.onError(-3, e.getMessage());
                }
            }
        });
    }

    public static void cancel() {
        if (recognizer == null) {
            return;
        }
        main.post(() -> {
            keepAlive = false; // 連続OFF
            try {
                recognizer.cancel();
            } catch (Exception e) {
                if (callback != null) {
                    callback.onError(-4, e.getMessage());
                }
            } finally {
                listening = false;
                if (callback != null) {
                    callback.onEnd();
                }
            }
        });
    }

    public static void destroy() {
        main.post(SpeechToTextBridge::destroyInternal);
    }

    // ---- internal ----

    private static void buildIntent() {
        intent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, languageTag);
        intent.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, partial);
        intent.putExtra(RecognizerIntent.EXTRA_PREFER_OFFLINE, preferOffline);
        intent.putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 3);

        // 任意: 区切りのしきい値（必要に応じて調整）
        intent.putExtra(RecognizerIntent.EXTRA_SPEECH_INPUT_COMPLETE_SILENCE_LENGTH_MILLIS, 800);
        intent.putExtra(RecognizerIntent.EXTRA_SPEECH_INPUT_POSSIBLY_COMPLETE_SILENCE_LENGTH_MILLIS, 800);
        intent.putExtra(RecognizerIntent.EXTRA_SPEECH_INPUT_MINIMUM_LENGTH_MILLIS, 2000);
    }

    private static void destroyInternal() {
        keepAlive = false;
        listening = false;
        if (recognizer != null) {
            try {
                recognizer.destroy();
            } catch (Exception ignored) {
            }
            recognizer = null;
        }
    }

    private static void startListeningSafely() {
        try {
            listening = true;
            recognizer.startListening(intent);
        } catch (Exception e) {
            listening = false;
            if (callback != null) {
                callback.onError(-2, e.getMessage());
                callback.onEnd();
            }
        }
    }

    private static void scheduleRestart(long delayMs) {
        if (!keepAlive || recognizer == null) {
            return;
        }
        main.postDelayed(() -> {
            if (keepAlive && recognizer != null) {
                startListeningSafely();
            }
        }, delayMs);
    }

    private static boolean shouldRestartOnError(int code) {
        // 再試行して良いエラー
        switch (code) {
            case SpeechRecognizer.ERROR_SPEECH_TIMEOUT:
            case SpeechRecognizer.ERROR_NO_MATCH:
            case SpeechRecognizer.ERROR_CLIENT:
            case SpeechRecognizer.ERROR_NETWORK:
            case SpeechRecognizer.ERROR_SERVER:
            case SpeechRecognizer.ERROR_RECOGNIZER_BUSY:
                return true;
            case SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS:
            case SpeechRecognizer.ERROR_AUDIO:
            default:
                return false;
        }
    }

    private static final RecognitionListener listener = new RecognitionListener() {
        @Override
        public void onReadyForSpeech(Bundle params) {
            if (callback != null) {
                callback.onReady();
            }
        }

        @Override
        public void onBeginningOfSpeech() {
            if (callback != null) {
                callback.onBegin();
            }
        }

        @Override
        public void onRmsChanged(float rmsdB) {
        }

        @Override
        public void onBufferReceived(byte[] buffer) {
        }

        @Override
        public void onEndOfSpeech() {
            // 結果 or エラーを待つ
        }

        @Override
        public void onError(int error) {
            listening = false;
            String errorName = mapError(error);

            if (keepAlive && shouldRestartOnError(error)) {
                // 自動復旧可能なエラー：内部処理のみ、Unity側には通知しない
                Log.d("SpeechToText", "Auto-recovering from error: " + errorName + " (" + error + ")");
                scheduleRestart(250);
            } else {
                // 重大エラー：Unity側に通知して完全停止
                Log.e("SpeechToText", "Critical error, stopping: " + errorName + " (" + error + ")");
                keepAlive = false;
                if (callback != null) {
                    callback.onError(error, errorName);
                    callback.onEnd();
                }
            }
        }

        @Override
        public void onResults(Bundle results) {
            listening = false;
            String text = first(results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION));
            if (callback != null) {
                callback.onFinal(text == null ? "" : text);
                callback.onEnd();
            }
            if (keepAlive) {
                scheduleRestart(150);
            }
        }

        @Override
        public void onPartialResults(Bundle partialResults) {
            if (!partial || callback == null) {
                return;
            }
            String text = first(partialResults.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION));
            if (text != null) {
                callback.onPartial(text);
            }
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

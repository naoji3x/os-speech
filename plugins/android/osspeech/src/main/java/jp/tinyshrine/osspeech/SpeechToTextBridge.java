package jp.tinyshrine.osspeech;

import java.util.ArrayList;

import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.speech.RecognitionListener;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;

public class SpeechToTextBridge {

    // RecognitionListener とほぼ同じインターフェース
    public interface Callback {
        void onReadyForSpeech(Bundle params);

        void onBeginningOfSpeech();

        void onRmsChanged(float rmsdB);

        void onBufferReceived(byte[] buffer);

        void onEndOfSpeech();

        void onError(int error);

        void onResults(Bundle results);

        void onPartialResults(Bundle partialResults);

        void onEvent(int eventType, Bundle params);
    }

    private static SpeechRecognizer recognizer;
    private static Intent intent;
    private static Callback callback;
    private static Context context;

    private static String languageTag = "ja-JP";
    private static boolean partial = true;
    private static boolean preferOffline = false;

    // ---- public API ----

    public static void init(Context ctx, Callback cb) {
        context = ctx.getApplicationContext();
        callback = cb;

        if (recognizer != null) {
            recognizer.destroy();
        }

        recognizer = SpeechRecognizer.createSpeechRecognizer(context);
        recognizer.setRecognitionListener(listener);
        buildIntent();
    }

    public static boolean isRecognitionAvailable(Context ctx) {
        return SpeechRecognizer.isRecognitionAvailable(ctx);
    }

    public static void setLanguage(String langTag) {
        if (langTag == null || langTag.isEmpty()) {
            languageTag = "ja-JP";
        } else {
            languageTag = langTag;
        }
        buildIntent();
    }

    public static void setPreferOffline(boolean offline) {
        preferOffline = offline;
        buildIntent();
    }

    public static void setPartialResults(boolean enable) {
        partial = enable;
        buildIntent();
    }

    public static void startListening() {
        if (recognizer != null && intent != null) {
            recognizer.startListening(intent);
        }
    }

    public static void stopListening() {
        if (recognizer != null) {
            recognizer.stopListening();
        }
    }

    public static void cancel() {
        if (recognizer != null) {
            recognizer.cancel();
        }
    }

    public static void destroy() {
        if (recognizer != null) {
            recognizer.destroy();
            recognizer = null;
        }
        callback = null;
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

    private static final RecognitionListener listener = new RecognitionListener() {
        @Override
        public void onReadyForSpeech(Bundle params) {
            if (callback != null) {
                callback.onReadyForSpeech(params);
            }
        }

        @Override
        public void onBeginningOfSpeech() {
            if (callback != null) {
                callback.onBeginningOfSpeech();
            }
        }

        @Override
        public void onRmsChanged(float rmsdB) {
            if (callback != null) {
                callback.onRmsChanged(rmsdB);
            }
        }

        @Override
        public void onBufferReceived(byte[] buffer) {
            if (callback != null) {
                callback.onBufferReceived(buffer);
            }
        }

        @Override
        public void onEndOfSpeech() {
            if (callback != null) {
                callback.onEndOfSpeech();
            }
        }

        @Override
        public void onError(int error) {
            if (callback != null) {
                callback.onError(error);
            }
        }

        @Override
        public void onResults(Bundle results) {
            if (callback != null) {
                callback.onResults(results);
            }
        }

        @Override
        public void onPartialResults(Bundle partialResults) {
            if (callback != null) {
                callback.onPartialResults(partialResults);
            }
        }

        @Override
        public void onEvent(int eventType, Bundle params) {
            if (callback != null) {
                callback.onEvent(eventType, params);
            }
        }
    };

    // ---- Utility methods for Unity C# ----

    public static String getFirstResult(Bundle results) {
        ArrayList<String> matches = results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
        return (matches != null && !matches.isEmpty()) ? matches.get(0) : "";
    }

    public static ArrayList<String> getAllResults(Bundle results) {
        return results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
    }

    public static float[] getConfidenceScores(Bundle results) {
        return results.getFloatArray(SpeechRecognizer.CONFIDENCE_SCORES);
    }
}

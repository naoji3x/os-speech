# scripts/new-android-lib.sh
set -euo pipefail
ROOT="$(pwd)"
DIR="$ROOT/plugins/android/osspeech"
mkdir -p "$DIR/src/main/java/jp/tinyshrine/osspeech" "$DIR/src/main/res/values"

# settings.gradle
cat > "$DIR/settings.gradle" <<'EOF'
pluginManagement {
  repositories { gradlePluginPortal(); google(); mavenCentral() }
}
dependencyResolutionManagement {
  repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
  repositories { google(); mavenCentral() }
}
rootProject.name = "osspeech"
EOF

# build.gradle（AGPとJava 17）
cat > "$DIR/build.gradle" <<'EOF'
plugins { id 'com.android.library' version '8.5.2' apply true }
android {
  namespace 'jp.tinyshrine.osspeech'
  compileSdk 34
  defaultConfig { minSdk 21 consumerProguardFiles 'consumer-rules.pro' }
  compileOptions { sourceCompatibility JavaVersion.VERSION_17; targetCompatibility JavaVersion.VERSION_17 }
  buildTypes { release { minifyEnabled false } }
}
dependencies { }
EOF

# gradle.properties
cat > "$DIR/gradle.properties" <<'EOF'
org.gradle.jvmargs=-Xmx2g -Dfile.encoding=UTF-8
android.nonTransitiveRClass=true
android.useAndroidX=true
EOF

# consumer-rules.pro
cat > "$DIR/consumer-rules.pro" <<'EOF'
-keep class jp.tinyshrine.osspeech.** { *; }
EOF

# AndroidManifest.xml
cat > "$DIR/src/main/AndroidManifest.xml" <<'EOF'
<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="jp.tinyshrine.osspeech"/>
EOF

# サンプル Java（後で自分のクラスに差し替えOK）
cat > "$DIR/src/main/java/jp/tinyshrine/osspeech/Hello.java" <<'EOF'
package jp.tinyshrine.osspeech;
public class Hello {
  public static String ping() { return "osspeech ok"; }
}
EOF

# gradle wrapper（Gradleを持っていれば）
if command -v gradle >/dev/null 2>&1; then
  (cd "$DIR" && gradle wrapper --gradle-version 8.7)
fi

echo "Scaffolded at $DIR"
echo "Build:  cd $DIR && ./gradlew assembleRelease   (Windows: gradlew.bat)"

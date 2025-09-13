# Unity Project Template利用ガイド

## 📋 目次

1. [📖 このプロジェクトについて](#-このプロジェクトについて)
2. [📝 コードの書き方](#-コードの書き方)

---

## 📖 このプロジェクトについて

### 開発環境

| ソフトウェア | バージョン | 用途                 |
| ------------ | ---------- | -------------------- |
| Unity        | 6000.0 LTS | ゲーム開発エンジン   |
| Node.js      | 20.x以上   | 開発ツール・自動化   |
| .NET SDK     | 8.x以上    | C#コンパイル・テスト |
| Git          | 最新版     | バージョン管理       |

### フォルダ構成

```text
Assets/
├── TinyShrine/
│   ├── Core/
│   │   ├── Editor/          # エディタ拡張
│   │   └── Runtime/         # ランタイムコード
└── Scenes/                  # シーンファイル
```

---

## 📝 コードの書き方

### 名前の付け方

#### C# コードの命名ルール

このプロジェクトでは統一された命名ルールを使っています：

- **クラス名**: 最初が大文字 - `AnimationEventReceiver`, `NavMeshAgentController`
- **メソッド名**: 最初が大文字 - `MoveTo()`, `OnFootstep()`
- **プロパティ名**: 最初が大文字 - `OnFootstepEvent`, `TargetAnimator`
- **フィールド名**: 最初が小文字 - `targetAnimator`, `speedHash`
- **パラメータ名**: 最初が小文字 - `worldPos`, `evt`
- **定数**: 最初が大文字 - `DefaultSpeed`, `MaxDistance`

#### ファイル名の付け方

- **すべて最初が大文字**（スペース・アンダースコア・ハイフンは使わない）
- **コード（クラス名等）と同じ名前にする**
- **種類（Prefab / Scene / Material など）は名前に含めない**（Unityが自動で区別するため）

良い例：

- `EnemyGoblin.prefab`（❌ `EnemyGoblinPrefab.prefab`）
- `MainMenu.unity`（❌ `MainMenuScene.unity`）
- `WaterSurface.mat`（❌ `WaterSurfaceMaterial.mat`）
- `InventoryConfig.asset`（✅ 設定の説明は含めてもOK）

### コメントスタイル

#### XML ドキュメントコメント

すべてのpublicメンバーには XML ドキュメントコメントを必須とします：

```csharp
/// <summary>
/// アニメーションイベントを受信してUnityEventで配信するコンポーネント。
/// Animatorコンポーネントと同じGameObjectに配置され、アニメーションから送信される
/// AnimationEventをUnityEventとして外部に通知します。
/// </summary>
public class AnimationEventReceiver : MonoBehaviour
{
    /// <summary>
    /// Gets 足音アニメーションイベント発生時に呼び出されるUnityEvent。
    /// 外部コンポーネントがこのイベントを購読して音声再生等の処理を実行できます。
    /// </summary>
    public UnityEvent<AnimationEvent> OnFootstepEvent { get; } = new();

    /// <summary>
    /// 足音アニメーションイベントの受信メソッド。
    /// アニメーション内のAnimationEventから呼び出され、対応するUnityEventを発火します。
    /// </summary>
    /// <param name="evt">アニメーションイベントの詳細情報</param>
    public void OnFootstep(AnimationEvent evt) => OnFootstepEvent.Invoke(evt);
}
```

#### インラインコメント

複雑なロジックには説明的なコメントを追加：

```csharp
// sqrMagnitudeを使用してsqrt計算を回避し、0との比較で最適化
var speed = agent.velocity.sqrMagnitude > 0 ? agent.velocity.magnitude : 0f;

// pathPendingが false = 経路計算完了
// remainingDistanceは経路が無い時に Mathf.Infinity になるため hasPath もチェック
if (agent.hasPath && agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, arriveThreshold))
{
    arrived = true;
    agent.isStopped = true;
}
```

### Lint/Formatter

#### EditorConfig 設定

プロジェクトでは統一されたコードスタイルのため `.editorconfig` を使用：

```editorconfig
# C# files
[*.cs]
indent_size = 4
charset = utf-8
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true
max_line_length = 120
```

#### StyleCop / Roslyn Analyzers

コード品質維持のため以下のルールを適用：

- **Performance**: エラーレベル（最優先）
- **Security**: エラーレベル
- **Usage**: エラーレベル
- **Design**: 警告レベル
- **Style**: CSharpier に委譲（基本オフ）

```editorconfig
# 性能系を最優先で厳格（Error）
dotnet_analyzer_diagnostic.category-Performance.severity = error

# セキュリティ/使用法も厳しめ
dotnet_analyzer_diagnostic.category-Security.severity = error
dotnet_analyzer_diagnostic.category-Usage.severity = error

# C# 9.0+ target-typed new expressions をサポート
dotnet_diagnostic.SA1000.severity = none
```

#### CSharpier

自動コードフォーマッターとして CSharpier を使用：

```bash
# フォーマット実行
npm run format:cs

# フォーマットチェック
npm run format:check
```

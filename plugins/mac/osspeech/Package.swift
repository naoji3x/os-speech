// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "OSSpeechMacOnly",
    platforms: [
        .macOS(.v11)
    ],
    products: [
        // 出力ファイル名: libOSSpeech.dylib
        .library(name: "OSSpeech", type: .dynamic, targets: ["OSSpeech"])
    ],
    targets: [
        .target(
            name: "OSSpeech",
            // Sources/OSSpeech は iOS 側フォルダへの symlink にします
            path: "Sources/OSSpeech",
            // mac で必要なフレームワークをリンク
            linkerSettings: [
                .linkedFramework("Speech"),
                .linkedFramework("AVFoundation")
            ],
        )
    ]
)

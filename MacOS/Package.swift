// swift-tools-version: 5.7
import PackageDescription

let package = Package(
    name: "HalogenMacOS",
    platforms: [.macOS(.v12)],
    products: [.executable(name: "HalogenMacOS", targets: ["HalogenMacOS"])],
    targets: [.executableTarget(name: "HalogenMacOS", path: "Sources")]
)

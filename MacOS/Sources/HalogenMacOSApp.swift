import AppKit
import SwiftUI

@main
struct HalogenMacOSApp: App {
    var body: some Scene {
        WindowGroup {
            HalogenUIWindowView()
        }
    }
}

struct HalogenUIWindowView: View {
    @StateObject private var model = HalogenUIModel()

    var body: some View {
        VStack(spacing: 0) {
            if !model.isPackagedUI {
                HStack(spacing: 10) {
                    Button("Open .hg…") { model.openFile() }
                    Button("Run") { model.runSource() }
                    Text(model.fileName)
                        .font(.system(size: 12, design: .monospaced))
                        .foregroundColor(.secondary)
                    Spacer()
                    Text("HalogenUI · macOS")
                        .font(.caption)
                        .foregroundColor(.secondary)
                }
                .padding(.horizontal, 12)
                .padding(.vertical, 8)

                Divider()
            }

            if model.program.showsUI {
                TimelineView(.animation(minimumInterval: 1.0 / 60.0)) { context in
                    let elapsed = max(0, context.date.timeIntervalSince(model.startDate))
                    let frameNumber = max(0, Int(elapsed * 60.0))
                    let frame = model.program.render(elapsed: elapsed, frameNumber: frameNumber)
                    Canvas { graphics, _ in
                        for command in frame.commands {
                            switch command {
                            case .rect(let x, let y, let width, let height, let color):
                                let rect = CGRect(x: x, y: y, width: max(0, width), height: max(0, height))
                                graphics.fill(Path(rect), with: .color(halogenColor(color)))
                            case .circle(let x, let y, let diameter, let color):
                                let circle = CGRect(x: x, y: y, width: max(0, diameter), height: max(0, diameter))
                                graphics.fill(Path(ellipseIn: circle), with: .color(halogenColor(color)))
                            case .text(let value, let x, let y, let color):
                                graphics.draw(
                                    Text(value).font(.system(size: 24, weight: .regular)).foregroundColor(halogenColor(color)),
                                    at: CGPoint(x: x, y: y),
                                    anchor: .topLeading
                                )
                            }
                        }
                    }
                    .background(halogenColor(frame.background))
                    .frame(width: model.program.options.width, height: model.program.options.height)
                }
            } else {
                VStack(spacing: 12) {
                    Image(systemName: "scribble.variable").font(.system(size: 36))
                    Text("No UI to show yet").font(.title2)
                    Text("Open a .hg file containing ui.show() to preview it.")
                        .foregroundColor(.secondary)
                }
                .frame(width: model.program.options.width, height: model.program.options.height)
            }
        }
        .fixedSize()
        .onAppear { model.applyWindowOptions() }
        .onChange(of: model.program.options.title) { _ in model.applyWindowOptions() }
        .alert("HalogenUI error", isPresented: Binding(
            get: { model.errorMessage != nil },
            set: { if !$0 { model.acknowledgeError() } }
        )) {
            Button("OK") { model.acknowledgeError() }
        } message: {
            Text(model.errorMessage ?? "Unknown error")
        }
    }
}

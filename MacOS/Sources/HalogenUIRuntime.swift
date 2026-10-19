import AppKit
import Foundation
import SwiftUI

struct HalogenUIWindowOptions {
    var title = "HalogenUI"
    var width = 900.0
    var height = 620.0
    var iconPath: String?
}

struct HalogenUIFrame {
    var background = "#101820"
    var commands: [HalogenUIDrawCommand] = []
}

enum HalogenUIDrawCommand {
    case rect(x: Double, y: Double, width: Double, height: Double, color: String)
    case circle(x: Double, y: Double, diameter: Double, color: String)
    case text(value: String, x: Double, y: Double, color: String)
}

enum HalogenUIRepeatUnit {
    case frames, seconds, minutes, hours
}

indirect enum HalogenUIStatement {
    case background(String)
    case rect(Double, Double, Double, Double, String)
    case circle(Double, Double, Double, String)
    case text(String, Double, Double, String)
    case show
    case repeatBlock(HalogenUIRepeatUnit, Double, [HalogenUIStatement])
}

struct ParsedHalogenUIProgram {
    let options: HalogenUIWindowOptions
    let statements: [HalogenUIStatement]
    let showsUI: Bool

    func render(elapsed: TimeInterval, frameNumber: Int) -> HalogenUIFrame {
        var frame = HalogenUIFrame()
        execute(statements, elapsed: elapsed, frameNumber: frameNumber, frame: &frame)
        return frame
    }

    private func execute(
        _ statements: [HalogenUIStatement],
        elapsed: TimeInterval,
        frameNumber: Int,
        frame: inout HalogenUIFrame
    ) {
        for statement in statements {
            switch statement {
            case .background(let color):
                frame.background = color
            case .rect(let x, let y, let width, let height, let color):
                frame.commands.append(.rect(x: x, y: y, width: width, height: height, color: color))
            case .circle(let x, let y, let diameter, let color):
                frame.commands.append(.circle(x: x, y: y, diameter: diameter, color: color))
            case .text(let value, let x, let y, let color):
                frame.commands.append(.text(value: value, x: x, y: y, color: color))
            case .show:
                break
            case .repeatBlock(let unit, let amount, let body):
                let active: Bool
                switch unit {
                case .frames: active = frameNumber < Int(amount)
                case .seconds: active = elapsed < amount
                case .minutes: active = elapsed < amount * 60
                case .hours: active = elapsed < amount * 3600
                }
                if active {
                    execute(body, elapsed: elapsed, frameNumber: frameNumber, frame: &frame)
                }
            }
        }
    }
}

enum HalogenUIParserError: LocalizedError {
    case malformedCommand(line: Int, command: String)
    case invalidNumber(line: Int, value: String)
    case missingEnd(line: Int)
    case invalidRepeat(line: Int, command: String)

    var errorDescription: String? {
        switch self {
        case .malformedCommand(let line, let command):
            return "Line \(line): invalid HalogenUI command: \(command)"
        case .invalidNumber(let line, let value):
            return "Line \(line): expected a number, got '\(value)'"
        case .missingEnd(let line):
            return "Line \(line): repetition block is missing 'end'."
        case .invalidRepeat(let line, let command):
            return "Line \(line): \(command) needs a non-negative duration; framerpt needs a whole frame count."
        }
    }
}

enum HalogenUIParser {
    static func parse(_ source: String) throws -> ParsedHalogenUIProgram {
        let uncommented = source.replacingOccurrences(
            of: "(?s)#/.*?(?:\\\\#|$)",
            with: "",
            options: .regularExpression
        )
        let sourceLines = uncommented.components(separatedBy: .newlines)
        var options = HalogenUIWindowOptions()
        var executableLines: [(number: Int, text: String)] = []
        var inDeclaration = false
        var declarationJustClosed = false

        for (index, rawLine) in sourceLines.enumerated() {
            let lineNumber = index + 1
            let line = rawLine.trimmingCharacters(in: .whitespacesAndNewlines)
            if line.isEmpty { continue }

            if line.hasPrefix("ui.declare") && line.contains("[") {
                inDeclaration = true
                declarationJustClosed = false
                parseOptions(from: line, into: &options)
                if line.contains("]") {
                    inDeclaration = false
                    declarationJustClosed = true
                }
                continue
            }
            if inDeclaration {
                parseOptions(from: line, into: &options)
                if line.contains("]") {
                    inDeclaration = false
                    declarationJustClosed = true
                }
                continue
            }
            if declarationJustClosed && line == "end" {
                declarationJustClosed = false
                continue
            }
            declarationJustClosed = false
            executableLines.append((lineNumber, line))
        }

        var cursor = 0
        let statements = try parseBlock(executableLines, cursor: &cursor, insideBlock: false)
        return ParsedHalogenUIProgram(options: options, statements: statements, showsUI: containsShow(statements))
    }

    private static func parseOptions(from line: String, into options: inout HalogenUIWindowOptions) {
        for group in captures(in: line, pattern: #"\(([^()]*)\)"#) {
            let item = group.trimmingCharacters(in: .whitespacesAndNewlines)
            let lower = item.lowercased()
            if lower.hasPrefix("title:"), let colon = item.firstIndex(of: ":") {
                options.title = unquote(String(item[item.index(after: colon)...]).trimmingCharacters(in: .whitespacesAndNewlines))
            } else if lower.hasPrefix("size:"), let colon = item.firstIndex(of: ":") {
                let size = String(item[item.index(after: colon)...]).trimmingCharacters(in: .whitespacesAndNewlines)
                let parts = size.lowercased().split(separator: "x", omittingEmptySubsequences: false)
                if parts.count == 2, let width = Double(parts[0]), let height = Double(parts[1]), width > 0, height > 0 {
                    options.width = min(width, 4096)
                    options.height = min(height, 4096)
                }
            } else if lower.hasPrefix("icon?") {
                let pieces = item.split(separator: "=", maxSplits: 1, omittingEmptySubsequences: false)
                if pieces.count == 2 {
                    let value = pieces[1].trimmingCharacters(in: .whitespacesAndNewlines)
                    if value.uppercased().hasPrefix("T ") {
                        options.iconPath = unquote(String(value.dropFirst(2)).trimmingCharacters(in: .whitespacesAndNewlines))
                    }
                }
            }
        }
    }

    private static func parseBlock(
        _ lines: [(number: Int, text: String)],
        cursor: inout Int,
        insideBlock: Bool
    ) throws -> [HalogenUIStatement] {
        var statements: [HalogenUIStatement] = []
        while cursor < lines.count {
            let (lineNumber, line) = lines[cursor]
            if line == "end" {
                cursor += 1
                if insideBlock { return statements }
                continue
            }

            if let parts = firstCapture(in: line, pattern: #"^ui\.(framerpt|secrpt|minrpt|hrsrpt)\s+([^:]+?)\s*:?$"#) {
                let command = "ui." + parts[0].lowercased()
                let amountText = parts[1].trimmingCharacters(in: .whitespacesAndNewlines)
                guard let amount = Double(amountText), amount >= 0, amount.isFinite,
                      amount <= 31_536_000,
                      command != "ui.framerpt" || amount.rounded(.towardZero) == amount else {
                    throw HalogenUIParserError.invalidRepeat(line: lineNumber, command: command)
                }
                let unit: HalogenUIRepeatUnit
                switch command {
                case "ui.framerpt": unit = .frames
                case "ui.secrpt": unit = .seconds
                case "ui.minrpt": unit = .minutes
                default: unit = .hours
                }
                cursor += 1
                let body = try parseBlock(lines, cursor: &cursor, insideBlock: true)
                statements.append(.repeatBlock(unit, amount, body))
                continue
            }

            if line == "ui.show()" {
                statements.append(.show)
            } else if let args = commandArguments(line, command: "ui.background") {
                guard args.count == 1 else { throw HalogenUIParserError.malformedCommand(line: lineNumber, command: line) }
                statements.append(.background(unquote(args[0])))
            } else if let args = commandArguments(line, command: "ui.rect") {
                guard args.count == 5 else { throw HalogenUIParserError.malformedCommand(line: lineNumber, command: line) }
                statements.append(.rect(
                    try number(args[0], line: lineNumber), try number(args[1], line: lineNumber),
                    try number(args[2], line: lineNumber), try number(args[3], line: lineNumber), unquote(args[4])
                ))
            } else if let args = commandArguments(line, command: "ui.circle") {
                guard args.count == 4 else { throw HalogenUIParserError.malformedCommand(line: lineNumber, command: line) }
                statements.append(.circle(
                    try number(args[0], line: lineNumber), try number(args[1], line: lineNumber),
                    try number(args[2], line: lineNumber), unquote(args[3])
                ))
            } else if let args = commandArguments(line, command: "ui.text") {
                guard args.count == 4 else { throw HalogenUIParserError.malformedCommand(line: lineNumber, command: line) }
                statements.append(.text(
                    unquote(args[0]), try number(args[1], line: lineNumber),
                    try number(args[2], line: lineNumber), unquote(args[3])
                ))
            }
            // Non-UI Halogen statements are ignored by this renderer-only starter.
            cursor += 1
        }

        if insideBlock {
            throw HalogenUIParserError.missingEnd(line: lines.last?.number ?? 1)
        }
        return statements
    }

    private static func containsShow(_ statements: [HalogenUIStatement]) -> Bool {
        for statement in statements {
            switch statement {
            case .show: return true
            case .repeatBlock(_, _, let body):
                if containsShow(body) { return true }
            default: break
            }
        }
        return false
    }

    private static func commandArguments(_ line: String, command: String) -> [String]? {
        guard line.hasPrefix(command + "("), line.hasSuffix(")") else { return nil }
        let start = line.index(line.startIndex, offsetBy: command.count + 1)
        let end = line.index(before: line.endIndex)
        return splitArguments(String(line[start..<end]))
    }

    private static func splitArguments(_ text: String) -> [String] {
        var result: [String] = []
        var current = ""
        var quote: Character?
        var depth = 0
        for character in text {
            if let activeQuote = quote {
                current.append(character)
                if character == activeQuote { quote = nil }
            } else if character == "\"" || character == "'" {
                quote = character
                current.append(character)
            } else if character == "(" {
                depth += 1
                current.append(character)
            } else if character == ")" {
                depth -= 1
                current.append(character)
            } else if character == "," && depth == 0 {
                result.append(current.trimmingCharacters(in: .whitespacesAndNewlines))
                current = ""
            } else {
                current.append(character)
            }
        }
        if !current.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            result.append(current.trimmingCharacters(in: .whitespacesAndNewlines))
        }
        return result
    }

    private static func number(_ text: String, line: Int) throws -> Double {
        guard let value = Double(text.trimmingCharacters(in: .whitespacesAndNewlines)), value.isFinite else {
            throw HalogenUIParserError.invalidNumber(line: line, value: text)
        }
        return value
    }

    private static func unquote(_ text: String) -> String {
        let value = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard value.count >= 2 else { return value }
        let first = value.first!
        if (first == "\"" || first == "'") && value.last == first {
            return String(value.dropFirst().dropLast())
        }
        return value
    }

    private static func firstCapture(in text: String, pattern: String) -> [String]? {
        guard let expression = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]) else { return nil }
        let range = NSRange(text.startIndex..., in: text)
        guard let match = expression.firstMatch(in: text, options: [], range: range), match.numberOfRanges > 2 else { return nil }
        var values: [String] = []
        for index in 1..<match.numberOfRanges {
            guard let range = Range(match.range(at: index), in: text) else { return nil }
            values.append(String(text[range]))
        }
        return values
    }

    private static func captures(in text: String, pattern: String) -> [String] {
        guard let expression = try? NSRegularExpression(pattern: pattern, options: []) else { return [] }
        let range = NSRange(text.startIndex..., in: text)
        return expression.matches(in: text, options: [], range: range).compactMap { match in
            guard let range = Range(match.range(at: 1), in: text) else { return nil }
            return String(text[range])
        }
    }
}

@MainActor
final class HalogenUIModel: ObservableObject {
    @Published private(set) var program: ParsedHalogenUIProgram
    @Published private(set) var errorMessage: String?
    @Published private(set) var fileName = "Built-in demo"
    private var source: String
    private(set) var startDate = Date()

    init() {
        let demo = """
        ui.declare [
            command.parameter:
                (title: "HalogenUI Demo")
                (size: 900x620)
        ]
        end

        ui.background("#101820")
        ui.rect(64, 70, 250, 140, "#00D9FF")
        ui.circle(420, 140, 96, "#FFB000")
        ui.text("HalogenUI is alive!", 64, 260, "#FFFFFF")
        ui.secrpt 5:
            ui.text("This text repeats for 5 seconds", 64, 320, "#7CFFB2")
        end
        ui.show()
        """
        var initialSource = demo
        var initialName = "Built-in demo"
        if CommandLine.arguments.count > 1 {
            let path = CommandLine.arguments[1]
            if FileManager.default.fileExists(atPath: path),
               let loaded = try? String(contentsOfFile: path, encoding: .utf8) {
                initialSource = loaded
                initialName = URL(fileURLWithPath: path).lastPathComponent
            }
        }
        source = initialSource
        fileName = initialName
        program = (try? HalogenUIParser.parse(initialSource)) ?? (try! HalogenUIParser.parse("ui.show()"))
    }

    func openFile() {
        let panel = NSOpenPanel()
        panel.allowedFileTypes = ["hg"]
        panel.canChooseDirectories = false
        panel.canChooseFiles = true
        panel.allowsMultipleSelection = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            source = try String(contentsOf: url, encoding: .utf8)
            fileName = url.lastPathComponent
            runSource()
        } catch {
            errorMessage = "Could not open \(url.lastPathComponent): \(error.localizedDescription)"
        }
    }

    func runSource() {
        do {
            program = try HalogenUIParser.parse(source)
            startDate = Date()
            errorMessage = nil
            applyWindowOptions()
        } catch {
            errorMessage = error.localizedDescription
        }
    }

    func acknowledgeError() {
        errorMessage = nil
    }

    func applyWindowOptions() {
        let options = program.options
        DispatchQueue.main.async {
            if let window = NSApplication.shared.windows.first {
                window.title = options.title
                window.setContentSize(NSSize(width: options.width, height: options.height + 42))
            }
            if let iconPath = options.iconPath {
                let expanded = NSString(string: iconPath).expandingTildeInPath
                if let image = NSImage(contentsOfFile: expanded) {
                    NSApplication.shared.applicationIconImage = image
                }
            }
        }
    }
}

func halogenColor(_ value: String) -> Color {
    let cleaned = value.trimmingCharacters(in: .whitespacesAndNewlines).replacingOccurrences(of: "#", with: "")
    guard cleaned.count == 6, let rgb = UInt32(cleaned, radix: 16) else { return .white }
    return Color(.sRGB,
                 red: Double((rgb >> 16) & 0xFF) / 255,
                 green: Double((rgb >> 8) & 0xFF) / 255,
                 blue: Double(rgb & 0xFF) / 255,
                 opacity: 1)
}

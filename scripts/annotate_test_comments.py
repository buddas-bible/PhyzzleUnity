#!/usr/bin/env python3
from __future__ import annotations

import argparse
import re
from pathlib import Path

TEST_ROOT = Path("Assets/Phyzzle/Tests")
TYPE_RE = re.compile(
    r"^(?P<indent>\s*)(?:(?:public|private|protected|internal)\s+)?"
    r"(?:(?:sealed|static|abstract|readonly|partial|ref)\s+)*"
    r"(?P<kind>class|struct|interface)\s+(?P<name>[A-Za-z_]\w*)\b"
)
METHOD_RE = re.compile(
    r"^(?P<indent>\s*)(?:public|private|protected|internal)\s+"
    r"(?:(?:static|virtual|override|sealed|async|new|extern|unsafe|readonly|partial)\s+)*"
    r"[A-Za-z_][\w.<>,?\[\]]*\s+(?P<name>[A-Za-z_]\w*)\s*\("
)
TEST_ATTRIBUTE_MARKERS = ("[Test", "[UnityTest", "[TestCase")


def comment_block(indent: str, text: str) -> list[str]:
    return [
        f"{indent}/// <summary>\n",
        f"{indent}/// {text}\n",
        f"{indent}/// </summary>\n",
    ]


def attribute_start(output: list[str]) -> int:
    """Return the insertion point before a contiguous attribute block."""
    index = len(output) - 1
    while index >= 0 and output[index].strip() == "":
        index -= 1
    insert_at = len(output)
    while index >= 0 and output[index].lstrip().startswith("["):
        insert_at = index
        index -= 1
    return insert_at


def already_documented(output: list[str], insert_at: int) -> bool:
    index = insert_at - 1
    while index >= 0 and output[index].strip() == "":
        index -= 1
    return index >= 0 and output[index].lstrip().startswith("///")


def has_test_attribute(output: list[str], insert_at: int) -> bool:
    for line in output[insert_at:]:
        stripped = line.lstrip()
        if stripped.startswith("["):
            if any(marker in stripped for marker in TEST_ATTRIBUTE_MARKERS):
                return True
            continue
        if stripped.strip() == "":
            continue
        break
    return False


def annotate_text(text: str) -> tuple[str, int]:
    lines = text.splitlines(keepends=True)
    output: list[str] = []
    inserted = 0

    for line in lines:
        type_match = TYPE_RE.match(line)
        method_match = METHOD_RE.match(line)
        if type_match or method_match:
            insert_at = attribute_start(output)
            if not already_documented(output, insert_at):
                if type_match:
                    indent = type_match.group("indent")
                    name = type_match.group("name")
                    if name.endswith("Tests"):
                        text_line = f"<c>{name}</c> 대상 동작을 검증하는 테스트 모음이다."
                    else:
                        text_line = f"테스트에서 사용하는 <c>{name}</c> 보조 타입이다."
                else:
                    indent = method_match.group("indent")
                    name = method_match.group("name")
                    is_test = has_test_attribute(output, insert_at)
                    text_line = (
                        f"<c>{name}</c> 테스트 시나리오를 검증한다."
                        if is_test
                        else f"<c>{name}</c> 테스트 지원 동작을 수행한다."
                    )
                block = comment_block(indent, text_line)
                output[insert_at:insert_at] = block
                inserted += len(block)

        output.append(line)

    return "".join(output), inserted


def self_test() -> None:
    sample = """using NUnit.Framework;\n\nnamespace Sample\n{\n    public sealed class DemoTests\n    {\n        [Test]\n        public void Works()\n        {\n        }\n\n        private static int Helper(int value) => value;\n    }\n}\n"""
    annotated, inserted = annotate_text(sample)
    assert inserted == 9, inserted
    assert "/// <c>DemoTests</c> 대상 동작을 검증하는 테스트 모음이다.\n    /// </summary>\n    public sealed class DemoTests" in annotated
    assert "/// <c>Works</c> 테스트 시나리오를 검증한다.\n        /// </summary>\n        [Test]" in annotated
    assert "/// <c>Helper</c> 테스트 지원 동작을 수행한다.\n        /// </summary>\n        private static int Helper" in annotated
    second, inserted_again = annotate_text(annotated)
    assert second == annotated
    assert inserted_again == 0


def annotate_files() -> tuple[int, int]:
    changed_files = 0
    inserted_lines = 0
    for path in sorted(TEST_ROOT.rglob("*.cs")):
        original = path.read_text(encoding="utf-8")
        annotated, inserted = annotate_text(original)
        if annotated == original:
            continue
        path.write_text(annotated, encoding="utf-8", newline="")
        changed_files += 1
        inserted_lines += inserted
    return changed_files, inserted_lines


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        self_test()
        print("self-test passed")
        return

    self_test()
    changed_files, inserted_lines = annotate_files()
    print(f"annotated {changed_files} files; inserted {inserted_lines} comment lines")


if __name__ == "__main__":
    main()

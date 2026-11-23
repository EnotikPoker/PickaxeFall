#!/usr/bin/env python3
"""Check source metadata and release contracts without installing Unity."""
import ast
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent

def require(condition, message):
    if not condition:
        raise SystemExit(message)

for path in (root / "Tools").glob("*.py"):
    ast.parse(path.read_text(), filename=str(path))
version = (root / "Assets/PickaxeFall/Scripts/Data/GameVersion.cs").read_text()
name = re.search(r'Name = "([^"]+)"', version).group(1)
code = re.search(r'AndroidVersionCode = (\d+)', version).group(1)
settings = (root / "ProjectSettings/ProjectSettings.asset").read_text()
require(f"bundleVersion: {name}\n" in settings, "Project version differs from GameVersion")
require(f"AndroidBundleVersionCode: {code}\n" in settings, "Android version code differs from GameVersion")

known = {}
assets = root / "Assets/PickaxeFall"
for meta in assets.rglob("*.meta"):
    match = re.search(r"(?m)^guid: ([0-9a-f]{32})$", meta.read_text())
    require(match is not None, f"Invalid GUID: {meta.relative_to(root)}")
    guid = match.group(1)
    require(guid not in known, f"Duplicate asset GUID: {meta.relative_to(root)} and {known.get(guid)}")
    known[guid] = str(meta.relative_to(root))
for png in (assets / "Resources").rglob("*.png"):
    require(png.read_bytes().startswith(b"\x89PNG\r\n\x1a\n"), f"PNG not materialized (run git lfs pull): {png.relative_to(root)}")
for source in assets.rglob("*.cs"):
    require(Path(str(source) + ".meta").is_file(), f"Missing Unity metadata: {source.relative_to(root)}")

catalog = json.loads((assets / "Resources/Localization/en.json").read_text())
seen = set()
for entry in catalog["entries"]:
    require(entry["ru"] not in seen, "Duplicate localization key: " + entry["ru"])
    seen.add(entry["ru"])
    require(entry["en"].strip(), "Empty translation: " + entry["ru"])
    pattern = r"\{\d+[^}]*\}"
    require(sorted(re.findall(pattern, entry["ru"])) == sorted(re.findall(pattern, entry["en"])), "Translation placeholders differ: " + entry["ru"])

config = json.loads((assets / "Resources/GooglePlay/config.json").read_text())
require(config["testMode"] and not config["kidozToken"], "Only the empty Play test template may be committed")
require(not (assets / "Resources/DeveloperAuth.json").exists(), "Generated developer access must not be in the source tree")
print(f"SOURCE_CONTRACTS_OK: {name}/{code}; {len(known)} Unity GUIDs; {len(seen)} translations")

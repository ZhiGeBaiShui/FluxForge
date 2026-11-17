"""Validate the reviewed source inventory, line coverage, and byte hashes."""

import hashlib
import json
from pathlib import Path


AUDIT = Path(__file__).resolve().parent
ROOT = AUDIT.parents[2]
expected = set()
for path in (ROOT / "src/Story/Runtime").rglob("*.cs"):
    expected.add(path.relative_to(ROOT).as_posix())
expected.update([
    "src/Story/Portraits/NpcPortraitRegistry.cs",
    "src/Story/Npcs/AcidBuyer/AcidBuyerStory.cs",
    "src/Story/Npcs/FlowEmitterMerchant/FlowEmitterMerchantStory.cs",
    "src/Story/Hospital/HospitalStoryPoints.cs",
])

files = []
seen = set()
for report_path in sorted(AUDIT.glob("*-review.json")):
    report = json.loads(report_path.read_text(encoding="utf-8"))
    for entry in report["files"]:
        name = entry["path"].replace("\\", "/")
        assert name not in seen, "Duplicate review: " + name
        seen.add(name)
        raw = (ROOT / name).read_bytes()
        lines = raw.decode("utf-8").splitlines()
        digest = hashlib.sha256(raw).hexdigest()
        assert digest.lower() == entry["sha256"].lower(), "Source changed: " + name
        assert len(lines) == entry["lineCount"], "Line count changed: " + name
        next_line = 1
        for block in entry["blocks"]:
            assert block["start"] == next_line, "Gap or overlap: " + name
            assert block["end"] >= block["start"], "Invalid range: " + name
            assert block["reason"].strip(), "Missing reason: " + name
            assert block["verdict"].strip(), "Missing verdict: " + name
            assert block["evidence"], "Missing evidence: " + name
            next_line = block["end"] + 1
        assert next_line == len(lines) + 1, "Unreviewed lines: " + name
        files.append({
            "path": name,
            "lineCount": len(lines),
            "blocks": len(entry["blocks"]),
            "sha256": digest,
            "reviewer": report["reviewer"],
            "report": report_path.name,
        })

assert seen == expected, "Inventory mismatch: " + str(seen.symmetric_difference(expected))
result = {
    "files": len(files),
    "lines": sum(entry["lineCount"] for entry in files),
    "blocks": sum(entry["blocks"] for entry in files),
    "inventory": sorted(files, key=lambda entry: entry["path"]),
}
(AUDIT / "coverage.json").write_text(
    json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
)
print("Verified: {files} files, {lines} lines, {blocks} reviewed blocks".format(**result))

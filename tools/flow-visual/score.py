"""盲测计分：把评审的答案与 key 对比，输出每项准确率。

用法：
    python score.py --key key.json --answers answers.json

answers 格式：{"dir": {"1":"up","2":"right",...}, "state": {...}, ...}
key 格式：qa.py blind 生成的 {"1": {"answer": "up", ...}, ...}（单组），
或 {"dir": {"1": {...}}, ...}（多组，需先用 group_keys.py 拆）。
"""
import argparse
import json
from pathlib import Path


def load_key(path):
    d = json.loads(Path(path).read_text(encoding="utf-8"))
    # 单组：{"1": {"answer": ...}}
    if all(isinstance(v, dict) and "answer" in v for v in d.values()):
        return {"_": {k: v["answer"] for k, v in d.items()}}
    # 多组：{"dir": {"1": {...}}}
    out = {}
    for g, items in d.items():
        out[g] = {k: (v["answer"] if isinstance(v, dict) else v) for k, v in items.items()}
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--key", required=True)
    ap.add_argument("--answers", required=True)
    args = ap.parse_args()

    key = load_key(args.key)
    answers = json.loads(Path(args.answers).read_text(encoding="utf-8"))

    total_ok = total = 0
    for group, truth in key.items():
        given = answers.get(group, answers.get("_", {}))
        ok = sum(1 for k, v in truth.items() if given.get(k) == v)
        total_ok += ok
        total += len(truth)
        print(f"{group}: {ok}/{len(truth)}  "
              f"({', '.join(f'{k}:{given.get(k, '?')}≠{v}' for k, v in truth.items() if given.get(k) != v) or '全对'})")
    if total:
        print(f"TOTAL: {total_ok}/{total} = {total_ok/total:.0%}")


if __name__ == "__main__":
    main()

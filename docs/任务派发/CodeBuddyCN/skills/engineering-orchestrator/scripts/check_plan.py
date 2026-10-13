"""Validate dependency DAG and shared-file writers; never certify code/runtime correctness."""
import argparse
import json
import posixpath
from pathlib import Path


def validate(plan):
    tasks = plan.get("tasks")
    if not isinstance(tasks, list) or not tasks:
        raise ValueError("tasks must be a non-empty list")
    by_id = {}
    for task in tasks:
        task_id = task.get("id")
        if not isinstance(task_id, str) or not task_id or task_id in by_id:
            raise ValueError("task ids must be unique non-empty strings")
        if not task.get("owner"):
            raise ValueError(f"{task_id}: missing owner")
        if task.get("status") not in {"planned", "running", "done", "blocked"}:
            raise ValueError(f"{task_id}: invalid status")
        for field in ("depends_on", "writes"):
            if not isinstance(task.get(field), list) or any(not isinstance(v, str) or not v for v in task[field]):
                raise ValueError(f"{task_id}: {field} must contain strings")
        if task_id in task["depends_on"]:
            raise ValueError(f"{task_id}: self dependency")
        by_id[task_id] = task
    for task in tasks:
        for dependency in task["depends_on"]:
            if dependency not in by_id:
                raise ValueError(f"{task['id']}: missing dependency {dependency}")
    active, visited = set(), set()

    def visit(task_id):
        if task_id in active:
            raise ValueError(f"dependency cycle at {task_id}")
        if task_id in visited:
            return
        active.add(task_id)
        for dependency in by_id[task_id]["depends_on"]:
            visit(dependency)
        active.remove(task_id)
        visited.add(task_id)

    for task_id in by_id:
        visit(task_id)
    writers = {}
    for task in tasks:
        if task["status"] != "running":
            continue
        if any(by_id[d]["status"] != "done" for d in task["depends_on"]):
            raise ValueError(f"{task['id']}: dependency not done")
        for raw in task["writes"]:
            path = posixpath.normpath(raw.replace("\\", "/")).casefold()
            for owned, writer in writers.items():
                if writer != task["id"] and (path == owned or path.startswith(owned.rstrip("/") + "/") or owned.startswith(path.rstrip("/") + "/")):
                    raise ValueError(f"write conflict: {writer} and {task['id']} on {raw}")
            writers[path] = task["id"]
    ready = [t["id"] for t in tasks if t["status"] == "planned" and all(by_id[d]["status"] == "done" for d in t["depends_on"])]
    return {"valid": True, "ready_candidates": ready, "note": "Candidates are not a parallel dispatch or proof of task completion."}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("plan", type=Path)
    args = parser.parse_args()
    try:
        result = validate(json.loads(args.plan.read_text(encoding="utf-8-sig")))
    except (ValueError, TypeError, AttributeError, OSError) as error:
        print(json.dumps({"valid": False, "error": str(error)}, ensure_ascii=False))
        return 1
    print(json.dumps(result, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

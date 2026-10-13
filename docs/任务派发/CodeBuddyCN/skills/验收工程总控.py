import copy
import importlib.util
import json
import re
from pathlib import Path

base = Path(__file__).parent
skill = base / "engineering-orchestrator"
spec = importlib.util.spec_from_file_location("plan_check", skill / "scripts/check_plan.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
plan = json.loads((skill / "assets/plan-example.json").read_text(encoding="utf-8"))
cases = []

def case(name, candidate, valid):
    try:
        module.validate(candidate)
        actual = True
    except ValueError:
        actual = False
    cases.append({"case": name, "passed": actual == valid})

case("valid-dag", plan, True)
p = copy.deepcopy(plan)
p["tasks"][0]["depends_on"] = ["integrate"]
case("reject-cycle", p, False)
p = copy.deepcopy(plan)
p["tasks"][0]["depends_on"] = ["absent"]
case("reject-missing-dependency", p, False)
p = copy.deepcopy(plan)
p["tasks"][2]["status"] = "running"
case("reject-start-before-contract", p, False)
p = copy.deepcopy(plan)
p["tasks"][0]["status"] = "done"
p["tasks"][2]["status"] = p["tasks"][3]["status"] = "running"
case("allow-independent-writers", p, True)
q = copy.deepcopy(p)
q["tasks"][3]["writes"] = ["runtime\\EMPLOYMENTAdapter.cs"]
case("reject-windows-same-file", q, False)
q = copy.deepcopy(p)
q["tasks"][3]["writes"] = ["Runtime"]
case("reject-directory-file-overlap", q, False)
q = copy.deepcopy(p)
q["tasks"][3]["writes"] = []
case("allow-readonly-peer", q, True)
q = copy.deepcopy(plan)
q["tasks"][1]["id"] = q["tasks"][0]["id"]
case("reject-duplicate-id", q, False)
links = []
for doc in skill.rglob("*.md"):
    for target in re.findall(r"\]\(([^)]+)\)", doc.read_text(encoding="utf-8")):
        if "://" not in target and not target.startswith("#"):
            links.append({"file": str(doc.relative_to(skill)), "target": target, "passed": (doc.parent / target).is_file()})
result = {"script_cases": cases, "reference_links": links, "passed": all(x["passed"] for x in cases + links), "limit": "Structural and deterministic helper verification only."}
(base / "engineering-orchestrator-结构验证.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(result, ensure_ascii=False))
raise SystemExit(0 if result["passed"] else 1)

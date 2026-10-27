#!/usr/bin/env python3
"""Generates the mock source-system data in src/main/resources/mock-data (deterministic).

Each file uses the canonical record format the adapters produce (see docs/integration-contract.md).
Run: python3 tools/generate_mock_data.py
"""
import json
import os
import random
from datetime import date

random.seed(42)
OUT = os.path.join(os.path.dirname(__file__), "..", "src", "main", "resources", "mock-data")

UNITS = [  # code, name, type, parent, location
    ("HQ", "Ceylon Holdings", "COMPANY", None, "Colombo"),
    ("FIN", "Finance", "DEPARTMENT", "HQ", "Colombo"),
    ("HR", "Human Resources", "DEPARTMENT", "HQ", "Colombo"),
    ("HR-PAY", "Payroll", "SECTION", "HR", "Colombo"),
    ("IT", "Information Technology", "DEPARTMENT", "HQ", "Colombo"),
    ("OPS", "Operations", "DEPARTMENT", "HQ", "Kandy"),
    ("SAL", "Sales", "DEPARTMENT", "HQ", "Galle"),
]

JOBS = [  # code, title, grade
    ("CEO", "Chief Executive Officer", "E1"),
    ("FIN-EXE", "Finance Executive", "G4"), ("FIN-SEXE", "Senior Finance Executive", "G5"),
    ("FIN-MGR", "Finance Manager", "M1"), ("FIN-HOD", "Head of Finance", "M3"),
    ("HR-EXE", "HR Executive", "G4"), ("HR-PAYO", "HRO Payroll", "G5"),
    ("HR-MGR", "HR Manager", "M1"), ("HR-HOD", "Head of HR", "M3"),
    ("IT-SE", "Software Engineer", "G4"), ("IT-SSE", "Senior Software Engineer", "G5"),
    ("IT-TL", "Technical Lead", "M1"), ("IT-MGR", "IT Manager", "M2"), ("IT-HOD", "Head of IT", "M3"),
    ("OPS-EXE", "Operations Executive", "G4"), ("OPS-MGR", "Operations Manager", "M1"), ("OPS-HOD", "Head of Operations", "M3"),
    ("SAL-EXE", "Sales Executive", "G4"), ("SAL-MGR", "Sales Manager", "M1"), ("SAL-HOD", "Head of Sales", "M3"),
]
JOB = {j[0]: j for j in JOBS}

FIRST = ["Nimal", "Kamala", "Sunil", "Dilani", "Ruwan", "Chamari", "Asanka", "Nadeesha", "Pradeep", "Ishara", "Tharindu",
         "Sanduni", "Mahesh", "Gayani", "Lahiru", "Anusha", "Kasun", "Harshi", "Dinesh", "Malsha", "Roshan", "Piumi",
         "Chathura", "Nirosha", "Janaka", "Thilini", "Supun", "Hiruni", "Amila", "Shanika", "Buddhika", "Yasodha"]
LAST = ["Perera", "Fernando", "Silva", "Jayasinghe", "Bandara", "Wickramasinghe", "Rajapaksa", "Gunawardena", "Dissanayake",
        "Herath", "Kumara", "Ranasinghe", "Senanayake", "Abeysekara", "Weerasinghe", "Samarasinghe"]

# employee no, job, unit, supervisor no
PEOPLE = [
    ("E0001", "CEO", "HQ", None),
    ("E0002", "HR-HOD", "HR", "E0001"),
    ("E0003", "HR-MGR", "HR", "E0002"),
    ("E0004", "HR-PAYO", "HR-PAY", "E0003"),
    ("E0005", "HR-EXE", "HR", "E0003"),
    ("E0006", "HR-EXE", "HR-PAY", "E0004"),
    ("E0007", "FIN-HOD", "FIN", "E0001"),
    ("E0008", "FIN-MGR", "FIN", "E0007"),
    ("E0009", "FIN-SEXE", "FIN", "E0008"),
    ("E0010", "FIN-SEXE", "FIN", "E0008"),
    ("E0011", "FIN-EXE", "FIN", "E0009"),
    ("E0012", "FIN-EXE", "FIN", "E0009"),
    ("E0013", "IT-HOD", "IT", "E0001"),
    ("E0014", "IT-MGR", "IT", "E0013"),
    ("E0015", "IT-TL", "IT", "E0014"),
    ("E0016", "IT-TL", "IT", "E0014"),
    ("E0017", "IT-SSE", "IT", "E0015"),
    ("E0018", "IT-SSE", "IT", "E0015"),
    ("E0019", "IT-SE", "IT", "E0016"),
    ("E0020", "IT-SE", "IT", "E0016"),
    ("E0021", "OPS-HOD", "OPS", "E0001"),
    ("E0022", "OPS-MGR", "OPS", "E0021"),
    ("E0023", "OPS-MGR", "OPS", "E0021"),
    ("E0024", "OPS-EXE", "OPS", "E0022"),
    ("E0025", "OPS-EXE", "OPS", "E0022"),
    ("E0026", "OPS-EXE", "OPS", "E0023"),
    ("E0027", "SAL-HOD", "SAL", "E0001"),
    ("E0028", "SAL-MGR", "SAL", "E0027"),
    ("E0029", "SAL-MGR", "SAL", "E0027"),
    ("E0030", "SAL-EXE", "SAL", "E0028"),
    ("E0031", "SAL-EXE", "SAL", "E0028"),
    ("E0032", "SAL-EXE", "SAL", "E0029"),
]
HEADS = {"HQ": "E0001", "HR": "E0002", "HR-PAY": "E0004", "FIN": "E0007", "IT": "E0013", "OPS": "E0021", "SAL": "E0027"}

COMPETENCIES = [
    ("LEAD", "Leadership", "Leadership", "Sets direction and inspires others"),
    ("STRAT", "Strategic Thinking", "Leadership", "Sees the big picture and plans ahead"),
    ("PEOPLE", "Developing People", "Leadership", "Coaches and grows team members"),
    ("COMM", "Communication", "Core", "Communicates clearly in speech and writing"),
    ("DELIV", "Delivering Results", "Core", "Plans work and delivers on commitments"),
    ("CUST", "Customer Focus", "Core", "Understands and serves customer needs"),
    ("TECH", "Technical Expertise", "Functional", "Depth of knowledge in own field"),
    ("FINAN", "Financial Acumen", "Functional", "Understands budgets, costs and financial results"),
]

UNIT_LOC = {u[0]: u[4] for u in UNITS}


def person(no, i):
    first = FIRST[i % len(FIRST)]
    last = LAST[(i * 7) % len(LAST)]
    return first, last


def main():
    os.makedirs(OUT, exist_ok=True)
    core = {"ORG_UNIT": [], "JOB": [], "POSITION": [], "EMPLOYEE": []}
    for code, name, typ, parent, loc in UNITS:
        core["ORG_UNIT"].append({"id": "OU-" + code, "code": code, "name": name, "type": typ,
                                 "parentRef": "OU-" + parent if parent else None, "headRef": "HR-" + HEADS[code],
                                 "costCentre": "CC-" + code, "location": loc, "country": "Sri Lanka"})
    for code, title, grade in JOBS:
        core["JOB"].append({"id": "JOB-" + code, "code": code, "title": title, "gradeCode": grade})

    for i, (no, job, unit, sup) in enumerate(PEOPLE):
        first, last = person(no, i)
        title = JOB[job][1]
        ptype = "HOD" if job.endswith("-HOD") or job == "CEO" else ("KEY" if job in ("HR-PAYO", "IT-MGR") else "STANDARD")
        core["POSITION"].append({"id": "POS-" + no, "code": "P-" + no[1:], "title": title, "positionType": ptype,
                                 "orgUnitRef": "OU-" + unit, "jobRef": "JOB-" + job, "incumbentRef": "HR-" + no,
                                 "costCentre": "CC-" + unit, "location": UNIT_LOC[unit], "country": "Sri Lanka",
                                 "workStation": UNIT_LOC[unit] + " Head Office" if unit != "OPS" else "Kandy Plant"})
        years = 2 + (i * 5) % 17
        core["EMPLOYEE"].append({"id": "HR-" + no, "employeeNo": no, "firstName": first, "lastName": last,
                                 "email": f"{first.lower()}.{last.lower()}{i}@ceylon.example",
                                 "pictureUrl": None, "gradeCode": JOB[job][2], "jobRef": "JOB-" + job,
                                 "positionRef": "POS-" + no, "orgUnitRef": "OU-" + unit,
                                 "supervisorRef": "HR-" + sup if sup else None, "status": "ACTIVE",
                                 "joinDate": date(2025 - years, 1 + i % 12, 1 + i % 27).isoformat(),
                                 "employmentCategory": "PERMANENT" if i % 9 else "CONTRACT",
                                 "location": UNIT_LOC[unit], "workStation": UNIT_LOC[unit] + " Head Office",
                                 "country": "Sri Lanka", "costCentre": "CC-" + unit})
    # Vacant critical position (successor needed, no incumbent)
    core["POSITION"].append({"id": "POS-V001", "code": "P-V001", "title": "Head of Risk", "positionType": "HOD",
                             "orgUnitRef": "OU-FIN", "jobRef": "JOB-FIN-HOD", "incumbentRef": None, "costCentre": "CC-FIN",
                             "location": "Colombo", "country": "Sri Lanka", "workStation": "Colombo Head Office"})

    perf = {"COMPETENCY": [], "APPRAISAL": []}
    for code, name, cat, desc in COMPETENCIES:
        perf["COMPETENCY"].append({"id": "C-" + code, "code": code, "name": name, "category": cat, "description": desc})
    for i, (no, job, unit, sup) in enumerate(PEOPLE):
        talent = random.random()
        base = 2.6 + talent * 2.2
        for y in (2023, 2024, 2025):
            score = round(min(5.0, max(1.0, base + random.uniform(-0.5, 0.5) + (y - 2023) * 0.1)), 2)
            comps = {c[0]: round(min(5.0, max(1.0, score + random.uniform(-0.8, 0.8))), 1) for c in COMPETENCIES}
            band = "Outstanding" if score >= 4.5 else "Exceeds" if score >= 3.75 else "Meets" if score >= 2.75 else "Below"
            perf["APPRAISAL"].append({"id": f"APR-{no}-{y}", "employeeRef": no, "cycleCode": f"FY{y}",
                                      "periodEnd": f"{y}-12-31", "finalScore": score, "ratingBand": band,
                                      "finalised": True, "competencyScores": comps})

    sshr = {"QUALIFICATION": [], "EXPERIENCE": [], "EXPERTISE": []}
    degree = {"FIN": ("BSc Accounting", "CA Sri Lanka"), "HR": ("BA Human Resource Management", "CIPM"),
              "HR-PAY": ("BCom", "CIPM"), "IT": ("BSc Computer Science", "AWS Solutions Architect"),
              "OPS": ("BSc Engineering", "Six Sigma Green Belt"), "SAL": ("BBA Marketing", "CIM"), "HQ": ("MBA", "Chartered Director")}
    for i, (no, job, unit, sup) in enumerate(PEOPLE):
        deg, cert = degree[unit]
        sshr["QUALIFICATION"].append({"id": f"Q-{no}-1", "employeeRef": no, "type": "DEGREE", "title": deg,
                                      "institution": "University of Colombo", "level": 3, "yearCompleted": 2005 + i % 15})
        sshr["QUALIFICATION"].append({"id": f"Q-{no}-2", "employeeRef": no, "type": "PROFESSIONAL", "title": cert,
                                      "institution": "Professional body", "level": 2, "yearCompleted": 2010 + i % 12})
        if JOB[job][2].startswith("M") or JOB[job][2] == "E1" or i % 4 == 0:
            sshr["QUALIFICATION"].append({"id": f"Q-{no}-3", "employeeRef": no, "type": "DEGREE", "title": "MBA",
                                          "institution": "PIM Sri Jayewardenepura", "level": 4, "yearCompleted": 2016 + i % 8})
        years = 2 + (i * 5) % 17
        start_internal = date(2025 - years, 1 + i % 12, 1 + i % 27)
        prev_years = 1 + i % 6
        sshr["EXPERIENCE"].append({"id": f"X-{no}-1", "employeeRef": no, "employer": "Lanka Trading PLC",
                                   "title": "Trainee " + JOB[job][1].split()[-1], "jobCode": None,
                                   "startDate": date(start_internal.year - prev_years, 1, 1).isoformat(),
                                   "endDate": start_internal.isoformat(), "internal": False})
        sshr["EXPERIENCE"].append({"id": f"X-{no}-2", "employeeRef": no, "employer": "Ceylon Holdings",
                                   "title": JOB[job][1], "jobCode": job, "startDate": start_internal.isoformat(),
                                   "endDate": None, "internal": True})
        for k, comp in enumerate(random.sample([c[0] for c in COMPETENCIES], 3)):
            sshr["EXPERTISE"].append({"id": f"S-{no}-{k}", "employeeRef": no, "area": next(c[1] for c in COMPETENCIES if c[0] == comp),
                                      "competencyCode": comp, "proficiency": random.randint(2, 5)})

    lms = {"COURSE": [], "ENROLMENT": []}
    courses = [
        ("LMS-101", "Leading High-Performing Teams", [("COMPETENCY", "LEAD"), ("ROLE", "FIN-HOD"), ("ROLE", "IT-HOD")], 16),
        ("LMS-102", "Strategic Planning Essentials", [("COMPETENCY", "STRAT"), ("ROLE", "OPS-HOD"), ("ROLE", "SAL-HOD")], 12),
        ("LMS-103", "Coaching for Managers", [("COMPETENCY", "PEOPLE"), ("ROLE", "HR-MGR"), ("ROLE", "IT-MGR")], 8),
        ("LMS-104", "Business Writing", [("COMPETENCY", "COMM")], 6),
        ("LMS-105", "Finance for Non-Finance Managers", [("COMPETENCY", "FINAN"), ("ROLE", "OPS-MGR"), ("ROLE", "SAL-MGR")], 10),
        ("LMS-106", "Payroll Compliance in Sri Lanka", [("ROLE", "HR-PAYO"), ("COMPETENCY", "TECH")], 8),
        ("LMS-107", "Cloud Architecture Fundamentals", [("ROLE", "IT-TL"), ("ROLE", "IT-MGR"), ("COMPETENCY", "TECH")], 20),
        ("LMS-108", "Customer Experience Design", [("COMPETENCY", "CUST"), ("ROLE", "SAL-MGR")], 6),
        ("LMS-109", "Project Delivery Excellence", [("COMPETENCY", "DELIV")], 12),
        ("LMS-110", "Executive Presence", [("COMPETENCY", "LEAD"), ("COMPETENCY", "COMM"), ("ROLE", "HR-HOD")], 6),
    ]
    for cid, title, maps, hours in courses:
        lms["COURSE"].append({"id": cid, "title": title, "url": f"https://lms.ceylon.example/course/{cid}",
                              "provider": "Ceylon Academy", "durationHours": hours,
                              "mappings": [{"type": t, "code": c} for t, c in maps]})
    n = 0
    for i, (no, job, unit, sup) in enumerate(PEOPLE):
        for cid, *_ in random.sample(courses, 2):
            n += 1
            status = random.choice(["ASSIGNED", "ONGOING", "COMPLETED"])
            lms["ENROLMENT"].append({"id": f"EN-{n:04d}", "employeeRef": no, "courseRef": cid, "status": status,
                                     "assignedOn": "2026-03-01",
                                     "completedOn": "2026-06-15" if status == "COMPLETED" else None,
                                     "progressPercent": 100 if status == "COMPLETED" else (0 if status == "ASSIGNED" else random.randint(10, 90)),
                                     "score": round(random.uniform(60, 98), 1) if status == "COMPLETED" else None})

    for name, data in (("core_hr", core), ("performance", perf), ("sshr", sshr), ("lms", lms)):
        with open(os.path.join(OUT, name + ".json"), "w") as f:
            json.dump(data, f, indent=1)
            f.write("\n")
    print("wrote", OUT)


if __name__ == "__main__":
    main()

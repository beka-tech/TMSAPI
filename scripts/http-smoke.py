"""Real HTTP regression checks. Requires a migrated, isolated TMS_TEST_DB named tms_test_*."""
import base64
import concurrent.futures
import http.client
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import uuid

root = Path(__file__).resolve().parents[1]
connection = os.environ["TMS_TEST_DB"]
parts = dict(part.split("=", 1) for part in connection.split(";") if "=" in part)
parts = {k.lower(): v for k, v in parts.items()}
assert parts.get("database", "").startswith("tms_test_"), "Only isolated test databases are allowed"
psql = shutil.which("psql")
assert psql, "PostgreSQL psql client is required"

def sql(statement):
    env = os.environ.copy()
    env["PGPASSWORD"] = parts.get("password", "")
    result = subprocess.run([psql, "-X", "-h", parts.get("host", "127.0.0.1"), "-p", parts.get("port", "5432"),
        "-U", parts.get("username", "postgres"), "-d", parts["database"], "-tA", "-v", "ON_ERROR_STOP=1"],
        input=statement, text=True, capture_output=True, env=env, check=True)
    return result.stdout.strip().splitlines()[0]

def request(method, path, body=None, token=None, source="127.0.0.2", extra=None):
    headers = {"Content-Type": "application/json"}
    if token: headers["Authorization"] = "Bearer " + token
    if extra: headers.update(extra)
    client = http.client.HTTPConnection("127.0.0.1", 5187, timeout=15, source_address=(source, 0))
    try:
        client.request(method, path, None if body is None else json.dumps(body), headers)
        response = client.getresponse()
        raw = response.read().decode()
        try: data = json.loads(raw)
        except json.JSONDecodeError: data = raw
        return response.status, data
    finally: client.close()

def check(expected, result):
    assert result[0] == expected, f"Expected HTTP {expected}, got {result[0]}: {result[1]}"
    return result[1]

env = os.environ.copy()
env.update({"ConnectionStrings__TmsDatabase": connection, "ASPNETCORE_ENVIRONMENT": "Production",
    "ASPNETCORE_URLS": "http://127.0.0.1:5187", "Jwt__Key": "isolated-smoke-test-key-with-at-least-32-bytes-123456",
    "Jwt__Issuer": "tms-test", "Jwt__Audience": "tms-test", "Jwt__ExpiryMinutes": "10",
    "Payments__GatewayUrl": "https://example.test", "Payments__MaxDepositBirr": "1000",
    "Logging__EventLog__LogLevel__Default": "None", "DataProtection__KeyPath": str(root / "TestResults/data-protection")})
log_path = root / "TestResults" / "http-smoke.log"
log_path.parent.mkdir(exist_ok=True)
with log_path.open("w", encoding="utf-8") as log:
    process = subprocess.Popen(["dotnet", str(root / "TmsApi.Api/bin/Debug/net10.0/TmsApi.Api.dll")],
        cwd=root / "TmsApi.Api", env=env, stdout=log, stderr=subprocess.STDOUT,
        creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
    try:
        for _ in range(60):
            if process.poll() is not None: raise RuntimeError(f"API exited; see {log_path}")
            try:
                if request("GET", "/health/ready")[0] == 200: break
            except OSError: pass
            time.sleep(0.5)
        else: raise RuntimeError("API did not become ready")
        suffix = uuid.uuid4().hex[:10]
        password = "TestPassword123"
        def register(label, source):
            email = f"{label}-{suffix}@example.test"
            check(200, request("POST", "/api/auth/register", {"email": email, "password": password,
                "firstName": label, "lastName": "Test"}, source=source))
            return email
        def login(email, source):
            return check(200, request("POST", "/api/auth/login", {"email": email, "password": password}, source=source))
        check(400, request("POST", "/api/auth/register", {"email": "bad@example.test", "password": password,
            "firstName": "Bad", "lastName": "Role", "role": "Admin"}))
        student_email = register("student", "127.0.0.2")
        student_id = int(sql(f'''INSERT INTO "Students" ("RegistrationNumber", "Name", "GPA", "IsActive", "IsDeleted")
            VALUES ('S{suffix}', 'Smoke Student', 0, TRUE, FALSE) RETURNING "Id";'''))
        sql(f'''UPDATE "AspNetUsers" SET "StudentId" = {student_id} WHERE "Email" = '{student_email}';''')
        student = login(student_email, "127.0.0.2")
        check(401, request("GET", "/api/v2/enrollments"))
        check(403, request("PATCH", "/api/v2/enrollments/1/grade", {"grade": 90}, student["accessToken"]))
        check(200, request("GET", f"/api/v2/students/{student_id}", token=student["accessToken"]))
        check(403, request("GET", "/api/v2/students/2147483647", token=student["accessToken"]))
        check(401, request("POST", "/hubs/tms/negotiate?negotiateVersion=1&studentId=1"))
        print("PASS: anonymous access, student ownership, grade protection, hub authentication", flush=True)
        source = "127.0.0.3"
        outsider = login(register("outsider", source), source)
        check(403, request("GET", f"/api/v2/students/{student_id}", token=outsider["accessToken"], source=source))
        with concurrent.futures.ThreadPoolExecutor(2) as pool:
            results = list(pool.map(lambda _: request("POST", "/api/auth/refresh",
                {"refreshToken": outsider["refreshToken"]}, source=source), range(2)))
        assert sorted(r[0] for r in results) == [200, 401], results
        winner = next(r[1] for r in results if r[0] == 200)
        check(401, request("GET", "/api/v2/courses", token=winner["accessToken"], source=source))
        print("PASS: concurrent refresh rotation and replay revocation", flush=True)
        source = "127.0.0.4"
        admin_email = register("admin", source)
        sql(f'''INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName") VALUES ('smoke-admin-{suffix}', 'Admin', 'ADMIN')
            ON CONFLICT ("NormalizedName") DO NOTHING;
            INSERT INTO "AspNetUserRoles" ("UserId", "RoleId") SELECT u."Id", r."Id" FROM "AspNetUsers" u,
            "AspNetRoles" r WHERE u."Email" = '{admin_email}' AND r."NormalizedName" = 'ADMIN';''')
        admin = login(admin_email, source)["accessToken"]
        check(404, request("POST", "/api/v2/enrollments/2147483647/approve", token=admin, source=source))
        course_id = int(sql(f'''INSERT INTO "Courses" ("Code", "Title", "MaxCapacity") VALUES ('{suffix}', 'Smoke Course', 1) RETURNING "Id";'''))
        enrollment_id = int(sql(f'''INSERT INTO "Enrollments" ("StudentId", "CourseId", "Status", "EnrolledAt")
            VALUES ({student_id}, {course_id}, 'Completed', now()) RETURNING "Id";'''))
        check(409, request("POST", f"/api/v2/enrollments/{enrollment_id}/approve", token=admin, source=source))
        job = check(202, request("POST", "/api/v2/transcripts", {"studentId": student_id}, admin, source,
            {"Idempotency-Key": "transcript-" + suffix}))
        duplicate = check(202, request("POST", "/api/v2/transcripts", {"studentId": student_id}, admin, source,
            {"Idempotency-Key": "transcript-" + suffix}))
        assert job["reportId"] == duplicate["reportId"]
        for _ in range(10):
            status = check(200, request("GET", f'/api/v2/transcripts/{job["reportId"]}/status', token=admin, source="127.0.0.5"))
            if status["state"] == "Ready": break
            time.sleep(1)
        else: raise RuntimeError("Transcript did not become ready")
        content = check(200, request("GET", status["downloadUrl"], token=admin, source="127.0.0.5"))
        assert "Smoke Student" in content and "Smoke Course" in content
        instructor_email = register("instructor", "127.0.0.8")
        instructor_id = sql(f'''SELECT "Id" FROM "AspNetUsers" WHERE "Email" = '{instructor_email}';''')
        check(204, request("POST", f"/api/administration/users/{instructor_id}/roles", {"role": "Instructor"}, admin, "127.0.0.9"))
        check(204, request("PUT", f"/api/administration/courses/{course_id}/instructor", {"userId": instructor_id}, admin, "127.0.0.9"))
        instructor = login(instructor_email, "127.0.0.8")["accessToken"]
        check(204, request("PUT", f"/api/v2/courses/{course_id}", {"title": "Updated by instructor"}, instructor, "127.0.0.8"))
        other_course = int(sql(f'''INSERT INTO "Courses" ("Code", "Title", "MaxCapacity") VALUES ('X{suffix[:9]}', 'Other course', 1) RETURNING "Id";'''))
        check(403, request("PUT", f"/api/v2/courses/{other_course}", {"title": "Forbidden"}, instructor, "127.0.0.8"))
        sql(f'''UPDATE "Enrollments" SET "Status" = 'Approved' WHERE "Id" = {enrollment_id};''')
        check(204, request("PATCH", f"/api/v2/enrollments/{enrollment_id}/grade", {"grade": 90}, instructor, "127.0.0.8"))
        check(400, request("PATCH", f"/api/v2/enrollments/{enrollment_id}/grade", {"grade": 100.1}, instructor, "127.0.0.8"))
        check(400, request("PATCH", f"/api/v2/enrollments/{enrollment_id}/status", {}, instructor, "127.0.0.8"))
        print("PASS: role grants, instructor assignment, course ownership, grade validation", flush=True)
        check(204, request("POST", "/api/auth/logout", token=admin, source=source))
        check(401, request("GET", "/api/v2/courses", token=admin, source=source))
        print("PASS: enrollment errors, durable transcript download, idempotency, logout", flush=True)
        for _ in range(5):
            check(401, request("POST", "/api/auth/login", {"email": "missing@example.test", "password": password}, source="127.0.0.6"))
        check(429, request("POST", "/api/auth/login", {"email": "missing@example.test", "password": password}, source="127.0.0.6"))
        check(401, request("POST", "/api/auth/login", {"email": "missing@example.test", "password": password}, source="127.0.0.7"))
        print("PASS: per-client login throttling", flush=True)
    finally:
        process.terminate()
        try: process.wait(timeout=10)
        except subprocess.TimeoutExpired: process.kill(); process.wait()

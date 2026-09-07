-- Run with psql against the intended database after verifying the account owner.
-- Supply the existing user ID with: psql ... -v user_id=VERIFIED_USER_ID -f docs/admin-bootstrap.sql
-- No default credentials are created. All sessions for this user are revoked.
\set ON_ERROR_STOP on
BEGIN;
SELECT 1 FROM "AspNetUsers" WHERE "Id" = :'user_id' FOR UPDATE;
INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
VALUES (gen_random_uuid()::text, 'Admin', 'ADMIN', gen_random_uuid()::text)
ON CONFLICT ("NormalizedName") DO NOTHING;
INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id" FROM "AspNetUsers" u, "AspNetRoles" r
WHERE u."Id" = :'user_id' AND r."NormalizedName" = 'ADMIN'
ON CONFLICT DO NOTHING;
UPDATE "AspNetUsers" SET "SecurityStamp" = gen_random_uuid()::text, "ConcurrencyStamp" = gen_random_uuid()::text WHERE "Id" = :'user_id';
UPDATE "RefreshTokens" SET "IsRevoked" = TRUE WHERE "UserId" = :'user_id';
INSERT INTO "AuditEntries" ("OccurredAt", "ActorId", "EntityType", "EntityId", "Action", "Changes")
SELECT now(), 'database-operator', 'User', "Id", 'BootstrapAdmin', '{"Role":"Admin"}' FROM "AspNetUsers" WHERE "Id" = :'user_id';
COMMIT;

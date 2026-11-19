-- Current seeds for a fresh installation; existing databases use 05_six_permissions_and_active_types.sql.
-- Idempotent seeds. Existing permission values are never overwritten.
-- Session-only encoding; does not alter the shared database's default collation.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
START TRANSACTION;
INSERT INTO sigocalco_meetings_acta_type (acta_type_id, code, name)
SELECT 1, 'REUNION', 'Acta de reunión'
WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_acta_type WHERE code = 'REUNION');
INSERT INTO sigocalco_meetings_acta_type (acta_type_id, code, name)
SELECT 3, 'ADMINISTRATIVA', 'Acta administrativa'
WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_acta_type WHERE code = 'ADMINISTRATIVA');

INSERT INTO sigocalco_meetings_acta_status (name)
SELECT s.name FROM (
    SELECT 'BORRADOR' AS name UNION ALL SELECT 'ABIERTA' UNION ALL SELECT 'EN_REVISION'
    UNION ALL SELECT 'CERRADA' UNION ALL SELECT 'ANULADA'
) s WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_acta_status existing WHERE existing.name = s.name);

INSERT INTO sigocalco_meetings_meeting_status (name)
SELECT s.name FROM (SELECT 'PROGRAMADA' AS name UNION ALL SELECT 'REALIZADA' UNION ALL SELECT 'CANCELADA') s
WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_meeting_status existing WHERE existing.name = s.name);

-- Explicit assignments authorized for this delivery, using existing active Authentication roles (SELECT only).
INSERT INTO sigocalco_meetings_permission (permission_key, permission_value)
SELECT 'PLANTILLAS_GLOBALES_GESTION', COALESCE((
    SELECT GROUP_CONCAT(TRIM(nameRole) ORDER BY IdRole SEPARATOR ',') FROM sigocalco_authentication_role
    WHERE UPPER(TRIM(nameRole)) = 'ADMINISTRADOR' AND statusRole = 1), '')
WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_permission WHERE permission_key = 'PLANTILLAS_GLOBALES_GESTION');
INSERT INTO sigocalco_meetings_permission (permission_key, permission_value)
SELECT 'PLANTILLAS_CENTRO_GESTION', COALESCE((
    SELECT GROUP_CONCAT(TRIM(nameRole) ORDER BY IdRole SEPARATOR ',') FROM sigocalco_authentication_role
    WHERE UPPER(TRIM(nameRole)) IN ('ADMINISTRADOR', 'ADMINISTRADOR CENTRO') AND statusRole = 1), '')
WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_permission WHERE permission_key = 'PLANTILLAS_CENTRO_GESTION');
INSERT INTO sigocalco_meetings_permission (permission_key, permission_value)
SELECT required.permission_key, '' FROM (
    SELECT 'PLANTILLAS_GLOBALES_LECTURA' AS permission_key UNION ALL SELECT 'PLANTILLAS_CENTRO_LECTURA'
    UNION ALL SELECT 'ACTAS_GESTION' UNION ALL SELECT 'ACTAS_LECTURA'
) required WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_permission p WHERE p.permission_key = required.permission_key);

COMMIT;

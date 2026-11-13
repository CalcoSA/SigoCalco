-- Idempotent seeds. Existing permission values are never overwritten.
-- Session-only encoding; does not alter the shared database's default collation.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
START TRANSACTION;
INSERT INTO sigocalco_meetings_acta_type (code, name)
SELECT 'REUNION', 'Acta de reunión'
WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_acta_type WHERE code = 'REUNION');
INSERT INTO sigocalco_meetings_acta_type (code, name)
SELECT 'DISCIPLINARIA', 'Acta disciplinaria'
WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_acta_type WHERE code = 'DISCIPLINARIA');
INSERT INTO sigocalco_meetings_acta_type (code, name)
SELECT 'ADMINISTRATIVA', 'Acta administrativa'
WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_acta_type WHERE code = 'ADMINISTRATIVA');

INSERT INTO sigocalco_meetings_acta_status (name)
SELECT s.name FROM (
    SELECT 'BORRADOR' AS name UNION ALL SELECT 'ABIERTA' UNION ALL SELECT 'EN_REVISION'
    UNION ALL SELECT 'CERRADA' UNION ALL SELECT 'ANULADA'
) s WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_acta_status existing WHERE existing.name = s.name);

INSERT INTO sigocalco_meetings_meeting_status (name)
SELECT s.name FROM (SELECT 'PROGRAMADA' AS name UNION ALL SELECT 'REALIZADA' UNION ALL SELECT 'CANCELADA') s
WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_meeting_status existing WHERE existing.name = s.name);

-- Nine explicitly approved grants, based on the real active Authentication role (SELECT only).
INSERT INTO sigocalco_meetings_permission (permission_key, permission_value)
SELECT CONCAT('ACTA_', t.code, '_', op.operation), r.nameRole
FROM sigocalco_meetings_acta_type t
CROSS JOIN (SELECT 'PLANTILLA_CREACION' AS operation UNION ALL SELECT 'PLANTILLA_LECTURA' UNION ALL SELECT 'VERSION_CREACION') op
JOIN sigocalco_authentication_role r ON UPPER(TRIM(r.nameRole)) = 'ADMINISTRADOR' AND r.statusRole = 1
WHERE t.code IN ('REUNION', 'DISCIPLINARIA', 'ADMINISTRATIVA')
AND NOT EXISTS (
    SELECT 1 FROM sigocalco_meetings_permission p WHERE p.permission_key = CONCAT('ACTA_', t.code, '_', op.operation)
);

-- General acta permissions and cross-center visibility start denied.
INSERT INTO sigocalco_meetings_permission (permission_key, permission_value)
SELECT CONCAT('ACTA_', t.code, '_', op.operation), ''
FROM sigocalco_meetings_acta_type t
CROSS JOIN (SELECT 'CREACION' AS operation UNION ALL SELECT 'LECTURA' UNION ALL SELECT 'EDICION') op
WHERE t.code IN ('REUNION', 'DISCIPLINARIA', 'ADMINISTRATIVA')
AND NOT EXISTS (
    SELECT 1 FROM sigocalco_meetings_permission p WHERE p.permission_key = CONCAT('ACTA_', t.code, '_', op.operation)
);
INSERT INTO sigocalco_meetings_permission (permission_key, permission_value)
SELECT 'VIEW_ALL_SOLUTION_CENTERS', ''
WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_permission WHERE permission_key = 'VIEW_ALL_SOLUTION_CENTERS');
COMMIT;


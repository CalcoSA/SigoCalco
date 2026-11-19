-- Incremental data migration. Apply only using Invoke-NextDelivery.ps1 after its local guards and backup verification.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
START TRANSACTION;

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

-- Old grants remain recoverable in the verified full backup. They are not promoted to broader authorizations.
DELETE FROM sigocalco_meetings_permission WHERE permission_key IN (
    'ACTA_REUNION_PLANTILLA_CREACION', 'ACTA_REUNION_PLANTILLA_LECTURA', 'ACTA_REUNION_VERSION_CREACION',
    'ACTA_ADMINISTRATIVA_PLANTILLA_CREACION', 'ACTA_ADMINISTRATIVA_PLANTILLA_LECTURA', 'ACTA_ADMINISTRATIVA_VERSION_CREACION',
    'ACTA_DISCIPLINARIA_PLANTILLA_CREACION', 'ACTA_DISCIPLINARIA_PLANTILLA_LECTURA', 'ACTA_DISCIPLINARIA_VERSION_CREACION',
    'ACTA_REUNION_CREACION', 'ACTA_REUNION_LECTURA', 'ACTA_REUNION_EDICION',
    'ACTA_ADMINISTRATIVA_CREACION', 'ACTA_ADMINISTRATIVA_LECTURA', 'ACTA_ADMINISTRATIVA_EDICION',
    'ACTA_DISCIPLINARIA_CREACION', 'ACTA_DISCIPLINARIA_LECTURA', 'ACTA_DISCIPLINARIA_EDICION', 'VIEW_ALL_SOLUTION_CENTERS'
);

-- Never delete/reassign historical records. The wrapper reports references before attempting this guarded deletion.
DELETE t FROM sigocalco_meetings_acta_type t WHERE t.code = 'DISCIPLINARIA'
AND NOT EXISTS (SELECT 1 FROM sigocalco_meetings_template p WHERE p.acta_type_id = t.acta_type_id)
AND NOT EXISTS (SELECT 1 FROM sigocalco_meetings_template_version v WHERE v.acta_type_id = t.acta_type_id)
AND NOT EXISTS (SELECT 1 FROM sigocalco_meetings_acta a WHERE a.acta_type_id = t.acta_type_id);
COMMIT;

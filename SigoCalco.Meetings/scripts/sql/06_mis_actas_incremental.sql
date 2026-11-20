-- Incremental upgrade from the 18-table model. Run only after guarded preflight and verified backup.
-- DDL commits implicitly. If interrupted, inspect and recover pending operations individually.
ALTER TABLE sigocalco_meetings_acta_participant ADD COLUMN attended tinyint(1) NULL;
CREATE INDEX IX_meetings_acta_owner_id ON sigocalco_meetings_acta (created_by_user, acta_id);

ALTER TABLE sigocalco_meetings_meeting_notification
  DROP FOREIGN KEY `FK_sigocalco_meetings_meeting_notification_sigocalco_meetings_m~`,
  MODIFY COLUMN meeting_id int NULL,
  ADD COLUMN acta_id int NULL,
  ADD CONSTRAINT FK_meetings_notification_meeting
    FOREIGN KEY (meeting_id) REFERENCES sigocalco_meetings_meeting (meeting_id) ON DELETE RESTRICT;

-- Preserve any existing notification by deriving its acta from its existing meeting.
UPDATE sigocalco_meetings_meeting_notification n
JOIN sigocalco_meetings_meeting m ON m.meeting_id=n.meeting_id
SET n.acta_id=m.acta_id WHERE n.acta_id IS NULL;

ALTER TABLE sigocalco_meetings_meeting_notification
  MODIFY COLUMN acta_id int NOT NULL,
  ADD CONSTRAINT FK_meetings_notification_acta FOREIGN KEY (acta_id) REFERENCES sigocalco_meetings_acta (acta_id) ON DELETE RESTRICT,
  ADD UNIQUE INDEX IX_meetings_notification_acta_recipient (acta_id, recipient_email);

START TRANSACTION;
INSERT INTO sigocalco_meetings_acta_status (name)
SELECT 'PUBLICADA' WHERE NOT EXISTS (SELECT 1 FROM sigocalco_meetings_acta_status WHERE name='PUBLICADA');
-- Referenced historical states are never renamed, reassigned or deleted.
DELETE s FROM sigocalco_meetings_acta_status s
LEFT JOIN sigocalco_meetings_acta a ON a.acta_status_id=s.acta_status_id
WHERE s.name IN ('ABIERTA','EN_REVISION','CERRADA') AND a.acta_id IS NULL;
COMMIT;

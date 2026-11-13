-- Authorized replacement of the nine EMPTY old Meetings tables.
-- Invoke-Migration.ps1 verifies emptiness, dependencies, local target and fresh backup.
-- Child tables precede parents; foreign_key_checks stays enabled.
DROP TABLE sigocalco_meetings_meeting_attendee;
DROP TABLE sigocalco_meetings_minute_attendee;
DROP TABLE sigocalco_meetings_commitment;
DROP TABLE sigocalco_meetings_topic;
DROP TABLE sigocalco_meetings_meeting;
DROP TABLE sigocalco_meetings_minute;
DROP TABLE sigocalco_meetings_minute_template;
DROP TABLE sigocalco_meetings_minute_type;
DROP TABLE sigocalco_meetings_attendee;

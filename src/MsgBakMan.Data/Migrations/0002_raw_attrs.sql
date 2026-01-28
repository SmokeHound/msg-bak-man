ALTER TABLE sms ADD COLUMN raw_attrs_json TEXT NULL;

ALTER TABLE mms ADD COLUMN address_raw TEXT NULL;
ALTER TABLE mms ADD COLUMN address_norm TEXT NULL;
ALTER TABLE mms ADD COLUMN raw_attrs_json TEXT NULL;

ALTER TABLE mms_addr ADD COLUMN raw_attrs_json TEXT NULL;
ALTER TABLE mms_part ADD COLUMN raw_attrs_json TEXT NULL;

BEGIN;

INSERT INTO "MealDays" ("Id", "Date", "MealType", "CutoffAt", "SettledAt")
VALUES ('00000000-0000-0000-0000-000000000001', '2026-11-19', 'Lunch', '2026-11-19T00:00:00Z', '2026-11-19T00:30:00Z');

INSERT INTO "PortionSettlements" ("Id", "MealDayId", "Count", "SettledAt", "SettledBy", "CutoffAt", "Version")
VALUES ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', 17,
        '2026-11-19T00:30:00Z', 'migration-fixture', '2026-11-19T00:00:00Z', 1);

INSERT INTO "PortionSettlements" ("Id", "MealDayId", "Count", "SettledAt", "SettledBy", "CutoffAt", "Version", "SupersedesId", "Reason")
VALUES ('00000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000001', 18,
        '2026-11-19T00:45:00Z', 'migration-fixture', '2026-11-19T00:00:00Z', 2,
        '00000000-0000-0000-0000-000000000002', 'Fixture correction');

INSERT INTO "ReportSnapshots" ("Id", "MealDayId", "SettlementId", "CreatedAt", "SignedAt", "EnergyKcalPerPortion", "CostPerPortion", "SourceJson")
VALUES ('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001',
        '00000000-0000-0000-0000-000000000002', '2026-11-19T01:00:00Z', '2026-11-19T02:00:00Z',
        50, 0, '{"fixture":"legacy"}');

INSERT INTO "NutrientReferenceRanges" ("Id", "AgeGroup", "Nutrient", "MinValue", "Source", "EffectiveFrom", "RecordedAt")
VALUES ('00000000-0000-0000-0000-000000000004', 'MigrationFixture', 'kcal', 123, 'Synthetic migration fixture',
        '2026-11-19T01:00:00Z', '2026-11-19T01:00:00Z');

INSERT INTO "SettlementAcknowledgements" ("Id", "MealDayId", "AcknowledgedAt", "AcknowledgedBy", "PortionsCount", "SnapshotToken")
VALUES ('00000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000001',
        '2026-11-19T00:35:00Z', 'migration-fixture', 17, REPEAT('a', 64));

COMMIT;

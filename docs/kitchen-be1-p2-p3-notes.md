# Kitchen (BE1) Phase 2 and Phase 3: implementation notes

Scope: P2-H01 (suppliers, dated source entries), P2-H02 (menu lifecycle), P2-H03 (frozen publication nutrition, recipe
catalogue, parent menu view), P3-H01 (ingredient estimates and CSV), P3-H02 (reference ranges with basis), P3-H03
(histories, legacy ml flag). Per LD-06 the EF migration, designer and snapshot are separate files
(`20261118100000_KitchenSourcesMenusEstimates*`, `MealTraceDbContextModelSnapshot.cs`); the migration integrator may
drop them and regenerate. Everything else is model, configuration, DTO, service, endpoint and test code.

## Routes

| Route | Roles | Task |
| --- | --- | --- |
| `POST /api/kitchen/suppliers`, `POST .../{id}/versions`, `GET /api/kitchen/suppliers`, `GET .../{id}/history` | Admin, Kitchen | P2-H01 |
| `POST /api/kitchen/ingredient-source-entries`, `POST .../{id}/versions`, `GET` (ingredientId, from, to, page), `GET .../{id}/history` | Admin, Kitchen | P2-H01 |
| `POST /api/kitchen/menus`, `GET /api/kitchen/menus`, `GET .../{id}?version=`, `POST .../{id}/versions`, `POST .../{id}/submit`, `GET .../{id}/history` | Admin, Kitchen | P2-H02 |
| `POST /api/admin/menus/{id}/review`, `POST /api/admin/menus/{id}/publish` | Admin | P2-H02 |
| `GET /api/kitchen/menus/{id}/nutrition?version=&mode=&ageGroup=&validAt=&knownAt=` | Admin, Kitchen | P2-H03 (route is a proposal) |
| `GET /api/parent/students/{studentId}/menus?from=&to=` | Parent (linked child only) | P2-H03 |
| `GET /api/kitchen/recipes?search=`, `GET /api/kitchen/recipes/{id}/versions` | Admin, Kitchen | P2-H03, P3-H03 |
| `POST /api/kitchen/ingredient-estimates`, `GET .../{id}`, `GET .../{id}/export?format=csv` | Admin, Kitchen | P3-H01 |
| `GET /api/kitchen/nutrition-references?ageGroup=`, `POST /api/admin/nutrition-references` | Admin, Kitchen read; Admin write | P3-H02 |
| Recipe nutrition `GET .../nutrition` gains `mealType=` | Admin, Kitchen | P3-H02 |

## Error codes (problem+json `code`)

400 `VALIDATION_ERROR`, `UNSUPPORTED_FORMAT` · 401 `ACTOR_NOT_RESOLVED` · 403 `FORBIDDEN` (parent not linked)
· 404 `SUPPLIER_NOT_FOUND`, `SOURCE_ENTRY_NOT_FOUND`, `MENU_NOT_FOUND`, `MENU_VERSION_NOT_FOUND`, `MEAL_DAY_NOT_FOUND`,
`ESTIMATE_NOT_FOUND`, `MENU_NOT_PUBLISHED` (no published version) · 409 `SUPPLIER_VERSION_CONFLICT`,
`SOURCE_ENTRY_VERSION_CONFLICT`, `MENU_VERSION_CONFLICT`, `MENU_STATE_INVALID`, `MENU_ALREADY_EXISTS`, `MEAL_DAY_CANCELLED`,
`STALE_SETTLEMENT`, `REFERENCE_OVERLAP`, `REFERENCE_ALREADY_SUPERSEDED`, `REFERENCE_CONFLICT`, `MENU_NOT_PUBLISHED` (asked
version is not published) · 422 `NUTRIENT_DATA_INCOMPLETE`, `LEGACY_CONVERSION_UNVERIFIED`, `UNIT_CONVERSION_MISSING`,
`EDIBLE_FRACTION_INVALID`, `MENU_NOT_PUBLISHED` (estimate of an unpublished menu), `AGE_GROUP_REFERENCE_MISSING`,
`REFERENCE_BASIS_MISMATCH`.

## Decisions taken in code (to confirm)

- **Menu state** is never a column: it is the latest append-only transition (draft, submitted, approved or rejected,
  published). A new version may follow any state except `submitted`. The published version is the highest version whose
  latest transition is `published`; parents keep seeing it while a newer draft exists.
- **Publication** computes every dish in `pinned` mode, then stores canonical JSON + SHA-256. Menu total = sum of the
  dishes' unrounded per-serving values, rounded once with kcal 0 / macros 1 decimal; a child is assumed to receive one
  portion of every dish. A 422 leaves nothing stored. Age comparison status is `not_requested`, `compared`,
  `reference_missing` or `reference_basis_mismatch`; the last two do not block publication.
- **Parent view** returns the frozen numbers and ids only (no suppliers, no raw references), and only for dates on which
  the child was enrolled in a class.
- **Estimates** use the latest settlement of each class (`GetSettlementDiffAsync`), require the snapshot token the
  caller last saw, and apply conversions from the recipe lines' pinned ingredient versions. Gross from edible weight is
  `edible / edibleFraction` (422 when the fraction is 0). Totals are rounded to 3 decimals once, after aggregation.
  Every dish is assumed served once per settled portion. CSV neutralises cells starting with `= + - @`, tab or CR.
- **Reference ranges** need an explicit basis (`perMeal` or `daily`) and optionally a meal type. Only `perMeal` ranges for
  that meal type (or for any meal) are compared with a per-meal value; daily and unspecified ranges (rows written before
  this change) are never compared and give `REFERENCE_BASIS_MISMATCH`. There is no daily-to-meal conversion rule.
  Selection and overlap use the same rule as nutrient versions (known, live, applicable, latest start wins; a new
  interval may overlap a live one only if it starts later, else it must supersede).
- **Legacy lines** (no stored quantity/unit) keep their grams, but a legacy line of an ml ingredient is flagged
  (`legacyConversionUnverified`) and blocks publication and estimates.

## Migration notes for the integrator (LD-06)

New tables: `Suppliers`, `SupplierRevisions`, `IngredientSourceEntries`, `IngredientSourceEntryVersions`, `Menus`,
`MenuVersions`, `MenuItems`, `MenuTransitions`, `MenuNutritionSnapshots`, `IngredientEstimates`. Changed:
`NutrientReferenceRanges` gains `Basis`, `MealType`, `Reason`, `SupersedesId` (nullable) and `Version` (default 1).
Concurrency relies on unique indexes: `(SupplierId, Version)`, `(SourceEntryId, Version)`, `(MenuId, Version)`,
`(MenuVersionId, Sequence)`, `Menus(MealDayId)`, `MenuNutritionSnapshots(MenuVersionId)`, and a unique `PriorVersionId` /
`SupersedesId` on each versioned table. All foreign keys are `Restrict`. Append-only protection is the SaveChanges guard in
`MealTraceDbContext`, which now covers all of these tables.

## Not done / not verified

Not covered by the offline checks: the EF queries, configuration, migration and snapshot (no EF Core or Npgsql was
available), the PostgreSQL race tests, the CI workflow, and any audit-log adapter (BE3). Not built: PostgreSQL race tests
for the new unique indexes, `GET /api/kitchen/recipes/{id}/history` (the versions list carries reason and actor),
price versions, ingredient category.

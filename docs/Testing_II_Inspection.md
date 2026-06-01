# Inspection / Code Review — Testing II

**Application:** Mini Library Loan Manager (ASP.NET Core MVC, .NET 8)
**Method:** Formal, checklist-based static inspection of the models, services, controllers and views.
**Scope:** CRUD (Book, Member, Loan), the Loan borrow/return workflow, and the Report.
**Artifacts reviewed:** [Models](../ssvv-th/Models), [Services](../ssvv-th/Services), [Controllers](../ssvv-th/Controllers), [Views](../ssvv-th/Views).

---

## 1. Inspection checklist

| ID | Area | Checklist item | CRUD | Loan | Report |
| :--- | :--- | :--- | :---: | :---: | :---: |
| I-01 | Validation | Required fields & formats enforced via data annotations | ✅ | ✅ | n/a |
| I-02 | Validation | Date relationships validated (Due ≥ Loan, Return ≥ Loan) | n/a | ✅ | n/a |
| I-03 | Business rule | Stock decremented on borrow, restored on return/delete | n/a | ✅ | n/a |
| I-04 | Business rule | Out-of-stock borrowing blocked | n/a | ✅ | n/a |
| I-05 | Data integrity | FK existence checked before insert | n/a | ✅ | n/a |
| I-06 | Data integrity | Referential delete handled gracefully (no raw 500) | ✅ | ✅ | n/a |
| I-07 | Security | `[ValidateAntiForgeryToken]` on every POST | ✅ | ✅ | n/a |
| I-08 | Security | Output encoding / no injection in generated files | n/a | n/a | ⚠️ |
| I-09 | Correctness | No dead / unreachable code | ✅ | ⚠️ | ✅ |
| I-10 | Robustness | Extreme input values bounded | ⚠️ | ⚠️ | ✅ |
| I-11 | Transactions | Multi-write operations wrapped in a transaction | n/a | ✅ | n/a |
| I-12 | Scalability | Large result sets paginated | ⚠️ | ⚠️ | ⚠️ |

Legend: ✅ pass · ⚠️ finding raised (see defect log) · n/a not applicable.

---

## 2. Strengths confirmed by the inspection

- **Inventory engine is sound.** `LoanService.CreateAsync`/`UpdateAsync`/`DeleteAsync` correctly adjust `AvailableCopies` inside an `IDbContextTransaction`, and out-of-stock borrowing and reopening are blocked — covered by [LoanWbtTests](../ssvv-th.Tests/WhiteBoxTests/LoanWbtTests.cs) and [LoanBbtTests](../ssvv-th.Tests/BlackBoxTests/LoanBbtTests.cs).
- **Referential integrity is graceful.** `BookService`/`MemberService` `DeleteAsync` raise a domain `InvalidOperationException` when loans reference the entity, and the controllers catch it and show a friendly `TempData["Error"]` banner instead of crashing. The DbContext also declares `OnDelete(DeleteBehavior.Restrict)` as a second line of defence.
- **CSRF protection** is present on all state-changing endpoints.
- **Clean layering** (Controller → Service interface → DbContext), one service per entity, no leakage of EF types into views.

---

## 3. Defect log

### DEF-L1 — Unreachable validation branch (Loan) · Severity: Low
- **File:** [LoanService.cs](../ssvv-th/Services/LoanService.cs) (`ValidateAsync`)
- **Finding:** `if (wasActive && !willBeActive && loan.ReturnDate == null)` can never be true: `willBeActive` is defined as `loan.ReturnDate == null`, so `!willBeActive` already guarantees `loan.ReturnDate != null`. The intended "Return date is required when closing a loan" message is therefore never produced.
- **Impact:** Dead code; a real validation gap (closing a loan with a null return date is silently treated as "still active").
- **Recommendation:** Remove the dead condition, or re-express the intent against the raw posted form value.

### DEF-R1 — CSV formula injection (Report) · Severity: Medium
- **File:** [ReportController.cs](../ssvv-th/Controllers/ReportController.cs) (`EscapeCsv`)
- **Finding:** `EscapeCsv` quotes values containing `,`, `"` or newlines, but does not neutralise leading `=`, `+`, `-` or `@`. A book title such as `=HYPERLINK(...)` is exported verbatim and may execute when the CSV is opened in a spreadsheet.
- **Recommendation:** Prefix values beginning with `= + - @` with a single quote, or wrap them so spreadsheet engines treat them as text.

### DEF-R2 — Search case-sensitivity parity (Report) · Severity: Low
- **File:** [ReportService.cs](../ssvv-th/Services/ReportService.cs)
- **Finding:** the search uses `string.Contains`, which is case-sensitive under the test In-Memory provider but collation-dependent (usually case-insensitive) under MySQL. Behaviour therefore differs between test and production.
- **Recommendation:** normalise both sides (`ToLower()`), or apply an explicit case-insensitive collation, so behaviour is deterministic and documented.

### DEF-G1 — Unbounded date input (CRUD/Loan) · Severity: Low
- **File:** [Loan.cs](../ssvv-th/Models/Loan.cs)
- **Finding:** `LoanDate`/`DueDate` have no lower/upper bound, so values such as `0001-01-01` or `9999-12-31` are accepted.
- **Recommendation:** add a sensible `[Range]`/custom bound (e.g. not before today − N years, not after today + N years).

### DEF-C1 — Duplicate ISBN allowed (CRUD) · Severity: Low
- **File:** [Book.cs](../ssvv-th/Models/Book.cs)
- **Finding:** `ISBN` has no uniqueness constraint; two books with the same ISBN can be created.
- **Recommendation:** add a unique index on `ISBN`.

### DEF-C2 — No pagination (CRUD/Report) · Severity: Low
- **Files:** index views and [ReportService.cs](../ssvv-th/Services/ReportService.cs)
- **Finding:** lists and reports load all rows; performance degrades as data grows.
- **Recommendation:** introduce paging / server-side limits.

---

## 4. Summary

| Severity | Count |
| :--- | :--- |
| Medium | 1 (DEF-R1) |
| Low | 5 |

The core domain logic — validation, inventory control, referential integrity and CSRF — passes inspection. The remaining findings are hardening items (formula-injection escaping, case-insensitivity parity, input bounds, uniqueness, pagination) and one unreachable branch. None block the exam functionality; DEF-R1 and DEF-L1 are the highest-value fixes.

# Exploratory Testing — Testing II

**Application:** Mini Library Loan Manager (ASP.NET Core MVC, .NET 8)
**Method:** Session-Based Test Management (SBTM) — time-boxed charters, heuristics, and session notes.
**Environment:** App run locally; In-Memory/MySQL backing store.
**Scope:** Three charters — CRUD (Book & Member), the Loan workflow, and the Report.

Each charter records what was explored, the heuristics applied, observed behaviour, and any bugs. Reproducible findings are covered by the automated test suites.

---

## Charter 1 — CRUD lifecycle of Book & Member

> **Explore** the create/edit/delete flows of Book and Member **with** invalid input, boundary values and referential constraints **to discover** validation gaps and ungraceful failures.

**Heuristics:** required-field omission, over-length input, format violations (email/phone), delete-while-referenced, double submit, navigation/back button.

| # | Action | Observed | Verdict |
| :--- | :--- | :--- | :--- |
| 1.1 | Create Book leaving Title empty | Inline "Title is required." shown, no insert | Pass |
| 1.2 | Create Book with 201-char Title | Rejected by length validation | Pass |
| 1.3 | Create Book with `AvailableCopies = -1` | Rejected ("cannot be negative") | Pass |
| 1.4 | Create Member with `bad-email` | "Invalid email address." shown | Pass |
| 1.5 | Create Member with empty Phone | Accepted (Phone optional) | Pass |
| 1.6 | Delete a Book that has a loan | Friendly red banner "…cannot be deleted because it is referenced…", book kept | Pass |
| 1.7 | Delete a Member that has a loan | Friendly banner, member kept | Pass |
| 1.8 | Create two Books with identical ISBN | Both accepted | **Bug → DEF-C1** (no ISBN uniqueness) |

---

## Charter 2 — Loan borrow/return workflow

> **Explore** the loan lifecycle and inventory engine **with** date reversals, stock boundaries, status transitions and book-swaps **to discover** inventory drift and validation gaps.

**Heuristics:** date reversal, equal dates, extreme dates, last-copy / out-of-stock boundary, return-then-reopen, change the book on an active loan, status (Active/Overdue/Returned) transitions.

| # | Action | Observed | Verdict |
| :--- | :--- | :--- | :--- |
| 2.1 | Borrow with DueDate before LoanDate | "Due date cannot be before the loan date." | Pass |
| 2.2 | Borrow with DueDate = LoanDate | Accepted (boundary) | Pass |
| 2.3 | Borrow the **last** copy (copies = 1) | Accepted, copies → 0 | Pass |
| 2.4 | Borrow an out-of-stock book (copies = 0) | "This book is currently unavailable." | Pass |
| 2.5 | Return a loan (set ReturnDate) | Status → Returned, copies +1 | Pass |
| 2.6 | Delete an active loan | Copies restored +1 | Pass |
| 2.7 | Re-open a returned loan when no copies remain | Blocked: "…cannot be reopened…no available copies." | Pass |
| 2.8 | Swap the book on an active loan | Old book +1, new book −1 | Pass |
| 2.9 | Set DueDate in the past, no ReturnDate | Status correctly shows **Overdue** | Pass |
| 2.10 | Enter LoanDate `0001-01-01` / `9999-12-31` | Accepted and stored | **Bug → DEF-G1** (unbounded dates) |

---

## Charter 3 — Report generation & export

> **Explore** the report filters and CSV export **with** combinations of type/date/search and edge data **to discover** incorrect filtering, empty-state handling and export issues.

**Heuristics:** each report type, open-ended and closed date ranges, boundary dates, search across every field, no-match search, empty data set, formula-injection in exported fields.

| # | Action | Observed | Verdict |
| :--- | :--- | :--- | :--- |
| 3.1 | Report type = Returned | Only returned loans listed | Pass |
| 3.2 | Report type = Overdue | Only overdue loans listed | Pass |
| 3.3 | From-date only (open-ended upper bound) | Earlier loans excluded | Pass |
| 3.4 | From-date equal to a loan's date | Boundary loan included | Pass |
| 3.5 | Search by author / member email | Matching rows across fields | Pass |
| 3.6 | Search term with different letter case | **Misses** rows that MySQL would match | **Bug → DEF-R2** (case-sensitivity parity) |
| 3.7 | Report with no loans | "No books were loaned for this report." | Pass |
| 3.8 | Summary cards (Books/Unique/Active/Overdue) | Counts match listed rows | Pass |
| 3.9 | Book titled `=2+5` then Export CSV | Value exported verbatim (formula risk) | **Bug → DEF-R1** (CSV injection) |
| 3.10 | Export CSV returns file | `text/csv` download with header + rows | Pass |

---

## Bug summary

| Bug | Title | Severity | Charter |
| :--- | :--- | :--- | :--- |
| DEF-C1 | Duplicate ISBN accepted | Low | 1 |
| DEF-G1 | Extreme dates accepted | Low | 2 |
| DEF-R2 | Search case-sensitivity parity | Low | 3 |
| DEF-R1 | CSV formula injection | Medium | 3 |

Exploration confirmed the headline business rules (validation, inventory control, referential integrity, report filtering) behave correctly, and surfaced four hardening defects that are catalogued in the [Inspection report](Testing_II_Inspection.md).

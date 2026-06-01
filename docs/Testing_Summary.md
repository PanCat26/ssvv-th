# Testing Summary — Mini Library Loan Manager

**Course:** Software System Verification and Validation — Exam Take-Home
**Application:** Mini Library Loan Manager (ASP.NET Core MVC, .NET 8, EF Core)
**This document** is the master summary for **Task 2 (Testing I)** and **Task 3 (Testing II)**. It shows that **both testing families (six techniques)** are applied to **all three functionalities**.

## 1. Functionalities under test

1. **CRUD** — Create/Read/Update/Delete for the three entities **Book**, **Member**, **Loan**.
2. **Loan workflow** — the cross-entity feature: borrowing/returning a book, which links a Member and adjusts a Book's stock (the *"one functionality with all three entities"*).
3. **Report** — generation of the loan report with type/date/search filters and CSV export.

## 2. Coverage matrix

Every cell is covered. Numbers are passing automated test cases; Inspection and Exploratory are document-based techniques.

| Technique (family) | CRUD (Book / Member / Loan) | Loan workflow | Report |
| :--- | :--- | :--- | :--- |
| **BBT** — Black-Box *(Testing I)* | ✅ Book/Member validation EP/BVA (22) | ✅ borrow/return + Loan model EP/BVA (16) | ✅ filter EP (15) |
| **WBT** — White-Box *(Testing I)* | ✅ service branch coverage (10) | ✅ inventory path coverage (8) | ✅ query branch coverage (10) |
| **Integration** *(Testing I)* | ✅ controller→service→DB (8) | ✅ service lifecycle + controller (11) | ✅ controller→service→CSV (3) |
| **Inspection** *(Testing II)* | ✅ checklist + DEF-C1/C2 | ✅ DEF-L1 | ✅ DEF-R1/R2 |
| **Exploratory** *(Testing II)* | ✅ Charter 1 | ✅ Charter 2 | ✅ Charter 3 |
| **GUI / Web** *(Testing II)* | ✅ Book (9) + Member (8) + browser | ✅ Loan pages (10) + browser (3) | ✅ Report (5) + browser |


## 3. Test inventory

**Total: 141 automated tests, 141 passing (100%).**

| Family | Technique | Location | Cases |
| :--- | :--- | :--- | :--- |
| Testing I | BBT | [ssvv-th.Tests/BlackBoxTests/](../ssvv-th.Tests/BlackBoxTests) | 53 |
| Testing I | WBT | [ssvv-th.Tests/WhiteBoxTests/](../ssvv-th.Tests/WhiteBoxTests) | 28 |
| Testing I | Integration | [ssvv-th.Tests/IntegrationTests/](../ssvv-th.Tests/IntegrationTests) | 22 |
| Testing II | GUI/Web (DOM) | [GuiTests/](../ssvv-th.Tests/GuiTests) + [FrontendTests/](../ssvv-th.Tests/FrontendTests) | 32 |
| Testing II | GUI/Web (browser) | Playwright in GuiTests/ + FrontendTests/ | 6 |

Document-based techniques: **Inspection** ([Testing_II_Inspection.md](Testing_II_Inspection.md), 6 defects) and **Exploratory** ([Testing_II_Exploratory.md](Testing_II_Exploratory.md), 3 charters / 28 sessions).

## 4. Technique documents

**Task 2 — Testing I**
- [Testing_I_BBT.md](Testing_I_BBT.md) — Black-Box Testing (EP/BVA tables, derived cases)
- [Testing_I_WBT.md](Testing_I_WBT.md) — White-Box Testing (control-flow graphs, cyclomatic complexity, independent paths)
- [Testing_I_Integration.md](Testing_I_Integration.md) — Integration Testing (strategy, layers, lifecycle scenarios)

**Task 3 — Testing II**
- [Testing_II_Inspection.md](Testing_II_Inspection.md) — Inspection/Review (checklist + defect log)
- [Testing_II_Exploratory.md](Testing_II_Exploratory.md) — Exploratory Testing (SBTM charters + session logs)
- [Testing_II_GUI.md](Testing_II_GUI.md) — GUI/Web Testing (AngleSharp DOM + Playwright browser cases)

## 5. Frameworks & infrastructure

- **xUnit** — test runner.
- **EF Core In-Memory provider** — deterministic, dependency-free database; production MySQL is substituted only at the provider level.
- **`WebApplicationFactory`** — hosts the real app in-memory for HTTP/DOM testing.
- **AngleSharp** — HTML parsing and DOM assertions.
- **Microsoft Playwright** (headless Chromium) — real-browser automation.
- Shared helpers: [InMemoryDb](../ssvv-th.Tests/Helpers/InMemoryDb.cs), [ControllerTestHelper](../ssvv-th.Tests/Helpers/ControllerTestHelper.cs), [TestHelper](../ssvv-th.Tests/Helpers/TestHelper.cs) (anti-forgery scraping), [GuiWebCollection](../ssvv-th.Tests/GuiTests/GuiWebCollection.cs) (serial DB isolation).

## 6. How to run

```bash
# from the repository root
dotnet test
```

All 141 tests run in a single pass on .NET 8 (no MySQL or external services required). Playwright's Chromium must be installed once (`pwsh ssvv-th.Tests/bin/Debug/net8.0/playwright.ps1 install chromium`).

```
Passed!  -  Failed: 0, Passed: 141, Skipped: 0, Total: 141
```

## 7. Defects found

The verification surfaced 6 defects (1 Medium, 5 Low), catalogued in the [Inspection report](Testing_II_Inspection.md) and corroborated by the [Exploratory report](Testing_II_Exploratory.md): CSV formula injection (DEF-R1, Medium), an unreachable validation branch (DEF-L1), search case-sensitivity parity (DEF-R2), unbounded dates (DEF-G1), duplicate ISBN (DEF-C1), and missing pagination (DEF-C2). The core business rules — validation, inventory control, referential integrity, CSRF protection and report filtering — pass all techniques.
